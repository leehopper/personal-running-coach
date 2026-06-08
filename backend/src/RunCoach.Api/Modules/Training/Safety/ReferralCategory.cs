namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// The kind of safety signal that drove a non-Green <see cref="SafetyTier"/>,
/// used to route the scripted referral content (Slice 3 / DEC-079 high-risk
/// subset). Values are explicitly numbered so reordering members does not shift
/// any stored or serialized integer encoding; <see cref="None"/> is <c>0</c> so
/// it pairs with the default <see cref="SafetyTier.Green"/>. The exhaustive
/// DEC-030 taxonomy (full pregnancy / youth / chronic breadth) is deferred to
/// the pre-public-release gate.
/// </summary>
public enum ReferralCategory
{
    /// <summary>No safety signal. Pairs with <see cref="SafetyTier.Green"/>.</summary>
    None = 0,

    /// <summary>
    /// Self-harm or suicidal ideation. Pairs with <see cref="SafetyTier.Red"/> and
    /// the scripted crisis turn (988 / Crisis Text Line).
    /// </summary>
    Crisis = 1,

    /// <summary>
    /// Urgent medical signal needing immediate professional care — cardiac
    /// symptoms, femoral-neck / groin pain, pregnancy bleeding or contractions.
    /// Pairs with <see cref="SafetyTier.Red"/> (stop-and-refer).
    /// </summary>
    EmergencyReferral = 2,

    /// <summary>
    /// Sharp / persistent / worsening pain that stopped or limited the run.
    /// Pairs with <see cref="SafetyTier.Amber"/> (refuse-to-increase + referral).
    /// </summary>
    Injury = 3,

    /// <summary>
    /// Relative-energy-deficiency / disordered-pattern signal — amenorrhea,
    /// running through injury, "earn my food" / "run it off", rest-day distress.
    /// Pairs with <see cref="SafetyTier.Amber"/> (refuse-to-increase + referral).
    /// </summary>
    RedS = 4,
}
