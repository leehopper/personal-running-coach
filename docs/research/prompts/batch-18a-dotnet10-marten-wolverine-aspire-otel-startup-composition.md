# Research Prompt: Batch 18a — R-054

# .NET 10 + Marten 8.31 + WolverineFx 5.31 + Aspire.Npgsql 13.2 + OpenTelemetry 1.15 — canonical startup composition (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: What is the **canonical, known-working** DI + host composition for an ASP.NET Core 10 application that uses Aspire.Npgsql 13.2 as the single `NpgsqlDataSource` owner, EF Core 10 with `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` that also maps Wolverine envelope storage (`MapWolverineEnvelopeStorage()`), ASP.NET Core Data Protection persisting keys to that same Postgres via a second `DpKeysContext` implementing `IDataProtectionKeyContext`, Marten 8.31 registered in schema `runcoach_events` with `AddAsyncDaemon(DaemonMode.Solo)` + `ApplyAllDatabaseChangesOnStartup` + `.IntegrateWithWolverine()`, WolverineFx 5.31 with a Postgres outbox bound to the shared `NpgsqlDataSource` + `AddDbContextWithWolverineIntegration` + `Policies.AutoApplyTransactions`, and OpenTelemetry 1.15 (`AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation` + `AddOtlpExporter` + Marten/Wolverine/custom `ActivitySource` + `Meter` sources)? Specifically, produce a recipe that does NOT deadlock at startup, survives `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` host boot, and keeps a single shared `NpgsqlDataSource` so the DEC-046 password-rotation seam (`UsePeriodicPasswordProvider`) works restartlessly for every consumer.

## Context

I'm mid-implementation on Slice 0 (Foundation) of a personal AI-coaching app (RunCoach). The spec is at `docs/specs/12-spec-slice-0-foundation/` and the current composition is landed in six commits on `main` (not yet pushed; no PR yet). The persistence substrate is all wired, but the host is in a debugging mess: the SUT either hangs at startup or passes a subset of smoke tests depending on which pattern I try, and I no longer trust that I understand the canonical composition — I want to stop trial-and-error and land the known-good shape from a single research pass.

### Exact pin set (non-negotiable; don't recommend major bumps)

From `backend/Directory.Packages.props`:

- TFM: `net10.0` (.NET 10 GA).
- `Aspire.Npgsql` **13.2.2**.
- `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.1**.
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **10.0.6**.
- `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` **10.0.6**.
- `Microsoft.EntityFrameworkCore.Design` **10.0.6**.
- `Marten` **8.31.0**.
- `WolverineFx` **5.31.1**.
- `WolverineFx.EntityFrameworkCore` **5.31.1**.
- `WolverineFx.Marten` **5.31.1**.
- `OpenTelemetry` **1.15.2** + `.Extensions.Hosting` **1.15.2** + `.Exporter.OpenTelemetryProtocol` **1.15.2** + `.Instrumentation.AspNetCore` **1.15.1** + `.Instrumentation.Http` **1.15.0**.
- `Microsoft.AspNetCore.Mvc.Testing` **10.0.5**; `Testcontainers.PostgreSql` **4.11.0**; `Respawn` **7.0.0**; `xunit.v3` **3.2.2**.

These are the versions in the repo today. Assume I cannot pivot to a different major (e.g., Marten 9, Wolverine 6) unless you show concrete evidence the current majors cannot compose cleanly on .NET 10.

### What's happening

The SUT boot appears to deadlock reproducibly. The proof file at `docs/specs/12-spec-slice-0-foundation/01-proofs/T01.5-proofs.md` documented it this way:

> `WebApplication.CreateBuilder(args)` (or something it invokes at process-init time in our current package set) hangs indefinitely with zero log output — confirmed by running the compiled `RunCoach.Api.dll` directly for 120 s with no stdout / stderr emitted even with `DOTNET_HOST_TRACE=1`.
>
> Under `WebApplicationFactory`, this manifests as:
>
> ```text
> System.InvalidOperationException : Timed out waiting for the entry point
>   to build the IHost after 00:05:00.
>     at Microsoft.Extensions.Hosting.HostFactoryResolver.HostingListener.CreateHost()
>     at Microsoft.AspNetCore.Mvc.Testing.DeferredHostBuilder.Build()
>     at Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`1.StartServer()
> ```

I subsequently tried three attempted workarounds and am no longer certain which symptom I'm observing — hence this research pass.

### Candidate hypotheses I've already considered (tell me which are right, which are red herrings, and what I missed)

1. **Aspire.Npgsql 13.2.2 running outside an Aspire AppHost context.** Does `builder.AddNpgsqlDataSource("runcoach")` in a non-AppHost process (plain `dotnet run`, `dotnet RunCoach.Api.dll`, `WebApplicationFactory<Program>`) perform service-discovery lookups, health-check registrations, or hosted-service work that blocks indefinitely when no AppHost is present? What's the correct way to use Aspire client integrations from a project that is NOT orchestrated by `Aspire.Hosting`?
2. **Roslyn code-generation blocking startup.** Marten + Wolverine use JasperFx code generation. With `CritterStackDefaults` configured as `Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate` and `Production.GeneratedCodeMode = TypeLoadMode.Static` (generation-at-runtime in Dev, pre-generated in Prod), can the Roslyn-driven path synchronously block on assembly loading / MSBuild spawning / nuget restore inside the test process?
3. **Circular DI dependency between Marten's `IDocumentStore` and Wolverine's outbox.** `Marten.IntegrateWithWolverine()` + `Wolverine.PersistMessagesWithPostgresql(NpgsqlDataSource)` + `AddDbContextWithWolverineIntegration<RunCoachDbContext>` — is there a canonical ordering that avoids the outbox needing the Marten session which needs the EF context which needs the outbox?
4. **`IWolverineExtension` vs inline `opts.Services` registration.** Our earliest shape resolved `NpgsqlDataSource` from a built `IServiceProvider` via a registered `IWolverineExtension`. Does `IWolverineExtension.Configure` fire before or after the container is frozen? Is resolving from `IServiceProvider` inside `Configure` legal, or is it the equivalent of constructing a child container?
5. **`EnableAdvancedAsyncTracking = true` + `AddAsyncDaemon(DaemonMode.Solo)` + `ApplyAllDatabaseChangesOnStartup`.** Does the Solo-mode daemon take a Postgres advisory lock at `StartAsync` that conflicts with something else taking the same lock (EF `MigrateAsync` via `IMigrationsDatabaseLock`, Marten's own schema migrator, Wolverine's database setup)?
6. **Hosted-service registration order.** `DevelopmentMigrationService` (EF migrator) + Marten async daemon + Wolverine bus + OpenTelemetry exporter workers all run as `IHostedService`. Does .NET 10 start them sequentially or in parallel? Is there a canonical ordering we should enforce?
7. **OpenTelemetry `AddAspNetCoreInstrumentation` ordering with `UseWolverine`.** Wolverine registers ASP.NET Core middleware / hosted services. Does OTel's instrumentation need to register before or after Wolverine to avoid a "provider already built" error or a silent no-op?
8. **`opts.Services.AddDbContextWithWolverineIntegration` placement.** WolverineFx.EntityFrameworkCore's docs show the call living inside `UseWolverine(opts => ...)`. Is this the only valid placement, or can it live at the top level (`builder.Services.AddDbContextWithWolverineIntegration`)? What subtle behavior differs?
9. **`options.UseNpgsql()` (parameterless) vs `options.UseNpgsql(connectionString)` vs `options.UseNpgsql(dataSource)`.** For the shared-data-source story, which overload must be used on each of the three EF contexts (`RunCoachDbContext` registered via Wolverine, `DpKeysContext` registered top-level, anything added later)? Does the parameterless overload correctly resolve the Aspire-registered `NpgsqlDataSource` singleton, or does it expect a different registration shape?
10. **`HostFactoryResolver` timeout root cause.** Is a 5-minute "timed out waiting for the entry point to build the IHost" always a deadlock downstream of `app.Build()`, or can it also be caused by something that never lets control reach the `app.RunAsync()` marker WebApplicationFactory watches for? What's the canonical diagnostic path?

### Constraints the answer must satisfy

- **Single shared `NpgsqlDataSource`.** DEC-046 requires that EF Core, Marten, Wolverine outbox, DataProtection all read from the same `NpgsqlDataSource` instance so `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider` rotates credentials restartless for every consumer. Any recipe that forks a second `NpgsqlDataSourceBuilder` for Wolverine (or any other consumer) fails this constraint.
- **Marten schema isolation.** Marten's schema is `runcoach_events`; EF Core owns `public`. They share one database.
- **WebApplicationFactory-friendly.** `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` must be able to boot this host. Tests use the xUnit v3 `[assembly: AssemblyFixture]` pattern (R-046 artifact already landed at `docs/research/artifacts/batch-15c-ef-migrations-testcontainers-xunit-v3.md`). The fixture overrides `ConfigureWebHost` to layer an in-memory `ConnectionStrings:runcoach` value pointing at a `Testcontainers.PostgreSql` container (`postgres:17-alpine`). EF migrations are applied by a `DevelopmentMigrationService : IHostedService`. `WithReuse(!CI)` keeps the container warm locally; CI runs ephemeral.
- **OpenTelemetry stays wired.** The Compose + Jaeger overlay (`docker-compose.otel.yml`) from R-050 / DEC-045 is live. The recipe must keep the `"Marten"` / `"Wolverine"` / `"RunCoach.Llm"` ActivitySources + Meters exporting to the OTLP endpoint without adding startup latency that trips the WebApplicationFactory 5-minute timeout.
- **Postgres-backed DataProtection.** DataProtection keys persist via `PersistKeysToDbContext<DpKeysContext>()` per DEC-046. The recipe must not move this to filesystem or Redis.
- **No Aspire AppHost for MVP-0.** Per R-050 / DEC-045, MVP-0 stays on Compose + Tilt. The recipe must work without `Aspire.Hosting` / `Aspire.AppHost`. If Aspire.Npgsql cannot be used safely without an AppHost, recommend the migration to plain `services.AddNpgsqlDataSource(...)` (or `NpgsqlDataSourceBuilder` construction + DI registration) — explicitly, with the exact call.
- **.NET 10 TFM.** Both SUT and tests run on `net10.0`. R-047's escape hatch (test assembly → `net9.0`) is on the table but undesired — prefer a solution that keeps both on .NET 10.
- **Zero project-wide build warnings.** `TreatWarningsAsErrors` is on; analyzers include SonarAnalyzer.CSharp + StyleCop.

## Why It Matters

Slice 0 is the load-bearing substrate for every later MVP-0 slice (auth in Slice 0 Units 2-3, onboarding in Slice 1, workout logging in Slice 2, adaptation in Slice 3, conversation in Slice 4). Every per-endpoint integration test the project will ever run (`WebApplicationFactory<Program>` + Testcontainers) depends on this host booting. Every production deploy target (MVP-1's chosen host) depends on the runtime composition being correct. Getting it wrong here multiplies across every later slice — bad composition means either every auth-endpoint test has to invent its own host construction, or we ship a production stack where credential rotation, prompt-cache observability, and daemon throughput are all silently broken.

The symptom pattern (reproducible hang, zero log output, 5-minute WebApplicationFactory timeout) is the worst possible failure mode — no stack trace, no partial output — and the combination of libraries (Marten + Wolverine + Aspire.Npgsql + OTel + EF + Identity + DataProtection) is specific enough that generic .NET 10 advice doesn't resolve it. The research artifacts from Batches 15 and 16 (R-044–R-050) each gave an isolated answer; this prompt asks for the composed recipe where they all coexist.

## Deliverables

Produce a single artifact at `docs/research/artifacts/batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md`. Structure:

1. **Primary recommendation (TL;DR).** One-paragraph canonical shape — if someone reads only this, they can fix the current hang. Include the ordering discipline (e.g., "Aspire.Npgsql → Marten → Wolverine → DbContexts → DataProtection → OpenTelemetry → MVC") and the data-source-sharing seam.
2. **Annotated reference `Program.cs`.** A complete, copy-pasteable `Program.cs` for this exact stack (Aspire.Npgsql + Marten + Wolverine + EF + Identity + DataProtection + OTel + MVC + HealthChecks). Every line that matters annotated with *why* — which version / library / GitHub issue drives each choice. Cite commit SHAs, PR numbers, or issue links where the behavior changed recently.
3. **Annotated reference `MartenConfiguration.cs`** (or equivalent extension). Every option we set today (`EventsSchema`, `StreamIdentity`, `TenancyStyle`, `AppendMode.Quick`, `UseIdentityMapForAggregates`, `EnableAdvancedAsyncTracking`, `OpenTelemetry.TrackConnections`, `OpenTelemetry.TrackEventCounters`, `Policies.AllDocumentsAreMultiTenanted`, `Projections.Errors.SkipUnknownEvents`, `UseLightweightSessions`, `UseNpgsqlDataSource`, `IntegrateWithWolverine`, `ApplyAllDatabaseChangesOnStartup`, `AddAsyncDaemon(Solo)`) — confirm, correct, or flag. Identify any option that is load-bearing for the composition-not-deadlocking story.
4. **Wolverine outbox wiring recipe.** Canonical shape for `PersistMessagesWithPostgresql(NpgsqlDataSource)` that (a) binds to the Aspire-registered single data source, (b) survives `StartAsync` without deadlock, (c) is compatible with `Policies.AutoApplyTransactions()` spanning Marten + EF + outbox, (d) supports DEC-046's future `UsePeriodicPasswordProvider` rotation. Explicit call site, explicit service-lifetime, explicit relative ordering vs `IntegrateWithWolverine` on the Marten side. If `IWolverineExtension` is the right route, say so; if not, say why, and what the right route is.
5. **Aspire.Npgsql usage-without-AppHost verdict.** Direct answer: is Aspire.Npgsql 13.2.2 safe to use from a non-orchestrated ASP.NET Core app, or not? If yes, the exact call. If no, the exact replacement that gives the same single-`NpgsqlDataSource`-in-DI guarantee (`builder.Services.AddSingleton<NpgsqlDataSource>(_ => new NpgsqlDataSourceBuilder(...).Build())` + identical EF/Marten/Wolverine/DataProtection consumption).
6. **DI validation strategy.** Should this stack enable `ValidateOnBuild` / `ValidateScopes`? If yes, the exact config. If no, the exact reason (which library is legitimately incompatible). The goal: the next composition bug surfaces as a clear error at `Build()`, not as a silent 5-minute hang.
7. **Startup deadlock diagnostic recipe.** If the hang recurs, the exact commands and env vars that would surface the cause within 60 seconds: `DOTNET_HOST_TRACE`, Wolverine's `--describe`, Marten's `Oakton` CLI, `dotnet-stack`, `dotnet-dump`, `COREHOST_TRACE`, Roslyn codegen dump paths. Order them by cost-to-apply.
8. **OpenTelemetry composition recipe.** Where OTel must be registered in the order (before / after Wolverine, before / after Marten's async daemon), what exporter shape keeps the WebApplicationFactory-timeout risk zero (synchronous OTLP export on a dead endpoint is a known startup-blocker — is there a timeout / retry we must set?), and how to keep the Jaeger overlay (`docker-compose.otel.yml`) fully working without the SUT blocking when the collector is absent (i.e., normal `dotnet test` runs where no collector is listening).
9. **`WebApplicationFactory<Program>` integration recipe.** The override shape for `ConfigureWebHost` that (a) points every consumer of `ConnectionStrings:runcoach` at the Testcontainers connection string, (b) does NOT build a second `NpgsqlDataSource` that forks the rotation seam, (c) forces `DaemonMode.Solo` regardless of non-test config (to avoid advisory-lock contention across rapid host restarts), (d) keeps `DevelopmentMigrationService` running for test-time migrations. Include whether `UseEnvironment("Development")` vs `UseEnvironment("Test")` is the 2026-canonical choice for this shape and what the behavior difference is.
10. **Known-bad shapes to avoid.** Explicitly call out the anti-patterns that look idiomatic but deadlock or silently break the shared-data-source seam:
    - The two-data-source pattern (Aspire-registered one for EF + Marten, fresh `new NpgsqlDataSourceBuilder(...).Build()` one for Wolverine).
    - `options.UseNpgsql(connectionString)` after Aspire has registered an `NpgsqlDataSource` (DE duplication).
    - `UseAntiforgery()` placed before `UseAuthentication()` / `UseAuthorization()` (.NET 10 order breakage — note: not Slice 0's current bug but a likely Slice 0 landmine in Unit 2).
    - Any other landmine the research surfaces.
11. **Version-specific notes that expire.** List each recommendation that is tied to a specific pin and the expected lifetime of that advice — e.g., "Marten 9 certifies .NET 10 officially; when it ships, re-verify §4", "Wolverine 5.32 fixes #691", "Aspire.Npgsql 14 changes the client-integration contract". Tie each to the repo URL + file path where someone would watch for the change.
12. **Source hierarchy.** Cite in this order: official library docs + release notes (JasperFx/marten, JasperFx/wolverine, dotnet/aspnetcore, Npgsql/efcore.pg, open-telemetry/opentelemetry-dotnet), issue trackers (cite issue numbers), authoritative blog posts by the library authors (Jeremy Miller, Shay Rojansky), real production-app repos on GitHub using this exact composition.

Avoid:

- Generic "consider simplifying by removing Marten/Wolverine" advice. Not on the table.
- Recommendations that require major upgrades (Marten 9, Wolverine 6) unless you show concrete evidence the 8.31 / 5.31 composition is unreachable on .NET 10.
- Patterns that assume an Aspire AppHost exists (it doesn't in MVP-0).
- Patterns that require moving DataProtection off Postgres (DEC-046).
- Patterns that fork the `NpgsqlDataSource` across consumers (DEC-046 rotation seam).

## Success Criteria

An answer that:

- Lets me delete the current uncommitted workaround in `backend/src/RunCoach.Api/Program.cs` (the hand-built second `NpgsqlDataSource` inside `UseWolverine`) and replace it with a single canonical composition.
- Lets `WebApplicationFactory<Program>` boot the full SUT in CI + local in ≤ 30s cold / ≤ 5s warm, running every one of the following smoke tests to green: (1) `IDocumentSession`/`IDocumentStore` resolve from scope, (2) `RunCoachDbContext` resolves and applies migrations, (3) `DpKeysContext` resolves and the `DataProtectionKeys` table is reachable, (4) `IDataProtectionProvider` resolves, (5) `GET /health` returns `{"status":"ok"}`, (6) `NpgsqlDataSource` resolves and is identity-equal across every consumer.
- Ships with a short diagnostic recipe I can apply in under a minute if any later slice's composition change re-breaks startup.
- Carries version-watch notes so the answer doesn't silently expire when Marten 9 / Wolverine 6 / Aspire 14 / .NET 11 ship.
