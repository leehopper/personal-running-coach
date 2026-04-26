using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.EntityFrameworkCore;
using Marten.Services;
using Marten.Storage;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Plan;
using Wolverine.Marten;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Registers the Marten document store with the production-shape configuration:
/// stream-per-user Guid identity, conjoined multitenancy on the
/// <c>runcoach_events</c> schema, Quick append mode, Solo async daemon, and
/// Wolverine outbox integration via <c>IntegrateWithWolverine()</c>.
/// Registration-only for Slice 0; no documents or streams are written yet.
/// </summary>
public static class MartenConfiguration
{
    public const string EventsSchema = "runcoach_events";

    /// <summary>
    /// Registers Marten against the shared <see cref="Npgsql.NpgsqlDataSource"/>
    /// and applies <see cref="CritterStackDefaults"/> so development auto-builds
    /// schema while production runs statically compiled code and never touches DDL.
    /// </summary>
    public static IServiceCollection AddRunCoachMarten(this IServiceCollection services)
    {
        services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = EventsSchema;
                opts.Events.DatabaseSchemaName = EventsSchema;
                opts.Events.StreamIdentity = StreamIdentity.AsGuid;
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;
                opts.Events.AppendMode = EventAppendMode.Quick;
                opts.Events.UseIdentityMapForAggregates = true;

                // `EnableAdvancedAsyncTracking` intentionally left at its
                // default (`false`) — it adds per-session cost with no current
                // load-bearing value. Flip on only when a concrete test-side
                // `WaitForNonStaleData` assertion needs it.
                opts.Policies.AllDocumentsAreMultiTenanted();

                // Explicit document registration so JasperFx static codegen
                // (`TypeLoadMode.Static` in Production) picks up the
                // idempotency-marker document at build time. Auto-discovery
                // only covers documents Marten observes via session calls,
                // which is fine in Development but breaks Production
                // pre-generated handler chains.
                opts.Schema.For<IdempotencyMarker>();

                // The Plan-stream projection materializes a `PlanProjectionDto`
                // keyed on `PlanId` (the document doubles as the per-plan
                // stream id) — Marten's default `Id`/`id` identity convention
                // does not find a member of that name, so the document
                // mapping needs an explicit `Identity` override. Without this
                // call, `Marten.Schema.DocumentMapping.CompileAndValidate()`
                // throws `InvalidDocumentException` at host start.
                opts.Schema.For<RunCoach.Api.Modules.Training.Plan.Models.PlanProjectionDto>()
                    .Identity(x => x.PlanId);

                // Onboarding projections (DEC-047 + DEC-060 / R-069). Both run inline so
                // the in-memory `OnboardingView` and the EF `UserProfile` row materialize
                // on the same `IDocumentSession.SaveChangesAsync` call as the event
                // append - no async daemon lag, no dual-write window. The EF projection
                // is a transaction participant on the Marten Npgsql connection, so the
                // EF write commits inside Marten's transaction (Marten.EntityFrameworkCore
                // 8.32 docs: "registers a transaction participant so the DbContext's
                // SaveChangesAsync is called within Marten's transaction, ensuring
                // atomicity").
                opts.Projections.Add(new OnboardingProjection(), ProjectionLifecycle.Inline);

                // EF-Core-backed projection registration. The
                // `Marten.EntityFrameworkCore` extension (`opts.Add(...)`
                // rather than `opts.Projections.Add(...)`) wires the projection
                // as a transaction participant via `RegisterEfCoreStorage` and
                // walks the `RunCoachDbContext` model with
                // `AddEntityTablesFromDbContext`. Writes route through
                // `CustomProjectionStorageProviders[typeof(UserProfile)]`,
                // so Marten never persists the row itself.
                //
                // Two concrete preconditions had to land together to make this
                // wiring boot under <c>TenancyStyle.Conjoined</c>:
                //
                // 1. <c>UserProfile</c> implements
                //    <c>Marten.Metadata.ITenanted</c>. Marten's
                //    <c>EfCoreSingleStreamProjection.ValidateConfiguration</c>
                //    explicitly fails the host start with
                //    <c>InvalidProjectionException</c> if the EF target type
                //    does not implement <c>ITenanted</c> when events use
                //    Conjoined tenancy.
                // 2. The explicit <c>Schema.For&lt;UserProfile&gt;().Identity</c>
                //    selector below. Marten's
                //    <c>StoreOptions.ApplyConfiguration()</c> walks every
                //    projection's published types and runs
                //    <c>DocumentMapping.CompileAndValidate()</c> on each, which
                //    requires an <c>Id</c>/<c>id</c> member or a configured
                //    identity selector. <c>UserProfile</c> uses <c>UserId</c>
                //    (a shared PK/FK with <c>ApplicationUser</c>), so the
                //    selector points there. Marten still creates an unused
                //    <c>mt_doc_userprofile</c> doc table — harmless because
                //    the storage delegate diverts every read and write to the
                //    EF row. The doc-table creation is also why
                //    <c>RunCoachDbContext.OnModelCreating</c> pins
                //    <c>HasDefaultSchema("public")</c>: without it,
                //    <c>AddEntityTablesFromDbContext</c> would relocate the
                //    Identity / DataProtection tables into
                //    <c>runcoach_events</c> and the cross-schema FKs would
                //    fail at boot.
                opts.Schema.For<UserProfile>().Identity(x => x.UserId);
                var efTablesBefore = opts.Storage.ExtendedSchemaObjects.Count;
                opts.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline);

                // The EF-Core extension also calls `AddEntityTablesFromDbContext`
                // which appends every <c>RunCoachDbContext</c> entity (Identity,
                // <c>UserProfile</c>, DataProtection) onto
                // <c>opts.Storage.ExtendedSchemaObjects</c> so Weasel will
                // create-or-migrate them at host start. The EF migrations
                // already own those tables (via
                // <c>RunCoachDbContext.Database.MigrateAsync</c> in production
                // and the integration-test fixture), and Weasel's
                // <c>Table.readExistingAsync</c> trips a NullReferenceException
                // when it tries to reconcile an EF-style PK column against the
                // tracked schema — Weasel resolves the PK column by name and
                // returns null if the EF migration's exact spelling does not
                // match. Pruning the EF tables from the Marten/Weasel migration
                // set avoids both the duplicate ownership and the NRE; the
                // projection storage delegate still works because it routes
                // through the live <c>RunCoachDbContext</c>, not through the
                // schema-objects list.
                if (opts.Storage.ExtendedSchemaObjects.Count > efTablesBefore)
                {
                    opts.Storage.ExtendedSchemaObjects.RemoveRange(
                        efTablesBefore,
                        opts.Storage.ExtendedSchemaObjects.Count - efTablesBefore);
                }

                // Plan projection (spec 13 § Unit 2, R02.3). Inline so the
                // `PlanProjectionDto` read model materializes on the same
                // `IDocumentSession.SaveChangesAsync` call as the Plan stream's
                // event append - the calling handler's transaction commits the
                // events and the document together. The frontend's
                // `GET /api/v1/plan/current` reads this document directly via
                // `session.LoadAsync<PlanProjectionDto>(planId)` with zero
                // LLM cost.
                opts.Projections.Add(new PlanProjection(), ProjectionLifecycle.Inline);

                opts.Projections.Errors.SkipUnknownEvents = true;

                // Surface Marten connection-use spans (`marten.connection`) and
                // the appended-event counter to the OTel pipeline wired in
                // Program.cs. `TrackLevel.Normal` is the spec default; bump to
                // `Verbose` temporarily when chasing N+1 or write-amplification
                // regressions in dev.
                opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;
                opts.OpenTelemetry.TrackEventCounters();
            })
            .UseLightweightSessions()
            .UseNpgsqlDataSource()
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup()
            .AddAsyncDaemon(DaemonMode.Solo);

        // CritterStackDefaults covers both Marten and Wolverine in the shared
        // JasperFx code-generation pipeline — one setting, both tools aligned.
        //
        // `SourceCodeWritingEnabled = false` in Development keeps codegen
        // purely in-memory so the test host (which boots in Development) does
        // not flush handler chains to `src/RunCoach.Api/Internal/Generated/`.
        // That matters because integration tests register
        // `StubPlanGenerationService` (a type from the test assembly) as
        // `IPlanGenerationService`. With source writing on, Wolverine emits a
        // generated handler that references the test-only type into the API
        // project's tree, and the next plain `dotnet build` of `RunCoach.Api`
        // fails CS0234 on the dangling cross-assembly reference.
        // Production-mode static codegen still writes via the explicit
        // `dotnet run -- codegen write` step (DEC-048).
        services.CritterStackDefaults(x =>
        {
            x.Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate;
            x.Development.SourceCodeWritingEnabled = false;
            x.Production.ResourceAutoCreate = AutoCreate.None;
            x.Production.GeneratedCodeMode = TypeLoadMode.Static;
        });

        return services;
    }
}
