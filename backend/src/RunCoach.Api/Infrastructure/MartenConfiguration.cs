using System.Reflection;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using JasperFx.MultiTenancy;
using JasperFx.OpenTelemetry;
using Marten;
using Marten.EntityFrameworkCore;
using Marten.Services;
using Marten.Storage;
using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Infrastructure.Marten;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Observability;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using Wolverine.Marten;
using LegacyOnboardingV1 = RunCoach.Api.Modules.Coaching.Onboarding.Legacy.Events.V1;

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

    // Cached open generic MethodInfo for MapEventTypeWithSchemaVersion<T>.
    // Initialized via the helper below; must be a field (not a property) so
    // StyleCop SA1201 keeps all fields before properties in this class.
    private static readonly MethodInfo MapEventTypeWithSchemaVersionMethod =
        ResolveMapEventTypeWithSchemaVersionMethod();

    /// <summary>
    /// Gets every Marten event type currently emitted by the system, in registration order.
    /// Every entry here is wired through <see cref="MapEventTypeWithSchemaVersionFor"/>
    /// at <see cref="Apply"/> time so its <c>mt_events.type</c> column carries the
    /// <c>{snake_name}_v1</c> suffix the <c>MartenStoreOptionsCompositionTests</c>
    /// asserts against (DEC-067 / R-072). Adding a new event type without listing
    /// it here fails that composition test by design — the failure is the
    /// load-bearing signal that schema-version hygiene was missed.
    /// </summary>
    /// <remarks>
    /// Legacy V1 records under <c>RunCoach.Api.Modules.{Module}.Legacy.Events.V{N}</c>
    /// are NOT listed here — they are not appended by production code, only read
    /// by Marten's deserializer when a registered upcaster routes
    /// <c>mt_dotnet_type</c>-tagged legacy rows back into the current shape.
    /// </remarks>
    public static IReadOnlyList<Type> RegisteredEventTypes { get; } =
    [
        typeof(OnboardingStarted),
        typeof(TopicAsked),
        typeof(AnswerCaptured),
        typeof(ClarificationRequested),
        typeof(UserTurnRecorded),
        typeof(AssistantTurnRecorded),
        typeof(PlanLinkedToUser),
        typeof(OnboardingCompleted),
        typeof(PlanGenerated),
        typeof(MesoCycleCreated),
        typeof(FirstMicroCycleCreated),
        typeof(ClientErrorReported),
    ];

    /// <summary>
    /// Applies the production-shape Marten configuration to the supplied
    /// <see cref="StoreOptions"/>. Extracted from the <see cref="AddMarten"/>
    /// lambda so the <c>MartenStoreOptionsCompositionTests</c> can exercise
    /// the same configuration against a bare <c>DocumentStore.For(...)</c>
    /// without paying the full DI host boot.
    /// </summary>
    /// <param name="opts">The Marten store options to mutate.</param>
    /// <param name="connectionString">
    /// Optional connection string for the bare-options test path. When
    /// <see langword="null"/>, the caller is expected to wire connection
    /// state itself (the production DI path uses <c>UseNpgsqlDataSource</c>
    /// against the shared <see cref="Npgsql.NpgsqlDataSource"/>).
    /// </param>
    public static void Apply(StoreOptions opts, string? connectionString = null)
    {
        if (connectionString is not null)
        {
            opts.Connection(connectionString);
        }

        opts.DatabaseSchemaName = EventsSchema;
        opts.Events.DatabaseSchemaName = EventsSchema;
        opts.Events.StreamIdentity = StreamIdentity.AsGuid;
        opts.Events.TenancyStyle = TenancyStyle.Conjoined;

        opts.Events.AppendMode = EventAppendMode.Rich;
        opts.Events.UseIdentityMapForAggregates = true;

        opts.Policies.AllDocumentsAreMultiTenanted();
        opts.Schema.For<IdempotencyMarker>();

        // Per-event schema-version registration. Every event type appended by
        // production code is tagged `{snake_name}_v1` in `mt_events.type` from
        // day one, so a future V2 schema bump can land an unambiguous direct
        // V1 -> current upcaster without a column-content scan (DEC-067 /
        // R-072 §9). Composition-test source-of-truth is
        // <see cref="RegisteredEventTypes"/>.
        foreach (var eventType in RegisteredEventTypes)
        {
            MapEventTypeWithSchemaVersionFor(opts.Events, eventType, version: 1U);
        }

        // Pre-register legacy V1 CLR records the upcaster routes from. The
        // V1 record for OnboardingStarted is byte-identical to the current
        // shape today (no production V2 exists yet); the upcaster below
        // exists solely to exercise the registration shape and to make the
        // synthetic-row regression test runnable. When a real V2 lands, the
        // upcaster body changes from identity to the actual transform and a
        // new MapEventTypeWithSchemaVersion<Current>(2) call lands here.
        opts.Events.AddEventType<LegacyOnboardingV1.OnboardingStarted>();
        opts.Events.Upcast<LegacyOnboardingV1.OnboardingStarted, OnboardingStarted>(
            legacy => UpcasterTelemetry.TraceUpcast(
                legacy,
                static v1 => new OnboardingStarted(v1.UserId, v1.StartedAt)));

        opts.Projections.Add(new OnboardingProjection(), ProjectionLifecycle.Inline);

        opts.Schema.For<RunnerOnboardingProfile>().Identity(x => x.UserId);
        RegisterEfProjectionWithoutWeaselTables(opts, new UserProfileFromOnboardingProjection());

        opts.Schema.For<PlanProjectionDto>().Identity(x => x.PlanId);
        opts.Projections.Add(new PlanProjection(), ProjectionLifecycle.Inline);

        opts.Projections.Errors.SkipUnknownEvents = true;

        opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;
        opts.OpenTelemetry.TrackEventCounters();
    }

    /// <summary>
    /// Registers Marten against the shared <see cref="Npgsql.NpgsqlDataSource"/>
    /// and applies <see cref="CritterStackDefaults"/> so development auto-builds
    /// schema while production runs statically compiled code and never touches DDL.
    /// </summary>
    public static IServiceCollection AddRunCoachMarten(this IServiceCollection services)
    {
        services.AddMarten(opts => Apply(opts))
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

    /// <summary>
    /// Resolves and returns the cached generic <c>MethodInfo</c> for
    /// <c>EventStoreOptionsExtensions.MapEventTypeWithSchemaVersion&lt;T&gt;(IEventStoreOptions, uint)</c>
    /// declared at the Marten assembly root.
    /// </summary>
    /// <returns>
    /// The open generic <see cref="MethodInfo"/> for
    /// <c>MapEventTypeWithSchemaVersion&lt;T&gt;</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>EventStoreOptionsExtensions</c> cannot be located in the Marten assembly,
    /// indicating the API moved between Marten versions.
    /// </exception>
    private static MethodInfo ResolveMapEventTypeWithSchemaVersionMethod()
    {
        // The single-arg generic extension lives on Marten's
        // EventStoreOptionsExtensions (declared at the assembly root, no
        // namespace) and takes (IEventStoreOptions, uint).
        var extensions = typeof(global::Marten.IDocumentStore)
            .Assembly
            .GetType("EventStoreOptionsExtensions")
            ?? throw new InvalidOperationException(
                "EventStoreOptionsExtensions not found in Marten assembly; " +
                "API moved between Marten versions — regenerate the helper.");
        return extensions
            .GetMethods()
            .Single(m =>
                m.Name == "MapEventTypeWithSchemaVersion"
                && m.IsGenericMethodDefinition
                && m.GetParameters() is [var p0, var p1]
                && typeof(global::Marten.Events.IEventStoreOptions).IsAssignableFrom(p0.ParameterType)
                && p1.ParameterType == typeof(uint));
    }

    /// <summary>
    /// Invokes the cached generic <c>MapEventTypeWithSchemaVersion&lt;T&gt;</c> method for a
    /// runtime-known event <see cref="Type"/> and <paramref name="version"/>. The closed
    /// generic dispatch keeps <see cref="RegisteredEventTypes"/> as a single source-of-truth
    /// list — adding a new event there auto-wires the schema-version registration without
    /// touching <see cref="Apply"/>.
    /// </summary>
    /// <param name="events">The Marten <c>IEventStoreOptions</c> exposed by <c>opts.Events</c>.</param>
    /// <param name="eventType">The event CLR type to register.</param>
    /// <param name="version">The schema version to append to the snake-cased type name.</param>
    private static void MapEventTypeWithSchemaVersionFor(
        global::Marten.Events.IEventStoreOptions events,
        Type eventType,
        uint version)
    {
        var method = MapEventTypeWithSchemaVersionMethod.MakeGenericMethod(eventType);
        method.Invoke(null, [events, version]);
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
