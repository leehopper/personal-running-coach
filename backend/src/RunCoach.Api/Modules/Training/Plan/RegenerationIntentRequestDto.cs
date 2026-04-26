namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Wire shape for the optional <c>intent</c> block on
/// <see cref="RegeneratePlanRequestDto"/>. Captured as its own DTO so the wire
/// contract stays decoupled from the internal
/// <see cref="Coaching.Models.RegenerationIntent"/> domain record (which holds
/// the post-sanitization, delimiter-wrapped payload — never user-supplied wire
/// bytes).
/// </summary>
/// <param name="FreeText">
/// Raw free-text supplied by the runner (e.g. "I'm coming back from a calf
/// strain"). Capped at
/// <see cref="Coaching.Models.RegenerationIntent.RawMaxFreeTextLength"/>
/// characters by the controller before sanitization runs.
/// </param>
public sealed record RegenerationIntentRequestDto(
    string FreeText);
