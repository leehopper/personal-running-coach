namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The aggregated pass-rate for one <see cref="AdaptationEvalCategory"/>: the
/// scenario count, how many matched their ground truth exactly, how many were
/// hard fails, and the resulting pass-rate. Serialized into the eval report so
/// the suite reports per-category (absorb/nudge/restructure/safety) pass-rates.
/// </summary>
/// <param name="Category">The reporting category.</param>
/// <param name="Total">Total scenarios in this category.</param>
/// <param name="Passed">Scenarios that matched their ground truth exactly.</param>
/// <param name="HardFails">Scenarios that under-reacted or missed a safety signal.</param>
/// <param name="PassRate">Passed / Total (1.0 when the category has no scenarios).</param>
internal sealed record AdaptationCategoryReport(
    AdaptationEvalCategory Category,
    int Total,
    int Passed,
    int HardFails,
    double PassRate);
