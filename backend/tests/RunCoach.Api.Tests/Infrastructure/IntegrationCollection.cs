using System.Diagnostics.CodeAnalysis;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Serializes every test class that touches the assembly-scoped
/// <see cref="RunCoachAppFactory"/> (Testcontainers Postgres + Marten
/// <c>IDocumentStore</c> + Wolverine host). Without this, parallel
/// collection execution races on Marten's schema-migration advisory lock
/// ("Unable to attain a global lock in time") and intermittently triggers
/// <see cref="ObjectDisposedException"/> on <c>IDocumentStore</c> shutdown.
///
/// Pure-unit tests outside this collection continue to run in parallel.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit collection-definition marker types canonically end in 'Collection' to mirror the [Collection(name)] attribute applied to their members.")]
[CollectionDefinition("Integration", DisableParallelization = true)]
public sealed class IntegrationCollection;
