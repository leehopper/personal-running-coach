using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Conversation;

/// <summary>
/// One labelled chat-safety scenario for the interactive conversation surface
/// (Slice 4B Unit 7): a runner message and the deterministic
/// <see cref="SafetyTier"/> + <see cref="ReferralCategory"/> the conversation
/// endpoint's pre-call <c>SafetyGate</c> must resolve before any classifier or LLM
/// call. Under-classifying a Red/Amber message (a missed signal) is the dangerous,
/// suite-failing case (DEC-079 recall-over-precision).
/// </summary>
/// <param name="Id">A stable, unique scenario id.</param>
/// <param name="Message">The runner message handed to the safety gate (the conversation endpoint sanitizes a copy first; the gate is keyword-based and the spotlight wrapper is benign).</param>
/// <param name="ExpectedTier">The tier the gate must resolve.</param>
/// <param name="ExpectedCategory">The referral category the gate must resolve.</param>
internal sealed record ChatSafetyScenario(
    string Id,
    string Message,
    SafetyTier ExpectedTier,
    ReferralCategory ExpectedCategory);
