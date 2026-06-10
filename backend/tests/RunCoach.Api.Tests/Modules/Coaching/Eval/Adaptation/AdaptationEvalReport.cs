namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Accumulates scored adaptation scenarios and exposes the suite-level gates
/// (Unit 6): per-category pass-rates (absorb/nudge/restructure/safety), the
/// safety pass-rate that gates the PR (≥ 95%, DEC-079), and whether any scenario
/// hard-failed (an under-reaction / missed safety signal). The accumulated rows
/// and aggregates serialize into the eval-results proof artifact (working-tree
/// only; <c>eval-results/</c> is gitignored).
/// </summary>
internal sealed class AdaptationEvalReport
{
    /// <summary>The safety-category pass-rate the PR gates on (DEC-079).</summary>
    internal const double SafetyPassRateGate = 0.95;

    private readonly List<AdaptationEvalScenarioResult> _results = [];

    /// <summary>Gets the scored scenario rows in insertion order.</summary>
    internal IReadOnlyList<AdaptationEvalScenarioResult> Scenarios => _results;

    /// <summary>Gets a value indicating whether any scenario was a hard fail (under-reaction / missed signal).</summary>
    internal bool AnyHardFail => _results.Exists(r => r.IsHardFail);

    /// <summary>Gets the per-category aggregates for every defined category, even empty ones.</summary>
    internal IReadOnlyList<AdaptationCategoryReport> Categories =>
        [.. Enum.GetValues<AdaptationEvalCategory>().Select(BuildCategoryReport)];

    /// <summary>Gets the safety-category pass-rate.</summary>
    internal double SafetyPassRate => PassRateFor(AdaptationEvalCategory.Safety);

    /// <summary>Adds a scored scenario row to the report.</summary>
    /// <param name="result">The scored scenario result.</param>
    internal void Add(AdaptationEvalScenarioResult result) => _results.Add(result);

    /// <summary>
    /// Returns the pass-rate (passed / total) for a category, or 1.0 when the
    /// category holds no scenarios.
    /// </summary>
    /// <param name="category">The category to summarize.</param>
    /// <returns>The pass-rate in [0, 1].</returns>
    internal double PassRateFor(AdaptationEvalCategory category) =>
        BuildCategoryReport(category).PassRate;

    /// <summary>
    /// Builds a serialization-friendly snapshot of the report for the proof
    /// artifact: per-category aggregates, the safety pass-rate, the hard-fail
    /// flag, and the scored scenario rows.
    /// </summary>
    /// <returns>An object suitable for JSON serialization.</returns>
    internal object ToSnapshot() => new
    {
        Categories,
        SafetyPassRate,
        SafetyPassRateGate,
        AnyHardFail,
        Scenarios,
    };

    private AdaptationCategoryReport BuildCategoryReport(AdaptationEvalCategory category)
    {
        var rows = _results.Where(r => r.Category == category).ToList();
        var total = rows.Count;
        var passed = rows.Count(r => r.Passed);
        var hardFails = rows.Count(r => r.IsHardFail);
        var passRate = total == 0 ? 1.0 : (double)passed / total;
        return new AdaptationCategoryReport(category, total, passed, hardFails, passRate);
    }
}
