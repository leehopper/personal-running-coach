# Research Prompt: Batch 10b — R-022

# .NET 10 / C# 14 Backend Code Review Best Practices (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: Establish comprehensive code review rules for a .NET 10 / C# 14 backend application

Context: I'm building the backend for an AI-powered running coach (RunCoach). The backend is a .NET 10 / C# 14 ASP.NET Core API with the following architecture:

- **API layer:** ASP.NET Core controllers (URL-versioned REST, `/api/v1/`), Swashbuckle OpenAPI
- **Module organization:** `Modules/{Domain}/` — currently Coaching (LLM adapter, context assembly, prompt storage), Training (deterministic computations), Common (BaseController)
- **Data layer (planned):** EF Core + Marten event sourcing on PostgreSQL. EF Core for relational tables, Marten for event streams/projections. Not yet implemented but architecturally committed.
- **Background processing (planned):** Wolverine
- **Auth (planned):** ASP.NET Core Identity + JWT
- **LLM integration:** Anthropic SDK (`Anthropic` v12.9.0), `ICoachingLlm` adapter interface with `GenerateAsync` and `GenerateStructuredAsync<T>`, YAML prompt storage (`YamlPromptStore`), constrained decoding for structured output via custom `AnthropicStructuredOutputClient` (DelegatingChatClient bridging M.E.AI's `ForJsonSchema()` to native Anthropic schemas)
- **Testing:** xUnit v3 (3.2.2) with MTP runner, FluentAssertions 8.9.0, NSubstitute 5.3.0, M.E.AI.Evaluation (10.4.0) for LLM eval tests with disk-based caching and replay
- **Build:** .NET 10 SDK, Central Package Management, `Directory.Build.props` with `TreatWarningsAsErrors`, StyleCop.Analyzers, SonarAnalyzer.CSharp
- **Quality:** Lefthook pre-commit, Trivy CI scanning, Codecov coverage

Current package versions (from Directory.Packages.props):

- Swashbuckle.AspNetCore: 10.1.5
- Anthropic: 12.9.0
- YamlDotNet: 16.3.0
- Microsoft.Extensions.AI.Evaluation: 10.4.0
- xunit.v3: 3.2.2
- FluentAssertions: 8.9.0
- NSubstitute: 5.3.0
- coverlet.msbuild: 8.0.1
- StyleCop.Analyzers: 1.2.0-beta.556
- SonarAnalyzer.CSharp: 10.21.0.135717

Established conventions (from backend/CLAUDE.md):

- Primary constructors, `_` prefixed private fields, one type per file
- Record types for DTOs with `Dto` suffix
- Sealed classes by default, immutable records, `ImmutableArray`/frozen collections
- Async throughout, `CancellationToken` propagation
- Structured logging with `ILogger<T>` and named placeholders
- `TestContext.Current.CancellationToken` and `SendDiagnosticMessage()` in tests
- Arrange/Act/Assert with comment markers, `expected`/`actual` prefixes

Architecture principle: **Deterministic computation layer + LLM coaching layer** — never use LLMs for structured data tasks (pace calculations, zone math). LLMs handle coaching conversation, plan narrative, and adaptation reasoning.

What I need to learn:

### 1. C# 14 Language Features for Review

- What are the new C# 14 features in .NET 10? (field keyword, extension types, null-conditional assignment, etc.)
- Which C# 14 features should reviewers actively look for as improvements to older patterns?
- Anti-patterns: where do C# 14 features get misused or overused?
- What should a reviewer flag as "should be using a newer C# construct"?

### 2. ASP.NET Core (.NET 10) Review Rules

- Latest middleware patterns, minimal API vs controllers tradeoffs
- Dependency injection best practices: when to use `AddScoped` vs `AddSingleton` vs `AddTransient`, common DI mistakes
- Configuration patterns: `IOptions<T>` vs `IOptionsSnapshot<T>` vs `IOptionsMonitor<T>` — when to use which
- Error handling: global exception handlers, ProblemDetails, RFC 9457
- Model validation: FluentValidation vs data annotations vs manual — what's the .NET 10 recommendation?
- Health checks: beyond basic `/health` — readiness vs liveness, dependency health

### 3. EF Core (.NET 10) Review Rules

- N+1 detection: what patterns should a reviewer flag?
- `AsNoTracking()` enforcement for read-only queries
- `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for bulk operations (not fetch-then-save)
- Compiled queries: when to use `EF.CompileAsyncQuery`
- Query filter gotchas, owned entity pitfalls, value converter traps
- Migration safety: what changes are dangerous in production (column drops, type changes, non-nullable additions)
- Connection management: connection pooling, `IDbContextFactory<T>` for background services
- JSON column support in PostgreSQL — when to use vs separate tables

### 4. Marten Event Sourcing Review Rules

- Event naming and versioning — how to handle schema evolution
- Projection patterns: inline vs async, when each is appropriate
- Stream naming conventions and aggregate design
- Anti-patterns: fat events, event sourcing for CRUD data, mixing event store with relational queries incorrectly
- Marten + EF Core coexistence — boundaries and integration patterns

### 5. Async / Concurrency Review Rules

- `ConfigureAwait(false)` — when to use (library code) vs when not to (app code)
- `ValueTask` vs `Task` — when `ValueTask` is appropriate (hot paths with frequent sync completion)
- `CancellationToken` propagation — every async method should accept and pass tokens
- Common async pitfalls: async void, fire-and-forget without error handling, sync-over-async, async-over-sync
- Thread safety in DI singletons, proper use of `ConcurrentDictionary`, `Lazy<T>`, `SemaphoreSlim`
- `IAsyncDisposable` patterns

### 6. Performance Anti-Patterns for Review

Cross-reference with Microsoft's dotnet-diag skills (~50 anti-patterns). Key categories:

- **String handling:** missing `StringComparison`, chained `.Replace()`, `ToLower()`/`ToUpper()` for comparison, string concat in loops
- **Collections:** wrong collection type for access pattern, `List<T>` where `ImmutableArray<T>` or `FrozenDictionary` is appropriate, LINQ in hot paths
- **Memory:** excessive allocations, `params` arrays, boxing value types, large struct copies
- **Regex:** `new Regex()` in loops (should be static/`[GeneratedRegex]`), missing compilation
- **Serialization:** `System.Text.Json` source generation for AOT, proper `JsonSerializerOptions` caching
- **HTTP:** `HttpClient` per-call (should use `IHttpClientFactory`), missing timeout configuration

### 7. Testing Review Rules (xUnit v3 + FluentAssertions + NSubstitute)

- xUnit v3 specifics: `TestContext.Current` usage, MTP runner gotchas, trait-based filtering
- FluentAssertions: proper assertion chaining, `Should().BeEquivalentTo()` configuration, async assertion patterns
- NSubstitute: `Received()` vs `DidNotReceive()` patterns, argument matchers, configuring returns for async methods
- Test organization: when `[Theory]` + `[InlineData]` vs separate `[Fact]` tests
- Test naming: `MethodName_Scenario_Expected` convention enforcement
- Anti-patterns: testing implementation details, excessive mocking, brittle string assertions, missing edge cases
- Eval test specifics: `EVAL_CACHE_MODE` enforcement, fixture staleness detection, structured output validation

### 8. LLM Integration Review Rules

- Anthropic SDK usage patterns: proper error handling, retry with backoff, token budget tracking
- `IChatClient` / `DelegatingChatClient` patterns in M.E.AI
- Structured output: schema generation via `JsonSchemaHelper`, `additionalProperties: false` enforcement
- Prompt management: YAML prompt store patterns, template rendering, version selection
- Context assembly: token budget enforcement, section prioritization, overflow cascade
- Anti-patterns: hardcoded prompts in code, raw string prompts, missing structured output validation, LLM calls without timeout/cancellation

### 9. Security Review Rules

- Input validation: where to validate (API boundary), what to validate (user input, not internal data)
- Authentication patterns with ASP.NET Core Identity + JWT
- Authorization: policy-based vs role-based, resource-based authorization
- OWASP top 10 for .NET: SQL injection (parameterized queries), XSS (output encoding), CSRF (anti-forgery tokens), mass assignment (DTOs not entities)
- Secrets management: user-secrets for dev, environment variables for production, never in `appsettings.json`
- API security: rate limiting, CORS configuration, HTTPS enforcement

### 10. Build and Project File Review Rules

Cross-reference with Microsoft's dotnet-msbuild skills. Key areas:

- `Directory.Build.props` organization: what belongs at repo level vs project level
- Central Package Management: version conflict detection, `VersionOverride` usage
- Analyzer configuration: when to suppress vs fix warnings
- MSBuild anti-patterns: hardcoded paths, restated defaults, missing `Inputs`/`Outputs` on custom targets

Output I need:

- For each area above: 5-10 specific, actionable code review rules suitable for REVIEW.md (natural language, AI-reviewable)
- Severity classification for each rule (critical / high / medium / low)
- Anti-patterns with concrete "flag this" → "suggest this instead" examples
- Any new .NET 10 features/patterns that supersede what we're currently doing
- Cross-references to Microsoft documentation and dotnet-diag/dotnet-data/dotnet-msbuild skill rules where applicable
