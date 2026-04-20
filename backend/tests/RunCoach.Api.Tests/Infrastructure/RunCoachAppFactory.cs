using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using RunCoach.Api.Infrastructure;
using Testcontainers.PostgreSql;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Assembly-wide test fixture: stands up a single <c>postgres:17-alpine</c>
/// container via Testcontainers, applies the Slice 0 EF migration so the
/// Identity + DataProtection + Wolverine envelope schema is ready, and builds
/// a <see cref="Respawner"/> snapshot of the <c>public</c> schema that
/// per-test cleanup can roll back to. Downstream auth-endpoint tests receive
/// this fixture via constructor injection (xUnit v3 <c>AssemblyFixture</c>).
///
/// The full <c>WebApplicationFactory&lt;Program&gt;</c> path is intentionally
/// NOT exercised here — a pre-existing startup hang in the SUT's
/// <c>WebApplication.CreateBuilder</c> path under <c>HostFactoryResolver</c>
/// is captured in this slice's proof summary and queued for a separate fix.
/// The container + migrated schema + Respawner that this fixture ships are
/// sufficient for T02.x integration tests that talk directly to the DB or
/// construct their own host in isolation.
///
/// <c>WithReuse(true)</c> outside CI keeps the container warm between
/// <c>dotnet test</c> invocations for a fast inner loop; in CI we always run a
/// fresh container so a corrupt snapshot can never leak between runs.
/// </summary>
public class RunCoachAppFactory : IAsyncLifetime
{
    private const string ReuseLabel = "runcoach-tests";
    private static readonly bool IsCi =
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private readonly PostgreSqlContainer _container;
    private Respawner? _respawner;

    public RunCoachAppFactory()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("runcoach")
            .WithUsername("runcoach")
            .WithPassword("testcontainer-dev")
            .WithReuse(!IsCi)
            .WithLabel("reuse-id", ReuseLabel)
            .Build();
    }

    /// <summary>Gets connection string to the live Testcontainers Postgres.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Apply the Slice 0 EF migration against a fresh DbContext pointed at
        // the container. Side-steps the Program.cs startup path entirely so
        // auth-endpoint tests can rely on the schema being present even while
        // the WebApplicationFactory hang is being investigated.
        var options = new DbContextOptionsBuilder<RunCoachDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using (var db = new RunCoachDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Build a fresh <see cref="RunCoachDbContext"/> pointed at the live
    /// Testcontainers Postgres. Use this inside tests that verify persistence
    /// directly; the Wolverine envelope mapping inside
    /// <see cref="RunCoachDbContext.OnModelCreating"/> is
    /// <c>ExcludeFromMigrations</c>, so nothing in the container's schema
    /// depends on Wolverine being registered.
    /// </summary>
    public RunCoachDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<RunCoachDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new RunCoachDbContext(options);
    }

    /// <summary>
    /// Clears data in the <c>public</c> schema (Identity + DataProtection +
    /// Wolverine envelopes) via Respawn. Marten's <c>runcoach_events</c>
    /// schema is reset separately through <c>ResetAllMartenDataAsync</c> so we
    /// don't wipe Marten's <c>mt_hilo</c> rows.
    /// </summary>
    public async Task ResetPublicSchemaAsync()
    {
        if (_respawner is null)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }
}
