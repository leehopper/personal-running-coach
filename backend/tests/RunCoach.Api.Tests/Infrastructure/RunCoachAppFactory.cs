using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;
using RunCoach.Api.Infrastructure;
using Testcontainers.PostgreSql;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Assembly-wide integration-test fixture. Starts one <c>postgres:17-alpine</c>
/// container via Testcontainers, applies the Slice 0 EF migration, builds a
/// <see cref="Respawner"/> snapshot of the <c>public</c> schema, and boots the
/// full SUT host via <see cref="WebApplicationFactory{TEntryPoint}"/> so tests
/// can resolve <c>IDocumentStore</c>, <c>RunCoachDbContext</c>,
/// <c>DpKeysContext</c>, <c>IDataProtectionProvider</c>, and
/// <c>NpgsqlDataSource</c> against the production DI wiring. HTTP endpoints
/// are reachable through <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>.
///
/// <c>WithReuse(true)</c> outside CI keeps the container warm between
/// <c>dotnet test</c> invocations; in CI we always run a fresh container so a
/// corrupt snapshot can never leak between runs.
/// </summary>
public sealed class RunCoachAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

        // Override the SUT's connection string via an environment variable so
        // every consumer (EF, Marten, Wolverine outbox, DataProtection) resolves
        // the Testcontainers Postgres, not the localhost:5432 fallback in
        // appsettings.Development.json. Env vars take precedence over the JSON
        // config providers in .NET's default chain, which `ConfigureAppConfiguration`
        // overrides do not reliably beat on this stack.
        Environment.SetEnvironmentVariable("ConnectionStrings__runcoach", ConnectionString);
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);

        // Apply the Slice 0 EF migration against the container before the SUT
        // boots, so any test that resolves the SUT's host finds the schema
        // ready. Using a throwaway DbContext bypasses the SUT's startup path —
        // keeps migration time outside the WebApplicationFactory boot window.
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

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Builds a fresh <see cref="RunCoachDbContext"/> bound directly to the
    /// live Testcontainers Postgres without booting the SUT host. Useful for
    /// tests that want to seed or inspect the DB without paying the SUT boot
    /// cost — most integration tests should prefer resolving
    /// <see cref="RunCoachDbContext"/> from <see cref="WebApplicationFactory{TEntryPoint}.Services"/>
    /// instead.
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
    /// schema is reset separately through <c>ResetAllMartenDataAsync</c> so
    /// the <c>mt_hilo</c> rows stay intact.
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Belt-and-suspenders against the macOS-arm64 FileSystemWatcher stall:
        // Program.cs sets the same flag via env var before CreateBuilder, but
        // re-asserting here protects future fixture evolutions (e.g. overrides
        // that add extra reloadable config sources) from re-enabling the
        // watchers.
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
    }
}
