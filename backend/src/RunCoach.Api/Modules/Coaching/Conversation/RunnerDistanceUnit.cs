namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The unit a runner stated a distance in, as captured verbatim by the intent
/// classifier (Slice 4B, DEC-085 D3). The LLM reports the runner's actuals in their
/// own units; the deterministic <see cref="WorkoutDraftUnitConverter"/> performs the
/// SI conversion server-side (REVIEW.md Architecture: distance conversions belong in
/// the unit-tested computation layer, never the LLM). Values are explicitly numbered
/// so reordering members never shifts the serialized integer encoding (matching the
/// <see cref="MessageIntent"/> / <see cref="Training.Adaptation.AdaptationKind"/>
/// convention).
/// </summary>
public enum RunnerDistanceUnit
{
    /// <summary>Distance stated in kilometers (e.g. "5 km").</summary>
    Kilometers = 0,

    /// <summary>Distance stated in miles (e.g. "3 miles").</summary>
    Miles = 1,

    /// <summary>Distance stated directly in meters (e.g. "400 m repeats").</summary>
    Meters = 2,
}
