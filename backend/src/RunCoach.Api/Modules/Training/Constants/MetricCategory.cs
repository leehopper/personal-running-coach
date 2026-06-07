namespace RunCoach.Api.Modules.Training.Constants;

/// <summary>
/// Coaching-relevance category for a workout metric, used by the LLM-context
/// formatter to decide rendering policy (DEC-076 / slice-2b brainstorm D):
/// effort signals drive an explicit absence marker when missing, peripheral
/// and contextual metrics are silently omitted when absent.
/// </summary>
public enum MetricCategory
{
    /// <summary>
    /// Effort / intensity signals (heart rate, RPE). Their <em>absence</em> is
    /// itself coaching signal, so the formatter emits an explicit
    /// "(no HR/RPE)" marker when none are present.
    /// </summary>
    Effort = 0,

    /// <summary>
    /// Biomechanical detail (cadence, power, elevation gain, running dynamics).
    /// Rendered when present; silently omitted when absent (their absence is
    /// not coaching signal).
    /// </summary>
    Peripheral = 1,

    /// <summary>
    /// Surrounding context (calories, HRV, sleep / recovery scores, weather,
    /// terrain). Rendered when present; silently omitted when absent.
    /// </summary>
    Contextual = 2,
}
