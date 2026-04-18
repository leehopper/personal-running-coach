# Review Configuration

## Severity Threshold

low

## Confidence Threshold

80

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
  occurrences of the literal term are carve-out-exempt: rule-enforcing test
  guard literals (`ContextAssemblerTests.cs` VDOT-absence assertions) and
  one math comment in `DanielsGilbertEquationsTests.cs`. Do not flag those.
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

## Ignore

# Pre-populated for known framework patterns

- conventions:"commit message format" for merge commits
- conventions:"file naming" for EF Core migration files
