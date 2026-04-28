using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
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
    /// <summary>
    /// Deterministic JWT signing material used by bearer-path integration
    /// tests. Safe to commit because it only unlocks the in-process
    /// TestServer — Production / Staging fail fast via
    /// <see cref="JwtAuthOptionsValidator"/> if real config is absent.
    /// 48 printable bytes satisfy <c>JwtAuthOptions.SigningKey</c>'s
    /// <c>[MinLength(32)]</c> and RFC 7518 §3.2 for HS256.
    /// </summary>
    public const string TestJwtIssuer = "runcoach-tests";
    public const string TestJwtAudience = "runcoach-tests-client";
    public const string TestJwtSigningKey = "test-hs256-signing-key-0123456789-abcdef0123456789";
    public const string TestJwtKeyId = "test-kid";

    private const string ReuseLabel = "runcoach-tests";
    private const string ConnectionStringEnvVar = "ConnectionStrings__runcoach";
    private const string OtlpEndpointEnvVar = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string JwtIssuerEnvVar = "Auth__Jwt__Issuer";
    private const string JwtAudienceEnvVar = "Auth__Jwt__Audience";
    private const string JwtSigningKeyEnvVar = "Auth__Jwt__SigningKey";
    private const string JwtKeyIdEnvVar = "Auth__Jwt__KeyId";

    private static readonly bool IsCi =
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private readonly PostgreSqlContainer _container;
    private Respawner? _respawner;
    private string? _priorConnectionString;
    private string? _priorOtlpEndpoint;
    private string? _priorJwtIssuer;
    private string? _priorJwtAudience;
    private string? _priorJwtSigningKey;
    private string? _priorJwtKeyId;
    private bool _envVarsOverridden;

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

        // Override the SUT's connection string via process-wide environment
        // variables so every consumer (EF, Marten, Wolverine outbox,
        // DataProtection) resolves the Testcontainers Postgres instead of
        // whatever user-secrets / env var the host would otherwise pick up for
        // local `dotnet run`. Env vars take precedence over the JSON config
        // providers in .NET's default chain, which `ConfigureAppConfiguration`
        // and `IWebHostBuilder.UseSetting` overrides did not reliably beat on
        // this stack per R-055.
        //
        // The mutation is scoped: prior values are captured here and restored
        // in DisposeAsync so the process state after the fixture disposes is
        // indistinguishable from before it ran. This guards against leaking
        // into any adjacent `WebApplicationFactory` instance that might later
        // land in the same test assembly.
        _priorConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        _priorOtlpEndpoint = Environment.GetEnvironmentVariable(OtlpEndpointEnvVar);
        _priorJwtIssuer = Environment.GetEnvironmentVariable(JwtIssuerEnvVar);
        _priorJwtAudience = Environment.GetEnvironmentVariable(JwtAudienceEnvVar);
        _priorJwtSigningKey = Environment.GetEnvironmentVariable(JwtSigningKeyEnvVar);
        _priorJwtKeyId = Environment.GetEnvironmentVariable(JwtKeyIdEnvVar);
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, ConnectionString);
        Environment.SetEnvironmentVariable(OtlpEndpointEnvVar, null);

        // Seed deterministic JWT bearer config so tests that exercise the
        // bearer path of `CookieOrBearer` can mint tokens against a known key.
        // Cookie-flow tests are unaffected — the bearer scheme only engages
        // when an incoming request carries `Authorization: Bearer …`. In Dev
        // the `JwtAuthOptionsValidator` is not registered (Program.cs gates it
        // on `!IsDevelopment()`), so an unset section would still boot, but
        // seeding fixed values keeps all bearer-auth behavior deterministic
        // across runs and makes the token-minting helper in tests trivial.
        Environment.SetEnvironmentVariable(JwtIssuerEnvVar, TestJwtIssuer);
        Environment.SetEnvironmentVariable(JwtAudienceEnvVar, TestJwtAudience);
        Environment.SetEnvironmentVariable(JwtSigningKeyEnvVar, TestJwtSigningKey);
        Environment.SetEnvironmentVariable(JwtKeyIdEnvVar, TestJwtKeyId);
        _envVarsOverridden = true;

        // Apply the Slice 0 EF migration against the container before the SUT
        // boots, so any test that resolves the SUT's host finds the schema
        // ready. Using CreateDbContext() bypasses the SUT's startup path —
        // keeps migration time outside the WebApplicationFactory boot window —
        // and reuses the same options-builder wiring the public helper exposes.
        await using (var db = CreateDbContext())
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

        // Eagerly resolve `IDocumentStore` to force the SUT host to build now,
        // sequentially, at fixture-init time. Marten's
        // `ApplyAllDatabaseChangesOnStartup()` acquires a Postgres advisory
        // lock at host-boot — without this warmup the lazy `Factory.Services`
        // accesses inside test classes can race the lock on slow CI runners,
        // producing "Unable to attain a global lock in time".
        _ = Services.GetRequiredService<IDocumentStore>();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();

        if (_envVarsOverridden)
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _priorConnectionString);
            Environment.SetEnvironmentVariable(OtlpEndpointEnvVar, _priorOtlpEndpoint);
            Environment.SetEnvironmentVariable(JwtIssuerEnvVar, _priorJwtIssuer);
            Environment.SetEnvironmentVariable(JwtAudienceEnvVar, _priorJwtAudience);
            Environment.SetEnvironmentVariable(JwtSigningKeyEnvVar, _priorJwtSigningKey);
            Environment.SetEnvironmentVariable(JwtKeyIdEnvVar, _priorJwtKeyId);
        }
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

        // The in-memory TestServer has no IServerAddressesFeature, so the
        // HttpsRedirectionMiddleware cannot resolve the HTTPS port on its own
        // and logs "[3] Failed to determine the https port for redirect." on
        // every request. Pinning it here silences the warning (R-056). Auth
        // tests additionally set `BaseAddress = https://localhost` on the
        // client so `Request.IsHttps = true` and the middleware short-circuits
        // without any redirect.
        builder.UseSetting("https_port", "443");

        // Strip the production-registered `IdempotencySweeper` hosted service
        // from the SUT's DI graph for tests. Production wires the sweeper as
        // an `IHostedService` bound to `TimeProvider.System`, so the moment
        // the host boots (which we force eagerly in `InitializeAsync` to
        // serialize Marten's startup advisory-lock acquisition), the sweeper
        // begins running on real wall-clock time. Idempotency tests, however,
        // construct their own `IdempotencySweeper` against a `FakeTimeProvider`
        // and seed marker rows at fake-time offsets that may sit hours or days
        // behind today's real clock. Leaving the hosted instance running races
        // against test-owned markers: it can delete a "fresh" fake-time marker
        // mid-assertion because the real-clock cutoff has long since passed
        // it. Removing the descriptor here makes the sweeper test-driven only.
        // Tests that exercise the sweep loop instantiate it directly via
        // `new IdempotencySweeper(store, fakeTime, NullLogger<...>.Instance)`.
        builder.ConfigureTestServices(services =>
        {
            var hostedDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(IdempotencySweeper));
            if (hostedDescriptor is not null)
            {
                services.Remove(hostedDescriptor);
            }
        });
    }
}
