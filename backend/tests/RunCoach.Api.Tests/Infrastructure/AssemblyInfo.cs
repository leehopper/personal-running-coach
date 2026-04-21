using RunCoach.Api.Tests.Infrastructure;

// One PostgreSqlContainer per test assembly (R-046 pattern). The fixture is
// created before any test runs and disposed after the last test completes;
// tests and the `DbBackedIntegrationTestBase` receive it via constructor
// injection, so no per-class IClassFixture boilerplate is needed.
[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]
