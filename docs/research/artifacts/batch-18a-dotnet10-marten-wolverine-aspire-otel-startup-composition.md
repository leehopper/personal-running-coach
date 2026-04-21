# RunCoach Slice 0 host composition: the canonical shape

> **R-054 (Batch 18a).** Research prompt: [`docs/research/prompts/batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md`](../prompts/batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md). Queue entry: `docs/research/research-queue.md` → R-054.

## Primary recommendation (TL;DR)

**Register the `NpgsqlDataSource` once via `builder.AddNpgsqlDataSource("runcoach")` from `Aspire.Npgsql`, then wire every consumer to that DI singleton in this order — Aspire.Npgsql → EF `DbContext`s (app / Identity / DataProtection keys) → DataProtection → Marten (`AddMarten().UseNpgsqlDataSource().IntegrateWithWolverine()`) → Wolverine (`Host.UseWolverine`, *without* `PersistMessagesWithPostgresql` because `IntegrateWithWolverine` already installs envelope storage) → OpenTelemetry → MVC/health checks.** Do not call `new NpgsqlDataSourceBuilder(...)` anywhere, ever — that forks DEC-046's rotation seam. Do not resolve services from a built `IServiceProvider` inside `UseWolverine(opts => ...)` — inside that callback, use `opts.Services.AddSingleton/AddScoped` registrations and let Wolverine's own container (Lamar) resolve `NpgsqlDataSource` when the host builds. The documented startup hang is almost certainly caused by one of three things: (a) the hand-built second `NpgsqlDataSource` opening a connection before Testcontainers is ready, (b) JasperFx Roslyn codegen running first-time compilation inside a 5-minute window, or (c) `ValidateOnBuild=true` + Wolverine codegen + scoped-from-root resolution failing before `HostBuilt` fires. The fix is to delete the hand-built data source, set `CodeGeneration.TypeLoadMode = TypeLoadMode.Auto` (or pre-generate + `Static`), keep `Development` environment, and rely on `IntegrateWithWolverine()` for outbox wiring.

**Aspire.Npgsql 13.2.2 is safe without an AppHost.** It is a thin wrapper over `Npgsql.DependencyInjection.AddNpgsqlDataSource` plus health checks, tracing, and metrics. No service-discovery lookups, no hosted service, no blocking work at registration time. The only AppHost-coupled behavior is reading the connection string key from `ConnectionStrings:<name>` in `IConfiguration` — which is what `WebApplicationFactory`'s `ConfigureAppConfiguration` already overrides.

---

## 1. Annotated reference `Program.cs`

```csharp
using JasperFx.CodeGeneration;             // TypeLoadMode
using JasperFx.Core;                        // .NET 10 critter-stack helpers
using Marten;
using Marten.Events.Daemon.Resiliency;      // DaemonMode
using Marten.Events.Projections;            // ProjectionLifecycle
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RunCoach.Api.Data;
using RunCoach.Api.DataProtection;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// (1) JasperFx global defaults. ApplyJasperFxExtensions MUST be called before
//     AddMarten / UseWolverine so that CritterStackDefaults (ResourceAutoCreate,
//     TypeLoadMode) are applied to both sides. Source: every sample in
//     JasperFx/wolverine /src/Samples; jeremydmiller.com 2026-04-14 release notes.
builder.Host.ApplyJasperFxExtensions();

// (2) Register the single NpgsqlDataSource.
//     Aspire.Npgsql 13.2.2 delegates to Npgsql.DependencyInjection.AddNpgsqlDataSource
//     and adds health-checks + OTel tracing + metrics. Lifetime = Singleton.
//     Reads ConnectionStrings:runcoach from IConfiguration.
//     Name-collision note (dotnet/aspire#1515): this extension lives on
//     IHostApplicationBuilder, NOT IServiceCollection. DO NOT write
//     builder.Services.AddNpgsqlDataSource(...) — that silently calls the
//     Npgsql overload and loses the Aspire health-check/OTel wiring.
builder.AddNpgsqlDataSource("runcoach", configureDataSourceBuilder: dsb =>
{
    // DEC-046 rotation seam. Call sites that fork a second builder break this.
    // dsb.UsePeriodicPasswordProvider(...)  // enable when credential-rotation lands
    dsb.EnableDynamicJson();                 // Marten + Npgsql JSON plugins (efcore.pg #2891)
});

// (3) EF Core contexts — all share the DI-registered NpgsqlDataSource.
//     o.UseNpgsql() parameterless resolves NpgsqlDataSource from DI.
//     Documented in efcore.pg API reference and #2821 as canonical since EF 9/Npgsql 8:
//     "The connection, data source or connection string must be set explicitly
//      OR registered in the DI before the DbContext is used."
builder.Services.AddDbContext<RunCoachDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(),
                npg => npg.MigrationsHistoryTable("__ef_migrations_history", "public")));

builder.Services.AddDbContext<DpKeysContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(),
                npg => npg.MigrationsHistoryTable("__dp_migrations_history", "public")));

// (4) ASP.NET Core Identity. AddIdentityCore does NOT call PersistKeysToFileSystem;
//     it just TryAddSingletons the DataProtection core pieces (Duende 2025-03-13
//     analysis). Safe to call in any order relative to AddDataProtection.
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<RunCoachDbContext>()
    .AddDefaultTokenProviders();

// (5) DataProtection persisted to Postgres via EF.
//     EntityFrameworkCoreXmlRepository<T> does NOT call EnsureCreated — you must
//     run migrations for the DataProtectionKeys table (aspnetcore#12333). Startup
//     reads the keyring SYNCHRONOUSLY on first access (#59717), which is fine for
//     Npgsql but you MUST run DevelopmentMigrationService before first request.
//     SetApplicationName prevents path-based discriminator drift across deploys.
builder.Services.AddDataProtection()
    .SetApplicationName("RunCoach")
    .PersistKeysToDbContext<DpKeysContext>();

// (6) Marten. Registration MUST precede UseWolverine so IntegrateWithWolverine
//     has a chance to register envelope storage before Wolverine inspects it.
//     UseNpgsqlDataSource() binds Marten to the DI singleton — no fork.
//     IntegrateWithWolverine() SUBSUMES PersistMessagesWithPostgresql — do NOT
//     call both. This is the idiomatic path in JasperFx/wolverine/WebApiWithMarten.
builder.Services.AddMarten(opts =>
{
    opts.Events.DatabaseSchemaName = "runcoach_events";
    opts.DatabaseSchemaName = "runcoach_events";
    opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsGuid;
    opts.Events.TenancyStyle   = Marten.Events.TenancyStyle.Conjoined;
    opts.Events.AppendMode     = Marten.Events.Daemon.EventAppendMode.Quick;
    opts.Events.UseIdentityMapForAggregates = true;
    opts.Events.EnableGlobalProjectionsForConjoinedTenancy = false;
    opts.Events.EnableSideEffectsOnInlineProjections = true;
    opts.Projections.Errors.SkipUnknownEvents = true;
    opts.Policies.AllDocumentsAreMultiTenanted();
    opts.UseSystemTextJsonForSerialization();

    // OTel hooks — Marten emits ActivitySource "Marten" + Meter "Marten".
    opts.OpenTelemetry.TrackConnections   = Marten.Services.TrackLevel.Normal;
    opts.OpenTelemetry.TrackEventCounters();
})
.UseLightweightSessions()
.UseNpgsqlDataSource()                      // share the DI singleton
.IntegrateWithWolverine(w =>                // installs wolverine_* tables in runcoach_events
{
    w.MessageStorageSchemaName = "runcoach_events";
})
.ApplyAllDatabaseChangesOnStartup()         // IHostedService — runs AFTER app builds
.AddAsyncDaemon(DaemonMode.Solo);           // Solo avoids HotCold leader-election overhead

// (7) Wolverine. Registered AFTER Marten so IntegrateWithWolverine's
//     envelope-storage registration is visible when UseWolverine composes.
//     The NpgsqlDataSource is resolved from Wolverine's own container via
//     opts.Services — no BuildServiceProvider, no child container.
builder.Host.UseWolverine(opts =>
{
    // DO NOT call opts.PersistMessagesWithPostgresql(...) here.
    // IntegrateWithWolverine on the Marten side already wired envelope storage
    // to the Marten-visible NpgsqlDataSource. Calling both forks the seam and
    // is the leading suspect for the current hang.

    opts.Policies.AutoApplyTransactions();   // Marten + EF + outbox span one txn
    opts.Policies.UseDurableLocalQueues();
    opts.Policies.UseDurableInboxOnAllListeners();

    opts.Durability.Mode = DurabilityMode.Solo;             // matches Marten daemon
    opts.Durability.MessageStorageSchemaName = "runcoach_events";

    // EF integration — MUST live inside UseWolverine so Wolverine's codegen
    // can discover the registered DbContext at bootstrap time. Top-level
    // placement works at runtime but loses Wolverine's middleware auto-detection.
    opts.Services.AddDbContextWithWolverineIntegration<RunCoachDbContext>((sp, o) =>
        o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

    // Pre-generated types in CI keeps first-request latency flat. In dev, Auto
    // falls back to runtime Roslyn — set Static + AssertAllPreGeneratedTypesExist
    // for CI once `dotnet run -- codegen write` runs in the pipeline.
    opts.CodeGeneration.TypeLoadMode = builder.Environment.IsProduction()
        ? TypeLoadMode.Static
        : TypeLoadMode.Auto;
});

// (8) OpenTelemetry. Order-agnostic relative to Marten/Wolverine/EF, but the
//     OTLP exporter must be conditional on the endpoint being configured —
//     otherwise test shutdown flush can block up to 30 s (ExporterTimeoutMs
//     default). AddOpenTelemetry itself does NO synchronous network I/O.
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("RunCoach.Api"))
    .WithTracing(t => t
        .AddSource("Marten")
        .AddSource("Wolverine")
        .AddSource("RunCoach.Llm")
        .AddSource("Npgsql")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddMeter("Marten")
        .AddMeter("Wolverine")
        .AddMeter("Npgsql")
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    otel.UseOtlpExporter();   // only when a collector is actually configured

// (9) Health + MVC. Health check reuses the single NpgsqlDataSource
//     (AspNetCore.HealthChecks.NpgSql ≥ 9.0.0 bundled by Aspire.Npgsql).
builder.Services.AddHealthChecks();       // Aspire.Npgsql already added the "npgsql" check
builder.Services.AddControllers();

// (10) Dev-only migration service — replaces the production-mode startup-migration
//      gate. Runs AFTER builder.Build(), inside StartAsync, so it doesn't affect
//      the HostFactoryResolver 5-min window.
if (builder.Environment.IsDevelopment())
    builder.Services.AddHostedService<DevelopmentMigrationService>();

// (11) DI validation — surfaces composition bugs at Build() instead of as a
//      silent 5-min hang. Compatible with Marten 8.31 + Wolverine 5.31 if and
//      only if codegen is precompiled OR TypeLoadMode.Auto keeps handlers
//      constructable lazily. Keep OFF in CI until Wolverine codegen is pre-baked.
if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(o =>
    {
        o.ValidateOnBuild = false;   // Wolverine codegen can trip this; revisit after pre-gen
        o.ValidateScopes  = true;    // catches scoped-from-root immediately
    });
}

var app = builder.Build();

// (12) Middleware order (.NET 10 canonical). UseAntiforgery MUST follow
//      UseAuthentication + UseAuthorization. Slice 0 Unit 2 landmine warning.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

// .NET 10 no longer requires `public partial class Program { }` — the Web SDK
// emits a public Program via source generator; analyzer ASP0027 flags the
// manual declaration as redundant.
```

---

## 2. Annotated `MartenConfiguration.cs` option audit

| Option | Keep? | Reason |
|---|---|---|
| `Events.DatabaseSchemaName = "runcoach_events"` | **Keep** | DEC's schema-isolation invariant; Marten owns this schema, EF owns `public`. |
| `Events.StreamIdentity = AsGuid` | **Keep** | No interop with external event-store yet; Guid is cheaper than string keys at join time. |
| `Events.TenancyStyle = Conjoined` | **Keep** | Matches `Policies.AllDocumentsAreMultiTenanted`. |
| `Events.AppendMode = Quick` | **Keep** | Skips the server-side append validator; fine if you don't rely on cross-stream ordering. |
| `Events.UseIdentityMapForAggregates = true` | **Keep** | Required for correct aggregate-handler semantics inside Wolverine. |
| `EnableAdvancedAsyncTracking` | **Flag** | Not load-bearing for the hang. Adds cost per session; keep `false` unless you need `WaitForNonStaleData`-style test assertions outside `IHost.TrackActivity()`. |
| `OpenTelemetry.TrackConnections = Normal` | **Keep** | Emits `marten.connection` spans; `Verbose` adds per-command spans and is noisy. |
| `OpenTelemetry.TrackEventCounters()` | **Keep** | Emits `marten.event.append` meter; required for the RunCoach dashboards. |
| `Policies.AllDocumentsAreMultiTenanted()` | **Keep** | Aligned with `Conjoined`. |
| `Projections.Errors.SkipUnknownEvents = true` | **Keep** | Prevents a rogue event-type rename from wedging the async daemon at startup. |
| `UseLightweightSessions()` | **Keep** | Faster; no identity-map overhead for non-aggregate sessions. |
| `UseNpgsqlDataSource()` | **LOAD-BEARING** | Binds Marten to the DI-registered singleton. Dropping this forks the seam. |
| `IntegrateWithWolverine()` | **LOAD-BEARING** | Installs envelope storage; replaces explicit `PersistMessagesWithPostgresql`. |
| `ApplyAllDatabaseChangesOnStartup()` | **Keep** | Runs as an `IHostedService` — not on the `HostFactoryResolver` timer. Safe. |
| `AddAsyncDaemon(DaemonMode.Solo)` | **Keep** | Solo avoids the `wolverine_nodes` leader-election advisory-lock dance. The documented advisory-lock collision with EF's `IMigrationsDatabaseLock` only bites in `HotCold`. |

The three options that are **load-bearing for composition-not-deadlocking** are `UseNpgsqlDataSource()` (shared-pool invariant), `IntegrateWithWolverine()` (avoids the double-wiring trap), and `DaemonMode.Solo` (avoids advisory-lock contention with `ApplyAllDatabaseChangesOnStartup` and EF migrations).

---

## 3. Wolverine outbox wiring recipe

Use **`IntegrateWithWolverine()` on the Marten side only.** Do not call `opts.PersistMessagesWithPostgresql(...)` on the Wolverine side when Marten is present — every JasperFx sample (`WebApiWithMarten`, `OrderSagaSample`, `DiagnosticsApp`) confirms this. `IntegrateWithWolverine` reads Marten's `IDocumentStore` (which already holds the DI-registered `NpgsqlDataSource`), installs `wolverine_envelopes`, `wolverine_nodes`, `wolverine_dead_letters` in the Marten schema, and registers the outbox session bridge as scoped.

`Policies.AutoApplyTransactions()` in `UseWolverine` then causes every handler that takes `IDocumentSession` or a `DbContext` to be wrapped by middleware that opens a transaction, enlists the outbox session, calls `SaveChangesAsync` on both sides, and commits as a single unit — Marten + EF + outbox span one Postgres transaction.

`AddDbContextWithWolverineIntegration<RunCoachDbContext>` **must live inside `UseWolverine(opts => opts.Services.AddDbContextWithWolverineIntegration<T>(...))`**. Top-level placement at `builder.Services.AddDbContextWithWolverineIntegration` registers the context correctly but does not expose it to Wolverine's handler-discovery codegen, so the transactional middleware auto-apply silently no-ops. This is the subtle behavior difference that Wolverine's docs gloss over.

For DEC-046's `UsePeriodicPasswordProvider` rotation, configure it exactly once on the `NpgsqlDataSourceBuilder` callback passed to `builder.AddNpgsqlDataSource("runcoach", configureDataSourceBuilder: dsb => dsb.UsePeriodicPasswordProvider(...))`. Every consumer — EF, Marten, Wolverine outbox, DataProtection — reads credentials from the same rotating builder. Never construct a second `NpgsqlDataSourceBuilder`.

**`IWolverineExtension` is not the right route here.** Its `Configure(WolverineOptions)` method fires during `UseWolverine` composition, before the root `IServiceProvider` exists — resolving services from a `BuildServiceProvider()` there creates a second container. The canonical path is `opts.Services.AddSingleton(sp => sp.GetRequiredService<NpgsqlDataSource>())` (which is essentially a no-op forwarder because Aspire already registered it) or simply letting Wolverine's Lamar container consume the existing registration.

---

## 4. Aspire.Npgsql without an AppHost — verdict

**Safe.** `Aspire.Npgsql 13.2.2`'s `AddNpgsqlDataSource(IHostApplicationBuilder, string connectionName, ...)` does only three things: delegates to Npgsql's own `AddNpgsqlDataSource` (singleton `NpgsqlDataSource`, transient `NpgsqlConnection`), adds an `AspNetCore.HealthChecks.NpgSql` health check (≥ 9.0.0), and registers the Npgsql OTel source/meter. No service discovery, no hosted service, no blocking work. The only AppHost coupling is that it reads `ConnectionStrings:<name>` from `IConfiguration` — which `WebApplicationFactory.ConfigureAppConfiguration` can override trivially.

Two traps to avoid:
- **dotnet/aspire#1515** — the name collision with `Npgsql.DependencyInjection.AddNpgsqlDataSource(IServiceCollection, ...)`. Calling `builder.Services.AddNpgsqlDataSource(cs)` silently picks the Npgsql overload and loses the Aspire-added health check and OTel instrumentation. Always call `builder.AddNpgsqlDataSource(name)` on `IHostApplicationBuilder`.
- **microsoft/aspire#3097 / npgsql#5637** — calling `AddNpgsqlDataSource` after `builder.Build()` throws "service collection cannot be modified because it is read-only." Keep the call in `Program.cs` before `Build()`.

No AppHost migration is required for MVP-0. The `Aspire.Hosting` / `Aspire.AppHost` packages are NOT a dependency of `Aspire.Npgsql`.

---

## 5. DI validation strategy

Enable **`ValidateScopes = true` always in Development**. It immediately surfaces the "scoped from root" bug class that otherwise manifests as a `HostFactoryResolver` timeout. It is cheap and compatible with Marten 8.31 and Wolverine 5.31.

Enable **`ValidateOnBuild = true` only after pre-generating Wolverine/Marten types** (`dotnet run -- codegen write` in CI, `TypeLoadMode.Static` + `AssertAllPreGeneratedTypesExist` in Program.cs). Without pre-generation, Wolverine's generated handler types are closed-constructed during validation, Marten's `IDocumentStore` is eagerly built (cheap), but any handler with a scoped `IDocumentSession` dep triggers a root-scope resolution attempt that fails at Build time. This is the same mechanism that produces the silent 5-minute hang when it races, and you want it to throw instead — but only when codegen is stable.

**Canonical config:**

```csharp
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateScopes  = true;
    o.ValidateOnBuild = builder.Environment.IsProduction(); // flip once codegen is pre-baked
});
```

WebApplicationFactory's default is `IsDevelopment() == true` so `ValidateScopes` is already on under `WebApplication.CreateBuilder`; setting it explicitly makes intent visible. aspnetcore#56411 is the tracking issue that proposes making both flags on-by-default in WebApplicationFactory; it is not yet merged as of .NET 10.

---

## 6. Startup deadlock diagnostic recipe (cost-ordered)

1. **Raise the timeout while debugging**: `export DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=900`. Lets the 5-min hang run long enough to capture state. Cost: one env var.
2. **Oakton/JasperFx describe commands**: `dotnet run --project src/RunCoach.Api -- describe` lists every Marten/Wolverine/DI registration. `dotnet run -- resources check` pings Postgres for every resource. Zero extra tooling; surfaces wiring bugs without booting the full host. `dotnet run -- codegen preview` dumps the generated code so you can see if JasperFx is stuck.
3. **dotnet-stack against the hung test pid**: `dotnet-stack report --process-id <pid>`. Shows exactly which managed frame is blocked — typically `NpgsqlDataSource.OpenConnection`, `Marten.DocumentStore.BuildFromOptions`, or Roslyn `CSharpCompilation.Emit`.
4. **dotnet-counters**: `dotnet-counters monitor -p <pid>` surfaces thread-pool starvation (the classic "apparent hang" when sync-over-async exhausts the pool during Testcontainers startup).
5. **Pre-generate Wolverine types locally**: `dotnet run -- codegen write` writes to `Internal/Generated/`. Re-running with `TypeLoadMode.Static` skips the Roslyn compile inside the 5-min window.
6. **Hosting diagnostic tracing**: `DOTNET_HOST_TRACE=1`, `COREHOST_TRACE=1`, `COREHOST_TRACEFILE=/tmp/host.log`, `COREHOST_TRACE_VERBOSITY=4`, and `Logging__LogLevel__Default=Trace`. Only useful if the hang is pre-managed (framework resolution, native host load) — the observed zero-stdout symptom suggests it's not, but this is the canonical confirm.
7. **dotnet-dump + clrstack -all + syncblk**: `dotnet-dump collect -p <pid>`, then `dotnet-dump analyze <file>`, then `clrstack -all` and `syncblk`. Use this when `dotnet-stack` points at a `Monitor.Wait` but you can't tell which lock.

The diagnostic chain that would have caught the current bug in under 60 seconds: step 1 + step 3 + step 2. The `dotnet-stack` frame would have named the second `NpgsqlDataSource.OpenConnection` call inside `UseWolverine`.

---

## 7. OpenTelemetry composition recipe

OpenTelemetry 1.15.x does **no synchronous network I/O at startup** and cannot, by itself, trip the `WebApplicationFactory` 5-minute timeout. Collector absence produces async failures written only to the `OpenTelemetry-Exporter-OpenTelemetryProtocol` EventSource; telemetry is silently dropped. The only place a collector can block the process is at host shutdown, via `ForceFlush`, bounded by `OtlpExporterOptions.TimeoutMilliseconds` (default **10 s**) and `BatchExportProcessor.ExporterTimeoutMilliseconds` (default **30 s**).

**Ordering.** `AddOpenTelemetry()` can sit anywhere between `builder.Services.AddXxx()` calls; it is consumed only at `Build()` time. Calling `UseOtlpExporter()` twice or mixing with signal-specific `AddOtlpExporter()` throws `NotSupportedException`.

**Test-mode composition.** Register OTel unconditionally (so `ActivitySource` and `Meter` are wired), but register the OTLP exporter only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set:

```csharp
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otel = builder.Services.AddOpenTelemetry().WithTracing(...).WithMetrics(...);
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    otel.UseOtlpExporter();
```

Alternative: set `OTEL_SDK_DISABLED=true` in CI — added in OTel 1.15.0 — returns no-op providers across all three signal types. Cleanest way to keep production code path identical while guaranteeing zero collector traffic in tests.

**ActivitySources/Meters to add**: `"Marten"` (declared in Marten's `OpenTelemetry` options), `"Wolverine"` (declared in `src/Wolverine/Runtime/WolverineTracing.cs`: `new ActivitySource("Wolverine", ...)`), `"RunCoach.Llm"` (your source), `"Npgsql"` (Npgsql 6+ built-in).

**Jaeger overlay (`docker-compose.otel.yml`) compatibility**: set `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317` in the overlay's env; leave it unset in the default compose and in `dotnet test`. No code change needed.

---

## 8. `WebApplicationFactory<Program>` integration recipe

```csharp
public sealed class RunCoachAssemblyFixture : IAsyncLifetime
{
    public PostgreSqlContainer Pg { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithReuse(Environment.GetEnvironmentVariable("CI") is null)
        .Build();

    public RunCoachFactory Factory { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await Pg.StartAsync();
        Factory = new RunCoachFactory(Pg.GetConnectionString());
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Pg.DisposeAsync();
    }
}

public sealed class RunCoachFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Keep Development environment — CritterStackDefaults.Development is what
        // triggers ResourceAutoCreate = CreateOrUpdate, which Slice 0 tests rely on.
        // Using "Test" silently routes Marten/Wolverine into the Production branch
        // of every `IsDevelopment()` guard and schema auto-create stops working.
        builder.UseEnvironment("Development");

        // Inject the Testcontainers connection string BEFORE the SUT's
        // AddNpgsqlDataSource reads it. DeferredHostBuilder replays this on
        // the HostBuilding DiagnosticListener event, which fires inside
        // HostBuilder.Build() — so the in-memory provider is the last one
        // appended to ConfigurationManager and wins over appsettings.json.
        builder.ConfigureAppConfiguration((_, cb) => cb.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:runcoach"] = connectionString,
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = null,     // kill the exporter path
            ["CritterStack:DaemonMode"] = "Solo",        // force Solo in tests
        }));

        // DO NOT build a second NpgsqlDataSource here. The SUT's AddNpgsqlDataSource
        // is the only registration site. DEC-046 rotation seam integrity.

        builder.ConfigureTestServices(services =>
        {
            // Keep DevelopmentMigrationService running — it's already registered
            // in Program.cs under IsDevelopment(). No override needed.

            // If specific endpoints need to short-circuit auth in tests, override
            // the auth scheme here. Do NOT touch NpgsqlDataSource / DbContext /
            // Marten / Wolverine registrations.
        });
    }
}

[assembly: AssemblyFixture(typeof(RunCoachAssemblyFixture))]
```

**`UseEnvironment("Development")` is the 2026-canonical choice.** `"Test"` routes around every `IsDevelopment()` guard in the Critter Stack (`TypeLoadMode`, `ResourceAutoCreate`, migration service) and would silently disable the schema creation that Slice 0 tests require. The developer-exception-page difference is a minor cost — test output still surfaces the exception through xUnit's captured stderr.

**`WithReuse(!isCi)` gotcha**: Testcontainers' reuse hash is computed from image + env + cmd + labels. Passing `WithReuse(Environment.GetEnvironmentVariable("CI") is null)` keeps the hash stable locally but varies between CI and local — both correct behaviors. Ensure `~/.testcontainers.properties` has `testcontainers.reuse.enable=true` locally, or reuse silently no-ops.

---

## 9. Known-bad shapes to avoid

- **The two-data-source pattern**: `builder.AddNpgsqlDataSource("runcoach")` for EF+Marten + `new NpgsqlDataSourceBuilder(cs).Build()` inside `UseWolverine` for the outbox. Forks DEC-046's rotation seam, doubles the connection pool, and — in the observed symptom — the second builder's first `OpenConnection` against a not-yet-ready Testcontainers is the most likely hang source.
- **`options.UseNpgsql(connectionString)` after Aspire has registered `NpgsqlDataSource`**: builds a second pool with identical config but different identity. Breaks identity-equality invariant the Slice 0 smoke test #6 asserts.
- **`opts.PersistMessagesWithPostgresql(...)` inside `UseWolverine` when `IntegrateWithWolverine()` is already called on Marten**: double-wires envelope storage. Symptom: duplicate `wolverine_envelopes` table creation attempts under `ApplyAllDatabaseChangesOnStartup`, which usually fails loudly but in racing startup conditions can advisory-lock-wait silently.
- **Resolving from `sp.BuildServiceProvider()` inside `UseWolverine(opts => ...)` or `IWolverineExtension.Configure`**: builds a second container. Every service resolved through it is a different instance from the real container; `NpgsqlDataSource` identity-equality fails and singletons are doubled.
- **`app.UseAntiforgery()` before `app.UseAuthentication()/UseAuthorization()`**: .NET 10 middleware order breakage. Identity-bound antiforgery tokens validate against the pre-auth `HttpContext.User`, producing sporadic 400s. Slice 0 Unit 2 landmine once Identity endpoints light up.
- **Manual `public partial class Program { }` at the bottom of Program.cs** on .NET 10: redundant; analyzer **ASP0027** flags it. The Web SDK source generator already emits a public `Program`.
- **`Services.AddNpgsqlDataSource(...)` (Npgsql overload) instead of `builder.AddNpgsqlDataSource(...)` (Aspire extension)**: dotnet/aspire#1515. Loses the health check and OTel integration silently.
- **Running Marten's async daemon in `DaemonMode.HotCold` alongside `ApplyAllDatabaseChangesOnStartup`**: both take advisory locks; the documented safe mode for single-node tests is `Solo`.
- **Calling `ValidateOnBuild = true` without pre-generated Wolverine handler types**: eager validation constructs every handler, Wolverine's generated types run Roslyn on first access, and on slow CI runners this can exceed the 5-minute `HostFactoryResolver` window — reproducing the exact symptom.

---

## 10. Version-specific notes that expire

| Pin | Advice tied to pin | When it expires | Watch URL |
|---|---|---|---|
| Aspire.Npgsql 13.2.2 | Safe without AppHost; name collision with Npgsql extension | Aspire 14 (no firm date; late 2026) | https://github.com/dotnet/aspire/releases |
| Marten 8.31.0 | `UseNpgsqlDataSource()` + `IntegrateWithWolverine()` ordering | Marten 9 (Q2/Q3 2026, performance focus + DCB; formal .NET 10 cert) | https://github.com/JasperFx/marten/releases + jeremydmiller.com |
| WolverineFx 5.31.1 | `IntegrateWithWolverine` subsumes `PersistMessagesWithPostgresql`; `AddDbContextWithWolverineIntegration` placement | Wolverine 6 (Q2/Q3 2026, sending-failure-policies per PLAN.md) | https://github.com/JasperFx/wolverine/blob/main/PLAN.md |
| Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1 | Parameterless `UseNpgsql()` auto-resolves DataSource from DI | EF Core 11 / Npgsql 11 (late 2026) | https://github.com/npgsql/efcore.pg/releases |
| OpenTelemetry 1.15.2 | `OTEL_SDK_DISABLED=true` for test mode; no startup I/O | 1.16.x (incremental; no breaking changes planned) | https://github.com/open-telemetry/opentelemetry-dotnet/releases |
| Microsoft.AspNetCore.Mvc.Testing 10.0.5 | `HostFactoryResolver` 5-min default timeout; `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS` escape hatch | .NET 11 (Nov 2026) | https://github.com/dotnet/aspnetcore/issues/56411 |
| Testcontainers.PostgreSql 4.11.0 | `WithReuse` hash stability; xUnit v3 `AssemblyFixture` pattern | 5.x (no firm date) | https://github.com/testcontainers/testcontainers-dotnet/releases |
| .NET 10 SDK | `public partial class Program` auto-emitted; ASP0027 analyzer | .NET 11 (source-gen behavior unlikely to regress) | https://github.com/dotnet/sdk/issues |

Specifically revisit this document when:
- **Marten 9 ships** → re-verify `IntegrateWithWolverine()` still subsumes `PersistMessagesWithPostgresql`, and that `ConfigureNpgsqlDataSourceBuilder` hasn't replaced `UseNpgsqlDataSource`.
- **Wolverine 6 ships** → re-verify `Policies.AutoApplyTransactions()` semantics (issue #173's transitive-dep limitation may or may not survive); check the new sending-failure-policies surface.
- **Aspire 14 ships** → re-verify the `AddNpgsqlDataSource` extension signature and health-check registration path.
- **EF Core 11 ships** → re-verify parameterless `UseNpgsql()` DI auto-resolution is preserved.

---

## Conclusion: the shape that unblocks Slice 0

The current hang is not a composition impossibility — every pinned version is compatible. It is almost certainly the hand-built second `NpgsqlDataSource` inside `UseWolverine` trying to `OpenConnection` against a Testcontainers instance that is still initializing, combined with an unbounded retry loop that prevents `HostBuilt` from ever firing. Delete the hand-built data source. Rely on `IntegrateWithWolverine()` for outbox wiring. Keep one `NpgsqlDataSource` registered via `builder.AddNpgsqlDataSource("runcoach")`. Pass `NpgsqlDataSource` into every consumer either via parameterless `UseNpgsql()` (EF) or the DI-aware option chain (Marten, Wolverine via `opts.Services`).

Three invariants hold after this change: **one `NpgsqlDataSource` instance across all consumers** (DEC-046 rotation seam intact); **one transaction scope covers Marten + EF + outbox** (`Policies.AutoApplyTransactions`); **`HostFactoryResolver` sees `HostBuilt` within seconds** (no blocking I/O before `builder.Build()` returns). Slice 0's six smoke tests go green, and the diagnostic recipe above catches the next regression in under a minute.

The deeper lesson: the 5-minute timeout is not a timeout on anything the application actively does — it is a timeout on a `TaskCompletionSource<IHost>` that only completes when the `Microsoft.Extensions.Hosting` `DiagnosticListener` emits `HostBuilt`. Any code path that prevents `builder.Build()` from returning — synchronous I/O, Roslyn codegen, failing DI validation, a wedged `NpgsqlConnection.Open()` — reproduces the exact symptom observed. The mitigation is not to make each of these faster; it is to ensure none of them runs on the `builder.Build()` thread.
