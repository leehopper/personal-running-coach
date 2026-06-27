using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// A scripted, deterministic safety turn (Slice 4B PR4) emitted as <c>event: safety</c>:
/// the Red crisis / emergency response that short-circuits the LLM, or the Amber
/// referral that accompanies an answer. The content is system-authored (never LLM prose)
/// and bypasses the prompt sanitizer, so resource strings like
/// <c>988 Suicide &amp; Crisis Lifeline</c> reach the client verbatim
/// (DEC-019/DEC-030/DEC-079 — safety is never left to LLM self-policing).
/// </summary>
/// <param name="Content">The scripted safety copy.</param>
/// <param name="Tier">The safety tier that drove it (<see cref="SafetyTier.Red"/> or <see cref="SafetyTier.Amber"/>).</param>
/// <param name="Category">The referral category driving the content (Crisis / EmergencyReferral / Injury / RedS).</param>
public sealed record SafetyFrame(string Content, SafetyTier Tier, ReferralCategory Category) : IConversationFrame
{
    string IConversationFrame.EventName => "safety";
}
