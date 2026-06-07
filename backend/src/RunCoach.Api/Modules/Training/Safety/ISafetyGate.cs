namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// Deterministic keyword/threshold classifier that resolves a logged workout's
/// free-text into a <see cref="SafetyClassification"/> (Green / Amber / Red +
/// <see cref="ReferralCategory"/>) <b>before</b> any LLM call (Slice 3 Unit 3 /
/// DEC-019 / DEC-030 / DEC-079). Safety detection is keyword-based and never
/// LLM self-policing.
/// </summary>
public interface ISafetyGate
{
    /// <summary>
    /// Classifies the user-authored free-text of a logged workout (the note and
    /// any free-text metric values) into a <see cref="SafetyClassification"/>.
    /// Highest tier wins. Pure and deterministic.
    /// </summary>
    /// <param name="notes">
    /// The runner's free-text note. Expected to have already passed through the
    /// DEC-059 sanitizer / Unicode normalization at the caller boundary; the
    /// gate matches case-insensitively and does not re-sanitize.
    /// </param>
    /// <param name="metrics">
    /// The logged metrics as canonical wire key → display value. Only the
    /// free-text values (weather / terrain) are scanned; numeric metrics are
    /// not prose and are ignored. May be <c>null</c>.
    /// </param>
    /// <returns>The resolved classification; <see cref="SafetyClassification.Green"/> when nothing matched.</returns>
    SafetyClassification Classify(string? notes, IReadOnlyDictionary<string, string>? metrics);
}
