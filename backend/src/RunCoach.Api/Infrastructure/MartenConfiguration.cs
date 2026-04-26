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
                opts.Projections.Add(new UserProfileFromOnboardingProjection(), ProjectionLifecycle.Inline);

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
        services.CritterStackDefaults(x =>
        {
            x.Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate;
            x.Production.ResourceAutoCreate = AutoCreate.None;
            x.Production.GeneratedCodeMode = TypeLoadMode.Static;
        });

        return services;
    }
}
