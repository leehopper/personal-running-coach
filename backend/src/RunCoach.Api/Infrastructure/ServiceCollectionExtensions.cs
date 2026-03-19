namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Extension methods for registering module services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application module services. Add module registrations here as modules are created.
    /// </summary>
    public static IServiceCollection AddApplicationModules(this IServiceCollection services)
    {
        return services;
    }
}
