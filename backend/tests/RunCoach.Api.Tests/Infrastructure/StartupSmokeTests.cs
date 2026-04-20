using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Smoke tests that verify the Slice 0 test infrastructure is actually
/// load-bearing: the Testcontainers Postgres boots, the initial EF migration
/// (Identity + DataProtection keys + Wolverine envelope tables) applies from
/// scratch, and Respawn clears the <c>public</c> schema between tests without
/// dropping the migrations history.
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
}
