# Review Configuration

## Model Tier

frontier

## Max Findings

0

## Skip

- "**/bin/**"
- "**/obj/**"
- "**/dist/**"
- "**/node_modules/**"
- "**/Migrations/*.Designer.cs"
- "**/*.g.cs"
- "**/Generated/**"
- "package-lock.json"
- "**/*.suo"
- "**/*.user"
- "**/.vs/**"
- "**/TestResults/**"
- "docs/research/artifacts/**"

## Rules

### Security

- CRITICAL: Never commit secrets, API keys, connection strings, or tokens in
  source files. This includes appsettings.json values. Use dotnet user-secrets
  locally and environment variables in production. If secrets appear in a diff,
  stop the review and flag immediately.
- CRITICAL: All user-supplied input must be validated at the API boundary
  (server-side). Never trust client-side validation alone — it is the security
  boundary for the application.
- All API endpoints must enforce authentication and authorization. Missing auth
  on a single endpoint exposes the entire resource. Prefer controller-level
  [Authorize] with [AllowAnonymous] exceptions over per-action attributes.
- Never expose stack traces, internal exception details, or infrastructure
  names in error responses. Use ProblemDetails (RFC 9457) with traceId for
  correlation.

### Architecture

- Dependencies flow inward: Controllers -> Services -> Domain. Domain must
  never reference infrastructure or API concerns. The LLM coaching layer
  consumes deterministic computation results — it never performs calculations.
- CRITICAL: Never use LLMs for deterministic tasks — pace calculations, zone
  math, distance conversions, ACWR, weekly volume aggregation. These belong
  in the computation layer with unit tests. LLMs handle coaching conversation,
  plan narrative, and adaptation reasoning only.
- Changes to shared API contracts (request/response DTOs) require both backend
  and frontend review. Flag any PR that modifies contract types without
  corresponding consumer updates.
- Module boundaries must be respected. Cross-module imports should go through
  public interfaces, not reach into another module's internal types.

### Error handling

- All public API endpoints must return structured ProblemDetails error
  responses with consistent error codes and traceId. Never return bare
  string error messages.
- Log errors with sufficient context for debugging: correlation ID, operation
  name, relevant entity IDs. Never log sensitive data (secrets, tokens, PII,
  connection strings).

### Git and CI

- Commit messages follow Conventional Commits format
  (feat|fix|docs|refactor|test|chore: description).
- PRs should address a single concern. Mixed refactor + feature + style
  changes reduce review quality and should be split.
- All GitHub Actions must be SHA-pinned with version comments. Tag-based
  references are vulnerable to supply chain attacks (ref: trivy-action
  March 2026 compromise).

### Code comments

- Productionize comments before merge: no planning-phase forward
  references ("Slice 1 only", "later slices will", "Unit 5 will"), no
  TODO/FIXME, no embedded source-file paths (`backend/src/...`,
  `frontend/src/...`), no chain-of-thought design narrative. Spec / DEC /
  R-NNN symbolic references are allowed. Keep load-bearing "why"
  comments that document non-obvious invariants.
- Comments must be self-contained. Do not reference external doc paths
  ("see `docs/planning/...`", "per `docs/research/artifacts/...`") or URLs
  that aren't load-bearing test fixtures. The reader should not need to
  open another file to understand the comment.
- Wrap identifiers and code-shaped tokens in backticks
  (`SameSite=Lax`, `SecurePolicy.Always`, `Func<T, U>`,
  `services.AddScoped<IFoo, Foo>()`). Bare identifiers in prose comments
  trip SonarAnalyzer S125 commented-out-code detection and fail
  `TreatWarningsAsErrors`.

### AI-generated code

- Verify all referenced packages actually exist on npm/NuGet registries.
  AI models hallucinate package names at a 5-20% rate, creating supply
  chain attack vectors.
- Check for unnecessary abstraction layers and over-engineering. AI tends to
  create helpers, utilities, and wrappers for one-time operations.
- Confirm error handling covers edge cases — AI-generated code is
  "confidently incomplete" with clean happy paths but missing failure modes.

### Trademark: VDOT

- CRITICAL: Flag any introduction of the string "VDOT" on user-facing surface.
  This covers coaching prompt YAML files under `backend/src/RunCoach.Api/Prompts/`,
  README, ROADMAP, active `docs/planning/` docs, UI strings, API response
  payloads, generated plan narrative, error messages, and commit messages.
  The VDOT mark is enforced by The Run SMART Project LLC (Runalyze
  precedent). Use "Daniels-Gilbert zones" or "pace-zone index" instead.
- All internal code identifiers were renamed to trademark-neutral names in
  DEC-042 and spec 11 (`PaceZoneIndexCalculator`, `PaceZoneCalculator`,
  `FitnessEstimate.EstimatedPaceZoneIndex`). The only remaining in-repo
  occurrences of the literal term are carve-out-exempt: rule-enforcing
  VDOT-absence test guards (e.g. `ContextAssemblerTests.cs`,
  `OnboardingPromptTests.cs`) and one math comment in
  `DanielsGilbertEquationsTests.cs`. Do not flag those.
- Historical research artifacts under `docs/research/artifacts/` and
  historical DEC entries in `docs/decisions/decision-log.md` are also exempt
  — they are append-only records preserved as-is with a top-of-file rename
  note per DEC-043.

### Tool authority partitioning (DEC-043)

- When reviewing CI changes, check the one-authority-per-signal mapping:
  CodeQL = first-party SAST, Codecov = coverage via Cobertura, SonarQube
  Cloud = dashboard via OpenCover, dependency-review-action = license + CVE
  gate. Reject any PR that adds a second tool owning the same signal.

### Snyk/Codacy proposal gate (DEC-043)

- Reject any proposal to add Snyk or Codacy unless at least one of the
  explicit reconsider-triggers in ROADMAP § Deferred Items has fired.
  See DEC-043 in docs/decisions/decision-log.md.

### Test infrastructure (DEC-064)

- `backend/tests/RunCoach.Api.Tests` runs all xunit collections
  **sequentially**. Reject any change that flips
  `parallelizeTestCollections` to `true` in
  `backend/tests/RunCoach.Api.Tests/xunit.runner.json`, removes
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
  from `Infrastructure/AssemblyInfo.cs`, or otherwise re-enables
  collection-level parallelism without first superseding DEC-064 in the
  decision log. The two enforcement mechanisms are deliberately
  redundant; both must stay.
- Reject any change that removes `global.json`'s
  `test.runner: Microsoft.Testing.Platform`, re-introduces
  `<TestingPlatformDotnetTestSupport>` (silently ignored on .NET 10+
  SDK), or downgrades any of the MTP-family pins
  (`Microsoft.Testing.Platform.MSBuild`,
  `Microsoft.Testing.Extensions.Telemetry`,
  `Microsoft.Testing.Extensions.TrxReport`,
  `Microsoft.Testing.Extensions.TrxReport.Abstractions`,
  `Microsoft.Testing.Platform`) below the 2.x line shared with
  `coverlet.MTP` and `xunit.v3.core.mtp-v2`. Mismatched majors throw
  `TypeLoadException` for `IDataConsumer` /
  `IOutputDevice.DisplayAsync` at test-host startup. Reject swapping
  `coverlet.MTP` back to `coverlet.msbuild` or `coverlet.collector`
  (both are VSTest-bridge-only and silently produce no coverage under
  MTP-native).
- Reject any patch that removes the
  `services.PostConfigure<HostOptions>(opts => opts.ShutdownTimeout = ...)`
  block in `RunCoachAppFactory.ConfigureWebHost`. The 30s framework
  default cancels Wolverine's
  `MessageStoreCollection.ReleaseAllOwnershipAsync` mid-flight on
  full-suite runs and surfaces as `[Test Assembly Cleanup Failure]` for
  every test in the assembly.
- Reject `dotnet test --filter "Category!=…"` /
  `dotnet test --filter "Trait=…"` patterns in scripts, lefthook hooks,
  or CI workflows. Under Microsoft.Testing.Platform these emit
  `MTP0001: VSTest-specific properties are set but will be ignored`
  and silently no-op. Use xUnit v3's `--filter-not-trait "name=value"`
  or partition via `[Collection]` and run `-class` / `-trait` filters
  against the test executable directly.
- Any new production-registered `IHostedService` that touches
  time-sensitive state (clocks, timers, sweepers) must be removed via
  `services.Remove(...)` inside `RunCoachAppFactory.ConfigureWebHost`
  so the test host doesn't race fake-time tests. Precedent:
  `IdempotencySweeper`.
- Reject re-introduction of `WithReuse(true)` (or `WithReuse(!IsCi)`)
  on the `PostgreSqlBuilder` in `RunCoachAppFactory`. The reuse path is
  unstable on macOS Colima: when a test process exits abnormally
  (Ctrl+C, kill, IDE crash) the container is left in `Exited` state
  with the reuse-id label still attached, and the next `dotnet test`
  hangs in `RunCoachAppFactory.InitializeAsync` trying to coordinate
  with the dead port (visible as `Monitor_Wait` on the main thread).
  Ryuk-managed cleanup (the default once `WithReuse` is off) reaps the
  container reliably on every test-process exit. Trade-off: ~5s of
  cold-start per run; the daily-driver workaround for tight iteration
  stays `dotnet test --filter-not-trait "Category=Integration"` (977
  tests in ~3s).

## Ignore

# Pre-populated for known framework patterns

- conventions:"commit message format" for merge commits
- conventions:"file naming" for EF Core migration files
