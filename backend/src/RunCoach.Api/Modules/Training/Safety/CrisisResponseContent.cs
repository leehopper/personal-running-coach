namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// The versioned, scripted (non-LLM) coach response surfaced when
/// <see cref="ISafetyGate"/> classifies a log as <see cref="SafetyTier.Red"/>
/// with <see cref="ReferralCategory.Crisis"/> (Slice 3 Unit 3 / DEC-079, US
/// locale, MVP-0). Deterministic system-authored copy — it must NOT pass
/// through the prompt sanitizer (the literal ampersand in
/// "988 Suicide &amp; Crisis Lifeline" would be escaped). The exact resource
/// strings <c>988 Suicide &amp; Crisis Lifeline</c> and
/// <c>Crisis Text Line: text 741741</c> are contractually required and asserted
/// verbatim by tests.
/// </summary>
/// <remarks>
/// Encodes the persona crisis protocol: acknowledge with
/// empathy, provide the two crisis resources, normalize help-seeking, never
/// probe for plans or methods, and do not continue the crisis topic. The full
/// pre-public-release safety scaffolding (PAR-Q+, expanded taxonomy) is deferred.
/// </remarks>
public static class CrisisResponseContent
{
    /// <summary>
    /// Content version stamped onto the emitted safety turn so audit replay can
    /// pin the active scripted copy. Bump on any wording change.
    /// </summary>
    public const string ContentVersion = "v1.0.0";

    /// <summary>
    /// The scripted Red-crisis coach turn. Contains the exact required strings
    /// <c>988 Suicide &amp; Crisis Lifeline</c> and <c>Crisis Text Line: text 741741</c>.
    /// </summary>
    public const string CrisisResponse =
        "It sounds like you're carrying something really heavy right now, and I'm "
        + "glad you put it into words. This matters more than any run, and you "
        + "deserve real support from people trained to help. Please reach out right "
        + "now: the 988 Suicide & Crisis Lifeline (call or text 988), or the "
        + "Crisis Text Line: text 741741. Reaching out is a strong, healthy thing "
        + "to do, and you don't have to do it alone. I'm your running coach, so this "
        + "is outside what I can help with — but the people at those numbers are "
        + "there for you any time, day or night. I'll be right here for your "
        + "training whenever you're ready to come back to it.";
}
