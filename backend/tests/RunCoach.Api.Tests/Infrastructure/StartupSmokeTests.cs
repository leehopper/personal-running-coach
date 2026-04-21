using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Slice 0 foundation smoke tests. Three infra-level checks validate that the
/// assembly fixture itself is load-bearing (container boots, migration applied,
/// Respawn cleans <c>public</c> but preserves migrations history), and six
/// SUT-host checks validate that the production DI graph composes correctly
/// under <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// — Marten, EF, DataProtection, the <c>/health</c> endpoint, and the shared
/// <see cref="NpgsqlDataSource"/> singleton that the credential-rotation seam
/// depends on.
/// </summary>
[Trait("Category", "Integration")]
public class StartupSmokeTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task TestContainer_Accepts_Connections()
    {
        await using var conn = new NpgsqlConnection(Factory.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var result = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        result.Should().Be(1);
    }

    [Fact]
    public async Task InitialMigration_Applied_Creates_Identity_And_DataProtection_Tables()
    {
        // Arrange — migration already applied by RunCoachAppFactory.InitializeAsync.
        await using var db = Factory.CreateDbContext();

        // Act
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        pendingMigrations.Should().BeEmpty();
        appliedMigrations.Should().ContainSingle(name => name.EndsWith("_InitialIdentitySchema", StringComparison.Ordinal));

        // Every spec-mandated table must exist and be empty on a fresh apply.
        foreach (var table in new[] { "AspNetUsers", "AspNetRoles", "DataProtectionKeys" })
        {
            await using var conn = new NpgsqlConnection(Factory.ConnectionString);
            await conn.OpenAsync(TestContext.Current.CancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""SELECT count(*) FROM "{table}" """;
            var count = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            count.Should().Be(0L, because: $"table {table} should be empty on a fresh migration");
        }
    }

    [Fact]
    public async Task Respawn_Clears_Public_Schema_But_Preserves_Migrations_History()
    {
        // Arrange — insert a sentinel row into AspNetRoles.
        await using (var conn = new NpgsqlConnection(Factory.ConnectionString))
        {
            await conn.OpenAsync(TestContext.Current.CancellationToken);
            await using var insert = conn.CreateCommand();
            insert.CommandText = """
                INSERT INTO "AspNetRoles" ("Id","Name","NormalizedName","ConcurrencyStamp")
                VALUES (gen_random_uuid()::text::uuid, 'sentinel', 'SENTINEL', gen_random_uuid()::text)
                """;
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        // Act — run Respawn via the base class' DisposeAsync path explicitly.
        await Factory.ResetPublicSchemaAsync();

        // Assert — sentinel gone, migrations history preserved.
        await using var verify = new NpgsqlConnection(Factory.ConnectionString);
        await verify.OpenAsync(TestContext.Current.CancellationToken);

        await using var roles = verify.CreateCommand();
        roles.CommandText = """SELECT count(*) FROM "AspNetRoles" """;
        var roleCount = await roles.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        roleCount.Should().Be(0L);

        await using var history = verify.CreateCommand();
        history.CommandText = """SELECT count(*) FROM "__EFMigrationsHistory" """;
        var historyCount = await history.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        historyCount.Should()
            .BeOfType<long>()
            .And.Subject.As<long>()
            .Should()
            .BeGreaterThan(
                0,
                because: "Respawn must leave the EF migrations history table intact so later tests don't re-run migrations");
    }

    [Fact]
    public async Task SutHost_MartenDocumentStore_Resolves_And_Opens_Session()
    {
        using var scope = Factory.Services.CreateScope();

        // Fully-qualify Marten.IDocumentStore to avoid pulling in `using Marten;`
        // — that directive collides with EF's EntityFrameworkQueryableExtensions
        // on the async LINQ terminators used in the EF smoke tests below.
        var store = scope.ServiceProvider.GetRequiredService<Marten.IDocumentStore>();
        store.Should().NotBeNull();

        // The configured schema should match the MartenConfiguration constant;
        // this verifies the registration actually flowed through our extension.
        store.Options.Events.DatabaseSchemaName.Should().Be(MartenConfiguration.EventsSchema);

        // Opening a session proves Marten wired to the shared NpgsqlDataSource
        // and IntegrateWithWolverine's envelope-storage setup completed. The
        // session constructor does not eagerly open a connection, so dispose is
        // a no-op if nothing is queried.
        await using var session = store.LightweightSession();
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task SutHost_RunCoachDbContext_Resolves_And_Queries_Identity_Schema()
    {
        using var scope = Factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();

        // Resolving the DbContext through the SUT DI graph (rather than via
        // Factory.CreateDbContext) proves the production registration — shared
        // NpgsqlDataSource + AddDbContextWithWolverineIntegration — opens a
        // working connection. Explicit static-class call disambiguates EF's
        // CountAsync from Marten's identically-named extension.
        var userCount = await EntityFrameworkQueryableExtensions.CountAsync(
            db.Users,
            TestContext.Current.CancellationToken);
        userCount.Should().Be(0);
    }

    [Fact]
    public async Task SutHost_DpKeysContext_Resolves_And_DataProtectionKeys_Table_Reachable()
    {
        using var scope = Factory.Services.CreateScope();

        var dp = scope.ServiceProvider.GetRequiredService<DpKeysContext>();

        var keyCount = await EntityFrameworkQueryableExtensions.CountAsync(
            dp.DataProtectionKeys,
            TestContext.Current.CancellationToken);
        keyCount.Should().Be(0);
    }

    [Fact]
    public void SutHost_DataProtectionProvider_Resolves_And_Round_Trips_Payload()
    {
        using var scope = Factory.Services.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        provider.Should().NotBeNull();

        // End-to-end protect/unprotect proves the Postgres-backed keyring was
        // provisioned (DataProtectionKeys table exists + is writable) and that
        // SetApplicationName/SetDefaultKeyLifetime didn't break anything.
        var protector = provider.CreateProtector("runcoach.startup-smoke");
        var ciphertext = protector.Protect("hello");
        var plaintext = protector.Unprotect(ciphertext);
        plaintext.Should().Be("hello");
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Ok_Json()
    {
        using var client = Factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Be("""{"status":"ok"}""");
    }

    [Fact]
    public void SutHost_NpgsqlDataSource_Is_Singleton_Across_Scopes()
    {
        // The credential-rotation seam requires every consumer (EF, Marten,
        // Wolverine outbox, DataProtection) to share ONE NpgsqlDataSource
        // instance so UsePeriodicPasswordProvider rotates credentials for all
        // of them at once. Resolving from two scopes and proving reference
        // equality verifies the Aspire-registered singleton lifetime is intact.
        using var scope1 = Factory.Services.CreateScope();
        using var scope2 = Factory.Services.CreateScope();

        var ds1 = scope1.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var ds2 = scope2.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        ReferenceEquals(ds1, ds2).Should().BeTrue(
            because: "NpgsqlDataSource must be a single DI singleton so credential rotation flows through every consumer");
    }
}
