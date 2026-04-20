# Review Configuration — backend

## Skip

- "tests/eval-cache/**"

## Rules

### Async discipline

- CRITICAL: Never use .Result, .Wait(), or .GetAwaiter().GetResult() on async
  calls. These cause thread pool starvation and deadlocks in ASP.NET Core.
  The only acceptable location is Program.cs bootstrap code.
- CRITICAL: async void is forbidden except for event handlers. It crashes the
  process on unhandled exceptions and prevents callers from observing
  completion or errors.
- Every async method must accept and forward CancellationToken. Controllers
  receive tokens from HttpContext.RequestAborted. Use
  CancellationTokenSource.CreateLinkedTokenSource to add timeouts for LLM
  calls and external services.
- Flag fire-and-forget patterns (_ = DoWorkAsync()) that discard exceptions.
  Background work must flow through Wolverine or IHostedService with error
  logging, not discarded tasks.
- Flag sequential awaits for independent operations. Use Task.WhenAll when
  operations have no data dependency on each other.

### EF Core

- CRITICAL: Flag navigation properties accessed in loops without .Include().
  Every collection navigation accessed after materialization triggers a
  separate query (N+1). Use .Include() for eager loading or .Select() to
  project only needed columns.
- Read-only queries must use .AsNoTracking() to skip change tracking overhead.
- Flag load-modify-save patterns for bulk operations. Use
  ExecuteUpdateAsync/ExecuteDeleteAsync for single-statement bulk operations
  instead of loading all entities into memory.
- CRITICAL: Never use FromSqlRaw with string interpolation — SQL injection.
  Use FromSql (EF 10) or FromSqlInterpolated which auto-parameterize.
- Never bind directly to EF entities in controllers. Always use DTOs for model
  binding to prevent mass assignment vulnerabilities.

### Dependency injection

- CRITICAL: A singleton must NEVER inject a scoped service directly (captive
  dependency). This captures a single scoped instance for the app's lifetime,
  causing data corruption under concurrent load. Use IServiceScopeFactory.
- DbContext must be Scoped (the AddDbContext default). Background services and
  Wolverine handlers must use IDbContextFactory<T>, never directly injected
  DbContext.
- Constructors with more than 7 dependencies signal a Single Responsibility
  violation. Refactor into aggregate services or split the class.

### Marten event sourcing

- CRITICAL: Never modify an existing event record's properties. Add a new
  event type for schema changes and register upcasters for old events. Events
  are immutable contracts.
- CRITICAL: Renaming a CLR event type without `opts.Events.MapEventType<NewName>("old_alias")`
  silently returns zero rows on replay — no exception, just mystery empty
  streams. Every event-type rename must register the old type name as an
  alias in the same change.
- Use inline projections for aggregate state (transactional consistency). Use
  async projections for read models (eventual consistency). Flag complex read
  model projections running inline — they slow every append.
- Flag CRUD entities forced into event sourcing. Simple create/update/delete
  data (user profiles, settings) should use EF Core relational tables, not
  event streams.

### HTTP verbs and state changes

- CRITICAL: Never use GET (or any safe-by-spec verb) for endpoints that mutate
  state. SameSite=Lax cookies are sent on top-level GET navigations; using GET
  for a mutation re-opens the CSRF attack surface that the antiforgery
  middleware is designed to close. State-changing operations are POST / PUT /
  PATCH / DELETE only.
- State-changing endpoints must require antiforgery validation via
  `[RequireAntiforgeryToken]` (NOT `[ValidateAntiForgeryToken]`, which does
  not integrate with the .NET 10 `UseAntiforgery()` middleware) or by
  explicitly calling `IAntiforgery.ValidateRequestAsync`. Endpoints that bind
  `IFormFile` auto-enforce; JSON body endpoints must opt in.

### Secrets and DataProtection

- CRITICAL: Never commit plaintext secrets. `.env` files are not used in this
  repo — shared/CI/prod secrets go in SOPS-encrypted YAML (`secrets/<env>.enc.yaml`)
  with the age key stored as `secrets.SOPS_AGE_KEY` in GitHub. Local dev uses
  `dotnet user-secrets`.
- CRITICAL: ASP.NET Core Data Protection keys MUST persist via
  `PersistKeysToDbContext<DpKeysContext>` against the project's Postgres —
  NOT `PersistKeysToFileSystem`. File-system persistence is single-instance
  only and silently invalidates every cookie on container rebuild.
- CRITICAL: GitHub Actions workflows must use `pull_request` (NOT
  `pull_request_target` combined with a checkout of `head.sha` — GitHub
  Security Lab's "pwn request" anti-pattern). Sensitive keys live in a GitHub
  Environment with required-reviewer protection. Pin every third-party action
  by 40-character commit SHA (tj-actions/changed-files CVE-2025-30066
  precedent — every version tag was retagged to a malicious commit).

### Wolverine and shared NpgsqlDataSource

- CRITICAL: `PersistMessagesWithPostgresql` MUST take an `NpgsqlDataSource`
  overload, not a connection-string overload (`wolverine#691`). Without this,
  Postgres password rotation manifests as `28P01` errors until the API is
  restarted. Pair with `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider`
  on the data-source registration.
- The same shared `NpgsqlDataSource` (registered via
  `builder.AddNpgsqlDataSource("runcoach")`) is consumed by EF Core, Marten
  (`UseNpgsqlDataSource()`), Wolverine outbox, and DataProtection. Flag any
  registration that bypasses the shared data source by calling `Connection(...)`
  with a raw connection string.

### Marten event-stream identity

- For aggregates that are 1:1 with the user (e.g., onboarding), derive the
  Marten stream id deterministically:
  `DeterministicGuid(userId, "onboarding")` via UUID-v5 shape (SHA-1 of
  `userId + ":" + streamPurpose` truncated to 16 bytes). This makes
  `StartStream<T>(deterministicId, ...)` naturally idempotent — retries hit
  a primary-key violation and are handled as "already started."
- For aggregates that are 1:many per user (e.g., Plan), use
  `CombGuidIdGeneration.NewGuid()` per instance and store the current id on
  the EF projection row.
- Onboarding events live in a SEPARATE stream from Plan events. Never
  commingle event types on a single per-user stream — `SingleStreamProjection<TDoc, TId>`
  assumes one stream per aggregate instance.

### Anthropic prompt caching and content-block serialization

- For any LLM call that uses Anthropic prompt caching, the request `messages[]`
  MUST reconstruct byte-identically turn after turn — caching is a pure
  prefix-hash mechanism. Reconstruct messages by replaying events from the
  onboarding (or conversation) stream, not by serializing a mutable snapshot.
- CRITICAL: Serialize Anthropic typed content blocks
  (`UserTurnRecorded.ContentBlocks`, `AssistantTurnRecorded.ContentBlocks`)
  via `System.Text.Json` with declared property-order records — NOT
  `Dictionary<string, object>`. Dictionary-based serialization can produce
  non-deterministic key ordering (per language: Swift/Go failures documented),
  invalidating the cache prefix. Records with a fixed property order are safe.
- Store `tool_use`, `tool_result`, `thinking`, and `redacted_thinking` blocks
  including their `signature` fields verbatim in the event payload — Anthropic
  requires verbatim echo when these features are used. Cheap insurance even
  if Slice 1 doesn't use them.
- Enable automatic prompt caching from day one with
  `cache_control: { type: "ephemeral", ttl: "1h" }` at the top of the request
  body. Add a second explicit breakpoint on the system prompt for longer-lived
  independent caching.

### Trademark: VDOT on prompt content

- CRITICAL: Flag any introduction of the literal string "VDOT" inside
  `src/RunCoach.Api/Prompts/*.yaml`. These files are LLM prompt content and
  any "VDOT" token flows directly into user-facing coaching output. Use
  "Daniels-Gilbert zones" or "pace-zone index" instead. The VDOT mark is
  enforced by The Run SMART Project LLC (Runalyze precedent).
- API response DTOs, `ProblemDetails` error messages, and any string that
  may reach a frontend or an HTTP consumer are treated as user-facing and
  must avoid "VDOT". Internal C# identifiers are exempt from this rule.

### LLM integration

- CRITICAL: No hardcoded prompts in C# code. All prompts must be loaded from
  YamlPromptStore. Flag string literals containing LLM instructions and raw
  string interpolation for prompt construction.
- ICoachingLlm is the ONLY LLM entry point. Domain services must not reference
  Microsoft.Extensions.AI or IChatClient directly.
- JSON schemas for structured output must include additionalProperties: false
  and all properties in the required array. This is mandatory for Anthropic's
  constrained decoding. Note: constrained decoding does not enforce numerical
  min/max — add post-deserialization validation.
- Always check stop_reason on LLM API responses. "max_tokens" means the
  response was truncated. Log token usage (input_tokens, output_tokens) from
  every response for cost tracking.

### Performance

- Flag string comparisons without explicit StringComparison parameter
  (.Equals, .Contains, .StartsWith, .EndsWith). Always specify
  StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase.
- Flag .ToLower()/.ToUpper() used for comparison — allocates a new string.
  Use OrdinalIgnoreCase instead.
- Flag new Regex() inside methods or loops. Use [GeneratedRegex] for
  AOT-friendly, build-time compiled patterns.
- Flag new HttpClient() per call — exhausts socket handles. Use
  IHttpClientFactory exclusively.

### Testing (xUnit v3)

- CRITICAL: All async test operations must receive
  TestContext.Current.CancellationToken. xUnit v3 uses this for timeout
  cancellation.
- CRITICAL: Always await async FluentAssertions. Un-awaited .ThrowAsync
  silently passes even when it should fail.
- Tests follow MethodName_Scenario_Expected naming convention. Use [Theory]
  with [InlineData] for parameterized value combinations, separate [Fact]
  tests when scenarios need different setup/assertion logic.
- Eval tests must use DiskBasedReportingConfiguration with
  ResponseCachingChatClient. In CI, tests FAIL on cache miss — no live API
  calls. This prevents unexpected costs and non-deterministic CI results.

### Tool authority partitioning (DEC-043)

- When reviewing CI changes, check the one-authority-per-signal mapping:
  CodeQL = first-party SAST, Codecov = coverage via Cobertura, SonarQube
  Cloud = dashboard via OpenCover, dependency-review-action = license + CVE
  gate. Reject any PR that adds a second tool owning the same signal.
- Backend-specific: SonarQube Cloud ingests OpenCover only (no Cobertura
  property exists for C#). Codecov ingests Cobertura. Do not merge them.

### Snyk/Codacy proposal gate (DEC-043)

- Reject any proposal to add Snyk or Codacy unless at least one of the
  explicit reconsider-triggers in ROADMAP § Deferred Items has fired.
  See DEC-043 in docs/decisions/decision-log.md.

## Ignore

# 2026-03-25: EF Core migrations are generated, naming conventions don't apply

- conventions:"file naming" for migration files

# 2026-03-25: Test helpers intentionally use nullable without guards

- types:"nullable reference" for test assertion helpers

# 2026-03-25: Eval cache fixtures are committed binary data

- conventions:"file length" for eval cache JSON fixtures
