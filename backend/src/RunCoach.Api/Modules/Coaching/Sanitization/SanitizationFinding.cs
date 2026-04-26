namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// A single sanitization-tier finding produced by <see cref="IPromptSanitizer"/>.
/// PII-free by construction: carries the pattern catalog id only, never the
/// matched text. Original input lives in Marten event-stream storage and joins
/// to telemetry via the parent coaching-turn id per R-068 § 9.
/// </summary>
public readonly record struct SanitizationFinding(
    SanitizationCategory Category,
    string PatternId,
    int OriginalLength,
    int SanitizedLength,
    bool Stripped);
