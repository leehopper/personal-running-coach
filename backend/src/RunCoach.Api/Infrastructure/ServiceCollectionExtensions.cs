using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Extension methods for registering module services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application module services. Call from Program.cs during startup.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The application configuration for binding settings sections.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind strongly-typed settings from configuration sections.
        services.Configure<CoachingLlmSettings>(
            configuration.GetSection(CoachingLlmSettings.SectionName));
        services.Configure<PromptStoreSettings>(
            configuration.GetSection(PromptStoreSettings.SectionName));

        // System services — TimeProvider for deterministic date/time operations.
        services.AddSingleton(TimeProvider.System);

        // Training module — stateless computation services (singleton).
        services.AddSingleton<IPaceZoneIndexCalculator, PaceZoneIndexCalculator>();
        services.AddSingleton<IPaceZoneCalculator, PaceZoneCalculator>();
        services.AddSingleton<IHeartRateZoneCalculator, HeartRateZoneCalculator>();

        // Training module — deterministic safety gate (Slice 3 Unit 3 / DEC-079).
        // Stateless keyword classifier; the keyword catalog is compiled once at
        // type-load. Consumed by the Slice 3 adaptation orchestration handler.
        services.AddSingleton<ISafetyGate, SafetyGate>();

        // Training module — workout-log persistence (scoped, shares the request DbContext).
        services.AddScoped<IWorkoutLogRepository, WorkoutLogRepository>();
        services.AddScoped<IWorkoutLogService, WorkoutLogService>();

        // Coaching module — prompt store is singleton (caches templates for app lifetime).
        services.AddSingleton<IPromptStore, YamlPromptStore>();

        // Coaching module — scoped services (per-request lifetime).
        services.AddScoped<ICoachingLlm, ClaudeCoachingLlm>();

        // ContextAssembler's 3-arg legacy constructor is `internal` (test-only via
        // InternalsVisibleTo); the public surface is the single 6-arg
        // onboarding-aware constructor, so the container unambiguously selects it
        // from a plain implementation-type registration. This MUST stay a type
        // registration, not an `sp => new ContextAssembler(...)` lambda factory:
        // Wolverine 6 handler codegen (ServiceLocationPolicy.NotAllowed, DEC-071)
        // cannot statically construct an opaque Scoped lambda factory, falls back
        // to service location, and rejects it — breaking the OnboardingTurnHandler
        // chain with an HTTP 500 on every onboarding turn. The no-opaque-factory
        // rule is guarded by WolverineCodegenCompositionTests; correct 6-arg
        // constructor selection is guarded by ContextAssemblerDiResolutionTests.
        services.AddScoped<IContextAssembler, ContextAssembler>();

        // Prompt-injection sanitizer (Slice 1 § Unit 6 / DEC-059 / R-068).
        // Stateless layered sanitizer — singleton-safe; the pattern catalog
        // is precompiled once at type-load.
        services.AddSingleton<IPromptSanitizer, LayeredPromptSanitizer>();

        // Recent-log free-text sanitizer coverage (Slice 3 Unit 3). Routes
        // LoggedWorkoutDetail notes + free-text metric values through the DEC-059
        // sanitizer before the recent-log prompt path; consumed by the Slice 3/4
        // WorkoutLog → LoggedWorkoutDetail mapper.
        services.AddSingleton<IRecentLogSanitizer, RecentLogSanitizer>();

        // Idempotency primitive (DEC-060) — scoped so Wolverine handlers and
        // the store share the same `IDocumentSession` instance per request.
        // The sweeper is a hosted singleton that opens its own per-iteration
        // cross-tenant session.
        services.AddScoped<IIdempotencyStore, MartenIdempotencyStore>();
        services.AddHostedService<IdempotencySweeper>();

        // Plan generation orchestrator — plain DI service per Slice 1 § Unit 2
        // (DEC-057 / R-066). NOT a Wolverine handler: invoked inline by the
        // caller's static handler body (e.g. OnboardingTurnHandler.Handle) so
        // events commit on the caller's session inside one Marten transaction.
        services.AddScoped<IPlanGenerationService, PlanGenerationService>();

        return services;
    }
}
