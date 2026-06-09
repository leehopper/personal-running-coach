namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// The versioned, scripted (non-LLM) referral turns surfaced alongside an Amber
/// adaptation when <see cref="ISafetyGate"/> classifies a log as
/// <see cref="SafetyTier.Amber"/> with <see cref="ReferralCategory.Injury"/> or
/// <see cref="ReferralCategory.RedS"/> (Slice 3 Unit 5 / DEC-079, US locale,
/// MVP-0). Deterministic system-authored copy routed by category — mirroring
/// <see cref="CrisisResponseContent"/> / <see cref="EmergencyResponseContent"/>
/// for the Red tiers — so the safety referral never depends on LLM prose.
/// System-authored — it must NOT pass through the prompt sanitizer.
/// </summary>
/// <remarks>
/// Encodes the persona refuse-to-increase + referral protocol: acknowledge with
/// empathy, point to the right professional, normalize help-seeking, never
/// diagnose or use controlling/clinical language — coaching continues (the
/// accompanying restructure carries the plan change). The full
/// pre-public-release safety scaffolding (PAR-Q+, expanded taxonomy) is
/// deferred.
/// </remarks>
public static class AmberReferralContent
{
    /// <summary>
    /// Content version stamped onto the emitted safety turn so audit replay can
    /// pin the active scripted copy. Bump on any wording change.
    /// </summary>
    public const string ContentVersion = "v1.0.0";

    /// <summary>
    /// The scripted Amber injury referral turn: pain that stops or limits a run
    /// warrants a professional look before load goes back up.
    /// </summary>
    public const string InjuryReferral =
        "Pain that's sharp enough to change how you run is your body asking for "
        + "backup, and listening to it now is the fastest way back. I've kept your "
        + "plan from adding any load while it's speaking up, and I'd really like "
        + "you to have it looked at by a physiotherapist or sports-medicine "
        + "professional — getting it checked early is how small things stay small. "
        + "I'll keep your training working around it in the meantime, and we'll "
        + "build back up once you're cleared.";

    /// <summary>
    /// The scripted Amber RED-S / disordered-pattern referral turn: fueling and
    /// rest are training, and energy balance deserves professional support.
    /// </summary>
    public const string RedSReferral =
        "I want to gently flag something I noticed: fueling and rest aren't "
        + "obstacles to your training — they ARE training. I've kept your plan "
        + "from adding any load right now, and I'd encourage you to talk with a "
        + "sports dietitian or your doctor about energy and recovery — they can "
        + "support you in ways a training plan can't, and reaching out is a "
        + "strong, healthy move. None of this changes how capable you are. I'm "
        + "here, and your plan will be ready to build again with you.";
}
