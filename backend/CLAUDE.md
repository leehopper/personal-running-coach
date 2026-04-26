# Backend — .NET 10 / ASP.NET Core

> **Trademark rule.** User-facing surface (coaching prompt YAML under `src/RunCoach.Api/Prompts/`, API responses, generated plan narrative, error messages) must use "Daniels-Gilbert zones" or "pace-zone index" — never the trademarked term `VDOT`. The mark is enforced by The Run SMART Project LLC (Runalyze precedent). Calculator classes are `PaceZoneIndexCalculator`, `PaceZoneCalculator`, and `HeartRateZoneCalculator` per DEC-042; the `FitnessEstimate.EstimatedPaceZoneIndex` property, `RaceTime` XML doc, and `TestProfiles` `AssessmentBasis` strings were scrubbed in spec 11 (2026-04-18). `ContextAssemblerTests` has a parameterized Theory asserting the trademarked term (case-insensitive) does not appear in the full assembled prompt for any of the 5 test profiles. The only remaining occurrences of the literal term in-repo are carve-out-exempt: policy/rule docs (root `CLAUDE.md`, `REVIEW.md`, `README.md`, `NOTICE`), the live guard assertions themselves, and `DanielsGilbertEquationsTests.cs:73` (math comment). See root `CLAUDE.md` and `NOTICE` for full context.

## Stack

See root CLAUDE.md for full tech stack. Additionally: Swashbuckle (OpenAPI), Anthropic SDK, YamlDotNet, M.E.AI.Evaluation (test project).

## Module-First Organization

```
backend/
  src/
    RunCoach.Api/
      Program.cs
      Prompts/                     # Versioned YAML prompt files (coaching-v1.yaml, etc.)
      Modules/
        Coaching/                  # LLM adapter, context assembly, prompt storage
          ClaudeCoachingLlm.cs     # ICoachingLlm implementation (sealed, disposable)
          ContextAssembler.cs      # Builds AssembledPrompt with token budget enforcement
          ICoachingLlm.cs
          IContextAssembler.cs
          Models/                  # AssembledPrompt, ConversationTurn, plan models
            Structured/            # JSON schema types for constrained decoding
          Prompts/                 # IPromptStore, YamlPromptStore, PromptRenderer
        Training/                  # Deterministic training science
          Computations/            # PaceZoneIndexCalculator, PaceZoneCalculator, HeartRateZoneCalculator, DanielsGilbertEquations (formula-based)
          Models/                  # UserProfile, TrainingPaces, WorkoutSummary, etc.
      Infrastructure/              # ServiceCollectionExtensions (DI registration hub), auth middleware, cross-cutting primitives
  tests/
    RunCoach.Api.Tests/
      Modules/                     # Mirrors src structure
        Coaching/
          Eval/                    # M.E.AI.Evaluation infrastructure (see Testing section)
        Training/
          Profiles/                # TestProfiles — 5 simulated runner profiles with history
    eval-cache/                    # Committed LLM response fixtures for CI replay
    scripts/                       # Eval cache maintenance scripts
```

**Convention for new modules:** follow the `{Domain}/` pattern with `Models/`, `Entities/`, `Extensions/` subfolders as needed. Controllers, services, and repositories at module root. When a module exceeds ~8 root files, create named subfolders.

## Coding Standards

- **Primary constructors** when applicable: `public class MyService(IMyRepo repo) : IMyService { }`
- **Private fields** prefixed with `_` (e.g., `_memberVariable`)
- **Properties** initialized with default non-null values: `public string Name { get; set; } = string.Empty;`
- **One type per file** — classes, interfaces, enums, records, structs. Exception: `internal` nested types used solely as serialization/deserialization models for their enclosing class may remain nested (e.g., `YamlPromptStore.YamlPromptDocument`).
- **Record types for DTOs** with `Dto` suffix (e.g., `WorkoutDto`, `CreatePlanRequestDto`)
- **Ternary operators** over if-else for simple conditional assignments
- **Async throughout** for all EF Core and I/O operations

## Dependency Injection

- `Infrastructure/ServiceCollectionExtensions.cs` has `AddApplicationModules()` — add per-module registrations here as modules gain injectable services
- **Scoped lifetime** by default for services and repositories (anything in the request pipeline)
- **Singleton** only for stateless infrastructure (configuration wrappers, HTTP client factories)
- Controllers registered via `builder.Services.AddControllers()`
- `InternalsVisibleTo` grants test project access to `internal` types

## Architecture Layers

- **Controllers** inherit from `ControllerBase` directly and carry their own `[ApiController]` + `[Route("api/v1/<literal-or-template>")]` attributes. Entry point only — delegate to services. (A `BaseController` abstract class was considered but never gained a second user — new controllers keep the attributes inline rather than inheriting an empty base.)
- **Services** contain business logic, injected into controllers. All services have interfaces.
- **Repositories** handle data access, injected into services. All repositories have interfaces.
- **Computation classes** are pure/deterministic (no I/O) — `PaceZoneIndexCalculator`, `PaceZoneCalculator`, `HeartRateZoneCalculator`, `DanielsGilbertEquations` (internal static helper). Interfaces for the injectable services; the equations helper is static because it has no state.

## Logging

- All controllers, services, and repositories inject `ILogger<T>`
- **Structured logging** with named placeholders (e.g., `{WorkoutId}`, `{Status}`), not string interpolation

## EF Core

- **Code-first migrations** — never modify or delete existing migrations
- **Primary keys:** `{EntityName}Id` (e.g., `WorkoutId`, `PlanId`), type `Guid`, with `[Key]` attribute
- **Foreign keys:** `{ParentEntity}Id` convention with `[ForeignKey]` on navigation property
- **Data annotations** preferred over Fluent API (`[Key]`, `[Required]`, `[Table]`, etc.)
- **DbSet names** are plural; table names are singular via `[Table("Workout")]`
- **Base entity** with audit fields: `CreatedOn`, `ModifiedOn`
- **Async operations** throughout — use `ToListAsync()`, `FirstOrDefaultAsync()`, etc.
- Add new migrations: `dotnet ef migrations add {ShortUniqueDescription}`

## Marten (Event Sourcing)

- Marten owns event streams and document projections (plan state)
- EF Core owns relational tables (users, workout history, structured data)
- Clear ownership boundaries — no entity belongs to both

## Configuration

- **Strongly-typed settings** as record types mapped from `IConfiguration`
- **`IOptions<T>` pattern** for injection
- **Layered config:** `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json` (git-ignored)
- All secrets via environment variables or .NET user-secrets, never in config files

## Testing

- **xUnit v3** (MTP runner) + **FluentAssertions** + **NSubstitute**
- `TestingPlatformDotnetTestSupport` enabled — uses Microsoft.Testing.Platform, not VSTest
- Use `TestContext.Current.CancellationToken` for async test cancellation
- Use `TestContext.Current.SendDiagnosticMessage()` for test output (not `Trace.WriteLine`)
- Test projects **mirror source directory structure**
- **Arrange / Act / Assert** with comment markers
- Prefix expected values with `expected`, actual with `actual`
- `[Theory]` + `[InlineData]` for parameterized scenarios; separate `[Fact]` for conceptually different scenarios
- **Unit tests** for all public methods in services and computation layer
- **Integration tests** via `WebApplicationFactory` + **Testcontainers** (real PostgreSQL, not in-memory)
- Integration tests validate the **full response contract** with deep equality (exclude audit fields)
- Test file naming: `{ClassName}Tests.cs` (unit), `{ClassName}IntegrationTests.cs` (integration)

### Test Parallelism

`RunCoach.Api.Tests` runs **collections sequentially** (parallel test-collection
execution disabled). Enforced via two redundant mechanisms:

- `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in
  `Infrastructure/AssemblyInfo.cs` — primary, compile-time, travels with the
  assembly
- `xunit.runner.json` next to the test assembly with
  `parallelizeTestCollections: false`, `parallelizeAssemblies: false`,
  `maxParallelThreads: 1` — runner-side override copied via
  `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`

Why: integration tests share an assembly-scoped `RunCoachAppFactory` that boots
one Testcontainers Postgres + Marten `IDocumentStore` + Wolverine host for the
whole suite. Under default parallel-by-collection scheduling, multiple
integration test classes raced on the shared `IDocumentStore`'s
schema-migration advisory lock and Marten async daemon, producing intermittent
failures (different set of 9-11 tests per run) with two recurring signatures:
"Unable to attain a global lock in time order to apply database changes" and
`ObjectDisposedException` on `IDocumentStore` shutdown.

Sequential execution is the canonical mode. `dotnet test` (default invocation)
is the supported, deterministic command — no `-- -parallel none` flag needed.
The future fix, deferred, is to partition the shared SUT into per-collection
isolated databases or schemas so collection-level parallelism becomes safe
again.

### Wolverine Handler Integration Coverage

Integration tests that exercise a Wolverine command handler (regenerate plan,
onboarding terminal-branch, future Slice 3 `PlanAdaptedFromLog`, Slice 4
`ConversationTurnRecorded`) drive the live `IMessageBus.InvokeAsync<TResponse>`
pipeline — no direct `Handler.Handle(...)` calls in test code. Three pieces
make this work under the shared `RunCoachAppFactory`:

- `Program.cs` pins `opts.ApplicationAssembly = typeof(Program).Assembly`
  inside `UseWolverine`. Without this, Wolverine's entry-assembly heuristic
  walks the call stack and picks `RunCoach.Api.Tests` under
  `WebApplicationFactory<Program>`, finds zero handlers in the test
  assembly, and every `bus.InvokeAsync<T>` from a controller fails with
  `IndeterminateRoutesException`.
- `RunCoachAppFactory.ConfigureWebHost` swaps the production
  `IPlanGenerationService` registration for
  `Infrastructure/StubPlanGenerationService.cs` so the bus-driven handler
  chain doesn't pay six structured-output LLM calls per test. The stub
  must be `public` — Wolverine's codegen uses service location for
  internal types and falls back to scope-based DI for the entire chain,
  which then resolves a different `IDocumentSession` for the idempotency
  store than the one Wolverine commits, breaking idempotency-replay tests
  silently.
- `MartenConfiguration.AddRunCoachMarten` sets
  `CritterStackDefaults.Development.SourceCodeWritingEnabled = false` so
  Wolverine's auto-codegen stays in-memory under the test host. Without
  this, the test fixture's `StubPlanGenerationService` registration would
  cause Wolverine to flush a generated `*Handler.cs` referencing the
  test-only type into `src/RunCoach.Api/Internal/Generated/`, after which
  any plain `dotnet build` of the API project fails CS0234 on the
  dangling cross-assembly reference. Production static codegen still
  writes via the explicit `dotnet run -- codegen write` step (DEC-048).

Tests then drive the bus the same way the controller does:

```csharp
using var scope = Factory.Services.CreateScope();
var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
var response = await bus.InvokeAsync<RegeneratePlanResponse>(cmd, ct);
```

Wolverine's transactional middleware brackets a single Marten
`SaveChangesAsync` around the handler body; the EF
`UseEntityFrameworkCoreTransactionParticipant` wiring enrols the inline
`UserProfileFromOnboardingProjection` write into that same Postgres
transaction (DEC-060 / R-069). No manual `SaveChangesAsync` in the test —
that would open a second session and hide a regression in the framework
bracket.

### Eval Infrastructure

LLM evaluation tests live in `tests/RunCoach.Api.Tests/Modules/Coaching/Eval/`:

- **`EvalTestBase`** — base class providing cached Sonnet (coaching) + Haiku (judging) clients via `DiskBasedReportingConfiguration`
- **`EVAL_CACHE_MODE`** env var: `Record` (call API, save responses), `Replay` (use committed fixtures, fail on miss), `Auto` (default — replay if fixture exists, record otherwise)
- **`ReplayGuardChatClient`** — `DelegatingChatClient` that throws descriptive errors on cache miss in Replay mode
- **`AnthropicStructuredOutputClient`** — bridges `ForJsonSchema()` to native Anthropic constrained decoding (SDK's IChatClient bridge silently drops schemas)
- **Evaluators:** `PlanConstraintEvaluator` (deterministic checks) + `SafetyRubricEvaluator` (LLM-as-judge with structured verdict output)
- **CI runs in Replay mode** — zero API calls, uses committed fixtures in `tests/eval-cache/`
- To re-record fixtures: `ANTHROPIC_API_KEY=... EVAL_CACHE_MODE=Record dotnet test` (or use `tests/scripts/rerecord-eval-cache.sh`)

## Quality Pipeline (DEC-043)

See root `CLAUDE.md` for the full five-layer pipeline. Backend-specific notes: CodeQL runs in `build-mode: manual` reusing the `dotnet restore` + `dotnet build --no-restore` flow. SonarQube Cloud ingests OpenCover coverage (not Cobertura — that property does not exist for C#). Build-time `SonarAnalyzer.CSharp` remains the compile-time hard gate via `TreatWarningsAsErrors`; SonarQube Cloud is advisory dashboard only. `backend/src/RunCoach.Api/Prompts/` is excluded from CodeQL and SonarQube analysis (coaching-prompt IP no-touch).

## Build & Test Commands

Run from `backend/`:

- `dotnet build` — build all projects
- `dotnet test` — run all tests
- `dotnet restore --force` — force NuGet restore

## API Key Configuration

The main API project and the test project use **different** user-secrets stores:

| Project | UserSecretsId | Set command |
| --- | --- | --- |
| `src/RunCoach.Api` | `runcoach-api` | `dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project backend/src/RunCoach.Api` |
| `tests/RunCoach.Api.Tests` | `runcoach-api-tests` | `dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project backend/tests/RunCoach.Api.Tests` |

**The eval tests read from `runcoach-api-tests`, not `runcoach-api`.** Setting the key on the wrong project is the most common cause of "credit balance too low" errors when the key is known-good. The `rerecord-eval-cache.sh` script checks user-secrets on the main project (step 1) but the tests themselves read from the test project's store. Both must be configured.

## Post-Change

See root CLAUDE.md checklist.
