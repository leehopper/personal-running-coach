# Backend — .NET 10 / ASP.NET Core

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
          Computations/            # VdotCalculator, PaceCalculator (formula-based)
          Models/                  # UserProfile, TrainingPaces, WorkoutSummary, etc.
        Common/                    # Cross-cutting (BaseController)
      Infrastructure/              # ServiceCollectionExtensions (DI registration hub)
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

- **Controllers** inherit from `BaseController` (`Common/`). Entry point only — delegate to services.
- **Services** contain business logic, injected into controllers. All services have interfaces.
- **Repositories** handle data access, injected into services. All repositories have interfaces.
- **Computation classes** are pure/deterministic (no I/O) — `VdotCalculator`, `PaceCalculator`. Interfaces for testability.

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

### Eval Infrastructure

LLM evaluation tests live in `tests/RunCoach.Api.Tests/Modules/Coaching/Eval/`:

- **`EvalTestBase`** — base class providing cached Sonnet (coaching) + Haiku (judging) clients via `DiskBasedReportingConfiguration`
- **`EVAL_CACHE_MODE`** env var: `Record` (call API, save responses), `Replay` (use committed fixtures, fail on miss), `Auto` (default — replay if fixture exists, record otherwise)
- **`ReplayGuardChatClient`** — `DelegatingChatClient` that throws descriptive errors on cache miss in Replay mode
- **`AnthropicStructuredOutputClient`** — bridges `ForJsonSchema()` to native Anthropic constrained decoding (SDK's IChatClient bridge silently drops schemas)
- **Evaluators:** `PlanConstraintEvaluator` (deterministic checks) + `SafetyRubricEvaluator` (LLM-as-judge with structured verdict output)
- **CI runs in Replay mode** — zero API calls, uses committed fixtures in `tests/eval-cache/`
- To re-record fixtures: `ANTHROPIC_API_KEY=... EVAL_CACHE_MODE=Record dotnet test` (or use `tests/scripts/rerecord-eval-cache.sh`)

## Build & Test Commands

Run from `backend/`:

- `dotnet build` — build all projects
- `dotnet test` — run all tests
- `dotnet restore --force` — force NuGet restore

## Post-Change

See root CLAUDE.md checklist.
