# Backend — .NET 10 / ASP.NET Core

## Stack

See root CLAUDE.md for full tech stack. Additionally: Swashbuckle (OpenAPI).

## Module-First Organization

```
backend/
  src/
    RunCoach.Api/
      Program.cs
      Modules/
        {Domain}/                  # e.g., Workouts, Plans, Coaching, Users
          {Domain}Controller.cs
          {Domain}Service.cs
          I{Domain}Service.cs
          {Domain}Repository.cs
          I{Domain}Repository.cs
          Models/                  # DTOs, request/response types
          Entities/                # EF Core entities
          Extensions/              # Module-specific extension methods
        Shared/                    # Cross-cutting services
      Infrastructure/              # EF Core DbContext, Marten config, middleware
  tests/
    RunCoach.Api.Tests/
      Modules/                     # Mirrors src structure
```

When a module exceeds ~8 root files, create submodules (named folders within the module). Not all files need to be in a submodule — keep root files that don't fit a submodule at the root.

## Coding Standards

- **Primary constructors** when applicable: `public class MyService(IMyRepo repo) : IMyService { }`
- **Private fields** prefixed with `_` (e.g., `_memberVariable`)
- **Properties** initialized with default non-null values: `public string Name { get; set; } = string.Empty;`
- **One type per file** — classes, interfaces, enums, records, structs
- **Record types for DTOs** with `Dto` suffix (e.g., `WorkoutDto`, `CreatePlanRequestDto`)
- **Ternary operators** over if-else for simple conditional assignments
- **Async throughout** for all EF Core and I/O operations

## Dependency Injection

- Each module registers its own services via `Add{Module}Services()` extension method
- Program.cs calls each registration explicitly
- **Scoped lifetime** by default for services and repositories (anything in the request pipeline)
- **Singleton** only for stateless infrastructure (configuration wrappers, HTTP client factories)
- Controllers registered via `builder.Services.AddControllers()`

## Architecture Layers

- **Controllers** inherit from a shared base controller. Entry point only — delegate to services.
- **Services** contain business logic, injected into controllers. All services have interfaces.
- **Repositories** handle data access, injected into services. All repositories have interfaces.
- Shared base controller provides common error handling and response formatting.

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

- **xUnit + FluentAssertions + NSubstitute**
- Test projects **mirror source directory structure**
- **Arrange / Act / Assert** with comment markers
- Prefix expected values with `expected`, actual with `actual`
- `[Theory]` + `[InlineData]` for parameterized scenarios; separate `[Fact]` for conceptually different scenarios
- **Unit tests** for all public methods in services and computation layer
- **Integration tests** via `WebApplicationFactory` + **Testcontainers** (real PostgreSQL, not in-memory)
- Integration tests validate the **full response contract** with deep equality (exclude audit fields)
- Test file naming: `{ClassName}Tests.cs` (unit), `{ClassName}IntegrationTests.cs` (integration)

## Build & Test Commands

Run from `backend/`:

- `dotnet build` — build all projects
- `dotnet test` — run all tests
- `dotnet restore --force` — force NuGet restore

## Post-Change

See root CLAUDE.md checklist.
