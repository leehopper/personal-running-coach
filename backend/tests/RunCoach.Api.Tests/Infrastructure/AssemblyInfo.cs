using RunCoach.Api.Tests.Infrastructure;

// One PostgreSqlContainer per test assembly (R-046 pattern). The fixture is
// created before any test runs and disposed after the last test completes;
// tests and the `DbBackedIntegrationTestBase` receive it via constructor
// injection, so no per-class IClassFixture boilerplate is needed.
[assembly: AssemblyFixture(typeof(RunCoachAppFactory))]

// Disable parallel test-collection execution for the entire assembly.
//
// Rationale: integration tests share the assembly-scoped `RunCoachAppFactory`,
// which boots one Testcontainers Postgres + Marten `IDocumentStore` +
// Wolverine host for the whole suite. Under xUnit v3's default
// parallel-by-collection scheduling, multiple integration test classes
// race on the shared `IDocumentStore`'s schema-migration advisory lock and
// the Marten async daemon, producing intermittent failures (different set
// of 9-11 failed tests per run) with two recurring signatures:
//   - "Unable to attain a global lock in time order to apply database changes"
//   - `ObjectDisposedException` on `IDocumentStore` shutdown
//
// Sequential collection execution eliminates the race deterministically.
// Tests within a collection still run sequentially by default in xUnit; this
// attribute additionally serializes execution *across* collections so the
// shared SUT is only touched by one collection at a time.
//
// Trade-off: total wall-clock test time grows, but the suite is reliable.
// If suite runtime becomes a concern, the future fix is to partition the
// shared SUT into per-collection isolated databases or schemas (deferred —
// see RunCoach task #117 description for context).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
