# Bundles for production, AssemblyFixture for tests

**Apply EF Core 10 migrations via bundles from CD (not at startup in production), and share one Postgres container per assembly with xUnit v3's new `[assembly: AssemblyFixture]`, reset between tests with Respawn on `public` and Marten's own `ResetAllMartenDataAsync()` on `marten`.** This is the pattern that survives the solo-dev → multi-instance → public-beta trajectory with zero retrofitting, matches current (April 2026) Microsoft Learn and JasperFx guidance, and keeps a 50–500-test Postgres suite in the tens of seconds rather than tens of minutes. Two caveats the user must internalize before Slice 0: **EF Core 9 shipped a real migration lock** that softens (but does not eliminate) the objection to `Database.Migrate()` on startup, and **Marten 8.28 officially lists .NET 10 as "untested"** — it works in practice but the JasperFx team has not certified it, so pin Marten ≥ 8.20 and watch the repo for the v9 cutover that will track Npgsql 10 and .NET 10 formally.

The rest of this report defends those two recommendations, translates them into decision matrices and working code, pins every package version, and catalogs the failure modes that will force escape hatches.

## What changed between 2024 and 2026 that matters here

Three shifts drive the recommendations. **First, EF Core 9 (Nov 2024) introduced `IMigrationsDatabaseLock`**, so `MigrateAsync()` now acquires a database-wide lock before applying migrations, and the Microsoft Learn "Applying Migrations" page has been rewritten to reflect it. The older "multiple instances will corrupt your DB" objection is gone; the remaining objections (elevated permissions, no rollback review, cold-start coupling, observability) are retained verbatim. EF Core 10 (GA 11 Nov 2025, LTS through Nov 2028) added no further migration-orchestration changes — it is a LINQ/JSON/vector release. **Second, xUnit v3 shipped GA in July 2025** (`xunit.v3` 3.0.0) with a genuinely new primitive: `[assembly: AssemblyFixture(typeof(...))]` creates a single fixture instance for the whole test assembly and reliably calls `IAsyncLifetime.InitializeAsync` — something v2 could not do cleanly. **Third, Marten 8 (current stable 8.28, March 2026) introduced `CritterStackDefaults`** to centralize dev/prod `AutoCreate` split and, in 8.8, added `services.MartenDaemonModeIsSolo()` specifically for test suites that rapidly stop and start the host.

Everything else — Testcontainers for .NET, Respawn, `WebApplicationFactory<T>` — is API-stable vs. 2024, just version-bumped.

## Part A — Migration application strategy

### The recommendation, with one paragraph of rationale

**Dev:** `await db.Database.MigrateAsync()` inside a `CreateScope()` block gated by `app.Environment.IsDevelopment()`. **CI test fixtures:** `MigrateAsync()` once per assembly inside `AssemblyFixture.InitializeAsync` against a Testcontainers Postgres; Respawn between tests. **CI build job:** emit `efbundle` via `dotnet ef migrations bundle --self-contained -r linux-x64` and also `dotnet ef migrations script --idempotent -o migrate.sql` as a reviewable artifact; tag both with the git SHA. **Production (when it exists):** a one-shot migration step in the deploy job — Kubernetes `Job`, Compose init-service, or a shell step — that runs `./efbundle --connection "$DB_URL"` **before** the API rolls out, using a dedicated `runcoach_migrator` Postgres role with DDL rights; the API runs as `runcoach_app` with DML-only rights.

Why this and not one of the alternatives: **bundles are the only option that decouples DDL privilege from runtime, produces a reviewable single-binary artifact per git SHA, works the same on a VPS / Compose / Kubernetes, and survives multi-instance scale-out without relying on the EF9 lock**. `Database.Migrate()` on startup is fine for dev and tests because the cost of being wrong is zero, but in production the EF9 lock only solves the race — it does not solve any of the other four objections that Microsoft explicitly keeps on the Learn page. FluentMigrator / DbUp / EvolveDb remain alive and maintained, but for a greenfield single-DbContext EF Core project they add a parallel migration framework with no offsetting benefit. The .NET Aspire migration-worker pattern is the right local-orchestration story if and when RunCoach adopts Aspire, but as of April 2026 Aspire's production publish pipeline (outside Azure Container Apps) is still maturing; **the bundle-as-Job fallback is more portable and is what Aspire itself emits in most manifests**.

### Migration-strategy decision matrix

✅ = recommended · ⚠️ = works with caveats · ❌ = avoid

| Pattern | Local dev | CI test fixtures | Production (single-instance) | Production (multi-instance) |
|---|---|---|---|---|
| **`Database.Migrate()` on startup** | ✅ **Recommended** — zero ceremony, EF9 lock is belt-and-suspenders | ✅ **Recommended** — called once per assembly fixture | ⚠️ Works (EF9 lock prevents races) but violates permission/rollback/observability best practices | ⚠️ Lock prevents corruption; still violates best practices; first replica pays cold-start cost |
| **`dotnet ef migrations bundle` (one-shot Job/step)** | ⚠️ Overkill for inner loop | ⚠️ Possible but slower than in-proc `Migrate` | ✅ **Recommended** — single artifact, separate credentials, reviewable | ✅ **Recommended** — runs exactly once, before any replica starts |
| **`dotnet ef migrations script --idempotent`** | ❌ Manual step, no value over `Migrate()` | ❌ Extra moving part | ⚠️ Good *review artifact*; apply with `psql -v ON_ERROR_STOP=1` | ⚠️ Same; no transaction across whole script |
| **`dotnet ef database update` from CI/CD** | ❌ Requires SDK + source on runner | ❌ Same | ❌ Requires SDK in CD image; source leakage | ❌ Same |
| **Sidecar / init-container running `Migrate()`** | ❌ Unnecessary | ❌ Unnecessary | ⚠️ Fine but bundle is simpler and doesn't ship EF runtime in the API image | ⚠️ Same |
| **.NET Aspire migration worker (`AddProject<_MigrationService>().WaitFor(db)`)** | ✅ Excellent for multi-service local orchestration if you're already in Aspire | ⚠️ Not the canonical test path | ⚠️ Great on Azure Container Apps; early-stage on generic K8s | ⚠️ Same |
| **DbUp / FluentMigrator / Evolve** | ❌ Parallel migration framework; no offsetting benefit for single-DbContext EF Core | ❌ | ⚠️ Reasonable if SQL-first or multi-DB portable; not required | ⚠️ Same |

**Highlighted cell: bundle-as-one-shot-Job in production** — it is the only pattern safe across the single-instance → multi-instance boundary without code changes.

### `Program.cs` wiring for the recommended pattern

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Marten on the same Postgres, separate schema (details in Part C below)
builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Default")!);
    opts.DatabaseSchemaName = "marten";
    opts.Events.DatabaseSchemaName = "marten";
})
.UseLightweightSessions()
.ApplyAllDatabaseChangesOnStartup()          // creates marten.* when AutoCreate=None
.AddAsyncDaemon(DaemonMode.HotCold);

builder.Services.CritterStackDefaults(x =>
{
    x.Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate;
    x.Production.ResourceAutoCreate  = AutoCreate.None;
    x.Production.GeneratedCodeMode   = TypeLoadMode.Static;
});

var app = builder.Build();

// Dev only: auto-migrate EF on startup. Production applies bundles out-of-band.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();
app.Run();

public partial class Program;   // marker for WebApplicationFactory<Program>
```

The bundle is built in CI and executed by CD:

```yaml
# .github/workflows/build.yml  (build job)
- run: dotnet tool install --global dotnet-ef --version 10.0.6
- run: dotnet ef migrations bundle --self-contained -r linux-x64
       --project src/RunCoach.Api -o artifacts/efbundle
- run: dotnet ef migrations script --idempotent
       --project src/RunCoach.Api -o artifacts/migrate.sql
- uses: actions/upload-artifact@v4
  with: { name: efbundle-${{ github.sha }}, path: artifacts/ }

# deploy job (future)
- run: ./efbundle --connection "$DB_URL"   # runs as runcoach_migrator
- run: kubectl rollout restart deployment/runcoach-api
```

### The Postgres sharp edges the lock does not save you from

`CREATE INDEX CONCURRENTLY` cannot run inside a transaction block (Postgres SQLSTATE 25001). The Npgsql EF provider supports `.IsCreatedConcurrently()`, but you **must put the concurrent index in its own migration file with no other operations** — the runner will then skip the implicit `BEGIN`/`COMMIT`. A failed concurrent index leaves an **invalid index** that must be dropped manually. `ALTER TYPE ... ADD VALUE` has the same constraint. Long data migrations will run inside the migration lock and can trip `statement_timeout`. None of this is a regression in EF 9/10, but it is the one category of production failure where idempotent scripts and partial-failure stories actually bite.

## Part B — xUnit v3 + Testcontainers fixture architecture

### The recommendation, with one paragraph of rationale

**One Postgres container per test assembly, shared via xUnit v3's `[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]`**, which boots a single `WebApplicationFactory<Program>` + `PostgreSqlContainer` for every test class in the assembly. EF Core's `Database.MigrateAsync()` runs exactly once in the fixture's `InitializeAsync`. Between tests, a Respawn instance scoped to `SchemasToInclude = ["public"]` and `TablesToIgnore = ["__EFMigrationsHistory"]` truncates EF tables, and `Host.ResetAllMartenDataAsync()` cleans Marten's `marten` schema and cycles its async daemon. Container image pinned to `postgres:17-alpine`. Locally, enable `.WithReuse(true)` so the container survives between `dotnet test` invocations for fast inner-loop iteration; in GitHub Actions, do not (runners are ephemeral and Ryuk cleanup works cleanly on fresh VMs).

Why this over the alternatives: on a 50–500 test suite, **container cold-start (1–3 s on Linux, 3–10 s on Docker Desktop) dominates any per-test work**, so any pattern that starts a container more than once per assembly is paying a fixed cost for no isolation gain. Per-test transaction rollback is the fastest option in benchmarks but **breaks the moment a test exercises an outbox pattern, a Marten session commit, or any code that manages its own transaction** — and RunCoach's slices 1+ will all do this, so transaction rollback is a trap. Per-test template-database clone (via `IntegreSQL` / `pgtestdb`) is 3–5× faster than Respawn on very large suites but adds a separate service and is unjustified at 50–500 tests. The AssemblyFixture + Respawn hybrid gives full cross-class parallelism, survives Marten document writes, does not depend on the code-under-test's transactional behavior, and pins its per-test overhead to ~20–80 ms for a small schema.

### Fixture-architecture decision matrix

✅ = recommended · ⚠️ = works with caveats · ❌ = avoid

| Option | Setup cost | Parallelism ceiling | EF Identity tables | Marten documents | Outbox / committed tx tests | Debuggability | Verdict |
|---|---|---|---|---|---|---|---|
| **A. Per-class `IClassFixture<PostgresFixture>` + Respawn** | One container + migration **per class** (~30 s × N classes) | Across classes only | ✅ | ✅ | ✅ | Good — each class has isolated state | ⚠️ Works but pays container cost N times |
| **B. `[assembly: AssemblyFixture]` + Respawn** | One container + migration **per assembly** | Across classes (fixture is thread-safe) | ✅ | ✅ (`ResetAllMartenDataAsync`) | ✅ | Good — class-scoped trait isolation | ✅ **Recommended** |
| **C. Per-test fresh schema / DB** | Full migration per test (~1–3 s × T tests) | Unbounded | ✅ | ⚠️ Marten re-applies schema too | ✅ | Good | ❌ Crushingly slow past ~50 tests |
| **D. Per-test transaction rollback** | ~5–20 ms per test | Full | ✅ | ❌ Marten session `SaveChangesAsync` commits | ❌ Breaks outbox, distributed tx, any `COMMIT`-under-test | Poor — surprising failures when SUT commits | ❌ Brittle for RunCoach's roadmap |
| **E. Template DB clone (`CREATE DATABASE … TEMPLATE …` via IntegreSQL / pgtestdb)** | Template built once; ~100–400 ms per clone on disk, ~90 ms on tmpfs | Unbounded | ✅ | ✅ if template includes Marten schema | ✅ | Good | ⚠️ Operationally heavier; justified only at 500+ tests |

**Highlighted cell: Option B — AssemblyFixture + Respawn (+ Marten's own cleaner).** This is the pattern Slice 0 should lock in.

### Why `AssemblyFixture` over `IClassFixture` or `ICollectionFixture` in v3

`IClassFixture<T>` in xUnit v3 still works exactly as in v2: one fixture instance per test class. `ICollectionFixture<T>` + `[CollectionDefinition]` shares across classes **in the same collection**, but forces **sequential execution** of those classes — parallelism dies. The new `[assembly: AssemblyFixture(typeof(T))]` in v3 shares one instance across every class in the assembly **while allowing classes to run in parallel**; the fixture must be thread-safe, which a Testcontainers host wrapping a `WebApplicationFactory` naturally is. This is a direct win over the v2 workaround (`AssemblyFixtureExample` extension), and it is why the Testcontainers.Xunit module's `ContainerFixture<TBuilder, TContainer>` gained assembly-level reuse support in the v3 line.

`WebApplicationFactory<Program>` integrates with this cleanly: make the fixture *be* a `CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime`, start the container in `InitializeAsync`, wire the connection string via `builder.UseSetting("ConnectionStrings:Default", container.GetConnectionString())` inside `ConfigureWebHost`. **Do not remove-and-re-register the `DbContext`** — that is a 2020-era pattern that `UseSetting` obsoleted and Milan Jovanović's 2025 best-practices article specifically warns against.

### The fixture, end-to-end

```csharp
// AssemblyInfo.cs
[assembly: Xunit.AssemblyFixture(typeof(RunCoach.Tests.RunCoachAppFactory))]

// RunCoachAppFactory.cs
public sealed class RunCoachAppFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithReuse(Environment.GetEnvironmentVariable("CI") != "true")
        .Build();

    private Respawner _respawner = default!;

    public string ConnectionString => _pg.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.ConfigureServices(s => s.MartenDaemonModeIsSolo());
    }

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();

        // Force host construction, which triggers ApplyAllDatabaseChangesOnStartup for Marten
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();                     // EF public.*
            // Marten marten.* was created by the hosted-service at host boot
        }

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter        = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },                // NEVER include "marten"
            TablesToIgnore   = new Table[] { "__EFMigrationsHistory" }
        });
    }

    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);                        // EF tables
        await Services.ResetAllMartenDataAsync();                 // marten schema + daemon
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _pg.DisposeAsync();
    }
}

// IntegrationTest.cs
public abstract class IntegrationTest(RunCoachAppFactory factory) : IAsyncLifetime
{
    protected RunCoachAppFactory Factory { get; } = factory;
    protected HttpClient Client => Factory.CreateClient();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync() => await Factory.ResetAsync();
}

// Example test class — no IClassFixture needed; AssemblyFixture injects the factory.
public class UserRegistrationTests(RunCoachAppFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task POST_register_creates_identity_user()
    {
        var r = await Client.PostAsJsonAsync("/register", new { Email = "x@y.z", Password = "Aa1!aaaa" });
        r.EnsureSuccessStatusCode();
    }
}
```

Three practical notes on this wiring. **First**, `Reset` is called in each test's `DisposeAsync`, not `InitializeAsync`, so a failing test leaves its data on disk for debugging if you stop the suite on first failure. **Second**, the `WithReuse(Environment.GetEnvironmentVariable("CI") != "true")` toggle enables container reuse only in local dev; CI runners are ephemeral so reuse is moot. **Third**, `MartenDaemonModeIsSolo()` is load-bearing — without it, rapid host restarts in a test suite can hang 30+ seconds on Hot/Cold leader election.

### Parallelization budget for a 50–500 test suite

xUnit v3 defaults unchanged from v2: **collection-per-class, collections run in parallel, tests within a class sequential, `MaxParallelThreads` = processor count**. With AssemblyFixture, every class in the assembly shares one `WebApplicationFactory` + one Postgres container, so parallel classes are all hitting the same DB. That is where Respawn's global reset in `DisposeAsync` becomes the choke point: two tests from two classes cannot run truly concurrently if both commit and both expect a clean DB afterward. Back-of-envelope for RunCoach: at ~50 tests, assembly-shared container + sequential-ish execution finishes in roughly **15–30 seconds** including container boot. At ~500 tests this stretches to ~2–5 minutes, at which point the Option E template-clone pattern becomes justifiable. **Do not pre-optimize for that** — Option B comfortably absorbs the Slice 0–4 roadmap.

If contention becomes a real problem before you hit 500 tests, the escape hatches are (1) split tests into multiple test assemblies (each gets its own container via its own AssemblyFixture) and run them in parallel via `dotnet test --filter` in separate CI jobs, or (2) introduce IntegreSQL for per-test database clones while keeping the same WebApplicationFactory.

## Marten coexistence notes

Marten 8.28 runs on .NET 10 in practice, but **JasperFx's migration guide still says ".NET 10 is untested"** — Marten 9 (tracking Npgsql 10, which drops sync APIs) is the version that will formally target .NET 10. For RunCoach, pin `Marten ≥ 8.20` and monitor the JasperFx/marten repo. If a .NET-10-specific bug surfaces, the escape hatch is to downgrade the test assembly alone to `net9.0` until Marten 9 ships — the test assembly does not need to match the SUT's TFM.

Four concrete rules govern coexistence at startup and in tests:

1. **Schema isolation by configuration.** `opts.DatabaseSchemaName = "marten"` and `opts.Events.DatabaseSchemaName = "marten"` keep every Marten-created object (`mt_doc_*`, `mt_upsert_*`, `mt_hilo`, `mt_events`, `mt_streams`) out of `public`, which EF Core owns. Confirmed no known collisions with `__EFMigrationsHistory`.
2. **Order: EF migrate first, Marten apply second.** EF's `Database.MigrateAsync()` acquires its own `IMigrationsDatabaseLock`; Marten uses a Postgres advisory lock (`StoreOptions.ApplyChangesLockId`). The locks don't contend, but running EF first makes the pending-changes warning (EF 9+) surface before Marten starts churning. In the fixture above, Marten's schema is applied by `ApplyAllDatabaseChangesOnStartup()` as a hosted service during host boot; EF's `MigrateAsync()` runs in the scope right after.
3. **Do not point Respawn at the `marten` schema.** Respawn is table-aware and will happily delete rows from `mt_hilo`, breaking HiLo id generation on the next test; it also won't know to stop the async daemon before deleting projection state. Use Respawn on `public` and `Host.ResetAllMartenDataAsync()` on Marten.
4. **Production `AutoCreate = None`, via `CritterStackDefaults`.** In production Marten schema changes ship as SQL scripts generated by `dotnet run -- db-patch`, committed to git, and applied out-of-band — analogous to EF bundles. In tests and dev, `AutoCreate.CreateOrUpdate` is fine. Do not call `CompletelyRemoveAllAsync()` between every test: it drops DDL and re-creates it, making the suite order-of-magnitude slower than `ResetAllData()`.

One forward-looking caveat: if you later adopt Marten's EF Core projection feature (`Marten.EntityFrameworkCore`), EF entity tables for those projections are migrated *by Marten/Weasel*, not by `dotnet ef database update`. That creates a split-brain where some EF tables are EF-migrated and others are Marten-migrated. RunCoach at Slice 1 does not use this feature; flag it for revisit when event-sourced read models arrive.

## Library version pins (April 2026)

| Package | Pin | Notes |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 10.0.x | LTS through Nov 2028 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.x | Aligns with EF 10 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.x | In migrations-owning project |
| `dotnet-ef` (global tool) | 10.0.6+ | For `migrations bundle` |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.x | For `WebApplicationFactory<Program>` |
| `xunit.v3` | **3.2.2** | v3 GA was July 2025 |
| `xunit.runner.visualstudio` | 3.x (≥ 3.0) | Required for v3 discovery |
| `Microsoft.NET.Test.Sdk` | 17.x | v3 test projects are `<OutputType>Exe</OutputType>` |
| `Testcontainers.PostgreSql` | **4.11.0** | Requires explicit image pin; supports Docker Engine 29 |
| `Respawn` | **7.0.0** (Nov 30 2025) | Still maintained by Jimmy Bogard; Apache-2.0; `ResetAsync` API since v5 |
| `Marten` | ≥ **8.20**, current **8.28.0** | .NET 10 officially "untested"; works in practice |
| `JasperFx` | 1.23.0 | Replaces the old `Oakton` CLI |
| Postgres image | `postgres:17-alpine` | Pin the tag explicitly — Testcontainers 4.11 requires it |

## Failure-mode catalog and escape hatches

**Test-fixture failure modes.** (1) A test under test that commits its own transaction inside a sub-scope (outbox pattern, Marten session) will defeat an Option D transactional fixture — **this is why Option D is rejected in the matrix**, not merely deprecated. (2) Respawn pointed at the `marten` schema deletes `mt_hilo` rows and breaks id generation on the next test; always `SchemasToInclude = ["public"]`. (3) Without `MartenDaemonModeIsSolo()`, rapid host restarts hang 30+ seconds on advisory-lock leader election. (4) `WithReuse(true)` in CI is pointless (ephemeral runners) and also does **not** reset data between runs — the fixture must still run Respawn/Marten reset on every test, not just on a fresh container. (5) `Testcontainers.Xunit` 4.11+ requires explicit image tags; omitting the tag now throws at build time.

**Migration failure modes.** (1) `CREATE INDEX CONCURRENTLY` in a migration that also contains other operations will fail with SQLSTATE 25001; split it into its own migration file. (2) `ALTER TYPE ... ADD VALUE` has the same constraint. (3) Applying an idempotent script without `psql -v ON_ERROR_STOP=1` silently continues past failures — always use the flag. (4) On Windows-2022 GitHub Actions runners, cross-compiling a bundle with `--target-runtime win-x64` can fail with "file is locked by .NET Host"; build bundles on Ubuntu runners targeting the actual prod runtime. (5) `EnsureCreated()` mixed with `Migrate()` leaves the history table in a bad state — the EF9 `PendingModelChangesWarning` catches this loudly, but don't mix them in the first place. (6) Marten's `TypeLoadMode.Static` embeds the schema name in generated code; changing `DatabaseSchemaName` requires `dotnet run -- codegen write`. Keep static codegen off in tests (it is, by default, in `Development`).

**Forward compatibility escape hatches.** (1) If RunCoach adopts .NET Aspire before public beta, keep the bundle-as-Job path for production and use the Aspire migration-worker only for local multi-service orchestration — Aspire's non-ACA K8s publisher is still early-stage. (2) If the test suite crosses ~500 tests and Respawn becomes the bottleneck, introduce `IntegreSQL` (has a .NET client, `IntegreSQL.EF` by `@Shaddix`) or `pgtestdb` to manage a warm pool of pre-migrated template databases; the AssemblyFixture shape does not change, only the reset mechanism. (3) If Marten 9 ships and formally supports .NET 10, upgrade the Marten package and delete any test-assembly-specific TFM downgrade.

## Conclusion — what Slice 0 should lock in

The non-obvious finding is that **the EF Core 9 migration lock genuinely changes the argument about `Database.Migrate()` on startup — but not enough to change the recommendation for production.** It does, however, kill the last objection to using `Migrate()` in dev and in CI fixtures with total confidence, so RunCoach gets a friction-free inner loop *and* a production-ready deploy story from one codebase, with the bundle artifact as the crossover point between them.

The xUnit v3 recommendation is the one that most changes from 2024 advice. As recently as a year ago, the default template was `IClassFixture<PostgresFixture>` + Respawn, paying the container cost N times per assembly. **xUnit v3's `[assembly: AssemblyFixture]` has made the per-class fixture obsolete for database-backed tests** — the assembly-fixture pattern pays the container cost once, supports cross-class parallelism, and is what Testcontainers.Xunit now nudges you toward via its `ContainerFixture` module. If RunCoach locks in the per-class pattern at Slice 0, every later slice inherits a needlessly slow suite. The AssemblyFixture pattern is the one to inherit.

The Marten .NET 10 status is the single item that deserves ongoing vigilance: as of April 2026 it is "works, not certified." Pin `Marten ≥ 8.20`, keep `MartenDaemonModeIsSolo()` on in tests, keep Respawn out of the `marten` schema, and watch for Marten 9. Everything else — Respawn 7.0, Testcontainers 4.11, xUnit v3.2.2, EF Core 10 bundles — is stable and ready for a pattern the rest of the project can safely inherit from Slice 0 forward.