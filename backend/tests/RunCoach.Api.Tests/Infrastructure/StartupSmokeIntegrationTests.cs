using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Slice 0 foundation smoke tests. Three infra-level checks validate that the
/// assembly fixture itself is load-bearing (container boots, migration applied,
/// Respawn cleans <c>public</c> but preserves migrations history), and seven
/// SUT-host checks validate that the production DI graph composes correctly
/// under <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>
/// — Marten, EF, DataProtection, the <c>/health</c> endpoint, the shared
/// <see cref="NpgsqlDataSource"/> singleton that the credential-rotation seam
/// depends on, and the <see cref="DevelopmentMigrationService"/> registration
/// that gates dev-time EF migrations.
/// </summary>
[Trait("Category", "Integration")]
public class StartupSmokeIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task TestContainer_Accepts_Connections()
    {
        // Arrange
        await using var conn = new NpgsqlConnection(Factory.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);

        // Act
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var actualResult = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        // Assert
        actualResult.Should().Be(1);
    }

    [Fact]
    public async Task InitialMigration_Applied_Creates_Identity_And_DataProtection_Tables()
    {
        // Arrange — migration already applied by RunCoachAppFactory.InitializeAsync.
        await using var db = Factory.CreateDbContext();

        // Act
        var actualPendingMigrations = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var actualAppliedMigrations = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        actualPendingMigrations.Should().BeEmpty();
        actualAppliedMigrations.Should().ContainSingle(name => name.EndsWith("_InitialIdentitySchema", StringComparison.Ordinal));

        // Every spec-mandated table must exist and be empty on a fresh apply.
        foreach (var table in new[] { "AspNetUsers", "AspNetRoles", "DataProtectionKeys" })
        {
            await using var conn = new NpgsqlConnection(Factory.ConnectionString);
            await conn.OpenAsync(TestContext.Current.CancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""SELECT count(*) FROM "{table}" """;
            var actualCount = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            actualCount.Should().Be(0L, because: $"table {table} should be empty on a fresh migration");
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
        var actualRoleCount = await roles.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        actualRoleCount.Should().Be(0L);

        await using var history = verify.CreateCommand();
        history.CommandText = """SELECT count(*) FROM "__EFMigrationsHistory" """;
        var actualHistoryCount = await history.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        actualHistoryCount.Should()
            .BeOfType<long>()
            .Which.Should()
            .BeGreaterThan(
                0,
                because: "Respawn must leave the EF migrations history table intact so later tests don't re-run migrations");
    }

    [Fact]
    public async Task SutHost_MartenDocumentStore_Resolves_And_Opens_Session()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();

        // Act — fully-qualify Marten.IDocumentStore to avoid pulling in `using Marten;`
        // — that directive collides with EF's EntityFrameworkQueryableExtensions on the
        // async LINQ terminators used in the EF smoke tests below.
        var actualStore = scope.ServiceProvider.GetRequiredService<Marten.IDocumentStore>();

        // Assert — configured schema matches MartenConfiguration constant; opening a
        // session proves Marten wired to the shared NpgsqlDataSource and
        // IntegrateWithWolverine's envelope-storage setup completed. The session
        // constructor does not eagerly open a connection, so dispose is a no-op if
        // nothing is queried.
        actualStore.Should().NotBeNull();
        actualStore.Options.Events.DatabaseSchemaName.Should().Be(MartenConfiguration.EventsSchema);
        await using var session = actualStore.LightweightSession();
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task SutHost_RunCoachDbContext_Resolves_And_Queries_Identity_Schema()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();

        // Act — resolving the DbContext through the SUT DI graph (rather than via
        // Factory.CreateDbContext) proves the production registration — shared
        // NpgsqlDataSource + AddDbContextWithWolverineIntegration — opens a working
        // connection. Explicit static-class call disambiguates EF's CountAsync from
        // Marten's identically-named extension.
        var actualUserCount = await EntityFrameworkQueryableExtensions.CountAsync(
            db.Users,
            TestContext.Current.CancellationToken);

        // Assert
        actualUserCount.Should().Be(0);
    }

    [Fact]
    public async Task SutHost_DpKeysContext_Resolves_And_DataProtectionKeys_Table_Reachable()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dp = scope.ServiceProvider.GetRequiredService<DpKeysContext>();

        // Act — the intent here is "DpKeysContext resolves from the SUT DI graph AND
        // the DataProtectionKeys table exists and is queryable." Asserting `count == 0`
        // is not safe because ASP.NET Core DataProtection's KeyRingProvider provisions
        // a default key during host startup (XmlKeyManager[58] "Creating key..." fires
        // between "Hosting starting" and "Hosting started"). Whichever smoke test is
        // first to access Factory.Services triggers SUT boot, so row count on the first
        // boot is non-deterministic across xUnit's test ordering — which differs between
        // macOS and Linux. BeGreaterThanOrEqualTo(0) is the real invariant: the query
        // executed against the real table.
        var actualKeyCount = await EntityFrameworkQueryableExtensions.CountAsync(
            dp.DataProtectionKeys,
            TestContext.Current.CancellationToken);

        // Assert
        actualKeyCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void SutHost_DataProtectionProvider_Resolves_And_Round_Trips_Payload()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();

        // Act — end-to-end protect/unprotect proves the Postgres-backed keyring was
        // provisioned (DataProtectionKeys table exists + is writable) and that
        // SetApplicationName/SetDefaultKeyLifetime didn't break anything.
        var protector = provider.CreateProtector("runcoach.startup-smoke");
        var ciphertext = protector.Protect("hello");
        var actualPlaintext = protector.Unprotect(ciphertext);

        // Assert
        provider.Should().NotBeNull();
        actualPlaintext.Should().Be("hello");
    }

    [Fact]
    public async Task Health_Endpoint_Returns_Ok_Json()
    {
        // Arrange
        using var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var actualBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        actualBody.Should().Be("""{"status":"ok"}""");
    }

    [Fact]
    public void SutHost_NpgsqlDataSource_Is_Singleton_Across_Scopes()
    {
        // Arrange — the credential-rotation seam requires every consumer (EF, Marten,
        // Wolverine outbox, DataProtection) to share ONE NpgsqlDataSource instance so
        // UsePeriodicPasswordProvider rotates credentials for all of them at once.
        using var scope1 = Factory.Services.CreateScope();
        using var scope2 = Factory.Services.CreateScope();

        // Act
        var ds1 = scope1.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var ds2 = scope2.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        // Assert — reference equality verifies the Aspire-registered singleton lifetime is intact.
        ReferenceEquals(ds1, ds2).Should().BeTrue(
            because: "NpgsqlDataSource must be a single DI singleton so credential rotation flows through every consumer");
    }

    [Fact]
    public void SutHost_DevelopmentMigrationService_Is_Registered_As_HostedService_In_Development()
    {
        // Arrange — the fixture uses UseEnvironment("Development") so Program.cs's
        // IsDevelopment branch that registers the DevelopmentMigrationService hosted
        // service runs during host build. Pull the concrete service list from the root
        // DI container; AddHostedService registers IHostedService as singleton.
        var hostedServices = Factory.Services.GetServices<IHostedService>().ToList();

        // Act
        var actualDevMigrationServices = hostedServices.OfType<DevelopmentMigrationService>().ToList();

        // Assert — exactly one instance is expected. This catches a regression that
        // silently drops the `AddHostedService<DevelopmentMigrationService>()` call in
        // Program.cs; the fixture pre-migrates via a throwaway DbContext so StartAsync
        // finds no pending migrations at runtime, which is why a registration-level
        // assertion (rather than a behavioral one) is the practical coverage for this
        // IHostedService under the current test topology.
        actualDevMigrationServices.Should()
            .ContainSingle(because: "Program.cs registers DevelopmentMigrationService as IHostedService only under IsDevelopment(); dropping that AddHostedService call would regress dev-time EF migration application");
    }
}
