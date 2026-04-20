using Microsoft.EntityFrameworkCore;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Applies pending EF Core migrations on startup in Development environments.
/// Registered as an <see cref="IHostedService"/> so the work runs inside the
/// host's <c>StartAsync</c> lifecycle — that plays cleanly with
/// <c>WebApplicationFactory</c> integration tests (which intercept the host at
/// Build time) and avoids blocking the entry-point between
/// <c>builder.Build()</c> and <c>app.RunAsync()</c>. EF Core 10's
/// <c>IMigrationsDatabaseLock</c> keeps this safe under concurrent starts;
/// production migrations ship as an <c>efbundle</c> step in the deploy
/// pipeline per R-046 (deferred to MVP-1).
/// </summary>
public sealed class DevelopmentMigrationService(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
