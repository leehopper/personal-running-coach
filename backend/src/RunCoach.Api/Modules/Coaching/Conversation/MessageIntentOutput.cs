using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Pattern-B (DEC-058) structured-output contract for the interactive-conversation
/// intent classifier (Slice 4B, DEC-085 D3). A single byte-stable schema with a
/// <see cref="MessageIntent"/> discriminator plus one nullable typed slot
/// (<see cref="WorkoutLog"/>): the slot is non-null exactly when the intent is
/// <see cref="MessageIntent.WorkoutLog"/>; <see cref="MessageIntent.Question"/> and
/// <see cref="MessageIntent.Ambiguous"/> fill no slot.
/// </summary>
/// <remarks>
/// Anthropic constrained decoding cannot express "the slot is non-null exactly when
/// the discriminator is WorkoutLog" (it rejects <c>oneOf</c>), so
/// <see cref="MessageIntentOutputValidator"/> enforces that invariant at the .NET
/// boundary after deserialization. The classifier runs on a Haiku binding via the
/// per-call model override (PR3a); it cannot pin a sampling temperature (the SDK
/// deprecates it — DEC-085 § PR3a correction), so determinism rests on this
/// byte-stable schema plus the deterministic prompt.
/// </remarks>
public sealed record MessageIntentOutput
{
    /// <summary>
    /// Gets the resolved intent of the runner's message. The <see cref="WorkoutLog"/>
    /// slot is non-null exactly when this is <see cref="MessageIntent.WorkoutLog"/>.
    /// </summary>
    [Description("The intent of the runner's message: Question (asking something), WorkoutLog (describing a completed workout to log), or Ambiguous (unclear or under-specified — ask for confirmation). Populate the workout_log slot only for WorkoutLog.")]
    public required MessageIntent Intent { get; init; }

    /// <summary>
    /// Gets the extracted workout draft when <see cref="Intent"/> is
    /// <see cref="MessageIntent.WorkoutLog"/>; null otherwise.
    /// </summary>
    [Description("The extracted workout draft when the intent is WorkoutLog: actuals only (date, distance in meters, duration in seconds, completion status, optional note). Null for Question and Ambiguous. Only populate this if the date, distance, and duration are all clear from the message; otherwise classify as Ambiguous.")]
    public required StructuredLogDraft? WorkoutLog { get; init; }
}
