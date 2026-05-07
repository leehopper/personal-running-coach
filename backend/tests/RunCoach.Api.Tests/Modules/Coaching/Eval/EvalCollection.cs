using System.Diagnostics.CodeAnalysis;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Serializes every test class that hits
/// <c>DiskBasedReportingConfiguration</c>. The eval cache JSON files live on
/// disk and are read/written by the M.E.AI.Evaluation reporting layer on
/// every test invocation. Parallel access produced sporadic file-locked /
/// duplicate-fixture issues even in Replay mode, so the eval suite runs
/// sequentially within its own collection while non-eval tests parallelize.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit collection-definition marker types canonically end in 'Collection' to mirror the [Collection(name)] attribute applied to their members.")]
[CollectionDefinition("Eval", DisableParallelization = true)]
public sealed class EvalCollection;
