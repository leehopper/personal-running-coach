namespace RunCoach.Api.Modules.Training.Constants;

/// <summary>
/// Display metadata for a single scalar workout metric: the human label and
/// compact-display unit used when the metric is inlined into a coaching
/// one-liner, plus its <see cref="MetricCategory"/>. Single-sourced on
/// <see cref="WorkoutMetricKeys.Metadata"/> so the LLM-context formatter and
/// the frontend metric-meta map render identical labels and cannot drift
/// (DEC-072 / DEC-076).
/// </summary>
/// <param name="Label">
/// The human label printed before the value (e.g. <c>"HR"</c>, <c>"cadence"</c>).
/// </param>
/// <param name="Unit">
/// The compact-display unit printed after the value (e.g. <c>"spm"</c>,
/// <c>"W"</c>), or the empty string when no unit is shown (HR, RPE, scores).
/// </param>
/// <param name="Category">The coaching-relevance category.</param>
public sealed record WorkoutMetricMetadata(string Label, string Unit, MetricCategory Category);
