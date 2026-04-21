# Batch 18B: WebApplication.CreateBuilder hang — follow-up root cause

**Artifact path:** `docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`

## 1. Primary recommendation (TL;DR)

**The cited root cause is one of two — the diagnostic sequence in §6 distinguishes them in under 60 seconds. Both have concrete, pin-safe workarounds.**

**Candidate A — most likely given the evidence profile (no debugger attached, hang reproduces under plain `dotnet RunCoach.Api.dll`): synchronous `FileSystemWatcher` init inside `CreateBuilder` on macOS 26 / Darwin 25.x arm64.** `WebApplication.CreateBuilder(args)` in Development with `<UserSecretsId>` set and `appsettings.Development.json` present synchronously constructs **three** `PhysicalFilesWatcher` instances (one per JSON source; default `reloadOnChange: true`). Each one on macOS calls `Interop.Sys.Sync()` (a full `sync(2)`) and then `FSEventStreamCreate` / `FSEventStreamScheduleWithRunLoop` / `FSEventStreamStart` on a dedicated runloop thread. `Interop.Sys.Sync()` is a **synchronous, unbounded stall point** on macOS (dotnet/runtime#77793), and Darwin 25.x (macOS 26 "Tahoe") is not in the main .NET 10 CI matrix (dotnet/runtime#118610). **Minimal fix: set `DOTNET_hostBuilder__reloadConfigOnChange=false`** (or `"hostBuilder:reloadConfigOnChange": false` in config) before `CreateBuilder`. This bypasses all three FSEvents setups without losing any production behavior — config reload on change is a dev-loop nicety, not a runtime contract.

**Candidate B — applies only if the SUT is ever launched under a debugger (F5 / Rider "Debug"/ vsdbg): confirmed .NET 10.0.4 macOS arm64 debugger-handshake deadlock, fixed in 10.0.5 (OOB 2026-03-12).** Tracked in dotnet/vscode-csharp#9059, microsoft/vscode#300809, dotnet/sdk#53382; fix shipped as the .NET 10.0.5 out-of-band release (devblogs.microsoft.com 2026-03-12). Symptom: process hangs at exactly `var builder = WebApplication.CreateBuilder(args)` under vsdbg launch; workaround is `dotnet run` + attach, or upgrade to ≥ 10.0.5. Strongly not-this for the user's evidence because (a) they are running under `dotnet test` (no debugger) and (b) `HostFactoryResolver` sets `Timeout.InfiniteTimeSpan` when `Debugger.IsAttached` is true, yet they see a 00:05:00 timeout — so no debugger is attached.

**There is no cited ASP.NET Core / aspnetcore issue that matches "CreateBuilder hangs under WebApplicationFactory+MTP" specifically**; the HostFactoryResolver / DeferredHostBuilder code path is unchanged from .NET 10.0.0 through 10.0.6, and the 5-minute constant is working as designed. That narrows the blame to runtime-level Darwin 25.x behavior or to hosted-service-level work, neither of which is Marten/Wolverine/Aspire/OTel code (none of those packages declare a `[ModuleInitializer]`).

## 2. Root cause analysis by sub-question

### 2.1 What runs inside `WebApplication.CreateBuilder(args)` on .NET 10

Source-read of `src/DefaultBuilder/src/WebApplicationBuilder.cs` on `dotnet/aspnetcore` (commit `fb05933145108588a38007f57d073aed50e66614`, tracks 10.0.x) and `src/libraries/Microsoft.Extensions.Hosting/src/HostingHostBuilderExtensions.cs` on `dotnet/runtime`. The in-order sequence is: host-configuration prepopulate (`DOTNET_*`, `ASPNETCORE_*`, argv, ContentRoot); `ApplyDefaultAppConfiguration` adds `appsettings.json` and `appsettings.{Env}.json` (`optional: true, reloadOnChange: true`); in Development with `env.ApplicationName` set, `Assembly.Load(ApplicationName)` + `AddUserSecrets(appAssembly, optional: true, reloadOnChange: true)`; `AddEnvironmentVariables`; `AddCommandLine(args)`; default logging provider registration (Console, Debug, EventSource; Windows-only EventLog) — DI registration only, no provider start; Kestrel defaults registered (options, `GenericWebHostService`) — **no socket bind**; `DefaultServiceProviderFactory` with scope validation if Development; hosting listeners / DiagnosticListener registration. **Network I/O: none. File I/O: three synchronous JSON reads plus three synchronous FileSystemWatcher installs when `reloadOnChange: true`.** **Rules in:** default-config file-watcher init is the only unbounded synchronous work `CreateBuilder` does on macOS. **Confidence: High.**

### 2.2 Module initializers in the assembly-load graph

**No `[ModuleInitializer]` declared by any pinned package** (audited OpenTelemetry.Api/SDK 1.15.2, OpenTelemetry.Instrumentation.AspNetCore 1.15.1, OpenTelemetry.Instrumentation.Http 1.15.0, Aspire.Npgsql 13.2.2, Marten 8.31.0, WolverineFx 5.31.1, WolverineFx.Marten 5.31.1, JasperFx, Microsoft.AspNetCore.Mvc.Testing 10.0.5, Npgsql). OpenTelemetry has a lazy `static Sdk()` that only runs on first `Sdk` member access — not at assembly load, and it performs only in-memory work (`Activity.DefaultIdFormat`, `Propagators.DefaultTextMapPropagator`, `SelfDiagnostics.EnsureInitialized` which spins up a lightweight background worker, no sync I/O). Marten/Wolverine's expensive work (Roslyn code generation, Schema discovery) is triggered by `AddMarten()`/`UseWolverine()` at user-code time, not at assembly load. **Rules out** the "hanging module initializer" hypothesis entirely. **Confidence: High.**

### 2.3 AddUserSecrets deadlock path on macOS arm64

`AddUserSecrets(appAssembly, optional: true, reloadOnChange: true)` adds a `JsonConfigurationSource` rooted at `~/.microsoft/usersecrets/<id>/`; with `reloadOnChange: true`, `PhysicalFileProvider.Watch` installs a `PhysicalFilesWatcher` which wraps `FileSystemWatcher` on that directory. On macOS, `FileSystemWatcher.StartRaisingEvents` calls `Interop.Sys.Sync()` (full `sync(2)` flush) — documented-slow behavior (dotnet/runtime#77793, "FileSystemWatcher start performance issues on macOS") — then `FSEventStreamCreate/Schedule/Start`. **No public issue names this as a deterministic hang on .NET 10 Darwin arm64**, but Darwin 25.x (macOS 26) is on `runtime-extra-platforms` only (#118610) — regressions here are systematically under-reported. **Rules in** as a plausible synchronous blocking point inside `CreateBuilder`. Falsification test: set `DOTNET_hostBuilder__reloadConfigOnChange=false` and re-run (experiment §7.1). **Confidence: Medium-High.**

### 2.4 HostFactoryResolver × Microsoft.Testing.Platform interaction

`HostFactoryResolver.cs` at `dotnet/runtime main` (`src/libraries/Microsoft.Extensions.HostFactoryResolver/src/HostFactoryResolver.cs`) shows: default timeout `TimeSpan.FromMinutes(5)`, overridable via `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS`, `Timeout.InfiniteTimeSpan` if `Debugger.IsAttached`. `HostingListener` subscribes `DiagnosticListener.AllListeners`, filters to `"Microsoft.Extensions.Hosting"`, handles events `"HostBuilding"` (invokes the `configure` action) and `"HostBuilt"` (resolves the `_hostTcs`). PR dotnet/runtime#61688 set the 5-minute default in Nov 2021; **no PR has touched HostFactoryResolver.cs between .NET 10.0.0 GA (Nov 2025) and 10.0.6 (Apr 14 2026).** `WebApplicationBuilder.Build()` on .NET 10 still emits `HostBuilt` via `HostBuilder.ResolveHost` — confirmed by source read and Andrew Lock's DiagnosticListener post. **So HostFactoryResolver is not broken; it never receives `HostBuilt` because `CreateBuilder` itself never returns — the entry point is stalled before `Build()` is reached.** MTP-specific finding: `microsoft/testfx#6776` confirms MTP's `--timeout` does not hard-kill a stuck testhost (opened 2025-10-21, milestone MSTest 4.1 / MTP 2.1, Open as of Apr 2026), which is why the 5:00 wait runs to completion. VSTest `<RunConfiguration><EnvironmentVariables>` runsettings blocks are NOT honored by the MTP native runner — documented in the VSTest→MTP migration guide. Process-level env vars and `dotnet test -e NAME=VALUE` DO forward to the testhost exe under MTP. **Rules out:** HostFactoryResolver bug, subscription race, MTP stripping `DOTNET_*` vars. **Rules in:** The hang is upstream of `Build()` — it's inside `CreateBuilder`. **Confidence: High.**

### 2.5 .NET 10 CreateBuilder regression since GA

No tracked "CreateBuilder hangs on macOS arm64" regression issue exists in `dotnet/aspnetcore` or `dotnet/runtime`. Servicing from 10.0.0 through 10.0.6 touched dev-cert, Blazor, OpenAPI validation, Kestrel, and `TestServerOptions in WebApplicationFactory` (#64809) — none touch `HostFactoryResolver` or `DefaultBuilder/WebApplicationBuilder` startup ordering. The relevant macOS regression is the **.NET 10.0.4 vsdbg × CoreCLR debugger-handshake deadlock** at `WebApplication.CreateBuilder`, fixed OOB in 10.0.5 on 2026-03-12 (devblogs.microsoft.com `/dotnet-10-0-5-oob-release-macos-debugger-fix/`; dotnet/core#10292; dotnet/sdk#53382; dotnet/vscode-csharp#9059; microsoft/vscode#300809). The vsdbg case shows a `sample <pid>` stack of `Debugger::FirstChanceNativeException` → `DebuggerController::DispatchPatchOrSingleStep` → `Thread::WaitSuspendEventsHelper` → `CLREventBase::WaitEx` → `_pthread_cond_wait`. **Only applies under attached debugger**; doesn't match the user's `dotnet test`-only reproduction but MUST be ruled out explicitly because the RunCoach SUT may be occasionally launched under Rider/VS Code for debugging. Known-issue list (`dotnet/core/release-notes/10.0/known-issues.md`) also documents Darwin-25-specific `dotnet/runtime#116545` "Debugger doesn't attach on macOS 26" (area-Diagnostics-coreclr, milestone 10.0.0, closed) — same OS bucket, different failure mode. **Rules in:** platform-level .NET 10 × macOS 26 brittleness is real. **Rules out:** a generic non-debugger `CreateBuilder` bug in 10.0.x aspnetcore code. **Confidence: High.**

### 2.6 Aspire.Npgsql 13.2.2 transitive graph

`Aspire.Npgsql` 13.2.2 has no `[ModuleInitializer]`. Code-search `ModuleInitializer` hits in dotnet/aspire are exclusively test-infrastructure (`TestModuleInitializer` for Verify snapshot tests, shipped only in test packages). Registration logic fires only when the user calls `builder.AddNpgsqlDataSource(...)` — which is never reached in this hang. No filed issue matches "Aspire.Npgsql hang macOS arm64." **Rules out.** **Confidence: High.**

### 2.7 Oakton / JasperFx argv interception and assembly preloads

`builder.Host.ApplyJasperFxExtensions()` is **not reached** if `CreateBuilder` never returns, so JasperFx argv interception cannot be the cause. `using JasperFx;` is a namespace import — it does NOT force assembly load without a type reference in Program.cs. Even if the assembly were preloaded (e.g., by a Roslyn analyzer consuming it at build time), no `[ModuleInitializer]` is declared in `JasperFx/jasperfx`, `JasperFx/marten`, or `JasperFx/wolverine`. **Rules out.** **Confidence: High.**

### 2.8 xUnit v3 3.2.2 MTP runner argv to SUT entry point

`WebApplicationFactory<T>` resolves `T.Assembly.EntryPoint` and invokes it via `HostFactoryResolver.ResolveHostFactory` which calls the entry point with a stubbed `string[] args`. In modern builds this is an empty `new string[0]`, not the MTP test host's argv — so Oakton/JasperFx argv-parsing cannot hijack the SUT. The fact that `Main started pid=X` logs at all confirms `Main` received args without crashing. Plus, the hang is *before* any JasperFx code runs. **Rules out.** **Confidence: High.**

### 2.9 OpenTelemetry 1.15.2 + .NET 10 AspNetCoreInstrumentation

No `[ModuleInitializer]` in OpenTelemetry 1.15.x. `AddAspNetCoreInstrumentation()` / `AddHttpClientInstrumentation()` only subscribe to `DiagnosticListener` when user code calls them — never reached here. OpenTelemetry.Api's lazy `static Sdk` cctor only sets `Activity.DefaultIdFormat = W3C`, `Propagators.DefaultTextMapPropagator`, and initializes `SelfDiagnostics` (background worker, memory-mapped self-log file) — none of which do synchronous network or locking. No filed issue matches ".NET 10 startup hang OpenTelemetry" or "Darwin instrumentation hang 1.15." DEC-048's OTLP-gating fix addresses `BatchExportProcessor.ForceFlush` at **shutdown**, not startup. **Rules out.** **Confidence: High.**

### 2.10 Minimal reproduction (expected diff based on analysis)

**Hangs (current):**
```csharp
// Program.cs — RunCoach.Api
File.AppendAllText("/tmp/rc-startup.log", $"Main started pid={Environment.ProcessId}\n");
var builder = WebApplication.CreateBuilder(args);   // ← hangs here
File.AppendAllText("/tmp/rc-startup.log", "AFTER CreateBuilder\n");
```
with `RunCoach.Api.csproj` having `<UserSecretsId>runcoach-api</UserSecretsId>` and `appsettings.Development.json` present, on Darwin 25.4.0 arm64.

**Expected to NOT hang (test — if Candidate A is correct):** set `DOTNET_hostBuilder__reloadConfigOnChange=false` before launching; OR pass `WebApplicationOptions { Args = args, Configuration = … }` to `CreateBuilder` with a manual config that sets `hostBuilder:reloadConfigOnChange=false`; OR temporarily delete `<UserSecretsId>` + `appsettings.Development.json`. If any of these unblock, Candidate A (FileSystemWatcher on macOS) is confirmed.

No public repro repo isolates this. If the bug is reproducible without RunCoach-specific packages on a fresh `dotnet new webapi --use-minimal-apis` with `<UserSecretsId>` added and run on Darwin 25.x arm64, that would be worth filing as a new `dotnet/runtime` issue.

### 2.11 The concrete fix

**One-line fix (apply first, costs nothing, pin-safe):**

```bash
# in the test runner shell or dotnet test -e flag
DOTNET_hostBuilder__reloadConfigOnChange=false
```

…or, more robustly, baked into Program.cs before `CreateBuilder`:

```csharp
Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
// then:
var builder = WebApplication.CreateBuilder(args);
```

This disables `reloadOnChange` for all three default config sources and eliminates the three synchronous `FileSystemWatcher.StartRaisingEvents` → `Interop.Sys.Sync()` + FSEvents stream setups on macOS — the only unbounded synchronous work `CreateBuilder` does on Darwin. If Candidate A is the cause, `CreateBuilder` returns in ≤ 1 s after this change. Cited: `ApplyDefaultAppConfiguration` in `HostingHostBuilderExtensions.cs` and `GetReloadConfigOnChangeValue` default documented at `learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-10.0` ("Key: hostBuilder:reloadConfigOnChange, Default: true").

**Belt-and-suspenders (apply second if the first doesn't resolve it):** upgrade from .NET 10.0.4 to **.NET 10.0.5 or newer** to absorb the vsdbg × macOS arm64 OOB fix (devblogs.microsoft.com 2026-03-12, `dotnet/core` 10.0.5 release notes). `dotnet --info` will confirm the current Host version; if it's 10.0.4 the upgrade is mandatory regardless of this bug.

**If both of the above fail** — meaning reduction §7.2 (disable reloadOnChange) still hangs AND §7.1 (plain `dotnet RunCoach.Api.dll`) still hangs — escalate via the diagnostic recipe in §6: `dotnet-stack report -p <pid>` on the hung process will name the exact managed frame. If that frame is outside `FileSystemWatcher.StartRaisingEvents` / `Interop.Sys.Sync`, Candidate A is falsified and the real cause is in whatever frame is shown.

## 3. Minimal reproduction diff

| Toggle | On state (hangs) | Off state (should return in ≤ 1 s if Candidate A) |
|---|---|---|
| Env var | unset | `DOTNET_hostBuilder__reloadConfigOnChange=false` |
| csproj | `<UserSecretsId>runcoach-api</UserSecretsId>` present | `<UserSecretsId>` removed (move Anthropic:ApiKey to env var) |
| appsettings | `appsettings.Development.json` present | file deleted / renamed |
| Runtime | Host 10.0.4 | Host 10.0.5+ (for debugger-attached scenarios) |

Commit/release citations: .NET 10.0.5 OOB notes at `github.com/dotnet/core/blob/main/release-notes/10.0/10.0.5/10.0.5.md` (shipped 2026-03-12); HostFactoryResolver default at `github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.HostFactoryResolver/src/HostFactoryResolver.cs`; WebApplicationBuilder config sequence at `github.com/dotnet/aspnetcore/blob/fb05933145108588a38007f57d073aed50e66614/src/DefaultBuilder/src/WebApplicationBuilder.cs`.

## 4. Concrete fix prescription

**Primary fix (pin-safe, DEC-048-compatible, apply in this exact order):**

1. **Add to `RunCoach.Api/Program.cs` at the very top of `Main` (before any other code):**
   ```csharp
   Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
   ```
   This must be set before `WebApplication.CreateBuilder(args)` observes host configuration. The env var is read by `HostApplicationBuilderSettings` / `HostingHostBuilderExtensions.ApplyDefaultAppConfiguration` via `GetReloadConfigOnChangeValue`. Effect: no `FileSystemWatcher.StartRaisingEvents` call on any of the three default JSON sources; `CreateBuilder` avoids `Interop.Sys.Sync()` (the sync(2) stall point on macOS, dotnet/runtime#77793).

2. **Additionally in RunCoachAppFactory (WebApplicationFactory<Program>) ConfigureWebHost or the `dotnet test` invocation:**
   ```csharp
   protected override void ConfigureWebHost(IWebHostBuilder builder)
   {
       builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
       // existing config…
   }
   ```
   Belt-and-suspenders so the factory-injected config doesn't re-enable the watcher.

3. **Upgrade `.NET SDK to ≥ 10.0.105 / runtime Host ≥ 10.0.5`** (the OOB release from 2026-03-12). `dotnet --list-runtimes` must show `Microsoft.NETCore.App 10.0.5` or newer. Release notes: `github.com/dotnet/core/blob/main/release-notes/10.0/10.0.5/10.0.5.md`, announcement at `devblogs.microsoft.com/dotnet/dotnet-10-0-5-oob-release-macos-debugger-fix/`. This is required regardless of this bug because 10.0.4 has a separate debugger-deadlock problem that will bite any F5/Rider debug session.

No package version bumps are required. Marten 8.31, Wolverine 5.31, Aspire.Npgsql 13.2.2, OpenTelemetry 1.15.x all stay pinned. No R-054 / DEC-048 invariant is violated.

## 5. WebApplicationFactory<Program> + xUnit v3 + MTP recipe amendment

R-054 §8's fixture shape remains correct. The additions are:

**ConfigureWebHost:**
```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.UseEnvironment("Development");
    builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");  // NEW
    // existing ConfigureTestServices…
}
```

**`.runsettings` is NOT honored under MTP** — the VSTest `<RunConfiguration><EnvironmentVariables>` block is ignored by MTP's native runner (VSTest→MTP migration guide, `learn.microsoft.com/en-us/dotnet/core/testing/migrating-vstest-microsoft-testing-platform`). Env vars must be set via one of: shell export, `dotnet test -e NAME=VALUE` (which IS forwarded to the MTP test host), or `testconfig.json`. For CI, prefer `-e DOTNET_hostBuilder__reloadConfigOnChange=false` on the `dotnet test` invocation.

**HostFactoryResolver timeout tuning:** `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=30` set via shell or `dotnet test -e` will shorten the 5:00 wait to 30 s during development so you stop burning 5-minute cycles on failed fixture boots. Runsettings-based injection will NOT work under MTP — confirmed by the user's null-result observation. Note: `Debugger.IsAttached` short-circuits the timeout to `Timeout.InfiniteTimeSpan`, so this variable is a no-op while debugging.

**No `[assembly: TestCandidateTarget]` / entry-point-indirection is needed.** ASP0027 on .NET 10 means the Web SDK auto-emits `public partial class Program {}` via source generator (aspnetcore#58488, captainsafia) and DEC-048's removal of the user-declared trailer is correct; WebApplicationFactory<Program> resolves the generated partial and this is by design. The factory is NOT broken by ASP0027.

## 6. Updated diagnostic recipe (macOS arm64 + .NET 10 + MTP)

R-054 §6's recipe (`dotnet-stack report` + `dotnet run -- describe`) is mostly correct for this platform but needs these amendments.

**The 60-second recipe — run these in this order:**

```bash
# -------- Step 1 (15 s): live managed stacks via EventPipe (no SOS/arm64 issues) --------
dotnet tool update -g dotnet-stack
dotnet-stack ps                            # find testhost pid
dotnet-stack report -p <pid> > stacks.txt
```
`dotnet-stack` uses EventPipe exclusively, so it sidesteps the SOS `SOS does not support the current target architecture 'arm64' (0xaa64)` history bug (dotnet/diagnostics#4779, milestone 9.0.0 — fixed in dotnet-dump 9.0+ but dotnet-stack never had this problem). **Prerequisites: same user as target; same `TMPDIR` (`/tmp` default); `DOTNET_EnableDiagnostics` not set to 0.** Expected signal: the topmost frame on the main thread will name the exact blocker. For Candidate A it will be inside `System.IO.FileSystem.Watcher` / `PhysicalFilesWatcher` / `Interop.Sys.Sync`. For Candidate B it will be inside `CLREventBase::WaitEx` + debugger frames.

```bash
# -------- Step 2 (15 s): native stack via Apple 'sample' (always works, no setup) --------
sample <pid> 10 -file /tmp/sample.txt
```
No codesign, no entitlements, no dotnet-tool install. Won't decode managed method names but will name the native frame: `FSEventStreamStart` / `CFRunLoopRun` / `psynch_cvwait` / `Interop.Sys.Sync` / `getaddrinfo` / `_pthread_cond_wait` etc. Extremely fast triage.

```bash
# -------- Step 3 (30 s): if Steps 1-2 can't attach — reverse diagnostic port --------
# Terminal A: start the collector first (it waits)
rm -f /tmp/diag.sock
dotnet-trace collect --diagnostic-port /tmp/diag.sock,suspend -f speedscope -o /tmp/startup.nettrace \
  --providers 'Microsoft-Extensions-Hosting,Microsoft-AspNetCore-Hosting,\
Microsoft-Extensions-Logging:0x4:4:FilterSpecs="Microsoft.AspNetCore.Hosting*:4;Microsoft.Extensions.Hosting*:4",\
System.Threading.Tasks.TplEventSource:0x80:4,System.Net.NameResolution,System.Net.Sockets,System.Net.Http' &
# Terminal B: re-launch the test with the env var; runtime pauses at t=0 until collector resumes it
dotnet test -e DOTNET_EnableDiagnostics=1 -e DOTNET_DiagnosticPorts='/tmp/diag.sock,suspend' -e TMPDIR=/tmp
```
Use this when the hang is so early that `dotnet-stack` / `dotnet-dump` can't race in fast enough. The suspended-startup port guarantees the collector is attached at t=0. Syntax cited at `learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostic-port` and `github.com/dotnet/docs/blob/main/docs/core/diagnostics/diagnostic-port.md`.

**Tools that are unreliable on Darwin 25.x arm64 — deprioritize:**
- **`dotnet-dump analyze` + SOS on arm64**: historically broken (dotnet/diagnostics#4779, `0xaa64` error). Fixed in 9.0+ dotnet-dump but verify `dotnet-dump --version ≥ 9.0.x` before relying on it. `collect` works; `analyze` is the fragile half.
- **`lldb + libsosplugin.dylib`**: Apple Silicon loading requires `dotnet-sos install --architecture Arm64` (arch must match target); has a history of EXC_GUARD crashes on macOS 14.4+ arm64 (dotnet/diagnostics#4551, milestone 9.0.0); may need the Xcode-lldb adhoc-sign workaround from `github.com/dotnet/diagnostics/blob/main/documentation/FAQ.md`. Avoid unless Steps 1-3 above can't attach.
- **`dotnet test --blame-hang`**: known-unreliable on macOS arm64 (dotnet/diagnostics#5196) because `createdump` is missing the executable bit in .NET 10.0.0 PKG installer — listed in the official .NET 10 known-issues file.
- **`dotnet run -- describe`**: useless when `Build` never completes, which is the case here.
- **R-054's `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=900` via runsettings**: DOES NOT WORK under MTP (user's observation confirmed). Must be set via shell or `dotnet test -e`.

**What would have found this in under 60 s:** `sample <pid> 10` (Step 2) produces a usable native stack in 10 seconds with zero setup on any macOS box. If the native frames show `FSEventStreamStart` or `Interop.Sys.Sync`, Candidate A is proven without any further work.

## 7. Prescribed reduction sequence

The user pre-authored nine experiments; here they are in priority order with expected-signal mapping based on §2. **Stop at the first experiment that unblocks.**

**§7.1 — Re-test plain `ASPNETCORE_ENVIRONMENT=Development dotnet backend/src/RunCoach.Api/bin/Debug/net10.0/RunCoach.Api.dll`.** Blocking prerequisite — rules in/out the WebApplicationFactory/MTP layer. **If still hangs → hang is inside SUT host construction (most likely Candidate A); proceed to §7.2.** **If it now boots → hang is in WAF×MTP×HostFactoryResolver interaction; go directly to §6 Step 1 to capture stacks and file a new aspnetcore issue.**

**§7.2 — `DOTNET_hostBuilder__reloadConfigOnChange=false dotnet RunCoach.Api.dll`.** Highest-signal single-env-var test for Candidate A. **If boots in seconds → Candidate A (macOS FileSystemWatcher init) confirmed; apply §4 fix.** **If still hangs → Candidate A falsified; go to §7.3.**

**§7.3 — `sample <pid> 10 -file /tmp/sample.txt` while the process is hung.** Native stack reveals the blocking syscall regardless of which library is responsible. **If stack shows `Interop.Sys.Sync` / `FSEventStream*` / `CFRunLoopRun` → still Candidate A despite §7.2 (i.e., the env var didn't propagate correctly; re-verify).** **If it shows `_pthread_cond_wait` + `Debugger::*` + `CLREventBase::WaitEx` → Candidate B (10.0.4 debugger bug) — but this is weird if no debugger is attached; may indicate DOTNET_DefaultDiagnosticPortSuspend / DOTNET_DiagnosticPorts got set somewhere.** **If it shows `getaddrinfo` / `read` on a socket → synchronous network I/O somewhere (unlikely given CreateBuilder's code path).** **If it shows something else entirely → that frame names the new hypothesis.**

**§7.4 — Swap `WebApplication.CreateBuilder(args)` → `WebApplication.CreateBuilder()` (no args).** Rules out argv processing. **Expected no change** given only a trivial `string[]` is passed by `HostFactoryResolver` under WAF, but cheap to try.

**§7.5 — Remove `<UserSecretsId>` from csproj + delete `appsettings.Development.json` temporarily.** Direct test of Candidate A's JSON-source hypothesis. **If boots → confirms one of the three FileSystemWatchers was the hang; combined with §7.2 isolates which source.**

**§7.6 — Swap to `Host.CreateApplicationBuilder(args)` + drop ASP.NET Core defaults.** Isolates Web-host vs generic-host pipeline. `Host.CreateApplicationBuilder` still calls `ApplyDefaultAppConfiguration` — same JSON+watcher code path — so this should hang identically **if Candidate A is correct**. If it does NOT hang, something Kestrel-ish or Web-specific is the cause, which the source read doesn't predict.

**§7.7 — Swap to `WebApplication.CreateEmptyBuilder(new WebApplicationOptions())`.** Empty builder skips `ApplyDefaultAppConfiguration` entirely. **Should boot instantly** if Candidate A is correct; if it still hangs, the cause is in the minimal DI root setup itself (very unlikely given source read).

**§7.8 — Target `net9.0` TFM for the SUT (separate csproj, same code).** Isolates .NET 10 runtime regression from library composition. R-047 escape hatch; apply only if §7.1-§7.7 all fail and a cited fix isn't shipping soon.

**§7.9 — `DOTNET_DiagnosticPorts=/tmp/diag.sock,suspend` + `dotnet-trace collect` pre-listening** (§6 Step 3). Last-resort — captures everything from t=0 before any managed code runs. Always works; slowest to set up.

**Highest-signal top three: §7.1 → §7.2 → §7.3.** These reliably distinguish Candidates A, B, and "something else entirely" in under 90 seconds total.

## 8. Version-watch additions

Entries to add to R-054 §10's version-watch table (each is pin-specific):

`dotnet/runtime HostFactoryResolver.cs` is unchanged since Nov 2021; **expires when** any PR between 10.0.6 and the next servicing lands touching `src/libraries/Microsoft.Extensions.HostFactoryResolver/` — re-check at that point. `dotnet/runtime#77793` (FileSystemWatcher startup on macOS) has no fix shipped; **expires when** a `dotnet/runtime` PR lands removing `Interop.Sys.Sync()` from the watcher init path or providing a way to skip it. `dotnet/runtime#116545` (macOS 26 CoreCLR startup event) is closed in milestone 10.0.0, but Darwin 25.x remains on `runtime-extra-platforms` only (#118610); **expires when** Darwin 25.x joins the main `runtime` CI matrix. `dotnet/vscode-csharp#9059` / `microsoft/vscode#300809` / `dotnet/sdk#53382` fixed in .NET 10.0.5 OOB (2026-03-12); **pin enforcement: require Host ≥ 10.0.5** at build time via a `global.json` `rollForward: latestFeature` policy or an explicit SDK-version check in CI. `microsoft/testfx#6776` (MTP `--timeout` doesn't hard-kill testhost) is Open as of 2026-04, milestone MSTest 4.1 / MTP 2.1 — **expires when** MTP 2.1 ships with real hang-abort. `dotnet/aspnetcore#64428` (ASP0027 strips attributes from `public partial class Program`) is Open, 10.0.100 milestone — **expires when** a servicing release fixes it; track if Program.cs attributes grow. `dotnet/diagnostics#4779` (SOS `0xaa64`) milestone 9.0.0 — verify `dotnet-dump --version ≥ 9.0.x` monthly. `dotnet/diagnostics#4551` (libsosplugin EXC_GUARD on macOS 14.4+) milestone 9.0.0 — same verification cadence.

## 9. Source citations

**dotnet/runtime:**  
`github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.HostFactoryResolver/src/HostFactoryResolver.cs` (timeout + env var + DiagnosticListener events); `github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs` (`LogHostBuilding` / `ResolveHost`, listener name `"Microsoft.Extensions.Hosting"`, events `"HostBuilding"`/`"HostBuilt"`); `github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Hosting/src/HostApplicationBuilder.cs`; `github.com/dotnet/runtime/pull/61688` (5s→5min timeout change, Nov 2021, commit `c91170a7ae2d8529541ad846bde1a8a563429bd8`); `github.com/dotnet/runtime/issues/77793` (FileSystemWatcher start performance on macOS — `Interop.Sys.Sync()` stall); `github.com/dotnet/runtime/issues/116545` (Debugger doesn't attach on macOS 26, area-Diagnostics-coreclr, milestone 10.0.0, opened 2025-06-11 by Rich Lander); `github.com/dotnet/runtime/issues/118610` (macOS 26 on runtime-extra-platforms only); `github.com/dotnet/runtime/issues/113945` (macOS FSW event ordering, Mar 2025, not a hang); `github.com/dotnet/runtime/issues/111628` (Apple Silicon Rosetta hang).

**dotnet/aspnetcore:**  
`github.com/dotnet/aspnetcore/blob/fb05933145108588a38007f57d073aed50e66614/src/DefaultBuilder/src/WebApplicationBuilder.cs` (CreateBuilder ordering); `github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.Testing/src/WebApplicationFactory.cs` (factory content-root resolution, no cctor I/O); `github.com/dotnet/aspnetcore/issues/56411` (WAF `ValidateScopes` defaults, Open, Jun 2024 — unrelated to this hang); `github.com/dotnet/aspnetcore/issues/58488` (ASP0027 + auto-generated `public partial class Program`, captainsafia, shipped 10.0); `github.com/dotnet/aspnetcore/issues/64428` (ASP0027 attribute regression, Open, Nov 2025); `github.com/dotnet/aspnetcore/issues/61372` (WAF host shutdown, Open, Apr 2025); `github.com/dotnet/aspnetcore/issues/40715` (historic 5-sec timeout, superseded); `github.com/dotnet/aspnetcore/issues/38335` (DeferredHostBuilder starts host during build action, Open); `github.com/dotnet/aspnetcore/issues/38649` (ASP.NET Core 6 integration-test deadlock on Linux); `github.com/dotnet/aspnetcore/pull/64809` (CreateServer with TestServerOptions, backported to release/10.0).

**dotnet/core + release notes:**  
`github.com/dotnet/core/blob/main/release-notes/10.0/known-issues.md` (debugger crashes on macOS 10.0.4; `createdump` missing +x in 10.0.0 PKG; fractional-CPU startup regression x64-only); `github.com/dotnet/core/blob/main/release-notes/10.0/10.0.5/10.0.5.md` (OOB release); `github.com/dotnet/core/issues/10292` (10.0.4/10.0.14/8.0.25 March 2026 servicing + OOB 10.0.5 announcement); `devblogs.microsoft.com/dotnet/dotnet-10-0-5-oob-release-macos-debugger-fix/` (2026-03-12); `devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-april-2026-servicing-updates/` (10.0.6 on 2026-04-14).

**Debugger × macOS arm64 regression:**  
`github.com/dotnet/vscode-csharp/issues/9059` (launch-mode debug hang at `WebApplication.CreateBuilder`, macOS 15.7.1 arm64, SDK 10.0.103 / vsdbg `coreclr-debug-2-90-0`, opened 2026-03-12; stack: `Debugger::FirstChanceNativeException` → `DebuggerController::DispatchPatchOrSingleStep` → `Thread::WaitSuspendEventsHelper` → `CLREventBase::WaitEx` → `_pthread_cond_wait`); `github.com/microsoft/vscode/issues/300809` (SDK 10.0.200 breaks debugging, Darwin 25.3.0); `github.com/dotnet/sdk/issues/53382` (10.0.104/10.0.200 debugger crashes on macOS 26.3 osx-arm64).

**dotnet/sdk + MTP:**  
`github.com/dotnet/sdk/issues/45927` (native `dotnet test` MTP, Jan 2025); `github.com/microsoft/testfx/issues/6776` (MTP `--timeout` doesn't abort hanging tests, Open 2025-10-21, MTP 2.1 milestone); `learn.microsoft.com/en-us/dotnet/core/testing/migrating-vstest-microsoft-testing-platform` (runsettings env-vars unsupported under MTP); `learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp` (CLI `-e` is honored); `xunit.net/docs/getting-started/v3/microsoft-testing-platform` (`UseMicrosoftTestingPlatformRunner=true` opt-in).

**OpenTelemetry / Aspire / JasperFx (module-initializer audit — negative):**  
`github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/Sdk.cs` (lazy `static Sdk()` cctor, no sync I/O); `github.com/dotnet/aspire/blob/main/src/Components/Aspire.Npgsql/README.md` (no `[ModuleInitializer]`); `github.com/JasperFx/wolverine/blob/main/src/Wolverine/Runtime/WolverineTracing.cs`; JasperFx/marten discussion #2489 (pre-built generated types — startup cost is runtime, not module-init). Code-search queries returned zero `[ModuleInitializer]` hits in any of these repos' src trees as of 2026-04-21.

**Diagnostic tooling:**  
`learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-stack`, `.../dotnet-dump`, `.../dotnet-trace`, `.../dotnet-sos`, `.../diagnostic-port`; `github.com/dotnet/diagnostics/issues/4779` (SOS `0xaa64` on Apple Silicon, fixed in milestone 9.0.0); `github.com/dotnet/diagnostics/issues/4551` (libsosplugin EXC_GUARD on macOS 14.4+, milestone 9.0.0); `github.com/dotnet/diagnostics/issues/4259` (arch-match for libsosplugin); `github.com/dotnet/diagnostics/issues/5196` (createdump / blame-hang on macOS arm64); `github.com/dotnet/diagnostics/blob/main/documentation/FAQ.md` (Xcode-lldb adhoc-sign workaround); `github.com/dotnet/diagnostics/blob/main/documentation/installing-sos-instructions.md`; `github.com/dotnet/dotnet-monitor/issues/1958` (DiagnosticPorts suspend semantics); Andrew Lock, "A brief introduction to Diagnostic Source" — `andrewlock.net/a-brief-introduction-to-diagnostic-source/` (proves WebApplication.CreateBuilder fires `HostBuilt`); `learn.microsoft.com/en-us/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-10.0` (confirms `hostBuilder:reloadConfigOnChange` default `true`).

**Related deadlock patterns (not matches, but referenced for diagnostic-recipe context):**  
`github.com/dotnet/runtime/issues/39911` (sync-over-async deadlock); `strathweb.com/2021/05/the-curious-case-of-asp-net-core-integration-test-deadlock/`.

**What I could NOT find despite targeted searching:** any open `dotnet/aspnetcore` or `dotnet/runtime` issue specifically titled "WebApplication.CreateBuilder hangs on macOS arm64 under WebApplicationFactory + Microsoft.Testing.Platform"; any public repro repo that isolates this on a minimal project. This suggests the bug either is undiagnosed-and-unreported, or reduces to the macOS-FSW / macOS-26-diagnostics bucket already tracked upstream. Filing a fresh issue at `dotnet/runtime` with the `sample` output from §7.3 would help close that gap if §7.2 confirms Candidate A.