# Research Prompt: Batch 18b — R-055 (revised 2026-04-20 post-DEC-048)

# `WebApplication.CreateBuilder(args)` hangs inside its own body on .NET 10 + Marten 8.31 + WolverineFx 5.31 + Aspire.Npgsql 13.2 + OTel 1.15 — R-054 follow-up

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: In a macOS arm64 / .NET 10 ASP.NET Core process, after every R-054 / DEC-048 composition correction is already applied, the very first line of `Program.cs` past a sentinel `File.AppendAllText` — `var builder = WebApplication.CreateBuilder(args);` — hangs for ≥5 minutes and never returns. Before control can reach `builder.Host.ApplyJasperFxExtensions()`, `builder.AddNpgsqlDataSource(...)`, `builder.Services.AddRunCoachMarten()`, or any line below that. The same Program.cs, invoked either via `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` + xUnit v3 + MTP, or invoked directly as `dotnet RunCoach.Api.dll`, produces the same hang. What is the root cause, the minimal fix, and the reduction sequence that pinpoints it inside 5 minutes?

## Context

I'm the same RunCoach solo dev who ran R-054 via you. You delivered `docs/research/artifacts/batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md` on 2026-04-20. Every recommendation in it has been implemented verbatim, the build is clean (0 warn / 0 err), every Context7 cross-check is reproduced, and DEC-048 codifies the resulting invariants. The hang is **unchanged**. None of the three hypotheses R-054 identified as the leading suspects match the evidence I captured when I tried to re-enable the `WebApplicationFactory<Program>` SUT path. I need a second, sharper pass — and this time I want either a cited root cause OR a prescribed 5-minute reduction sequence, not another hypothesis sweep.

### State of the SUT after DEC-048 (all R-054 corrections applied)

Explicit list of what landed:

- **Delete** `backend/src/RunCoach.Api/Infrastructure/WolverinePostgresqlDataSourceExtension.cs` — the `IWolverineExtension` that called `opts.PersistMessagesWithPostgresql(NpgsqlDataSource)` is gone. `Marten.IntegrateWithWolverine()` is the sole envelope-storage wiring (DEC-048 §1).
- **Add** `builder.Host.ApplyJasperFxExtensions()` **before** `AddMarten` and `UseWolverine` so `CritterStackDefaults` reach both code generators.
- **Pair** `DaemonMode.Solo` with `opts.Durability.Mode = DurabilityMode.Solo`.
- **Gate** the OTLP exporter on `OTEL_EXPORTER_OTLP_ENDPOINT` being non-empty so test runs without a collector don't pay the 30 s `BatchExportProcessor.ForceFlush` cost at shutdown.
- **Drop** the `public partial class Program;` trailer (Web SDK source generator emits it on .NET 10; ASP0027).
- **Resolve** `NpgsqlDataSource` for Wolverine's `AddDbContextWithWolverineIntegration<RunCoachDbContext>` from DI (`(sp, o) => o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())`) **inside** the `UseWolverine(opts => ...)` callback — top-level placement silently disables `Policies.AutoApplyTransactions`.
- **Enable** `options.ValidateScopes = true` / `options.ValidateOnBuild = false` in Development via `builder.Host.UseDefaultServiceProvider(...)`.
- **Move** EF `MigrateAsync` out of the `Build → RunAsync` gap into a `DevelopmentMigrationService : IHostedService` so it runs inside `StartAsync` (WebApplicationFactory-friendly shape — though this line is *never reached* because CreateBuilder hangs before it).

The canonical composition is in place. `MartenConfiguration.cs`, `Program.cs`, and the test fixture in the current uncommitted tree all reflect this shape. The tests that pass today (575 passing / 1 skipped / 0 failing) run against a **scope-reduced** fixture — `RunCoachAppFactory : IAsyncLifetime` only, no `WebApplicationFactory<Program>` inheritance — that spins up Testcontainers Postgres, applies the initial EF migration via a direct `RunCoachDbContext` outside the SUT DI graph, and configures Respawn. The fixture is enough to prove the DB schema is correct and migrations apply cleanly. It cannot prove that the SUT's DI graph can be built or that any HTTP endpoint responds. Attempting to reintroduce `: WebApplicationFactory<Program>` and trigger `_ = Services` is how we reproduce the hang.

### The actual evidence

I instrumented `Program.cs` with `File.AppendAllText("/tmp/rc-startup.log", ...)` at every step: before `WebApplication.CreateBuilder(args)`, before `ApplyJasperFxExtensions`, before `AddNpgsqlDataSource`, before `AddRunCoachMarten`, before `UseWolverine`, before `builder.Build()`. Ran `dotnet test --filter "FullyQualifiedName~StartupSmokeTests.TestContainer_Accepts_Connections"` against an `AssemblyFixture`-based `RunCoachAppFactory : WebApplicationFactory<Program>, IAsyncLifetime` whose `InitializeAsync` triggered SUT boot via `_ = Services`.

After a full 5-minute timeout, the trace file contained **exactly one line**:

```
[2026-04-20T20:07:28.2260750-05:00] Main started pid=49765
```

No `AFTER CreateBuilder` line. No later trace line. The first `File.AppendAllText` call flushed to disk successfully; the next statement (`WebApplication.CreateBuilder(args)`) never returned. Reproduced five times across clean rebuilds.

Supporting observations:

- Raw-bytes UTF-16 LE search against `RunCoach.Api.dll` confirms both `"/tmp/rc-startup.log"` and `"AFTER CreateBuilder"` are present in the compiled assembly (so the hang isn't the trace being stripped).
- The test process did invoke `Main` — the first `File.AppendAllText` call observably succeeded.
- The hang is **fully synchronous** — no visible `async` sandwich before it.
- Timeout stack (captured after 5:00):

  ```
  System.InvalidOperationException : Timed out waiting for the entry point to build the IHost after 00:05:00.
      at Microsoft.Extensions.Hosting.HostFactoryResolver.HostingListener.CreateHost()
      at Microsoft.Extensions.Hosting.HostFactoryResolver.<>c__DisplayClass10_0.<ResolveHostFactory>b__0(String[] args)
      at Microsoft.AspNetCore.Mvc.Testing.DeferredHostBuilder.Build()
      at Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`1.CreateHost(IHostBuilder builder)
      at Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`1.StartServer()
      at Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`1.get_Services()
      at RunCoach.Api.Tests.Infrastructure.RunCoachAppFactory.InitializeAsync()
  ```

- **Pre-DEC-048 corroborating signal from `docs/specs/12-spec-slice-0-foundation/01-proofs/T01.5-proofs.md`**: running the compiled `RunCoach.Api.dll` directly (no `WebApplicationFactory`, no xUnit) for 120 s produced zero stdout and zero stderr, even with `DOTNET_HOST_TRACE=1`. This was captured before DEC-048 landed, so the post-DEC-048 behavior of plain `dotnet RunCoach.Api.dll` is a 5-minute re-test (see Prescribed Reductions §0). If it still hangs, `WebApplicationFactory`-specific blame is ruled out; if it now boots, the hang is inside the `WebApplicationFactory` ↔ `HostFactoryResolver` ↔ MTP interaction.

### Null results after DEC-048 — what did *not* unblock

Please do not propose any of these as the fix; they're already known-negative:

| Attempt | Result |
|---|---|
| `Environment.SetEnvironmentVariable("ASPNETCORE_PREVENTHOSTINGSTARTUP", "true")` before `CreateBuilder(args)` | Hang unchanged |
| `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` | Already empty; setting it empty explicitly changes nothing |
| `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=60` before `dotnet test` | Does **not** shorten the 5:00 timeout (suggests MTP is not propagating this env var, or the timeout constant is set elsewhere) |
| Clean rebuild + 5-trial repro | Hang is deterministic, not a cold-start / codegen warm-up artifact |
| DEC-048 composition corrections (delete `PersistMessagesWithPostgresql`, add `ApplyJasperFxExtensions`, gate OTLP, etc.) | Build clean; hang unchanged |
| Scope-reduced fixture (no `WebApplicationFactory<Program>`, direct `RunCoachDbContext`) | 575/0/1 green — proves the SUT assemblies themselves don't wedge the test host; it's the act of booting the SUT host that hangs |

### Current pin set (unchanged from R-054; **do not recommend major bumps**)

- `net10.0` TFM for SUT and tests.
- `Aspire.Npgsql` **13.2.2**; `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.1**.
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **10.0.6**; `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` **10.0.6**.
- `Marten` **8.31.0**; `WolverineFx` **5.31.1**; `WolverineFx.EntityFrameworkCore` **5.31.1**; `WolverineFx.Marten` **5.31.1**.
- `OpenTelemetry` **1.15.2**; `.Exporter.OpenTelemetryProtocol` **1.15.2**; `.Extensions.Hosting` **1.15.2**; `.Instrumentation.AspNetCore` **1.15.1**; `.Instrumentation.Http` **1.15.0**.
- `Microsoft.AspNetCore.Mvc.Testing` **10.0.5**; `Testcontainers.PostgreSql` **4.11.0**; `Respawn` **7.0.0**; `xunit.v3` **3.2.2**.
- macOS arm64 (Darwin 25.4.0); Colima-backed Docker for Testcontainers.
- `<UserSecretsId>runcoach-api</UserSecretsId>` in csproj. User-secrets file only contains `Anthropic:ApiKey`. `appsettings.Development.json` contains `"ConnectionStrings:runcoach": "..."` fallback.
- `TreatWarningsAsErrors` + SonarAnalyzer.CSharp + StyleCop.Analyzers.
- The test project runs under `Microsoft.Testing.Platform` (`TestingPlatformDotnetTestSupport=true`), **not** VSTest.

## Research Questions

**Primary:** Given that the hang is **inside `WebApplication.CreateBuilder(args)` itself** — the very first library call of `Program.cs`, before any user-registered DI, before any Marten / Wolverine / Aspire.Npgsql / OTel code has a chance to run — what is the root cause on this exact stack (.NET 10 / macOS arm64 / Microsoft.Testing.Platform / the pin set above), and what is the minimal fix?

**Sub-questions (each must have a falsifiable, actionable answer — prefer a cited dotnet/aspnetcore / dotnet/aspire / dotnet/runtime / jasperfx issue over speculation):**

1. **What runs inside `WebApplication.CreateBuilder(args)` on .NET 10?** Enumerate, in order, what that call does before returning. Config-provider registrations, default logging, Kestrel default server, DI root, diagnostic listeners, module initializers for the ASP.NET Core / Microsoft.Extensions.* assemblies it touches. Which of these can synchronously block, and which have known macOS arm64 bugs reported since .NET 10 GA?
2. **Module initializers in the assembly-load graph triggered by `CreateBuilder`.** The user-written code hasn't referenced Marten / Wolverine / Aspire.Npgsql types yet (those `using` statements don't force assembly load without a type reference). Which ASP.NET Core / Microsoft.Extensions.* / OpenTelemetry assemblies **are** loaded during `CreateBuilder`, and do any of them declare a `[ModuleInitializer]` or `static class` cctor that performs file I/O, DNS, network, or locking on macOS arm64? `OpenTelemetry.Api.dll` is a prime suspect (it's referenced by instrumentation packages and may load eagerly). `Microsoft.AspNetCore.Http.dll`, `Microsoft.Extensions.Configuration.UserSecrets.dll`, `Microsoft.Extensions.Hosting.dll` — any initializer pitfalls?
3. **`AddUserSecrets` deadlock path on macOS arm64.** `WebApplication.CreateBuilder` in Development auto-calls `AddUserSecrets<T>()` when `<UserSecretsId>` is set. The file lives at `~/.microsoft/usersecrets/<id>/secrets.json` on macOS. Is there a known 2025-2026 file-lock / symlink-resolution / sandboxed-entitlement bug on macOS arm64 that causes this path to hang? Check dotnet/runtime, dotnet/extensions, and dotnet/aspnetcore issues.
4. **`HostFactoryResolver` + `Microsoft.Testing.Platform` interaction on .NET 10.** `HostFactoryResolver.HostingListener` subscribes to the `Microsoft.Extensions.Hosting` DiagnosticListener and waits for the `HostBuilt` event. In MTP (not VSTest), is there a known issue where the DiagnosticListener subscription race-loses against the SUT's `HostBuilder` construction — so `HostBuilding` fires but the listener hasn't subscribed yet, and `HostBuilt` never completes the `TaskCompletionSource`? aspnetcore#56411 is adjacent — is it this? xunit/xunit.net or microsoft/testfx issues covering WebApplicationFactory interop?
5. **.NET 10 `WebApplication.CreateBuilder` regression vs .NET 9.** Is there a tracked regression in dotnet/aspnetcore where `CreateBuilder` on .NET 10 blocks on macOS arm64 specifically, that didn't exist on .NET 9? `Microsoft.AspNetCore.App` shared framework 10.0.x changelog / aspnetcore issues filed since Nov 2025 mentioning `CreateBuilder`, `deadlock`, `Darwin`, `arm64`, or `macOS`.
6. **Aspire.Npgsql 13.2.2 transitive package graph.** Even though `AddNpgsqlDataSource` is not reached, the Aspire.Npgsql assembly may be resolved (referenced by the SUT) and a static initializer may run. Does `Aspire.Npgsql` 13.2.2 or any of `Aspire.Hosting.*` transitive closure declare a module initializer? Aspire repo issues for macOS arm64 deadlock since 13.2.x.
7. **`Oakton` / JasperFx argv interception.** `builder.Host.ApplyJasperFxExtensions()` is reached only if `CreateBuilder` returns, so argv hijacking *inside* the JasperFx extension is ruled out. But does the `JasperFx.Core` / `Baseline` / `JasperFx.CodeGeneration` assembly declare a module initializer that runs when its TYPE metadata is loaded (e.g., if a Roslyn analyzer or source generator in the build pipeline already loaded it)? The Program.cs `using JasperFx;` at top of file — does that reference force-load the assembly before Main runs?
8. **xUnit v3 3.2.2 MTP runner passing args to the SUT entry point.** When `WebApplicationFactory<T>` reflects out `Program.Main`, what `string[]` is passed? Empty? Inherited from the test host? If non-empty, do any of those args trigger a JasperFx/Oakton command that blocks on DB access?
9. **OpenTelemetry 1.15.2 + .NET 10 `AspNetCoreInstrumentation`.** The instrumentation package wires into `DiagnosticListener` and `ILoggerFactory`. Is there a known bug where `OpenTelemetry.Instrumentation.AspNetCore` 1.15.1 blocks during ASP.NET Core 10's default logging configuration? Any issue filed against open-telemetry/opentelemetry-dotnet mentioning .NET 10 / Darwin / startup hang?
10. **Minimal reproduction — the diff between hang and no-hang.** Based on your analysis, describe (a) the minimal Program.cs that reproduces the hang on this stack, and (b) the minimal Program.cs that does NOT. The delta between them pinpoints the cause. If you can cite a public repro repo that isolates the behavior, link it.
11. **The concrete fix.** Code / env var / package version / program-structure change that makes `WebApplication.CreateBuilder(args)` return within seconds. If it's a package bump, name the package, version, and release-note citation. If it's a program-structure change (e.g., swap to `Host.CreateApplicationBuilder` + manual web config), name it. If it's an `AppContext` switch, name it.
12. **Updated diagnostic recipe for this platform combination.** R-054 proposed `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=900` + `dotnet-stack report -p <pid>` + `dotnet run -- describe`. On macOS arm64 + .NET 10 + MTP: (a) does `dotnet-stack report` actually produce a useful managed stack for a process hung inside `WebApplication.CreateBuilder`? (b) does `dotnet-dump collect` work reliably on Darwin 25.x arm64? (c) is there a more effective diagnostic (lldb attach + `clrstack`, `DOTNET_DiagnosticPorts`, `DOTNET_EnableDiagnostics=1`, `DOTNET_EnableEventPipe`, per-event-source keyword traces)? Order by cost-to-apply and effectiveness on this exact platform.

### Prescribed 5-minute reduction experiments — what I want you to prescribe

If a cited root-cause issue doesn't surface, prescribe this reduction sequence in priority order and tell me, for each, what the result would rule in or out. The goal: pinpoint the cause in under an hour of my time without re-running your research pass.

0. **Re-test plain `dotnet RunCoach.Api.dll`** post-DEC-048. If it still hangs → `WebApplicationFactory` / MTP are innocent; the hang is fully in the SUT's host construction. If it now boots → the fixture / MTP is the culprit.
1. **Swap `WebApplication.CreateBuilder(args)` → `WebApplication.CreateBuilder()` (no args).** Isolates args processing.
2. **Swap `WebApplication.CreateBuilder(args)` → `WebApplication.CreateEmptyBuilder(new WebApplicationOptions())`** and manually add only the config providers / services the SUT needs. Isolates the default-configure pipeline (user-secrets, appsettings auto-loading, default logging, Kestrel defaults).
3. **Swap `WebApplication.CreateBuilder(args)` → `Host.CreateApplicationBuilder(args)`** and drop ASP.NET Core pieces. Isolates Web-host vs generic-host construction.
4. **Remove `<UserSecretsId>` from `RunCoach.Api.csproj`** temporarily (the user-secrets for Anthropic:ApiKey can be moved to an env var for the test). Isolates `AddUserSecrets` on macOS.
5. **Delete `appsettings.Development.json`** temporarily. Isolates environment-specific config providers.
6. **Target `net9.0` TFM for the SUT** temporarily (separate csproj, same code). Isolates .NET 10 runtime regression from library composition.
7. **Run the SUT outside `dotnet test` entirely** — `ASPNETCORE_ENVIRONMENT=Development dotnet backend/src/RunCoach.Api/bin/Debug/net10.0/RunCoach.Api.dll` on the host user, with `lldb` attached and `process interrupt` / `bt all` after 30 s. Produces a native + managed stack of whatever is blocked.
8. **Attach `dotnet-dump` (if functional on Darwin 25.x arm64) or `lldb` + `sos` + `clrstack -all`** to the hung test process and capture the managed frame at `WebApplication.CreateBuilder`. Name the specific command sequence on this platform.
9. **Run under `DOTNET_EnableDiagnostics=1` + `DOTNET_DiagnosticPorts=<path>,suspend`** and attach `dotnet-trace` / `PerfView` for a startup trace — does any counter / event fire between "Main started" and the 5-min timeout?

Each experiment must have a stated "if this unblocks → root cause is X; if it still hangs → rule out Y." Do not prescribe more than nine; the top three should be the highest-signal single-file reductions.

## Why It Matters

Slice 0 Unit 1 is done against a **scope-reduced** fixture (`IAsyncLifetime` only, no `WebApplicationFactory<Program>`) — 575/0/1 green — which proves the DB schema is correct but cannot exercise any HTTP endpoint, middleware pipeline, authentication flow, antiforgery token issuance, or cookie persistence. Unit 2 (T02.x — auth endpoints: register / login / logout / me / xsrf) and every subsequent slice's integration tests depend on `WebApplicationFactory<Program>` booting the SUT. Shipping Slice 0 with Unit 2 in an "uncoverable" state forfeits the entire value of the Testcontainers investment T01.5 landed and pushes the reckoning into Slice 1, Slice 2, Slice 3, where the cost of debugging it on top of new features compounds.

This is the last moment to resolve this cleanly before T02 starts.

## Deliverables

Produce a single artifact at `docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`. Structure:

1. **Primary recommendation (TL;DR).** One paragraph: root cause + minimal fix. If the root cause is an open upstream bug, name the issue and the workaround.
2. **Root cause analysis.** Per-sub-question: what you found (positive or negative), the source you cite, what the finding rules in or out. If a sub-question couldn't be resolved through literature, name the reduction experiment that would resolve it.
3. **The minimal reproduction diff.** Program.cs / csproj / env deltas that toggle the hang on and off. Commit SHAs and release-note links where applicable.
4. **Concrete fix prescription.** Exact code, env var, package version, or config change that lets `WebApplication.CreateBuilder(args)` return within seconds on this pin set. If a package bump is required, include the release-notes citation for the relevant fix. If the fix is a pin-compatible workaround with an upstream-issue reference, say so.
5. **`WebApplicationFactory<Program>` + xUnit v3 + MTP recipe update.** If the fixture shape R-054 §8 ships needs any amendment (env var, `ConfigureWebHost` override, entry-point indirection like `[assembly: TestCandidateTarget]`), document it.
6. **Updated diagnostic recipe.** What would have found this in under 5 minutes on macOS arm64 / .NET 10 / xUnit v3 / MTP? Replace R-054 §6 where its tools are ineffective on this platform combination. Include the exact `lldb` / `dotnet-dump` / `dotnet-trace` commands for the failure mode (hang inside `WebApplication.CreateBuilder`).
7. **Prescribed reduction sequence (if root cause couldn't be literature-cited).** The nine (or fewer) experiments in priority order, each with expected pass/fail signals.
8. **Version-watch additions.** Any pin-specific "expires when" notes that join R-054 §10's table.
9. **Source citations.** Issues / PRs / release notes / authoritative blog posts consulted. Be specific — URLs and dates.

Avoid:

- Speculation without a cited source **or** a prescribed reduction experiment. Every claim must be either citable or testable in ≤5 minutes.
- Recommendations that contradict DEC-048 (R-054 corrections are known-correct; don't unwind them unless you cite evidence they themselves cause the hang).
- Generic "use an in-process test server" advice — we're already using `WebApplicationFactory<Program>`; the question is why its entry-point invocation path hangs on this stack.
- Recommendations that move the SUT off `net10.0`. The R-047 escape hatch (test assembly on `net9.0`, SUT on `net10.0`) is acceptable only with cited evidence the `net10.0` path is upstream-broken on this combination.
- Recommendations that fork the `NpgsqlDataSource` across consumers (DEC-046 rotation seam).

## Success Criteria

An answer that, when applied:

- Lets `RunCoachAppFactory : WebApplicationFactory<Program>` boot the SUT in ≤ 30 s cold / ≤ 5 s warm under `dotnet test --no-build`.
- Lets the six Slice 0 Unit 1 smoke tests R-054 §8 specifies (`IDocumentSession` / `RunCoachDbContext` / `DpKeysContext` / `IDataProtectionProvider` / `GET /health` returning `{"status":"ok"}` / `NpgsqlDataSource` identity-equality across consumers) go green.
- Carries a diagnostic recipe that would have pinpointed the `CreateBuilder` hang in under 60 s rather than trial-and-erroring for an afternoon, using tools verified to work on macOS arm64 + .NET 10 + MTP.
- Is pin-safe: does **not** require Marten 9, Wolverine 6, Aspire 14, or .NET 11.
- If the fix is "wait for an upstream fix in version X," names X, the tracking issue, and the fallback workaround I can run today.
