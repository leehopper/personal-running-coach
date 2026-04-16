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
- Use inline projections for aggregate state (transactional consistency). Use
  async projections for read models (eventual consistency). Flag complex read
  model projections running inline — they slow every append.
- Flag CRUD entities forced into event sourcing. Simple create/update/delete
  data (user profiles, settings) should use EF Core relational tables, not
  event streams.

### Trademark: VDOT on prompt content

- CRITICAL: Flag any introduction of the literal string "VDOT" inside
  `src/RunCoach.Api/Prompts/*.yaml`. These files are LLM prompt content and
  any "VDOT" token flows directly into user-facing coaching output. Use
  "Daniels-Gilbert zones" or "pace-zone index" instead. The VDOT mark is
  enforced by The Run SMART Project LLC (Runalyze precedent).
- Internal C# identifiers (`VdotCalculator`, `IVdotCalculator`,
  `EstimatedVdot`, test class names, variable names) are explicitly exempt
  until DEC-042's pace-calculator rewrite lands. Do not flag those in code
  files.
- API response DTOs, `ProblemDetails` error messages, and any string that
  may reach a frontend or an HTTP consumer are treated as user-facing and
  must avoid "VDOT".

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

## Ignore

# 2026-03-25: EF Core migrations are generated, naming conventions don't apply

- conventions:"file naming" for migration files

# 2026-03-25: Test helpers intentionally use nullable without guards

- types:"nullable reference" for test assertion helpers

# 2026-03-25: Eval cache fixtures are committed binary data

- conventions:"file length" for eval cache JSON fixtures
