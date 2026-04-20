using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Design-time factory consumed by <c>dotnet ef</c> tooling to construct
/// <see cref="RunCoachDbContext"/> without booting the full web host. The
/// connection string here never runs code paths against a real database —
/// it exists solely so the Npgsql provider can generate DDL for migrations.
/// </summary>
public class RunCoachDbContextDesignTimeFactory : IDesignTimeDbContextFactory<RunCoachDbContext>
{
    public RunCoachDbContext CreateDbContext(string[] args)
    {
        // Design-time connection never opens a DB connection — EF only queries the
        // Npgsql provider for DDL generation. Omitting credentials avoids tripping
        // analyzer rules against hard-coded secrets.
        var optionsBuilder = new DbContextOptionsBuilder<RunCoachDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=runcoach");
        return new RunCoachDbContext(optionsBuilder.Options);
    }
}
