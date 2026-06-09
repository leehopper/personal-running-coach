namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// The versioned, scripted (non-LLM) coach response surfaced when
/// <see cref="ISafetyGate"/> classifies a log as <see cref="SafetyTier.Red"/>
/// with <see cref="ReferralCategory.EmergencyReferral"/> — cardiac symptoms,
/// femoral-neck / hip-groin pain worse with activity, pregnancy bleeding or
/// contractions (Slice 3 Unit 3 / DEC-079, US locale, MVP-0). Deterministic
/// stop-and-refer copy, distinct from <see cref="CrisisResponseContent"/>: an
/// urgent medical signal must direct the runner to immediate professional
/// care, never to the mental-health crisis lines. System-authored — it must
/// NOT pass through the prompt sanitizer.
/// </summary>
/// <remarks>
/// Encodes the persona stop-and-refer protocol: acknowledge with empathy,
/// stop training immediately, direct to urgent professional evaluation, never
/// diagnose or minimize, and do not continue coaching on the symptom. The full
/// pre-public-release safety scaffolding (PAR-Q+, expanded taxonomy) is
/// deferred.
/// </remarks>
public static class EmergencyResponseContent
{
    /// <summary>
    /// Content version stamped onto the emitted safety turn so audit replay can
    /// pin the active scripted copy. Bump on any wording change.
    /// </summary>
    public const string ContentVersion = "v1.0.0";

    /// <summary>
    /// The scripted Red emergency-referral coach turn: stop now, get checked
    /// out by a professional, training waits.
    /// </summary>
    public const string EmergencyResponse =
        "What you're describing is outside anything a training adjustment can help "
        + "with, and it needs prompt medical attention. Please stop running now and "
        + "get evaluated by a medical professional right away — and if the symptoms "
        + "are severe or getting worse, call 911 or go to emergency care immediately. "
        + "I can't assess this for you, and guessing would be the wrong move. Your "
        + "plan will keep — once a professional has cleared you, I'll be right here "
        + "to pick your training back up safely.";
}
