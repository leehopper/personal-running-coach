# Comprehensive Code Review Rules for RunCoach (.NET 10 / C# 14)

**This document establishes 150+ actionable review rules across 10 areas for the RunCoach backend.** Every rule carries a severity classification (🔴 Critical, 🟠 High, 🟡 Medium, 🔵 Low), a concrete "flag → suggest" pattern, and exceptions where the rule should not apply. Rules are designed for both human and AI-assisted code review. .NET 10 shipped as an LTS release in November 2025 with C# 14; this document reflects that baseline.

---

## 1. C# 14 language features reviewers must enforce

C# 14 introduces nine features. Reviewers should actively look for opportunities to modernize code and flag older patterns that now have cleaner replacements. The most impactful features for RunCoach are the **`field` keyword**, **extension members**, **null-conditional assignment**, and **implicit span conversions**.

### The `field` keyword eliminates boilerplate backing fields

The contextual keyword `field` inside a property accessor references a compiler-synthesized backing field. This replaces explicit `private` backing fields when only one accessor has custom logic.

**🟡 RULE C14-01**: Replace explicit backing fields with `field` keyword when only one accessor needs custom logic.

```csharp
// ❌ Flag this
private string _name;
public string Name
{
    get => _name;
    set => _name = value ?? throw new ArgumentNullException(nameof(value));
}

// ✅ Suggest this
public string Name
{
    get;
    set => field = value ?? throw new ArgumentNullException(nameof(value));
}
```

**Exceptions**: When the backing field is accessed elsewhere in the class, when reflection-based frameworks (EF Core field access, AutoMapper) depend on the field name, or when the type already has a member named `field`. Note that property initializers bypass setters and write directly to the backing field.

**🟡 RULE C14-02**: Flag types with a member named `field` that also have auto-properties — naming conflict risk (CS9258 warning).

### Extension members supersede `GetXxx()` extension methods

C# 14 extends the extension concept to support **extension properties, operators, and static members** via `extension` blocks. Extension properties should be preferred over `GetXxx()` extension methods for characteristics/state queries.

**🔵 RULE C14-03**: In new code, prefer extension properties over `GetXxx()` extension methods for O(1) characteristics.

```csharp
// ❌ Flag this (new code)
public static int GetWordCount(this string s) => s.Split(' ').Length;

// ✅ Suggest this
extension(string s)
{
    public int WordCount => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}
```

**🟠 RULE C14-04**: Do not hide expensive O(n) operations behind extension properties. Extension properties must be O(1). Keep expensive computations as methods.

### Null-conditional assignment reduces guard clauses

The `?.` operator now works on the left side of assignments. The right-hand expression is skipped entirely when the receiver is null.

**🔵 RULE C14-05**: Replace `if (x != null) { x.Prop = val; }` with `x?.Prop = val;` for single-statement null-guarded assignments.

```csharp
// ❌ Flag this
if (customer is not null) { customer.Order = GetCurrentOrder(); }

// ✅ Suggest this
customer?.Order = GetCurrentOrder();
```

**Exceptions**: When the `if` block contains multiple statements, when the RHS must always execute for its side effects, or when deep chaining (`a?.B?.C?.D = val`) obscures which null check is failing.

### Implicit span conversions remove ceremony

C# 14 adds implicit conversions from `T[]` → `Span<T>`, `T[]` → `ReadOnlySpan<T>`, and `string` → `ReadOnlySpan<char>`. These participate in overload resolution.

**🔵 RULE C14-06**: Remove explicit `.AsSpan()` calls when passing arrays/strings to methods accepting `Span<T>` or `ReadOnlySpan<T>`.

**🟡 RULE C14-07**: Consolidate redundant array + span overloads into a single `ReadOnlySpan<T>` overload where callers pass arrays directly via implicit conversion.

### Additional C# 14 features

**🔵 RULE C14-08**: Use `nameof(List<>)` instead of `nameof(List<int>)` when the type argument is irrelevant to the context (unbound generic `nameof`).

**🔵 RULE C14-09**: Remove redundant type annotations from lambda parameters when modifiers (`out`, `ref`, `in`) are the only reason for explicit types. C# 14 allows `(text, out result) => ...` instead of `(string text, out int result) => ...`.

**🟡 RULE C14-10**: For performance-critical types with operator overloads used in tight loops, consider user-defined compound assignment operators (`void operator +=`) to avoid unnecessary allocations.

**🟠 RULE C14-11**: File-based `.cs` apps (`#:sdk` directives) must not appear in production service directories. They are for scripts, prototyping, and CLI utilities only.

---

## 2. ASP.NET Core (.NET 10) patterns and pitfalls

### Middleware ordering is a correctness requirement, not a preference

Wrong middleware order silently breaks authentication, CORS, and error handling. The canonical order for RunCoach:

```csharp
app.UseExceptionHandler();          // 1. Must be first
app.UseHttpsRedirection();          // 2. HTTPS
app.UseRouting();                   // 3. Routing
app.UseCors();                      // 4. After routing, before auth
app.UseRateLimiter();               // 5. After routing
app.UseAuthentication();            // 6. Auth
app.UseAuthorization();             // 7. Authz
app.MapControllers();               // 8. Endpoints
```

**🔴 RULE ASP-01**: `UseExceptionHandler()` must be the first middleware in the pipeline. **🔴 RULE ASP-02**: `UseAuthentication()` must precede `UseAuthorization()`. **🟠 RULE ASP-03**: `UseCors()` must come after `UseRouting()` but before auth middleware. **🟠 RULE ASP-04**: Never use `.Result` or `.Wait()` inside middleware — all middleware must be fully async.

### Dependency injection mistakes cause silent runtime failures

**🔴 RULE DI-01**: **Captive dependency** — a singleton must NEVER inject a scoped service directly. This captures a single scoped instance for the app's lifetime. Use `IServiceScopeFactory` to create scopes within the singleton. Enable `validateScopes: true` in Development.

**🔴 RULE DI-02**: `DbContext` must be registered as Scoped (the `AddDbContext<T>()` default). Never register as singleton.

**🔴 RULE DI-03**: Background services (`IHostedService`) must create their own scopes via `IServiceScopeFactory` — they cannot directly inject scoped services.

**🟠 RULE DI-04**: Avoid the Service Locator anti-pattern — injecting `IServiceProvider` into business services and calling `GetService<T>()`. Use constructor injection. Reserve `IServiceProvider` for factories and middleware.

**🟡 RULE DI-05**: Constructors with **more than 7 dependencies** signal a Single Responsibility violation. Refactor into aggregate services or split the class.

**🟡 RULE DI-06**: Use keyed services (`AddKeyed*` + `[FromKeyedServices("key")]`) when multiple implementations of the same interface exist, instead of `IEnumerable<T>` filtering or factory patterns.

### IOptions versus IOptionsSnapshot versus IOptionsMonitor

This choice causes subtle bugs when mismatched with service lifetimes:

| Interface | Lifetime | Reloads | Use when |
|---|---|---|---|
| `IOptions<T>` | Singleton | ❌ | Config that never changes at runtime |
| `IOptionsSnapshot<T>` | Scoped | ✅ Per-request | Config that may change, need per-request consistency |
| `IOptionsMonitor<T>` | Singleton | ✅ Real-time | Singletons/background services needing live config |

**🔴 RULE CFG-01**: Never inject `IOptionsSnapshot<T>` into a singleton — it's scoped. Use `IOptionsMonitor<T>.CurrentValue` instead.

**🟠 RULE CFG-02**: All options registrations must include `.ValidateDataAnnotations().ValidateOnStart()` to fail fast on misconfiguration at startup.

**🟠 RULE CFG-03**: Never inject raw `IConfiguration` into services. Use the Options pattern with strongly-typed classes.

### Error handling must produce RFC 9457 ProblemDetails

**🔴 RULE ERR-01**: Register `IExceptionHandler` + `AddProblemDetails()` globally. Never rely on try/catch in every controller.

**🟠 RULE ERR-02**: All error responses must return `ProblemDetails` (RFC 9457). Flag `return BadRequest("Some string")` — suggest `return Problem(detail: "...", statusCode: 400)`.

**🔴 RULE ERR-03**: Never expose stack traces or internal details in production error responses. Only include `exception.Message` for known domain exceptions; use generic messages for unexpected errors. Always include `traceId` via `HttpContext.TraceIdentifier`.

**🟠 RULE ERR-04**: Validation errors must return `ValidationProblemDetails` with field-level errors, not generic `BadRequest()`.

### Model validation at the API boundary

**🔴 RULE VAL-01**: All API controllers must have `[ApiController]` attribute — this enables automatic `ModelState` validation returning **400** on invalid input.

**🟠 RULE VAL-02**: Validate at the API boundary only, not in domain/service layers. DTOs carry validation attributes; domain entities do not.

**🟠 RULE VAL-03**: Do not use the deprecated `FluentValidation.AspNetCore` auto-validation package (removed in v12). Use manual validation or endpoint filters.

### Health checks need liveness/readiness separation

**🔴 RULE HC-01**: The application must have at least basic health check endpoints registered. **🟠 RULE HC-02**: Separate liveness (`/health/live` — is the process alive?) from readiness (`/health/ready` — can it serve traffic?). Liveness must be fast and lightweight with no dependency checks. **🟡 RULE HC-03**: External API failures should report `HealthStatus.Degraded`, not `Unhealthy`, to avoid cascading failures. **🟡 RULE HC-04**: Health check endpoints should not require authentication.

---

## 3. EF Core (.NET 10) data access rules

### N+1 queries are the most common performance killer

**🔴 RULE EF-01**: Flag navigation properties accessed in loops without `.Include()`. Every collection navigation accessed after materialization triggers a separate query.

```csharp
// ❌ Flag: N+1 — one query per order for Items
var orders = await ctx.Orders.ToListAsync();
foreach (var order in orders)
    Console.WriteLine(order.Items.Count); // Triggers lazy load

// ✅ Suggest: Eager load or project
var orders = await ctx.Orders.Include(o => o.Items).ToListAsync();
// Or project only what's needed:
var summaries = await ctx.Orders
    .Select(o => new { o.Id, ItemCount = o.Items.Count })
    .ToListAsync();
```

**🟠 RULE EF-02**: Use `.AsSplitQuery()` when including multiple collection navigations to avoid Cartesian explosion.

### Read-only queries must skip change tracking

**🟡 RULE EF-03**: All read-only queries must use `.AsNoTracking()`. Consider setting `QueryTrackingBehavior.NoTracking` as the default on the DbContext and opting in to tracking explicitly when mutations are needed.

### Bulk operations must use ExecuteUpdateAsync/ExecuteDeleteAsync

**🟠 RULE EF-04**: Flag the load-modify-save pattern for bulk operations. Use single-statement bulk operations instead.

```csharp
// ❌ Flag: Loads all entities, tracks them, saves one-by-one
var items = await ctx.Items.Where(i => i.ExpiredAt < now).ToListAsync();
foreach (var i in items) ctx.Remove(i);
await ctx.SaveChangesAsync();

// ✅ Suggest: Single DELETE — no entity loading
await ctx.Items.Where(i => i.ExpiredAt < now).ExecuteDeleteAsync(ct);

// ✅ Single UPDATE
await ctx.Posts.Where(p => p.BlogId == blogId)
    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsArchived, true), ct);
```

EF Core 10 now supports non-expression lambdas and JSON property updates in `ExecuteUpdateAsync`.

### Named query filters are an EF Core 10 improvement over global filters

**🟠 RULE EF-05**: Use EF Core 10's **named query filters** instead of unnamed global filters. This allows selectively disabling individual filters (e.g., soft-delete) without leaking tenant data.

```csharp
// ✅ EF Core 10 named filters
modelBuilder.Entity<Order>()
    .HasQueryFilter("SoftDelete", o => !o.IsDeleted)
    .HasQueryFilter("Tenant", o => o.TenantId == _tenantId);

// Selectively disable only soft-delete
var allOrders = await ctx.Orders.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();
```

### Migration safety is non-negotiable

**🔴 RULE EF-06**: All migrations must be peer-reviewed for data loss risk. **Dangerous operations**: column drops, type changes (`int` → `string`), adding non-nullable columns without defaults. **Safe operations**: adding nullable columns, adding tables, creating indexes with `CONCURRENTLY`.

```csharp
// ❌ Flag: non-nullable column without default
migrationBuilder.AddColumn<string>(name: "Sku", table: "Products", nullable: false);

// ✅ Suggest: add with default, backfill, then optionally remove default
migrationBuilder.AddColumn<string>(
    name: "Sku", table: "Products", nullable: false, defaultValue: "UNKNOWN");
```

### Connection management for background services

**🔴 RULE EF-07**: Background services and Wolverine handlers must use `IDbContextFactory<T>`, never directly injected `DbContext`. DbContext is not thread-safe and must not be a singleton.

### JSON columns versus separate tables

**🟡 RULE EF-08**: Use JSON columns (`.ToJson()`) for semi-structured data, configuration blobs, and denormalized read models. Do **not** use JSON for data needing referential integrity, frequently filtered/indexed columns, or independently updated nested entities. EF Core 10 supports bulk-updating JSON column properties directly.

---

## 4. Marten event sourcing discipline

### Events are immutable contracts — never modify published shapes

**🔴 RULE MRT-01**: Never modify an existing event record's properties. Add a **new event type** for schema changes and register upcasters for old events.

```csharp
// ❌ Flag: Modifying published event shape
public record OrderPlaced(Guid OrderId, decimal Total, string Currency); // was (Guid OrderId, decimal Total)

// ✅ Suggest: New version + upcasting
public record OrderPlacedV1(Guid OrderId, decimal Total);        // untouched
public record OrderPlaced(Guid OrderId, decimal Total, string Currency); // new

opts.Events.Upcast<OrderPlacedV1, OrderPlaced>(old =>
    new OrderPlaced(old.OrderId, old.Total, "USD"));
```

**🔴 RULE MRT-02**: Plan for event versioning from day one. Register upcasting transformations in the Marten configuration for all evolved events.

### Projection lifecycle must match consistency requirements

**🟠 RULE MRT-03**: Use **inline** projections for aggregate state (transactional consistency). Use **async** projections for read models and cross-aggregate views (eventual consistency). Use **live** projections for rarely-accessed data (computed on read, no storage).

```csharp
// ❌ Flag: Complex read model as inline — slows every append
opts.Projections.Add<DashboardProjection>(ProjectionLifecycle.Inline);

// ✅ Suggest: Async for read models
opts.Projections.Add<DashboardProjection>(ProjectionLifecycle.Async);
opts.Projections.Snapshot<Order>(SnapshotLifecycle.Inline); // aggregates stay inline
```

### Event sourcing anti-patterns to flag immediately

**🟠 RULE MRT-04**: Flag **fat events** carrying entire aggregate state. Events should be small and capture only what changed with clear intent.

**🟠 RULE MRT-05**: Flag **CRUD entities forced into event sourcing**. Simple create/update/delete entities (user profiles, settings) should use EF Core relational tables, not event streams.

**🟠 RULE MRT-06**: Flag direct SQL queries against Marten's `mt_events` table. Always use projections for read models.

### Marten and EF Core coexistence boundaries

**🔴 RULE MIX-01**: Use **separate schemas** — Marten owns `marten` schema for events and projections, EF Core owns `public` (or a named schema) for relational data.

**🔴 RULE MIX-02**: Do not span Marten + EF Core operations in one transaction unless sharing the same `NpgsqlConnection`. Prefer Wolverine's outbox pattern for cross-system atomicity.

**🟠 RULE MIX-03**: Clearly define ownership boundaries. Marten owns event streams and event-sourced projections. EF Core owns relational CRUD data. Integration flows through projections or domain events via Wolverine.

---

## 5. Async and concurrency correctness

### ConfigureAwait guidance for ASP.NET Core

**🟡 RULE ASYNC-01**: In **library projects** (shared packages), use `ConfigureAwait(false)` on all awaits. In **application code** (controllers, services in the RunCoach API), omit it — ASP.NET Core has no `SynchronizationContext`, making it unnecessary.

### ValueTask is for measured hot paths only

**🟠 RULE ASYNC-02**: Default to `Task<T>`. Use `ValueTask<T>` only on **measured hot paths** where the method frequently completes synchronously (cache hits, buffered reads). Never await a `ValueTask` more than once, never use `.Result` on an incomplete `ValueTask`, and never store/reuse a `ValueTask`.

### CancellationToken propagation is mandatory

**🔴 RULE ASYNC-03**: Every async method must accept and forward `CancellationToken`. Controllers receive tokens from `HttpContext.RequestAborted`. Use `CancellationTokenSource.CreateLinkedTokenSource` to add timeouts:

```csharp
// ❌ Flag: No cancellation token
public async Task<CoachingResponse> GenerateAdviceAsync(string prompt)

// ✅ Suggest: Accept and propagate
public async Task<CoachingResponse> GenerateAdviceAsync(string prompt, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(60));
    return await _llm.GenerateAsync(prompt, cts.Token);
}
```

### Async anti-patterns that cause production incidents

**🔴 RULE ASYNC-04**: `async void` is **forbidden** except in event handlers. It crashes the process on unhandled exceptions and prevents callers from observing completion.

**🔴 RULE ASYNC-05**: Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — sync-over-async causes thread pool starvation and deadlocks. The only acceptable location is `Program.cs` bootstrap or truly terminal code.

**🟠 RULE ASYNC-06**: Flag fire-and-forget patterns (`_ = DoWorkAsync()`) that discard exceptions. All background work must flow through `IHostApplicationLifetime`, Wolverine, or a background queue with error logging.

**🟠 RULE ASYNC-07**: Flag sequential awaits for independent operations. Use `Task.WhenAll` when operations are independent:

```csharp
// ❌ Flag: Sequential when independent
var user = await GetUserAsync(id, ct);
var history = await GetHistoryAsync(id, ct);

// ✅ Suggest: Parallel
var userTask = GetUserAsync(id, ct);
var historyTask = GetHistoryAsync(id, ct);
var (user, history) = (await userTask, await historyTask);
```

**🟠 RULE ASYNC-08**: Flag `Task.Run` wrapping synchronous code in async methods (async-over-sync). This deceives callers about the nature of the operation and wastes thread pool threads.

### Thread safety in DI singletons

**🟠 RULE ASYNC-09**: All singleton services must be thread-safe. Use `ConcurrentDictionary` with `GetOrAdd`/`AddOrUpdate` (not check-then-act patterns), `SemaphoreSlim` for async locking (never `lock`/`Monitor` which blocks threads), and `Channel<T>` for producer-consumer patterns.

**🟡 RULE ASYNC-10**: Implement `IAsyncDisposable` when a class holds async resources. Always use `await using` in consuming code.

---

## 6. Performance anti-patterns to flag in review

These rules cross-reference Microsoft's dotnet-diag performance diagnostics guidance. Focus on the patterns most relevant to a web API processing running data.

### String operations are the most common allocation source

**🟠 RULE PERF-01**: Flag `.Equals()`, `.Contains()`, `.StartsWith()`, `.EndsWith()` without explicit `StringComparison`. Always specify `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase`.

**🟠 RULE PERF-02**: Flag `.ToLower()` / `.ToUpper()` used for comparison. Use `OrdinalIgnoreCase` instead — it avoids allocating a new string.

```csharp
// ❌ Flag: Allocates lowercase copy
if (input.ToLower() == "marathon") { ... }

// ✅ Suggest: No allocation
if (input.Equals("marathon", StringComparison.OrdinalIgnoreCase)) { ... }
```

**🟠 RULE PERF-03**: Flag string concatenation in loops. Use `StringBuilder` or `string.Create()`.

**🟡 RULE PERF-04**: Flag chained `.Replace()` calls (3+). Use `StringBuilder` or `Regex.Replace` with a single pass.

### Collection type must match access pattern

**🟡 RULE PERF-05**: Flag `List<T>` used for lookup by key — use `Dictionary<TKey, TValue>` or `HashSet<T>`.

**🟡 RULE PERF-06**: Flag mutable collections holding read-only data. Use `FrozenDictionary<TKey, TValue>` / `FrozenSet<T>` (.NET 8+) for data populated once and read many times. Use `ImmutableArray<T>` for immutable sequences. This aligns with RunCoach's established convention for frozen/immutable collections.

**🟡 RULE PERF-07**: Flag `.Count()` LINQ extension on types that have a `.Count` property (arrays, `List<T>`, `ICollection<T>`). Flag multiple enumeration of `IEnumerable<T>` — materialize with `.ToList()` when accessed more than once.

### Memory allocation in hot paths

**🟡 RULE PERF-08**: In .NET 10, prefer `params ReadOnlySpan<T>` over `params T[]` to avoid heap allocation of the params array.

**🟡 RULE PERF-09**: Flag boxing of value types — passing `int`/`struct` to `object` parameters. Flag large struct copies (>16 bytes) — pass by `in` or use `ref`.

**🟡 RULE PERF-10**: Use `ArrayPool<T>.Shared` for temporary buffers in hot paths rather than allocating new arrays.

### Regex must use source generation

**🟠 RULE PERF-11**: Flag `new Regex()` inside methods or loops. Regex instances must be `static readonly` or use `[GeneratedRegex]`. In .NET 10, **always prefer `[GeneratedRegex]`** — it's AOT-friendly and compiled at build time.

```csharp
// ❌ Flag: Allocates + compiles regex per call
bool IsValid(string input) => new Regex(@"^\d{4}-\d{2}-\d{2}$").IsMatch(input);

// ✅ Suggest: Source-generated
[GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
private static partial Regex DatePattern();
bool IsValid(string input) => DatePattern().IsMatch(input);
```

### JSON serialization must be source-generated

**🟠 RULE PERF-12**: Use `[JsonSerializable]` source generation context for `System.Text.Json`. This enables AOT compatibility and eliminates reflection-based serialization overhead.

**🟠 RULE PERF-13**: `JsonSerializerOptions` must be cached as `static readonly` — never create per-call. Creating options triggers expensive reflection and caching setup.

### HttpClient lifetime management

**🔴 RULE PERF-14**: Flag `new HttpClient()` per call — this exhausts socket handles. Use `IHttpClientFactory` exclusively.

**🟠 RULE PERF-15**: All `HttpClient` usage must have explicit timeout configuration and resilience policies (retry + circuit breaker) via `Microsoft.Extensions.Http.Resilience`.

---

## 7. Testing rules for xUnit v3, FluentAssertions, and NSubstitute

### xUnit v3 requires CancellationToken discipline

**🔴 RULE TEST-01**: All async test operations must receive `TestContext.Current.CancellationToken`. xUnit v3 ships an analyzer that flags this. This enables fast cancellation on timeout.

```csharp
// ❌ Flag
await Task.Delay(1000);

// ✅ Suggest
await Task.Delay(1000, TestContext.Current.CancellationToken);
```

**🟠 RULE TEST-02**: Never store `TestContext.Current` — access it at the point of use each time. It provides a moment-in-time snapshot.

**🟡 RULE TEST-03**: Use `TestContext.Current.SendDiagnosticMessage()` for test infrastructure diagnostics instead of `Console.WriteLine`.

**🟠 RULE TEST-04**: Use `IAsyncLifetime` for async setup/teardown. Never perform async work in constructors (sync-over-async deadlock risk). xUnit v3 fully supports `ValueTask` returns from `IAsyncLifetime`.

### FluentAssertions patterns

**🔴 RULE TEST-05**: Always `await` async assertions. Un-awaited `ThrowAsync` silently passes even when it should fail.

```csharp
// ❌ Flag: Missing await — always passes!
action.Should().ThrowAsync<ArgumentException>();

// ✅ Suggest
await action.Should().ThrowAsync<ArgumentException>();
```

**🟠 RULE TEST-06**: Use `BeEquivalentTo()` for structural/property-by-property comparison. Use `Be()` for value/reference equality. Configure `BeEquivalentTo` with `.Excluding()`, `.WithStrictOrdering()`, and custom comparison rules as needed.

**🟡 RULE TEST-07**: Use `BeApproximately()` for floating-point comparisons (pace calculations, distance). Never use exact equality on `double`/`float`.

**🟡 RULE TEST-08**: Use specific collection assertions — `ContainSingle()`, `HaveCount()`, `BeInAscendingOrder()` — instead of manual LINQ + `Assert.True()`.

### NSubstitute patterns

**🔴 RULE TEST-09**: For async method returns, use `.Returns(value)` directly — NSubstitute auto-wraps in `Task.FromResult`. Flag verbose `Returns(Task.FromResult(...))`.

```csharp
// ❌ Flag: Unnecessary Task.FromResult wrapping
mock.GetAsync(1).Returns(Task.FromResult(new Item()));

// ✅ Suggest: NSubstitute auto-wraps
mock.GetAsync(1).Returns(new Item());
```

**🟠 RULE TEST-10**: Prefer `Substitute.For<T>()` (interface mocking) over `Substitute.ForPartsOf<T>()` (partial mock). Needing partial mocks often signals a design problem.

**🟠 RULE TEST-11**: More than 3 mocks per test is a design smell. If the SUT requires 5+ mocked dependencies, the class likely violates Single Responsibility.

### Test organization and naming

**🟠 RULE TEST-12**: Follow `MethodName_Scenario_Expected` naming convention strictly. Use descriptive scenario names — no abbreviations.

```csharp
[Fact]
public async Task CalculatePace_NegativeDistance_ThrowsArgumentException()
```

**🟡 RULE TEST-13**: Use `[Theory]` + `[InlineData]` for simple value combinations (<5 params, primitives). Use `[MemberData]` for complex objects. Use separate `[Fact]` tests when scenarios require different setup/assertion logic.

### Test anti-patterns that waste CI time

**🔴 RULE TEST-14**: Never use `Thread.Sleep()`. Use `await Task.Delay(..., TestContext.Current.CancellationToken)` or redesign to eliminate waits.

**🔴 RULE TEST-15**: Tests must not depend on execution order. xUnit v3 uses stable randomization. Never use shared mutable `static` state.

**🟠 RULE TEST-16**: Don't test implementation details — test observable behavior. Flag `Received()` calls verifying internal helper methods. Assert on return values, output state, or documented side-effects.

**🟠 RULE TEST-17**: Avoid brittle string assertions on error messages. Flag `ex.Message.Should().Be("The order with ID 42...")` — suggest `ex.Message.Should().Contain("not found")` or assert on exception type/properties.

### LLM eval test specifics (M.E.AI.Evaluation)

**🔴 RULE TEST-18**: All eval tests must use `DiskBasedReportingConfiguration` with `ResponseCachingChatClient`. In CI, tests must FAIL on cache miss rather than making live API calls. This prevents unexpected costs and non-deterministic results.

**🟠 RULE TEST-19**: Never assert exact LLM output text. Use evaluation metrics with thresholds (`RelevanceEvaluator`, `CoherenceEvaluator`) and assert on scores.

**🟡 RULE TEST-20**: Tag eval tests with `[Trait("Category", "LLMEval")]` to enable separate CI scheduling. Set `Temperature = 0.0f` for more deterministic eval responses.

**🟠 RULE TEST-21**: For structured output validation, deserialize to the expected type and validate schema conformance — never assert on raw JSON strings.

---

## 8. LLM integration rules for the coaching layer

### Every Anthropic API call needs structured error handling and retry

**🔴 RULE LLM-01**: Every LLM API call must distinguish retryable errors (**429**, **500**, **529**) from non-retryable (**400**, **401**, **403**). Use `Microsoft.Extensions.Http.Resilience` with exponential backoff + jitter. Never use hedging (parallel requests) for message creation — it's non-idempotent.

**🔴 RULE LLM-02**: Always pass `CancellationToken` through the entire call chain from controller to SDK call. Set explicit timeouts via `CancellationTokenSource.CancelAfter()`.

**🟠 RULE LLM-03**: Always check `stop_reason` on API responses. `"max_tokens"` means the response was **truncated** — handle accordingly. `"end_turn"` indicates completion.

**🟠 RULE LLM-04**: Log token usage (`input_tokens`, `output_tokens`) from every response for cost tracking. Calculate cost per feature/operation.

### Structured output requires strict schema enforcement

**🔴 RULE LLM-05**: JSON schemas for structured output must include `additionalProperties: false` and all properties in the `required` array. This is mandatory for Anthropic's constrained decoding.

**🟠 RULE LLM-06**: Constrained decoding does **not** enforce numerical constraints (`minimum`, `maximum`). Always add post-deserialization validation for numerical bounds.

**🟠 RULE LLM-07**: Use `GenerateStructuredAsync<T>` with strongly-typed deserialization. Handle deserialization failures and model refusals (`stop_reason: "refusal"`) gracefully.

### Prompt management must use the YAML store

**🔴 RULE LLM-08**: No hardcoded prompts in C# code. Flag string literals containing LLM instructions. All prompts must be loaded from `YamlPromptStore`.

```csharp
// ❌ Flag: Hardcoded prompt
string prompt = "You are a running coach. Analyze this workout...";

// ✅ Suggest: Load from store
var template = promptStore.GetTemplate("coaching/workout-feedback", version: 2);
var rendered = template.Render(new { athlete_name, workout_data });
```

**🔴 RULE LLM-09**: No raw string interpolation for prompts (`$"Analyze {data}..."`). Use the template engine with named variables.

**🟠 RULE LLM-10**: Prompts must be version-controlled with explicit versioning (`weekly-summary.v1.yaml`, `weekly-summary.v2.yaml`).

### Context assembly must enforce token budgets

**🔴 RULE LLM-11**: Always reserve `max_tokens` output space from the context window. `available_input = context_window - max_tokens`. Newer Claude models reject oversized contexts with validation errors.

**🟠 RULE LLM-12**: Pre-count tokens before API calls using Anthropic's token counting API. Implement overflow cascade: skip lowest-priority sections first, truncate mid-priority sections, never skip the system prompt.

### The deterministic/LLM boundary is the most important architectural rule

**🔴 RULE LLM-13**: Never use LLM for deterministic tasks — pace calculations, zone math, distance conversions, weekly volume aggregation, TSS/ATL/CTL/TSB computations. These belong in the deterministic computation layer.

**🟠 RULE LLM-14**: Pre-compute all structured data deterministically, then pass as context to the LLM for narrative generation. The LLM receives computed results, not raw data to compute.

**🟠 RULE LLM-15**: `ICoachingLlm` is the **only** LLM entry point. Domain services must not reference `Microsoft.Extensions.AI` or `IChatClient` directly.

**🟠 RULE LLM-16**: Custom `DelegatingChatClient` implementations must override **both** `GetResponseAsync` and `GetStreamingResponseAsync` — callers may use either.

---

## 9. Security review rules

### Input validation happens at the API boundary and nowhere else

**🔴 RULE SEC-01**: Never bind directly to EF entities. Always use DTOs for model binding to prevent mass assignment.

```csharp
// ❌ Flag: Mass assignment vulnerability
public IActionResult Create(UserEntity user)

// ✅ Suggest: DTO with explicit mapping
public IActionResult Create(CreateUserDto dto)
```

**🔴 RULE SEC-02**: Use parameterized queries exclusively. Flag `FromSqlRaw` with string interpolation — use `FromSql` (EF 10) or `FromSqlInterpolated` which auto-parameterizes.

```csharp
// ❌ Flag: SQL injection
ctx.Users.FromSqlRaw($"SELECT * FROM Users WHERE Email = '{email}'");

// ✅ Suggest: Auto-parameterized
ctx.Users.FromSql($"SELECT * FROM Users WHERE Email = {email}");
```

**🟠 RULE SEC-03**: Enforce input length limits on all string properties via `[MaxLength]` attributes or FluentValidation `.MaximumLength()`.

### JWT authentication must validate all parameters

**🔴 RULE SEC-04**: JWT token validation must enable **all four flags**: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`. Flag any set to `false`.

**🟠 RULE SEC-05**: Use RS256 (asymmetric) for distributed systems. Access token lifetime should be **5-15 minutes** with refresh token rotation.

**🟠 RULE SEC-06**: Place `[Authorize]` at the controller level with `[AllowAnonymous]` for exceptions. Set a **fallback authorization policy** requiring authentication by default.

**🔴 RULE SEC-07**: Implement resource-based authorization via `IAuthorizationHandler` to prevent IDOR (Insecure Direct Object Reference). Flag `GET /api/orders/{id}` endpoints that return any order regardless of authenticated user.

### OWASP-aligned rules for .NET

**🔴 RULE SEC-08**: Never use `TypeNameHandling` with Newtonsoft.Json (insecure deserialization). Prefer `System.Text.Json` which is safe by default.

**🟠 RULE SEC-09**: Never expose stack traces in production. Guard `UseDeveloperExceptionPage()` with `IsDevelopment()` check.

**🟠 RULE SEC-10**: Add security response headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Strict-Transport-Security` (1-year max-age), `Referrer-Policy: strict-origin-when-cross-origin`. Remove the `Server` header via `KestrelServerOptions.AddServerHeader = false`.

### Secrets must never appear in source control

**🔴 RULE SEC-11**: Never hardcode secrets in source code or `appsettings.json`. Use `dotnet user-secrets` for development, environment variables or managed secret stores (Azure Key Vault, AWS Secrets Manager) for production.

**🔴 RULE SEC-12**: Never log connection strings, API keys, or PII. Log only user IDs and metadata, never passwords or tokens.

### API security configuration

**🔴 RULE SEC-13**: CORS must have explicit origin allowlists in production. Flag `.AllowAnyOrigin()`. Never combine `AllowAnyOrigin()` with `AllowCredentials()` — this violates the CORS specification.

**🟠 RULE SEC-14**: Enable rate limiting with per-user/IP partitioning. Set `RejectionStatusCode = 429` (not the default 503). Place the rate limiter middleware after routing and auth for endpoint-specific limits.

**🟠 RULE SEC-15**: Enforce HTTPS with `UseHttpsRedirection()` + `UseHsts()` with 1-year `max-age` in production.

---

## 10. Build and project file hygiene

### Directory.Build.props prevents configuration drift

**🟠 RULE BUILD-01**: All shared properties (`TargetFramework`, `LangVersion`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) belong in the root `Directory.Build.props`. Flag duplication in individual `.csproj` files.

**🟡 RULE BUILD-02**: Use conditional `TreatWarningsAsErrors` for CI vs dev to avoid blocking prototyping: `<TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>`.

**🟠 RULE BUILD-03**: If nested `Directory.Build.props` exists, it must import the parent: `<Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />`.

**🟡 RULE BUILD-04**: Set `<ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>` for deterministic CI builds.

### Central Package Management enforcement

**🔴 RULE BUILD-05**: With CPM enabled, `.csproj` files must NOT contain `Version` on `PackageReference`. NuGet enforces this via NU1008.

**🟠 RULE BUILD-06**: Use `GlobalPackageReference` for analyzer packages (StyleCop, SonarAnalyzer) in `Directory.Packages.props` with `PrivateAssets="All"` instead of repeating in each `.csproj`.

**🟡 RULE BUILD-07**: Use `VersionOverride` sparingly with documented XML comment justification. Minimize overrides.

**🟠 RULE BUILD-08**: Run `dotnet list package --vulnerable --include-transitive` in CI. Enable `CentralPackageTransitivePinningEnabled` to control transitive dependency versions. Trivy scanning (already configured) provides additional coverage.

### Analyzer configuration

**🟡 RULE BUILD-09**: Prefer `.editorconfig` for diagnostic severity configuration over scattered `#pragma` directives. Use `GlobalSuppressions.cs` for project-wide suppressions with `Justification` strings. Always pair `#pragma warning disable` with `#pragma warning restore`.

**🟠 RULE BUILD-10**: Never suppress security-critical SonarAnalyzer rules (S3649 SQL injection, S5131 XSS, S2068 hardcoded credentials, S5332 insecure HTTP) without documented justification and team review.

**🟡 RULE BUILD-11**: Use `WarningsNotAsErrors` instead of `NoWarn` for false positives — this still surfaces the warning without failing the build.

### MSBuild anti-patterns

**🔴 RULE BUILD-12**: Never hardcode absolute paths in project files. Use MSBuild properties: `$(MSBuildThisFileDirectory)`, `$(MSBuildExtensionsPath)`.

**🟠 RULE BUILD-13**: Custom targets must have `Inputs` and `Outputs` attributes for incremental build support. Missing these causes unnecessary rebuilds.

**🔵 RULE BUILD-14**: Don't restate SDK defaults (`<OutputType>Library</OutputType>` in class libraries, `<Compile Include="**/*.cs" />` in SDK-style projects). Restating adds noise and risks conflicts.

**🟡 RULE BUILD-15**: Avoid `$(SolutionDir)` in project files — it breaks CLI builds. Use `$(MSBuildThisFileDirectory)` or `$([MSBuild]::GetPathOfFileAbove(...))`.

---

## What .NET 10 changes supersede current practices

Several .NET 10 features directly improve patterns RunCoach currently uses or plans to use:

- **Named query filters** (EF Core 10) supersede the all-or-nothing `IgnoreQueryFilters()` approach for soft-delete and multi-tenancy. Adopt immediately when implementing the data layer.
- **`ExecuteUpdateAsync` with non-expression lambdas** (EF Core 10) now supports conditional logic and JSON property updates directly, making bulk operations more expressive.
- **`field` keyword** (C# 14) eliminates dozens of backing field declarations across DTOs and domain types with validated setters.
- **`params ReadOnlySpan<T>`** (C# 14) replaces `params T[]` on hot paths, eliminating array allocations.
- **`[GeneratedRegex]`** should replace any remaining `Regex.Compiled` instances — it's AOT-compatible and compiled at build time.
- **`[JsonSerializable]` source generation** should be adopted for all serialization contexts to prepare for AOT and reduce startup time.
- **`IExceptionHandler`** (introduced .NET 8, standard in .NET 10) replaces older exception filter patterns for global error handling.
- **`UseEndpoints()`** is no longer needed in .NET 10 — `MapControllers()` handles routing implicitly.

## Conclusion

This rule set prioritizes **correctness over convenience** — the critical rules around SQL injection prevention, JWT validation, captive dependencies, and migration safety exist because violations cause production incidents. The high-severity rules around async patterns, error handling, and LLM integration boundaries enforce the architectural separation between deterministic computation and coaching narrative that makes RunCoach reliable.

Three insights stand out as especially important for this codebase. First, the deterministic/LLM boundary (Rules LLM-13 through LLM-15) is the single most important architectural invariant — every code review should verify that pace calculations, zone math, and volume aggregations never flow through the LLM. Second, the Marten + EF Core coexistence rules (MIX-01 through MIX-03) need to be established before the data layer is built, not retrofitted after — schema separation and transaction boundaries are foundational decisions. Third, the eval test caching rules (TEST-18 through TEST-21) prevent the most expensive CI mistake possible: accidentally calling Claude's API on every test run.

Rules classified as 🔵 Low are style preferences that improve readability. All other rules address measurable correctness, performance, or security concerns. Adopt critical and high rules immediately; medium rules in the first sprint; low rules when the codebase matures.