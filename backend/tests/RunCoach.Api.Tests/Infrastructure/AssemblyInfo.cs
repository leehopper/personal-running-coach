using RunCoach.Api.Tests.Infrastructure;

// One PostgreSqlContainer per test assembly (R-046 pattern). The fixture is
// created before any test runs and disposed after the last test completes;
// tests and the `DbBackedIntegrationTestBase` receive it via constructor
// injection, so no per-class IClassFixture boilerplate is needed.
[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]

// Parallelization policy:
//
// - Pure-unit tests (the bulk of the suite) run in parallel — xUnit v3's
//   default. This is what kept the suite under a few seconds before the
//   integration scaffolding landed.
// - Tests touching the shared `RunCoachAppFactory` (Testcontainers Postgres
//   + Marten `IDocumentStore` + Wolverine host) opt into the `Integration`
//   collection — `[CollectionDefinition("Integration",
//   DisableParallelization = true)]` in `IntegrationCollection.cs`. That
//   serializes them among themselves so they no longer race on Marten's
//   schema-migration advisory lock or trigger `ObjectDisposedException` on
//   `IDocumentStore` shutdown.
// - Eval tests opt into the `Eval` collection so concurrent reads/writes
//   against the disk-backed `DiskBasedReportingConfiguration` cache don't
//   race.
//
// The previous assembly-level `[CollectionBehavior(DisableTestParallelization
// = true)]` serialised the entire suite, regressing 1041 tests from a few
// seconds to over 90 seconds. The collection-scoped opt-in restores
// per-collection parallelism without re-introducing the integration races.
