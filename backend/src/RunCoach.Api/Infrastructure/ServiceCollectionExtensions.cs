using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Idempotency;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Computations;

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

        // Coaching module — prompt store is singleton (caches templates for app lifetime).
        services.AddSingleton<IPromptStore, YamlPromptStore>();

        // Coaching module — scoped services (per-request lifetime).
        services.AddScoped<ICoachingLlm, ClaudeCoachingLlm>();
        services.AddScoped<IContextAssembler, ContextAssembler>();

        // Idempotency primitive (DEC-060) — scoped so Wolverine handlers and
        // the store share the same `IDocumentSession` instance per request.
        // The sweeper is a hosted singleton that opens its own per-iteration
        // cross-tenant session.
        services.AddScoped<IIdempotencyStore, MartenIdempotencyStore>();
        services.AddHostedService<IdempotencySweeper>();

        return services;
    }
}
