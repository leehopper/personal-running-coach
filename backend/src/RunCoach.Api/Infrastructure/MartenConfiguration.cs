using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.EntityFrameworkCore;
using Marten.Services;
using Marten.Storage;
using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using Wolverine.Marten;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Registers the Marten document store with the production-shape configuration:
/// stream-per-user Guid identity, conjoined multitenancy on the
/// <c>runcoach_events</c> schema, Rich append mode, Solo async daemon, and
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

                // Rich mode (DEC-057): performs the two-step SQL append that
                // tracks per-stream version numbers and enforces stream-version
                // consistency at SaveChangesAsync time. Concurrent submits to
                // the same stream that race past the idempotency check will
                // collide — the second committer gets `ExistingStreamIdCollisionException`
                // (first-turn StartStream race) or `ConcurrentUpdateException`
                // (subsequent-turn Append race). Wolverine's concurrency policy
                // in Program.cs routes both to the dead-letter queue. Quick
                // mode skips version checks entirely, making the DEC-057
                // concurrency guarantee design-only.
                opts.Events.AppendMode = EventAppendMode.Rich;
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
                // `CustomProjectionStorageProviders[typeof(RunnerOnboardingProfile)]`,
                // so Marten never persists the row itself.
                //
                // Two concrete preconditions had to land together to make this
                // wiring boot under <c>TenancyStyle.Conjoined</c>:
                //
                // 1. <c>RunnerOnboardingProfile</c> implements
                //    <c>Marten.Metadata.ITenanted</c>. Marten's
                //    <c>EfCoreSingleStreamProjection.ValidateConfiguration</c>
                //    explicitly fails the host start with
                //    <c>InvalidProjectionException</c> if the EF target type
                //    does not implement <c>ITenanted</c> when events use
                //    Conjoined tenancy.
                // 2. The explicit <c>Schema.For&lt;RunnerOnboardingProfile&gt;().Identity</c>
                //    selector below. Marten's
                //    <c>StoreOptions.ApplyConfiguration()</c> walks every
                //    projection's published types and runs
                //    <c>DocumentMapping.CompileAndValidate()</c> on each, which
                //    requires an <c>Id</c>/<c>id</c> member or a configured
                //    identity selector. <c>RunnerOnboardingProfile</c> uses <c>UserId</c>
                //    (a shared PK/FK with <c>ApplicationUser</c>), so the
                //    selector points there. Marten still creates an unused
                //    <c>mt_doc_runneronboardingprofile</c> doc table — harmless because
                //    the storage delegate diverts every read and write to the
                //    EF row. The doc-table creation is also why
                //    <c>RunCoachDbContext.OnModelCreating</c> pins
                //    <c>HasDefaultSchema("public")</c>: without it,
                //    <c>AddEntityTablesFromDbContext</c> would relocate the
                //    Identity / DataProtection tables into
                //    <c>runcoach_events</c> and the cross-schema FKs would
                //    fail at boot.
                opts.Schema.For<RunnerOnboardingProfile>().Identity(x => x.UserId);
                RegisterEfProjectionWithoutWeaselTables(opts, new UserProfileFromOnboardingProjection());

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
        services.CritterStackDefaults(x =>
        {
            x.Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate;
            x.Production.ResourceAutoCreate = AutoCreate.None;
            x.Production.GeneratedCodeMode = TypeLoadMode.Static;
        });

        return services;
    }

    /// <summary>
    /// Registers an EF-Core-backed projection with Marten and prunes the EF entity tables
    /// that the extension automatically appends to Weasel's migration set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>Marten.EntityFrameworkCore</c> extension's <c>opts.Add(...)</c> overload calls
    /// <c>AddEntityTablesFromDbContext</c>, which appends every <c>TDbContext</c> entity
    /// (Identity, <c>RunnerOnboardingProfile</c>, DataProtection) onto
    /// <c>opts.Storage.ExtendedSchemaObjects</c> so Weasel will create-or-migrate them at
    /// host start. The EF migrations already own those tables (via
    /// <c>RunCoachDbContext.Database.MigrateAsync</c> in production and the
    /// integration-test fixture), and Weasel's <c>Table.readExistingAsync</c> trips a
    /// NullReferenceException when it tries to reconcile an EF-style PK column against the
    /// tracked schema — Weasel resolves the PK column by name and returns <see langword="null"/>
    /// if the EF migration's exact spelling does not match.
    /// </para>
    /// <para>
    /// Pruning the EF tables from the Marten/Weasel migration set avoids both the duplicate
    /// ownership and the NRE. The projection storage delegate still works because it routes
    /// through the live <c>TDbContext</c> instance, not through the schema-objects list.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDoc">The EF entity type produced by the projection.</typeparam>
    /// <typeparam name="TId">The stream identity type.</typeparam>
    /// <typeparam name="TDbContext">The <see cref="DbContext"/> that owns the EF row.</typeparam>
    /// <param name="options">The Marten <see cref="StoreOptions"/> being configured.</param>
    /// <param name="projection">The EF-Core single-stream projection to register.</param>
    /// <param name="lifecycle">The projection lifecycle. Defaults to <see cref="ProjectionLifecycle.Inline"/>.</param>
    private static void RegisterEfProjectionWithoutWeaselTables<TDoc, TId, TDbContext>(
        StoreOptions options,
        EfCoreSingleStreamProjection<TDoc, TId, TDbContext> projection,
        ProjectionLifecycle lifecycle = ProjectionLifecycle.Inline)
        where TDoc : class
        where TId : notnull
        where TDbContext : DbContext
    {
        var before = options.Storage.ExtendedSchemaObjects.Count;
        options.Add(projection, lifecycle);
        if (options.Storage.ExtendedSchemaObjects.Count > before)
        {
            options.Storage.ExtendedSchemaObjects.RemoveRange(
                before,
                options.Storage.ExtendedSchemaObjects.Count - before);
        }
    }
}
