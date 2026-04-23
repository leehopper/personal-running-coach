# MVP-0 + Adaptation Loop — Build Cycle Plan

> **Status:** Approved (2026-04-19). Active cycle.

## Status

- **Current Cycle:** MVP-0 + Adaptation Loop
- **Active Slice:** Slice 0 (Foundation) — Unit 1 persistence substrate merged (PR #49, commit `46348fc`, 2026-04-21). Unit 2 (T02.x — Auth API) is in-flight on PR #50. T02.1–T02.5 have shipped (register / login / me / logout / xsrf + CookieOrBearer + antiforgery double-submit + timing-safe login + Identity-error → DTO-bucket translation + the 15-case integration test matrix); T02.6 (backend dev environment — Docker HTTPS + Swagger antiforgery helper + CONTRIBUTING.md) landed alongside. DEC-050 through DEC-056 all integrated on the branch (auth / secrets / dotnet-10 SVE2 SIGILL posture). Post-open review response covers CodeRabbit's login-CSRF, /me `sub` fallback, antiforgery cookie Secure flag, General-bucket default-arm bug, DTO `Dto`-suffix rename, `JwtAuthOptions` record shape, `IdentityErrorMapping` own-file extraction, test rename to `AuthControllerIntegrationTests`, normalized 401-body deep-equality assertion, antiforgery bridge ProblemDetails, `AuthCookieNames` constants consolidation, CodeQL HttpOnly suppression with DEC-054 justification, and the coverage gate (mapper + extension unit tests close the 60.5% → ≥ 80% gap). Full suite **638 passing / 0 failing / 0 skipped** locally.
- **Active Slice Spec:** `docs/specs/12-spec-slice-0-foundation/`
- **Next Step:** Land PR #50, then begin Unit 3 (T03.x — frontend auth UX) with T03.0 (Vite HTTPS + `/api` proxy + CONTRIBUTING.md frontend half), then T03.1–T03.4 (RTK Query + LoginPage / RegisterPage + unit tests + Playwright happy-path). Unit 3 closes Slice 0 acceptance. Slice 1 requirements doc (`./slice-1-onboarding.md`) was amended with R-048 / DEC-047 integration 2026-04-19.
- **Blockers:** None.

Pre-slice-0 housekeeping landed in PR #46 (commit `9d4c51e`). Slice 0 spec written 2026-04-19. Batch 15 research (R-044 through R-047) and Batch 16 research (R-048 through R-050) landed and integrated 2026-04-19 across two passes — the headline architectural pivots are: DEC-044 (cookie-not-JWT browser auth), DEC-045 (Aspire deferred to MVP-1, stay on Compose + Tilt with `docker-compose.otel.yml` overlay), DEC-046 (SOPS + age + Postgres-backed DataProtection + dotnet user-secrets), DEC-047 (onboarding event-sourcing pattern locked for Slice 1). Unit 1 implementation landed across six commits 2026-04-20 (`dc047b0` → `9b95291`), but a reproducible startup hang surfaced during T01.5 (`WebApplicationFactory<Program>` + SUT boot). R-054 / Batch 18a research returned the canonical composition recipe 2026-04-20: Marten's `IntegrateWithWolverine()` subsumes Wolverine's `PersistMessagesWithPostgresql()` — never call both. DEC-048 codifies the invariants; code changes applied. Investigation showed the hang is **inside `WebApplication.CreateBuilder(args)` itself** (Main runs but the framework builder-creation never returns) — deeper than R-054 diagnosed. R-055 / Batch 18b (`docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`) returned the root cause 2026-04-20: synchronous `FileSystemWatcher` init on macOS arm64 / Darwin 25.x — three default-reloading JSON config sources each install a watcher whose `PhysicalFilesWatcher.StartRaisingEvents` calls `Interop.Sys.Sync()` (dotnet/runtime#77793), stalling unboundedly. DEC-049 captures the fix: `DOTNET_hostBuilder__reloadConfigOnChange=false` at process start + runtime envelope provisioning via `WolverineModelCustomizer`. Unit 1 shipped with the full `WebApplicationFactory<Program>` fixture + six SUT-host smoke tests green in ≤ 2 s cold; Unit 2 auth-endpoint integration tests are unblocked.

This status block is the single source of truth for "where are we?" — mirrored into `ROADMAP.md` so `/catchup` finds it. Update both whenever a slice completes or the active slice changes.

---

## Captured During Cycle

Running log of "we should also do this" items found during the cycle but intentionally deferred — preserves the affordance the old `ROADMAP.md` Deferred Items section had, scoped to the active cycle so the list doesn't grow unboundedly.

**How to use**

- Any agent (or human) may append an entry when finding work that shouldn't block the current slice but shouldn't be lost.
- Each slice's PR description includes a `### Follow-ups found` section (empty is fine); items move into this table at slice completion.
- At cycle completion, every entry gets one of four dispositions:
  - (a) promoted to `docs/features/backlog.md`,
  - (b) becomes its own `docs/decisions/decision-log.md` entry,
  - (c) becomes a research prompt (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)),
  - (d) scheduled into the next cycle.
- The table does not survive cycle completion un-triaged.

| Found | In slice | Item | Triage disposition |
|---|---|---|---|
| 2026-04-19 | (cycle-plan) | Frontend/backend breakdown pass on cycle-plan organization — sections currently mix layers, may read cleaner with explicit F/E vs B/E separation | Deferred; re-evaluate after Slice 1 when the shape of per-slice plans clarifies whether a layer-split would help |
| 2026-04-19 | Slice 0 (Batch 15 audit) | LLM context assembly for the projected `Plan` document — R-047 covers Marten's `WriteLatest<Plan>` zero-copy HTTP streaming, but RunCoach's `ContextAssembler` builds prompts internally (not over HTTP). The Plan-projection-to-prompt-tokens shape is undefined. | **Partially answered by R-048 / DEC-047** — `ContextAssembler.ComposeForClaude(view, appendUser: ...)` event-replay pattern is the shape; finalize the Plan-side projection→prompt at Slice 1 spec-writing time. Open. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Production deployment topology — single VPS / managed PaaS / container orchestrator / managed Postgres / CDN. R-046's bundle-as-Job production migration assumes *some* target. No env exists yet. | **Partially answered by R-049 / DEC-046** — secrets bootstrap layer is per-target (systemd-creds for VPS, native PaaS, ACA Key Vault references). Target choice itself still open. Pre-MVP-1 research prompt when target is committed. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Observability strategy — OTel exporter + collector + dashboard. R-047 listed Marten daemon metrics worth alerting on; zero observability infrastructure decided. | **Resolved for Slice 0 by R-050 / DEC-045** — `docker-compose.otel.yml` overlay (Collector + Jaeger) wired with Marten / Wolverine / `RunCoach.Llm` ActivitySource + Meter sources; transferable to Aspire later. Production observability remains a pre-MVP-1 prompt. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Database backup / restore / data lifecycle — Plan adaptation history is irreplaceable once real users exist. | Pre-MVP-1 research prompt; coordinate with the production-deployment-topology decision. Disposition (c) — research prompt. |
| 2026-04-19 | Slice 0 (Batch 16 integration) | FTC HBNR pre-public-release escalation point — R-049 confirmed the rule applies to RunCoach as a PHR vendor the moment any Apple Health / Strava / Garmin ingest exists; the migration to Azure Key Vault + Managed Identity wrapping `ProtectKeysWithAzureKeyVault` happens before the first non-alpha user. | Captured in DEC-046 cross-reference. Promote to a concrete pre-public-release task list (rotation runbook, breach runbook, DPAs with Anthropic + analytics, formal program against ASVS L1 V13.3) at MVP-1 cycle start. |
| 2026-04-19 | Slice 0 (Batch 17 audit) | Batch 17 research queued (R-051 LLM observability, R-052 Anthropic SDK choice, R-053 multi-turn eval pattern). All three target Slice 1's LLM call sites; **none block Slice 0**. | Slice 0 implementation can begin in parallel with Batch 17 research. Slice 1 spec session awaits the three artifacts. Disposition (c) — research prompts at `docs/research/prompts/batch-17{a,b,c}-*.md`. |
| 2026-04-20 | Slice 0 (T01.5 wrap-up → R-054 Batch 18a) | Reproducible startup hang surfaced during `WebApplicationFactory<Program>` SUT boot — 5-min `HostFactoryResolver.CreateHost` timeout, zero log output. Scope-reduced test shipped with T01.5 as documented follow-up. R-054 deep-research pass returned the canonical composition recipe (artifact: `batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md`); spec amended 2026-04-20. | **Resolved (DEC-048 + code)**. Applied: delete `WolverinePostgresqlDataSourceExtension.cs`, drop `PersistMessagesWithPostgresql` (Marten's `IntegrateWithWolverine` subsumes it), add `ApplyJasperFxExtensions` before `AddMarten`/`UseWolverine`, pair `DaemonMode.Solo` + `DurabilityMode.Solo`, make OTLP exporter conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`, drop obsolete `public partial class Program`, resolve `NpgsqlDataSource` from DI via `sp.GetRequiredService<NpgsqlDataSource>()`, enable `ValidateScopes=true`/`ValidateOnBuild=false` in Dev. DEC-048 landed. |
| 2026-04-20 | Slice 0 (DEC-048 verification → R-055 Batch 18b) | After applying every R-054 correction the SUT host **still** hangs. Instrumented `Program.cs` tracing showed `Main` enters and writes the first trace line, then `WebApplication.CreateBuilder(args)` itself blocks for the full 5-min `HostFactoryResolver` window — deeper than R-054 diagnosed. R-054's three hypotheses (second `NpgsqlDataSource` connecting too early, Roslyn codegen in the 5-min window, `ValidateOnBuild=true` + scoped-from-root) do not match. `ASPNETCORE_PREVENTHOSTINGSTARTUP=true` does not unblock; `HOSTINGSTARTUPASSEMBLIES` was already empty. | **Resolved (DEC-049 + code).** R-055 artifact (`docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`) identified the cause as synchronous `FileSystemWatcher` init on macOS arm64 / Darwin 25.x — `PhysicalFilesWatcher.StartRaisingEvents` calls `Interop.Sys.Sync()` (dotnet/runtime#77793) which stalls unboundedly when three JSON config sources each install a watcher (appsettings, appsettings.Development, user-secrets — all default `reloadOnChange: true`). The prescribed §7.1 / §7.2 reductions confirmed it (without env var: zero stdout for 10 s; with `DOTNET_hostBuilder__reloadConfigOnChange=false`: `CreateBuilder` returns in ~3 s). Applied: `Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false")` at top of `Program.cs`, `builder.UseSetting(...)` belt-and-suspenders in `RunCoachAppFactory.ConfigureWebHost`, connection-string override via `ConnectionStrings__runcoach` env var in `InitializeAsync` (takes precedence over `appsettings.Development.json`). **Dormant bug unmasked:** `RunCoachDbContext.OnModelCreating`'s manual `MapWolverineEnvelopeStorage()` collided with `WolverineModelCustomizer`'s runtime call (duplicate `WolverineEnabled` annotation); removed from `OnModelCreating`. **Outcome:** 575/0/1 (scope-reduced) → 581/0/1 (full `WebApplicationFactory<Program>` fixture with six SUT-host smoke tests — IDocumentStore, RunCoachDbContext, DpKeysContext, IDataProtectionProvider, `/health`, NpgsqlDataSource identity-equality — all green in ≤ 2 s cold). DEC-049 captures the invariants. **Unit 2 (auth endpoints) is now unblocked.** |
| 2026-04-21 | Slice 0 (PR #49 CodeRabbit review) | `appsettings.Development.json` committed a plaintext dev-only Postgres password (`Password=runcoach_dev`). Initially deferred as "same value already in docker-compose.yml, addressing only one is security theater." SonarCloud's Security Rating gate then hard-failed on the same string, so we revisited. | **Applied.** Removed the `ConnectionStrings` block from `appsettings.Development.json` entirely. Local `dotnet run` now requires `dotnet user-secrets set "ConnectionStrings:runcoach" "…"` against `runcoach-api` (same pattern already used for `Anthropic:ApiKey`). Tests use Testcontainers-set env var; Compose `api` service sets env var. `docker-compose.yml`'s remaining plaintext is scoped to the compose-internal Postgres service (DEC-046 replaces it with SOPS at pre-public-release); it is not a committed-secret signal SonarCloud analyzes. |
| 2026-04-21 | Slice 0 (PR #49 CodeRabbit review) | `RunCoachAppFactory` uses `Environment.SetEnvironmentVariable` to override the connection string and OTLP endpoint for the test host — process-wide state mutation. CodeRabbit suggested per-host `ConfigureAppConfiguration` or `UseSetting(...)` instead. | **Applied (scoped).** Kept the env-var override per R-055 (`ConfigureAppConfiguration` / `UseSetting` did not reliably beat the JSON providers on this stack) but bracketed the mutation with save/restore: `InitializeAsync` captures prior values before overwrite, `DisposeAsync` restores them. Process state after fixture dispose is indistinguishable from before, eliminating the cross-contamination risk CodeRabbit flagged while preserving the one override path known to reliably beat the JSON config providers. |
| 2026-04-21 | Slice 0 (PR #49 CodeRabbit review) | `docker-compose.otel.yml` uses `depends_on: {jaeger: condition: service_started}` instead of `service_healthy`. Jaeger's OTLP port is up immediately after the container starts, so this is cosmetic; a `service_healthy` gate would require authoring a Jaeger healthcheck. | **Applied (jaeger side).** Added Jaeger healthcheck probing `http://localhost:14269/` (admin endpoint, BusyBox wget is available in the `jaegertracing/all-in-one` Alpine base). Collector's `depends_on: jaeger` upgraded to `condition: service_healthy`. The `api` → `otel-collector` gate kept at `service_started`: the contrib collector image (`otel/opentelemetry-collector-contrib:0.150.1`) is built `FROM scratch` with no shell, so CMD-SHELL healthchecks are not runnable in-container. Collector binds OTLP ports synchronously during start and the exporter retries on transient failure, so `service_started` is effectively readiness for this opt-in overlay. |
| 2026-04-21 | Slice 0 (PR #49 deep-review pass) | Seven findings surfaced by the deep-review pipeline: (F-01) `DevelopmentMigrationService` has no test coverage; (F-02) spec lines 47 / 61 / 170 prescribe `MapWolverineEnvelopeStorage()` but DEC-049 moved envelope provisioning to runtime; (F-03) 7 / 9 `StartupSmokeTests` lack AAA comment markers; (F-04) `InitializeAsync` duplicates the options-builder pattern already in `CreateDbContext`; (F-05) the `historyCount` assertion chains `.And.Subject.As<long>().Should()` instead of `.Which.Should()`; (F-06) file named `StartupSmokeTests.cs` though the class is `[Trait(Category, Integration)]` + `WebApplicationFactory<Program>`; (F-07) opt-in `SmokeTests.cs` (vanilla factory) silently depends on the AssemblyFixture env-var side effect. | **Applied in one pass.** (F-01) Added `SutHost_DevelopmentMigrationService_Is_Registered_As_HostedService_In_Development` — catches regressions that drop the `AddHostedService<DevelopmentMigrationService>()` call in `Program.cs`. (F-02) Spec lines 47 / 61 / 170 rewritten to describe runtime envelope provisioning via `WolverineModelCustomizer` + `ApplyAllDatabaseChangesOnStartup` per DEC-049; manual `MapWolverineEnvelopeStorage()` in `OnModelCreating` is now explicitly prohibited in the spec. (F-03) All nine test methods now carry `// Arrange` / `// Act` / `// Assert` markers plus `actualXxx` locals where the convention is natural. (F-04) `InitializeAsync` calls `CreateDbContext()` — single source of truth for the test-side options builder. (F-05) `historyCount.Should().BeOfType<long>().Which.Should().BeGreaterThan(0, ...)` — single fluent chain, no `.And.Subject.As<T>()` detour. (F-06) `git mv` → `StartupSmokeIntegrationTests.cs`, class renamed, `AssemblyInfo.cs` still references `RunCoachAppFactory`. (F-07) `SmokeTests.cs` deleted — `StartupSmokeIntegrationTests.Health_Endpoint_Returns_Ok_Json` subsumes it with stronger assertions (content type + body shape), eliminating the AssemblyFixture coupling that opt-in test had. Residual scope: a `Production`-environment fixture that proves `DevelopmentMigrationService` is **not** registered in Prod — not yet added; captured below. |
| 2026-04-21 | Slice 0 (PR #49 review follow-up) | No test fixture currently boots the SUT at `UseEnvironment("Production")`, so the `!IsDevelopment()` branch that withholds `DevelopmentMigrationService` is not directly exercised. | Deferred to post-Unit-2. Adding a second `WebApplicationFactory` subclass for Prod-env coverage is straightforward once the auth-endpoint integration suite lands; it is not a blocker for Unit 1. |
| 2026-04-21 | Slice 0 (PR #49 review follow-up) | CodeRabbit flagged duplicate TRX-upload-on-failure step between `.github/workflows/ci.yml` and `.github/workflows/sonarqube.yml` — ~10 line duplication. | Deferred as optional cleanup. A shared composite action would add a separate file for 10 lines that read cleanly inline; current duplication is low-drift-risk because the two workflows run the same `dotnet test` command and output the same TRX paths. Revisit if a third workflow lands that also uploads the same artifact. |
| 2026-04-21 | Slice 0 (R-056 / R-057 integration) | `dotnet dev-certs https --trust` is a hard contributor prerequisite: the `__Host-RunCoach` + `Secure` cookie is functionally broken on `http://localhost` in Chrome and Safari (only Firefox tolerates it). Needs a `CONTRIBUTING.md` or root `README.md` paragraph documenting the one-time trust step and the `mkcert` escape hatch for corporate Linux. | Pre-PR-open blocker for Slice 0: must land before the first real browser login is attempted (T03.x). The code changes shipping with DEC-050 are necessary but not sufficient — a contributor discovering the contract the hard way is exactly the regression that doc is designed to prevent. |
| 2026-04-21 | Slice 0 (R-056 integration) | Forwarded Headers middleware (`app.UseForwardedHeaders()` + `services.Configure<ForwardedHeadersOptions>(...)` with `KnownProxies` / `KnownNetworks`) is called out in R-056 as the canonical fix for the `ERR_TOO_MANY_REDIRECTS` loop behind Nginx / Azure Linux App Service / K8s ingress, but no hosted environment exists yet. | Deferred to MVP-1 deployment-target decision. The middleware must run *before* everything else in the pipeline when it lands, so adding it is a pipeline-ordering change — not a drop-in. Revisit alongside the production-deployment-topology research pass (already on the cross-cycle deferred list). |
| 2026-04-21 | Slice 0 (R-057 integration) | Test-host `UseEnvironment("Testing")` migration is the artifact's recommended environment split so `ValidateOnStart` gating can extend to `!IsDevelopment() && !IsEnvironment("Testing")` and Test can diverge from Dev (e.g. exercise Production-shape flags while skipping `DevelopmentMigrationService`). Today Dev and Test share the migration trigger, which is correct under the current topology. | Deferred to T02.5 + Unit 2 close-out. When the Prod-environment fixture earlier captured as 2026-04-21 PR #49 deep-review follow-up (`DevelopmentMigrationService` NOT registered in Prod) lands, bundle the `Testing` environment move with it — same one-new-fixture-subclass surface area. |
| 2026-04-21 | Slice 0 (R-057 integration) | `PostConfigure<JwtBearerOptions>` pattern with a deterministic test-key + pre-built `OpenIdConnectConfiguration` is the recommended approach when iOS-path tests arrive (short-circuits OIDC metadata discovery; works with `JsonWebTokenHandler`). Not needed now — cookie-only tests leave the JWT handler dormant and it rejects every token with `IDX10500`. | Deferred to iOS-shim workstream (post-MVP-0 per DEC-033). Code pattern is captured in R-057 artifact §"Test-host posture (later …)" so it's ready to lift when that slice lands. |
| 2026-04-21 | Slice 0 (R-057 integration verification) | Initial R-057 integration (commit `fc8382a`) registered `IValidateOptions<JwtAuthOptions>` unconditionally and gated only `ValidateOnStart` on `!IsDevelopment()`. A one-off probe test confirmed that `IOptionsMonitor<JwtBearerOptions>.Get("Bearer")` triggers the `Configure<IOptions<JwtAuthOptions>>` callback which resolves `.Value`, which fires the validator eagerly — throwing `OptionsValidationException` because `Auth:Jwt` is unset in Dev / CI. This would have surfaced as 500 on every T02.4 protected-endpoint test because `PolicyEvaluator` iterates the `CookieOrBearer` policy's scheme list on every protected request (including requests with no bearer header). | **Resolved (code).** The validator registration itself is now gated on `!IsDevelopment()` in `Program.cs` — same condition as `ValidateOnStart`. Full suite 588/0/0 confirms Dev / CI no longer fires the validator; Prod / Staging still fail fast at startup if `Auth:Jwt` is missing or malformed. No decision-log entry needed — DEC-051 already documents the intended posture; this entry captures the implementation correction. |
| 2026-04-21 | Slice 0 (post-integration review — T02.4 blockers) | Two genuine unknowns surfaced while looking ahead at T02.4's `AuthController`: (a) the canonical ASP.NET Core 10 pattern for translating `IdentityResult.Errors` into `ValidationProblemDetails` keyed by DTO property name rather than `IdentityError.Code` — the spec mandates the DTO-property shape but the mechanics aren't obvious (R-058); (b) whether `SignInManager.PasswordSignInAsync` in .NET 10 is timing-safe on the unknown-email path or requires a manual dummy-hash mitigation — the spec's "or" between the two options signaled uncertainty (R-059, security-sensitive). | **Resolved.** Artifacts landed at `docs/research/artifacts/batch-19c-*.md` + `batch-19d-*.md`; queue rows R-058 + R-059 marked Done. Findings integrated as DEC-052 (per-action `ModelState.AddModelError` loop + DTO DataAnnotations pre-validation + `IdentityErrorCodeMapper` + 409 split to plain ProblemDetails) and DEC-053 (manual dummy-hash mitigation — `SignInManager.PasswordSignInAsync(string, ...)` is NOT timing-safe in .NET 10; cached `VerifyHashedPassword` pass required; all failure modes collapse to byte-identical 401). Spec §Unit 2 lines 83–86 + 91 amended to remove the "or" ambiguity and tighten the error-contract language. T02.4 is now unblocked. |
| 2026-04-21 | Slice 0 (R-058 / DEC-052 follow-ups) | Frontend (T03.x) parity requires the Zod schemas on register / login forms to mirror the `RegisterRequest` DataAnnotations (email format + 254 max; password 12–128 length). A contract-test asserting equivalence (`"A1a!bcdefghij"` passes both; `"short"` fails both) belongs in the `shared-contracts` folder. | Deferred to T03.1. Capture in T03.x task description so the contract-test lands alongside the Zod schema and doesn't drift from the backend DataAnnotations. |
| 2026-04-21 | Slice 0 (R-058 / DEC-052 follow-ups) | The 409 duplicate-email posture ships with a deliberate enumeration-resistance gap — status code itself signals conflict even though body is generic. OWASP ASVS 5.0 V6.3 flags 409-on-duplicate-registration as a user-enumeration leak. MVP-0 (personal use + friends) accepts this; pre-public-release should migrate to the "202 Accepted + email-to-existing-account" pattern (OWASP registration-hardening guidance) which eliminates the status-code signal. | Deferred to pre-public-release. Captures the migration path so a future security pass has the context without re-researching. |
| 2026-04-21 | Slice 0 (R-059 / DEC-053 follow-ups) | When `lockoutOnFailure: true` lands in MVP-1 (currently deferred per spec Non-Goals), the known-user failure branch gains a DB write via `UserManager.AccessFailedAsync` that the unknown-user branch does not. That re-opens the timing leak on a secondary axis that the current manual mitigation does not close. Mitigation options in R-059: (a) override `SignInManager` to track failed attempts on unknown users; (b) uniform-delay envelope wrapping the whole login action (OWASP-aligned); (c) accept residual leak + per-IP rate limiting. | Deferred to MVP-1 lockout-enablement workstream. DEC-053's "Known limitations" section captures the options + preference (option b, uniform delay). |
| 2026-04-21 | Slice 0 (R-059 / DEC-053 follow-ups) | Password-reset endpoint (post-Slice 0) must match the login's timing-safety posture — return identical 200 whether the email exists or not; execute token-generation (or a dummy equivalent) to equalize timing; email-send must be fire-and-forget and identical across both branches. | Deferred to password-reset workstream (post-MVP-0 per Non-Goals). Pattern is documented in DEC-053's "Known limitations" so the workstream starts with the right shape. |
| 2026-04-21 | Slice 0 (post-R-058/R-059 integration review → DEC-054) | Post-integration review surfaced four items: (a) R-060 candidate on antiforgery SPA-readable cookie attributes; (b) R-061 candidate on `CookieAuthenticationEvents.OnRedirectToLogin` JSON-401 override for SPA API endpoints; (c) verification of the task-#54 assertion that `[ValidateAntiForgeryToken]` is "broken with UseAntiforgery middleware in .NET 10"; (d) a missed `options.User.RequireUniqueEmail = true` in the T02.1 Identity Core registration. | **All four resolved via DEC-054, no deep-research artifacts needed.** (a) Microsoft Learn documents the SPA cookie pattern + RunCoach-specific attribute choices captured in DEC-054 (cookie named `__Host-Xsrf-Request` for `__Host-` posture parity). (b) ASP.NET Core 10 automatically returns 401/403 for `[ApiController]`-decorated endpoints instead of redirecting — no override required (Microsoft Learn `security/authentication/api-endpoint-auth`). (c) Unfounded claim — `[ValidateAntiForgeryToken]` is the canonical MVC attribute per Microsoft Learn; `[RequireAntiforgeryToken]` is the parallel Minimal-API metadata attribute, also functional but not the primary choice for `[ApiController]`. (d) One-line fix slotted into T02.4 task description. Research-queue rows R-060 + R-061 added marked Done with pointers to the Microsoft Learn docs for traceability; task #54 description and spec §Unit 2 amended. |
| 2026-04-22 | Slice 0 (T02.6 Tilt verification → R-063) | Attempted `tilt up` on Apple M4 Pro host to validate the T02.6 Swagger + antiforgery round-trip end-to-end. Containerized `dotnet restore` inside `mcr.microsoft.com/dotnet/sdk:10.0` SIGILLs deterministically (exit 132). Ruled out resources (persists at 4 CPU / 8 GiB), VM driver (same on VZ and QEMU), arch emulation (all aarch64), image version (container SDK 10.0.203 matches host), universal SDK brokenness (trivial `dotnet new console` restores fine in the same container), and NuGet signature verification. R-063 returned `dotnet/runtime#122608` (milestone .NET 11): CoreCLR SVE2 JIT codegen on M3/M4/M5 under VZ. | **Integrated 2026-04-22 as DEC-056.** Empirical verification on M4 Pro rejected the artifact's primary narrow-knob recommendation (`DOTNET_EnableArm64Sve/Sve2/Sme/Sme2=0` all 5/5 FAIL) and the master switch (`DOTNET_EnableHWIntrinsic=0` — 1 lucky run then 5/5 FAIL). Rosetta also broken on this Colima profile. Durable posture: pin SDK + aspnet digests, no ENV JIT knobs, demote Path A to CI-only on x86_64, Path B (host-run) is the sole Apple-Silicon dev loop until .NET 11. `CONTRIBUTING.md` rewritten to lead with Path B and label Path A "x86_64 only." T02.6 PR continues via Path B validation — Swagger contract is identical either way. |
| 2026-04-22 | Slice 0 (DEC-056 follow-ups — deferred from R-063 §6) | R-063's durability harness had three components: (a) `packages.lock.json` + `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` + `dotnet restore --locked-mode` as defense-in-depth for NuGet content-hash tampering; (b) `scripts/verify-container-restore.sh` local smoke + GitHub Actions matrix job; (c) Dependabot `ignore` rule on the `mcr.microsoft.com/dotnet/sdk` tag pending .NET 11. | Deferred. (a) is orthogonal to #122608 and is a workflow change (regenerate the lockfile, verify Dependabot's lockfile update flow, decide transitive-lock policy) — land as its own task when time permits. (b) is only useful once GitHub provides Apple-Silicon M3/M4/M5 runners; today `macos-latest` is M1 which does NOT reproduce the fault. (c) is insurance against a silent regression bump, but also blocks legitimate security updates — the Dockerfile's top-of-file comment pointing at DEC-056 + the digest pins serve as the manual-review gate for a solo-dev workflow at this scale. Revisit all three at MVP-1 if team grows or if an M3+ runner surfaces in GitHub Actions. |
| 2026-04-22 | Slice 0 (R-064 second-opinion verification of DEC-056) | R-063's narrow-knob and master-switch recommendations both failed empirically (5/5 FAIL). Before shipping T02 PR with "Path A broken until .NET 11" documentation as final, commissioned R-064 to exhaustively rule out a missed workaround across 12 angles (correct env-var spelling, sub-feature fan-out, `runtimeconfig.json` configProperties, alternative container runtimes, VM CPU-feature tuning, nightly SDK tags, cross-SDK build, host-side restore + COPY, NativeAOT/ReadyToRun, inline-RUN env pattern, community workarounds, Microsoft backport posture). | **Integrated 2026-04-22 as DEC-056 "Post-R-064 verification" supplement.** All 15 candidates rejected with primary-source citations. Headline findings: R-063's env-var names were spelled correctly (verbatim from `jitconfigvalues.h`) — the fault is in `cpufeatures.c` detection-probe or SME2 instructions with no .NET 10 gate, outside the env-var surface. Podman applehv closed "not planned" (#28312). Apple VZ has no CPU-feature-mask API. Two partial escapes documented (Docker Desktop ≥ 4.39, Colima QEMU + cortex-a72) but neither preserves the reference profile; added to CONTRIBUTING.md as opt-in hatches, not project posture. Defensive ENV block (6 SVE/SVE2 knobs from `jitconfigvalues.h`) added to `backend/Dockerfile` as belt-and-suspenders — zero-cost no-op on x86_64 CI, reduces JIT-side emission surface if ever misused on arm64. 13 specific re-check triggers captured in DEC-056 + R-064 artifact §D. DEC-056 is now officially final under two independent research passes. |

---

## Goal & Done-State

Build a real multi-tenant product where you can:

1. Sign up and log in.
2. Complete chat-driven onboarding that builds your user profile.
3. See a generated macro/meso/micro training plan on the home page.
4. Log a workout with as much or as little data as you want (bare minimum: distance + duration + completion; rich path: add RPE, HR, splits, HRV, weather — whatever you've got — plus freeform "what happened" notes).
5. Watch the plan adapt when logged workouts deviate from prescription, with the coach explaining *why* in a persistent chat panel.
6. Ask the coach open-ended questions grounded in your profile + plan + recent history.

"Done" = the above end-to-end loop works, you are personally using it to run, and the eval suite covers the adaptation scenarios that matter.

---

## In Scope

- **Auth:** ASP.NET Identity + JWT; register/login/logout UI; password reset deferred until pre-public-release.
- **Persistence:** PostgreSQL + EF Core (relational entities) + Marten (event-sourced plan state). Both run against the same Postgres instance; clear ownership boundaries per `backend/CLAUDE.md`.
- **Onboarding:** Chat-driven multi-turn flow covering the topics in `docs/planning/interaction-model.md` (primary goal, target event, current fitness, schedule constraints, injury history, preferences).
- **Plan generation + persistence:** Macro/meso/micro tiers per `docs/planning/planning-architecture.md`. Plan is a Marten event-sourced aggregate; projection = current plan document for LLM consumption.
- **Plan view:** Structured UI surface — this-week card, today's workout, upcoming list.
- **Workout logging:** Structured form with required core fields + optional rich metrics (JSONB) + freeform notes. Flexible: bare-minimum logs and rich logs both work; the LLM renders what's present and gracefully handles what isn't.
- **Adaptation loop:** Logged workout → deterministic deviation computation → LLM adaptation prompt with full context (including freeform notes) → structured-output plan modification → event appended to Marten stream → projection updated → plan view re-renders → chat panel surfaces the explanation.
- **Open conversation:** Persistent chat panel for ad-hoc coaching questions. Context-assembler routes by query type per the existing `ContextAssembler` design.
- **Eval-suite extension:** Slice 3 adds adaptation scenarios to the existing M.E.AI.Evaluation infrastructure. CI continues replay-only.

## Out of Scope (Deferred — Designed-For)

These are explicitly not built in this cycle, but the architecture leaves room so they bolt on without schema thrash or refactoring.

- **Apple HealthKit / iOS shim.** Auto-fill of workout metrics. Needs an iOS companion app per DEC-033. Design accommodation: `WorkoutLog.metrics` JSONB column takes whatever HealthKit gives us.
- **Garmin Connect integration.** Post-MVP-1 per `CLAUDE.md`. Design accommodation: same JSONB shape; webhook ingress pipeline is a future slice.
- **Pre-public-release safety scaffolding.** PAR-Q+ extended screening, medical-scope keyword triggers, population-adjusted guardrails, beta participation agreement, full ToS, LLC formation. Blocks public exposure, not personal use.
- **Tiered model routing.** Post-MVP-0 cost optimization per DEC-038. Design accommodation: existing `ICoachingLlm` interface is the natural seam.
- **Voice notes / mid-run logging.** Re-opens the temporal-binding problem (which workout does "I had to walk" refer to?). Further out.
- **Proactive notifications.** Light-touch missed-workout detection *may* land in Slice 4 if it's cheap; full proactive system deferred.
- **Coach personalities, multi-sport, nutrition guidance, injury prediction, social features.** All in `docs/features/backlog.md` as Future.

---

## Slice Structure

Each slice ships top-to-bottom through every layer (DB → repo → controller → frontend → tests) and is usable when done. The product is stoppable after any slice.

**Tier-3 requirements docs live alongside this file** — one per slice, ~90 lines each. They elaborate requirements without crossing into implementation. When a slice's implementation session starts, that session reads the per-slice requirements doc + this cycle plan + the referenced research artifacts, then writes a spec under `docs/specs/`.

| # | Name | Requirements doc |
|---|---|---|
| 0 | Foundation | [`./slice-0-foundation.md`](./slice-0-foundation.md) |
| 1 | Onboarding → Plan | [`./slice-1-onboarding.md`](./slice-1-onboarding.md) |
| 2 | Workout Logging | [`./slice-2-logging.md`](./slice-2-logging.md) |
| 3 | Adaptation Loop | [`./slice-3-adaptation.md`](./slice-3-adaptation.md) |
| 4 | Open Conversation | [`./slice-4-conversation.md`](./slice-4-conversation.md) |

### Slice 0 — Foundation

**Requirements:** [`./slice-0-foundation.md`](./slice-0-foundation.md)


**Acceptance — "I can…"**

- [ ] …run `docker compose up` and have Postgres + API + web all healthy.
- [ ] …hit `POST /api/v1/auth/register` and create an account.
- [ ] …hit `POST /api/v1/auth/login` and receive a JWT.
- [ ] …open the frontend, register/login through the UI, and see an authenticated empty home page.
- [ ] …see CI green on the slice-0 PR with all six required checks passing.

**Scope**

- Backend: `RunCoachDbContext` (EF Core) + Marten registration wired into DI; initial migration applied on startup in development. `Modules/Identity/` module with Identity tables, JWT issuance, register/login/logout endpoints. Global error-handling middleware.
- Frontend: `app/modules/auth/` with login + register pages, JWT stored in Redux + persisted, axios/fetch interceptor attaches the token, protected-route wrapper. Auth store slice.
- Tests: Integration tests for register/login/logout using `WebApplicationFactory` + Testcontainers. Component tests for auth pages. One Playwright happy-path E2E (register → login → see home).
- No business features — no plan, no logging, no coaching. Foundation only.

**Key risks**

- First time wiring Identity + EF Core + Marten + JWT together — integration surprises possible. Allocate time for this.
- Testcontainers + local Postgres configuration on macOS (Colima) — verified in existing CI but not yet exercised at this scope.

**Relevant research artifacts**

- `batch-10b-dotnet-backend-review-practices.md` — backend conventions applied here.
- `batch-10c-ci-quality-gates-private-repo.md` — CI pipeline structure.
- `batch-14a` / `batch-14b` / `batch-14c` / `batch-14f` — CodeRabbit, CodeQL, SonarQube, branch protection patterns.
- `batch-10a-frontend-latest-practices.md` — React 19 + TS + Vite conventions for the auth module.

---

### Slice 1 — Onboarding → Plan

**Requirements:** [`./slice-1-onboarding.md`](./slice-1-onboarding.md)

**Acceptance — "I can…"**

- [ ] …complete a multi-turn chat-driven onboarding flow that builds my user profile.
- [ ] …see a generated macro/meso/micro training plan on the home page after onboarding completes.
- [ ] …reload the page and see the same plan (persisted, not regenerated).
- [ ] …re-trigger plan generation from a settings action (for iteration / correction).

**Scope**

- Backend: `Modules/Training/` gains the Marten-backed `Plan` aggregate (events: `PlanGenerated`). `Modules/Coaching/` gains an `OnboardingController` — multi-turn: each POST returns either "next question" (with structured-output schema for the question + which profile field it fills) or "complete, plan generated." Uses the existing `ContextAssembler` and `ClaudeCoachingLlm`. `UserProfile` entity (EF Core) persists onboarding answers. Plan generation invokes the existing brain layer; projection materializes to a structured document.
- Frontend: `app/modules/onboarding/` — guided chat UI (progress indicator, "we're almost done" framing, not the day-to-day chat panel). `app/modules/plan/` — this-week card, today's workout, upcoming list. RTK Query slices.
- Tests: Integration tests for onboarding controller (multi-turn flow, completion). Eval cache extended with onboarding scenarios. Playwright: register → onboard → see plan.

**Key risks**

- Multi-turn onboarding state management — where does the "in-progress onboarding" live? Probably a `UserProfile.OnboardingStatus` column + per-turn requests that pass the accumulating state. Decide at slice-1 plan time.
- Plan projection shape — the existing brain layer emits plan documents; they need to land in a stable projection the frontend can render. Design the projection schema at slice-1 plan time.

**Relevant research artifacts**

- `batch-2a-training-methodologies.md` — training-science basis for plan generation.
- `batch-2b-planning-architecture.md` — macro/meso/micro tier semantics, event-sourcing patterns.
- `batch-4a-coaching-conversation-design.md` — onboarding tone, question ordering, OARS/GROW patterns.
- `batch-6a-llm-eval-strategies.md` + `batch-6b-dotnet-llm-testing-tooling.md` — eval patterns for onboarding scenarios.
- `batch-7a-ichatclient-structured-output-bridge.md` — structured output for per-turn onboarding responses.
- `batch-4b-special-populations-safety.md` — safety considerations for onboarding profile questions (injury history, pregnancy, chronic conditions) even though pre-public-release safety scaffolding is deferred.

---

### Slice 2 — Workout Logging

**Requirements:** [`./slice-2-logging.md`](./slice-2-logging.md)

**Acceptance — "I can…"**

- [ ] …see today's prescribed workout on the home page.
- [ ] …open a log form, fill in at minimum distance + duration + completion, save it.
- [ ] …optionally expand "more details" and fill in RPE, HR avg/max, calories, splits, HRV, sleep score, weather — whatever I have — without the form yelling at me for missing fields.
- [ ] …write freeform "what happened?" notes and have them persisted.
- [ ] …see my logged workout appear in a history list, with notes visible.
- [ ] …verify via eval that the logged notes + metrics flow into LLM context (no adaptation wired yet — just context injection).

**Scope**

- Backend: `WorkoutLog` entity (EF Core). Required cols: `Id`, `UserId`, `PlannedWorkoutId` (nullable), `LoggedAt`, `Distance`, `Duration`, `CompletionStatus` (enum: complete/partial/skipped), `Notes`. One nullable JSONB col: `Metrics` (takes arbitrary keys: `rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`, etc. — no schema enforcement). Repo + log endpoint. `ContextAssembler` extension to include recent `WorkoutLog.Notes` + `Metrics` keys in the training-history block.
- Frontend: `app/modules/logging/` — today's workout card gets a "Log" action. Log form with collapsed "More details" expander. Render whatever metrics are present in the history list.
- Tests: Integration test for log endpoint. Unit tests for `ContextAssembler` extension (various metric shapes, including empty metrics). Playwright: today's workout → log with minimum → log with rich metrics → both appear in history.

**Key risks**

- JSONB key naming conventions — decide the canonical keys for the metrics most likely to come in (from manual entry now, from HealthKit later). Lock them in a shared constants file so manual logging and future auto-fill write the same shape.
- Metrics-absent LLM prompting — the context assembler must gracefully express "HR not provided" vs. "HR avg 142 bpm" without confusing the coaching prompt. Validate with eval scenarios.

**Relevant research artifacts**

- `batch-3c-wearable-integrations.md` — what metric shapes to anticipate for future HealthKit/Garmin ingestion; informs canonical JSONB key choices.
- `batch-9b-unit-system-design.md` — distance/pace unit handling.
- `batch-10b-dotnet-backend-review-practices.md` — EF Core + JSONB patterns.

---

### Slice 3 — Adaptation Loop

**Requirements:** [`./slice-3-adaptation.md`](./slice-3-adaptation.md)

**Acceptance — "I can…"**

- [ ] …log a workout that deviates meaningfully from plan (e.g., distance way off, or freeform notes indicating walking/injury/external factor).
- [ ] …see the plan adjust in response, with the event stored in the Marten stream.
- [ ] …see the coach's explanation ("I adjusted your plan because…") appear in the chat panel.
- [ ] …verify via eval that adaptation handles the absorb/nudge/restructure cases correctly per DEC-012's escalation ladder (at least levels 1-3).

**Scope**

- Backend: Adaptation prompt + structured-output schema (events: `PlanAdaptedFromLog` with reason + modified workouts). Post-log hook triggers adaptation evaluation. Event appended to Marten stream; projection updated. Coach's explanation persisted as a `ConversationTurn` (new entity — see below).
- Frontend: Plan view re-renders after adaptation. Chat panel appears with the "I adjusted your plan because…" message. Panel is read-only in this slice — interactive input lands in Slice 4.
- Tests: Integration tests for the full log-triggers-adaptation path. Eval suite extended with 5-10 adaptation scenarios spanning DEC-012 levels 1-3 (absorb / nudge / restructure). Replay-mode in CI.
- `ConversationTurn` entity arrives here (not Slice 4) because adaptation explanations are the first conversational content.

**Key risks**

- Adaptation gate logic — when does a log trigger adaptation vs. no-op? Probably: always invoke the LLM adaptation prompt; let the LLM decide "no adjustment needed" and emit that as an event (or not, and skip the stream write). Decide at slice-3 plan time based on cost.
- Structured-output schema stability for plan modifications — the existing `MesoWeekOutput` restructuring lesson from DEC-042 applies; design structurally, not via `[Description]` hints.

**Relevant research artifacts**

- `batch-2b-planning-architecture.md` — event-driven recomposition, DEC-012 escalation ladder mapping, hysteresis thresholds.
- `batch-4a-coaching-conversation-design.md` — how to communicate plan changes (OARS, Elicit-Provide-Elicit, traffic-light shorthand).
- `batch-4b-special-populations-safety.md` — safety gates that must trigger before any pace/volume increase.
- `batch-2c-testing-nondeterministic.md` — adaptation evaluation patterns.
- `batch-6a-llm-eval-strategies.md` — LLM-as-judge patterns for adaptation quality.

---

### Slice 4 — Open Conversation

**Requirements:** [`./slice-4-conversation.md`](./slice-4-conversation.md)

**Acceptance — "I can…"**

- [ ] …type a question into the chat panel ("how am I doing?", "should I push harder next week?", "my knee feels tight").
- [ ] …see a streaming response grounded in my profile + plan + recent logs.
- [ ] …have the conversation persist across sessions (chat history visible on reload).
- [ ] …see the system handle the three interaction modes from `docs/planning/interaction-model.md` — onboarding (slice 1), proactive adaptation messages (slice 3), and open conversation (this slice).

**Scope**

- Backend: Conversation endpoint (streaming). Full `ConversationTurn` persistence (user turns + assistant turns). `ContextAssembler` routes by query type per the existing design. Possibly a lightweight triage prompt to classify intent.
- Frontend: Chat panel becomes interactive — text input, streaming response rendering, conversation history. Panel is always visible (right rail on desktop, bottom drawer on mobile).
- Tests: Integration tests for the conversation endpoint (streaming, context routing). Eval scenarios for a few representative open-conversation prompts. Playwright: ask question → see grounded response.

**Key risks**

- Streaming response rendering in React + RTK Query — RTK Query isn't ideal for streams; may need raw fetch + state management for the chat panel alone.
- Context-assembler routing quality — the existing design mentions interaction-specific assembly; slice 4 is when that actually gets exercised in production flow (not just eval).

**Relevant research artifacts**

- `batch-4a-coaching-conversation-design.md` — open-conversation tone, intent classification, response patterns.
- `batch-4b-special-populations-safety.md` — keyword triggers for safety escalation (injury, crisis, medical scope).
- `batch-2c-testing-nondeterministic.md` — eval patterns for open-conversation quality.

---

## Architecture Additions

### Backend module layout after this cycle

```
backend/src/RunCoach.Api/
  Program.cs
  Modules/
    Identity/                   # NEW — Slice 0
      AuthController.cs
      JwtIssuer.cs
      UserRegistrationService.cs
      Entities/                 # ApplicationUser (Identity-extended)
    Coaching/                   # EXISTING — extended in Slice 1, 3, 4
      ClaudeCoachingLlm.cs      # existing
      ContextAssembler.cs       # existing, extended in Slice 2
      OnboardingController.cs   # NEW — Slice 1
      AdaptationService.cs      # NEW — Slice 3
      ConversationController.cs # NEW — Slice 4 (read-only in Slice 3)
      Prompts/                  # existing
        coaching-v1.yaml
        onboarding-v1.yaml      # NEW — Slice 1
        adaptation-v1.yaml      # NEW — Slice 3
    Training/                   # EXISTING — extended in Slice 1, 2, 3
      Computations/             # existing (PaceZoneIndex, PaceZone, HR)
      Plan/                     # NEW — Slice 1
        PlanAggregate.cs
        Events/                 # PlanGenerated, PlanAdaptedFromLog, …
        Projections/            # current-plan document projection
      WorkoutLog/               # NEW — Slice 2
        WorkoutLog.cs
        WorkoutLogRepository.cs
        WorkoutLogController.cs
    Common/                     # existing
      BaseController.cs
  Infrastructure/
    ServiceCollectionExtensions.cs  # existing, extended
    RunCoachDbContext.cs            # NEW — Slice 0
    MartenConfiguration.cs          # NEW — Slice 0
    JwtMiddleware.cs                # NEW — Slice 0
    ErrorHandlingMiddleware.cs      # NEW — Slice 0
  Migrations/                       # NEW — Slice 0 onward (EF Core)
```

### Frontend module layout after this cycle

```
frontend/src/app/
  modules/
    auth/                       # NEW — Slice 0
      pages/{LoginPage,RegisterPage}.tsx
      store/authSlice.ts
      hooks/useAuth.ts
    onboarding/                 # NEW — Slice 1
      pages/OnboardingPage.tsx
      components/{ChatFlow,ProgressIndicator}.tsx
    plan/                       # NEW — Slice 1
      pages/HomePage.tsx
      components/{TodayCard,ThisWeek,UpcomingList}.tsx
    logging/                    # NEW — Slice 2
      components/{LogForm,MoreDetailsExpander,HistoryList}.tsx
    coaching/                   # NEW — Slice 3 (read-only) → Slice 4 (interactive)
      components/{ChatPanel,MessageList,ChatInput}.tsx
      store/chatSlice.ts
    common/                     # existing
    app/                        # existing — extended with ChatPanel layout slot
  api/                          # NEW — Slice 0 onward
    apiSlice.ts                 # RTK Query root
    auth.api.ts
    onboarding.api.ts           # Slice 1
    plan.api.ts                 # Slice 1
    workoutLog.api.ts           # Slice 2
    conversation.api.ts         # Slice 4
  pages/                        # existing (home/) — replaced/extended
```

---

## Data Model

### Relational (EF Core)

- **`ApplicationUser`** — extends `IdentityUser`. Identity-managed. No custom cols in this cycle.
- **`UserProfile`** — 1:1 with `ApplicationUser`. Onboarding answers (primary goal, target event + date, current fitness assessment, weekly schedule, injury history, preferences). `OnboardingStatus` column tracks in-progress onboarding.
- **`WorkoutLog`** — FK to `ApplicationUser`, optional FK to `PlannedWorkoutId` (matches a prescribed workout in the Marten projection). Required: `Distance`, `Duration`, `CompletionStatus`, `LoggedAt`, `Notes`. Nullable: `Metrics` (JSONB — arbitrary shape; canonical keys in a shared constants file).
- **`ConversationTurn`** — FK to `ApplicationUser`, `Role` (user/assistant/system-adaptation), `Content`, `CreatedAt`, optional FK to triggering `PlanEventId` (adaptation explanations link to the event that caused them).

### Event-sourced (Marten)

- **`Plan` aggregate** per user.
  - Events (evolve across slices):
    - `PlanGenerated` (Slice 1) — initial plan from onboarding.
    - `PlanAdaptedFromLog` (Slice 3) — plan modification triggered by a workout log. Includes reason + modified workouts.
    - `PlanRestructuredFromConversation` (Slice 4 or later) — plan modification triggered by a chat turn (goal change, injury report). Might land later.
    - `PlanRegenerated` (Slice 1+) — user-triggered regeneration for iteration/correction.
  - Projection: current plan document (macro phase schedule + this-week meso template + active-day micro prescriptions) as a single JSON document for LLM context injection.

### Why this split

- EF Core owns mutable user-state entities (profile, logs, turns) — standard CRUD, relational joins, Identity integration.
- Marten owns the plan — the coaching decisions, adaptation history, and audit trail. Event stream IS the audit trail per DEC-031 and `memory-and-architecture.md`.
- Both run against the same Postgres instance; the ownership boundary is entity-level, not database-level.

---

## Testing Strategy

- **Backend integration tests**: `WebApplicationFactory` + Testcontainers (real Postgres, not in-memory) per `backend/CLAUDE.md`. Every controller gets an integration test; the full log-triggers-adaptation path in Slice 3 is the flagship integration test.
- **Backend unit tests**: services, repositories, `ContextAssembler` extensions, computation extensions. Existing patterns hold.
- **Eval suite**: existing M.E.AI.Evaluation infrastructure. Extended in Slice 1 (onboarding scenarios — verify plan quality across profile types), Slice 3 (adaptation scenarios — absorb/nudge/restructure, freeform-notes interpretation), Slice 4 (open-conversation scenarios — coaching quality, safety). CI runs replay-only with committed fixtures per existing convention.
- **Frontend component tests**: Vitest + React Testing Library for significant components (forms, cards, chat panel).
- **Frontend E2E**: Playwright. One happy-path scenario per slice covering the "I can…" criteria end-to-end. Goal: "did this feature work end-to-end" — not exhaustive edge coverage.
- **Coverage**: maintain the existing 60% project / 70% patch Codecov thresholds. No new thresholds.

---

## Roadmapping Hygiene

This cycle introduces a three-tier roadmapping structure to stop `ROADMAP.md` from growing unboundedly and to make session-start catchup fast.

### Tier 1 — `ROADMAP.md` (front door)

Compacted to:

1. **Status block at top** — current cycle, active slice, next step, blockers, pointer to cycle plan. 10-15 lines max. Mirrored from this doc's Status section.
2. **Strategic links** — decision log, feature backlog, vision docs, forward-path items.
3. **Cycle History** — one-line-per-cycle log at the bottom. Each entry: cycle name, completion date, pointer to the cycle plan + key artifacts (PRs, specs, decisions). No narrative.

The 200-line "What's Been Done" narrative currently in `ROADMAP.md` moves out — decisions are already in `decision-log.md`, implementation details are in completed plan files under `docs/plans/`, git log holds the rest. `ROADMAP.md` is a status-first document, not a history document.

### Tier 2 — Cycle Plan (this doc)

Lives for the cycle duration at `docs/plans/{cycle-name}/cycle-plan.md`. Declares the active slice. Tracks slice acceptance criteria with checkboxes. When the cycle completes, it becomes a historical artifact (referenced from `ROADMAP.md` Cycle History).

### Tier 3 — Per-Slice Spec + Tasks

Each slice moves through a conceptual pipeline. The durable parts are the **artifact shapes and locations** and the **ordering discipline** — not the specific tools used to produce them. Pick the skills that fit the moment; the structure below is what the docs commit to.

1. **(Optional) Preliminary codebase research** — only when the slice's requirements doc + the cycle-plan Slice N section + the named research artifacts don't give enough codebase-grounded context. Output lives under `docs/specs/research-{topic}/`.
2. **Spec** — inputs: the slice's requirements doc (`./slice-N-{name}.md`), this cycle plan's Slice N section, and any relevant research artifacts. Output: `docs/specs/{NN}-spec-{topic}/spec.md` with demoable units, acceptance criteria, and proof-artifact definitions. Existing project precedent: see `docs/specs/05-spec-*` through `09-spec-*`.
3. **Task decomposition** — break the spec into dependency-aware tasks on a task board (or equivalent tracker). Independent tasks are marked for parallel execution.
4. **Execution** — tasks are implemented, tested, and committed per the project's normal conventions (see `backend/CLAUDE.md`, `frontend/CLAUDE.md`).
5. **Validation** — coverage matrix against the spec's acceptance criteria, then code review, before merge.

The `docs/specs/{NN}-spec-{topic}/` convention was adopted from the `claude-workflow` plugin's output shape — that attribution is the only reason the plugin is named here. How each step is actually executed (which skill, which tool, whether a skill at all) is a per-slice judgment call. A small slice can use a hand-written spec and skip the task board; a larger one benefits from more structure. Don't encode the tool choice in these docs.

DEC-008 plan-first rule applies — no slice implementation until the spec has been reviewed. The requirements doc at `./slice-N-{name}.md` is durable across implementation churn; the spec under `docs/specs/` is allowed to churn as implementation reveals detail.

### Per-Slice Hygiene Rule

Each slice's "done" criteria include:

- [ ] Slice acceptance checkboxes in this cycle plan marked complete.
- [ ] Cycle plan's Status section updated: active slice advanced to the next one (or "Cycle complete" if this was the last).
- [ ] `ROADMAP.md` Status block synced from this doc.
- [ ] Follow-ups discovered during the slice captured in the **Captured During Cycle** section. The slice PR description must include a `### Follow-ups found` checklist (empty is fine; omission is not).
- [ ] If the slice produced durable architecture decisions, record them in `docs/decisions/decision-log.md`.
- [ ] Completed slice spec directory under `docs/specs/` stays in place (historical reference) — not deleted, not moved.

### `/catchup` Update

`.claude/commands/catchup.md` updated to walk the new tiers:

1. `ROADMAP.md` Status block — current cycle + active slice pointers.
2. The active cycle plan (path from Status block) — slice structure and progress.
3. The active slice spec under `docs/specs/` if one exists (path from cycle plan Active Slice).
4. Last 5-10 commits on current branch.
5. Working-tree changes vs. `main` (if any).

Summarize in 3-5 sentences: current cycle, active slice, last shipped work, recommended next action.

### Tracking shape

Per-slice work is tracked on a task board (dependency-aware, atomic tasks) with the slice spec under `docs/specs/` holding the connected reasoning. GitHub Issues remain available for cross-cycle items (bugs found in main, cross-repo coordination) but are not the primary per-slice tracker. Revisit at the slice-0/slice-1 boundary if the granularity proves insufficient.

---

## When Agents Encounter Unknowns

The baseline rule lives in the project-root `CLAUDE.md` § Research Protocol and applies to every agent session, not just this cycle. **Never guess at implementation.** This section adds cycle-specific affordances.

### Prompt template

When writing a deep-research prompt, follow the existing `docs/research/prompts/batch-*.md` format:

```markdown
# R-XXX: {Topic}

## Context
{why this question came up — slice N scope, specific decision being blocked}

## Research Question
{primary question + 2-5 sub-questions that would make the answer actionable}

## Why It Matters
{what this unblocks, what happens if we get it wrong}

## Deliverables
- Concrete recommendation with rationale
- Alternatives considered and why rejected
- Library/tool version pins if applicable
- Gotchas, security implications, version compatibility notes
```

### Handoff protocol

1. Write the prompt at `docs/research/prompts/{filename}.md`.
2. Add the entry to `docs/research/research-queue.md` following the existing table format (next `R-XXX` number, Status = `Queued`, Artifact = `(pending)`).
3. Return control to the user: *"I encountered X in slice N, needs research before I proceed. Prompt at `docs/research/prompts/{file}.md`. Please run it in a separate research agent and provide the artifact."*
4. Wait for the artifact to land at `docs/research/artifacts/{file}.md`.
5. Integrate findings into the relevant planning doc, decision-log entry, or active slice spec before resuming implementation.

### Unknowns likely to surface in this cycle

Pre-flagged so agents know these are explicit research triggers, not "pick one and see." This is not a commitment to research all of them — some may be resolved by existing research once an agent reads it. The list exists so "guess and move on" is never the default for these topics.

- **Marten event-sourcing patterns for a per-user plan aggregate** — stream-per-user vs. stream-per-plan, projection update strategy (inline vs. async), snapshot frequency. `batch-2b-planning-architecture.md` has the conceptual model; implementation-time Marten conventions may need a dedicated research pass.
- **RTK Query streaming patterns for LLM responses** — RTK Query's caching model isn't a great fit for streams. Slice 4's chat panel will need either raw fetch + manual state or an alternative library; not obvious which.
- **JWT rotation / refresh-token strategy for long-lived personal-use sessions** — Slice 0 scope decision. Existing research covers CI/quality gates, not client-side token lifecycle.
- **PostgreSQL JSONB query patterns for `WorkoutLog.Metrics`** — Slice 2 stores heterogeneous metric shapes. If any downstream slice needs to query by metric value (e.g., "show me all runs where HR avg > 150"), indexing and query strategy is non-obvious — GIN vs. expression indexes, search performance at scale.
- **Onboarding conversation-state persistence** — Slice 1 multi-turn onboarding needs a state model. Whether in-progress state lives in a column, in the Marten stream, or in the client is a real architectural question.

---

## Pre-Slice-0 Housekeeping

Before Slice 0 proper begins, a small housekeeping pass:

- [ ] Compact `ROADMAP.md` to the new shape (status block + strategic links + Cycle History).
- [ ] Move historical "What's Been Done" narrative out of `ROADMAP.md` — the detail is preserved in `decision-log.md`, existing plan files, git log, and merged PRs. `ROADMAP.md` should not duplicate it.
- [ ] Update `.claude/commands/catchup.md` to the new walk order.
- [ ] Verify `/catchup` on a fresh session and confirm the summary hits "where are we?" in one scan.
- [ ] Commit as a single atomic commit: `chore(roadmap): compact ROADMAP.md and /catchup for slice-based cycle tracking`.

This is a tiny pass — probably 30-60 minutes. It lands before the Slice 0 plan file is written so the new flow is in place when Slice 0 starts.

---

## Forward Path — Carried-Forward Learnings from the Brain

This cycle is built on top of real work that's already landed. Preserved patterns:

- **Deterministic + LLM split** — every slice respects the layering. Deterministic: plan structure, compliance, pace/HR zone computation, ACWR. LLM: coaching reasoning, adaptation narrative, open conversation. Never the other way around.
- **Prompts in versioned YAML** — `onboarding-v1.yaml` and `adaptation-v1.yaml` follow the `coaching-v1.yaml` pattern. Existing `YamlPromptStore` is the vehicle.
- **Constrained decoding via `AnthropicStructuredOutputClient`** — onboarding turns, plan generation, and adaptation all use structured output. Per the DEC-042 lesson: design invariants structurally, don't rely on `[Description]` hints.
- **Eval-first for LLM changes** — adaptation prompt changes are eval-gated. CI replay-only. `EVAL_CACHE_MODE` discipline preserved.
- **`ContextAssembler` as the central context-assembly primitive** — every new prompt invocation goes through it. Extensions for workout logs + conversation history happen here.
- **Floating model aliases** per DEC-037 — `claude-sonnet-4-6` for coaching, `claude-haiku-4-5` for judging. No hard-coded model IDs in application code.
- **Trademark discipline** per `CLAUDE.md` — user-facing text uses "Daniels-Gilbert zones" / "pace-zone index." Onboarding prompt, adaptation prompt, chat responses all subject to this.

---

## References

### Planning & Decisions

- `docs/planning/vision-and-principles.md` — why this exists, design principles.
- `docs/planning/interaction-model.md` — three interaction modes.
- `docs/planning/planning-architecture.md` — macro/meso/micro tiers, event-sourced plan state.
- `docs/planning/memory-and-architecture.md` — context injection strategy, five-layer summarization.
- `docs/planning/safety-and-legal.md` — safety guardrails (pre-public-release items live here).
- `docs/decisions/decision-log.md` — all architectural decisions (DEC-001 through DEC-044).
- `docs/features/backlog.md` — feature backlog by priority tier.
- `backend/CLAUDE.md` — backend conventions, module-first organization, testing patterns.
- `frontend/CLAUDE.md` — frontend conventions.

### Cross-Cutting Research Artifacts

Apply throughout this cycle — read these before starting any slice, not just the one you're working on. Per-slice artifacts live in each slice's "Relevant research artifacts" subsection.

- `batch-1-claude-code-workflow.md` — agent workflow patterns, context management.
- `batch-7b-anthropic-model-ids-versioning.md` — floating model aliases, DEC-037.
- `batch-10a-frontend-latest-practices.md` — React 19 + TS + Vite + Tailwind conventions.
- `batch-10b-dotnet-backend-review-practices.md` — .NET 10 conventions, anti-patterns.
- `batch-10c-ci-quality-gates-private-repo.md` — CI strategy.
- `batch-6a-llm-eval-strategies.md` — eval patterns applied cycle-wide.
- `batch-7a-ichatclient-structured-output-bridge.md` — structured-output bridge pattern used by every LLM call in this cycle.

### Research Queue & Prompts

- `docs/research/research-queue.md` — full queue of topics (R-001 through R-039), status, and artifact pointers.
- `docs/research/prompts/` — deep-research prompts. Follow this format when writing new ones (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)).
- `docs/research/artifacts/` — completed research artifacts.

### Historical

- POC 1 plan: `docs/plans/poc-1-context-injection-plan-quality.md`.
- POC 1 LLM testing plan: `docs/plans/poc-1-llm-testing-architecture.md`.
