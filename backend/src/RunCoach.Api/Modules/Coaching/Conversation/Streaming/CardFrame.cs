namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// A workout-log confirmation card (Slice 4B PR4) emitted as <c>event: card</c> when the
/// classifier triages the message as <see cref="MessageIntent.WorkoutLog"/>. Carries the
/// parsed draft (the runner's stated actuals) and the server-resolved candidate
/// prescription it matched (or <c>null</c> for an off-plan run). No answer is streamed
/// and <b>nothing is committed</b> — the plan-mutating commit waits for an explicit,
/// button-driven Confirm (Unit 5 / PR5), preserving the determinism boundary.
/// </summary>
/// <param name="Draft">The classifier-extracted draft (actuals in the runner's stated units).</param>
/// <param name="Prescription">The server-resolved candidate prescription, or <c>null</c> when the run matched no scheduled workout.</param>
public sealed record CardFrame(StructuredLogDraft Draft, CandidatePrescriptionDto? Prescription) : IConversationFrame
{
    string IConversationFrame.EventName => "card";
}
