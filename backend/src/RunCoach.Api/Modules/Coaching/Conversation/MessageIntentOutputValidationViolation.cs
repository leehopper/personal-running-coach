namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Pattern-B-invariant violation taxonomy for <see cref="MessageIntentOutput"/>. Stable
/// enum values — never reorder.
/// </summary>
public enum MessageIntentOutputValidationViolation
{
    /// <summary>The invariant holds — output is valid.</summary>
    None = 0,

    /// <summary>
    /// The populated slot (or its absence) does not match the discriminator: the
    /// <c>workout_log</c> slot is set for a non-WorkoutLog intent, is null for a
    /// WorkoutLog intent, or the intent is an unknown value.
    /// </summary>
    SlotIntentMismatch = 1,
}
