using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Wire-input shape for the InjuryHistory topic on POST /api/v1/onboarding/answers. A loosened,
/// non-throwing counterpart to <see cref="InjuryHistoryAnswer"/> (see
/// <see cref="PrimaryGoalInputDto"/> for the rationale). <see cref="PastInjurySummary"/> is this
/// topic's free-text nuance field.
/// </summary>
/// <param name="HasActiveInjury">Whether the runner currently has an active injury or pain limiting training.</param>
/// <param name="ActiveInjuryDescription">Optional description of the active injury or limitation.</param>
/// <param name="PastInjurySummary">Optional summary of past injuries or recurring issues (the nuance field).</param>
public sealed record InjuryHistoryInputDto(
    [property: JsonRequired] bool HasActiveInjury,
    string? ActiveInjuryDescription,
    string? PastInjurySummary);
