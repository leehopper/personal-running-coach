namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// The result of <see cref="ISafetyGate.Classify"/>: a <see cref="SafetyTier"/>
/// paired with the <see cref="ReferralCategory"/> that drove it. PII-free —
/// carries only the tier + category, never the matched note text.
/// </summary>
/// <remarks>
/// The constructor is <c>internal</c>; callers construct via <see cref="Green"/>,
/// <see cref="Amber"/>, or <see cref="Red"/> so contradictory pairings (a
/// non-Green tier with <see cref="ReferralCategory.None"/>, or a Green tier
/// carrying a category) cannot be expressed. Mirrors
/// <c>OnboardingTurnOutputValidationResult</c>.
/// </remarks>
public sealed record SafetyClassification
{
    /// <summary>
    /// Constructs a classification directly from its components. Internal so
    /// production callers route through the factories; the test assembly
    /// retains access via <c>InternalsVisibleTo</c>.
    /// </summary>
    /// <param name="tier">The resolved safety tier.</param>
    /// <param name="category">The referral category driving the tier.</param>
    internal SafetyClassification(SafetyTier tier, ReferralCategory category)
    {
        Tier = tier;
        Category = category;
    }

    /// <summary>Gets the resolved safety tier.</summary>
    public SafetyTier Tier { get; }

    /// <summary>Gets the referral category driving the tier (<see cref="ReferralCategory.None"/> when Green).</summary>
    public ReferralCategory Category { get; }

    /// <summary>Returns the no-signal result: <see cref="SafetyTier.Green"/> + <see cref="ReferralCategory.None"/>.</summary>
    public static SafetyClassification Green() => new(SafetyTier.Green, ReferralCategory.None);

    /// <summary>
    /// Returns an <see cref="SafetyTier.Amber"/> result for the given referral category.
    /// </summary>
    /// <param name="category">The driving category. Must not be <see cref="ReferralCategory.None"/>.</param>
    public static SafetyClassification Amber(ReferralCategory category) => Create(SafetyTier.Amber, category);

    /// <summary>
    /// Returns a <see cref="SafetyTier.Red"/> result for the given referral category.
    /// </summary>
    /// <param name="category">The driving category. Must not be <see cref="ReferralCategory.None"/>.</param>
    public static SafetyClassification Red(ReferralCategory category) => Create(SafetyTier.Red, category);

    private static SafetyClassification Create(SafetyTier tier, ReferralCategory category)
    {
        if (category == ReferralCategory.None)
        {
            throw new ArgumentException(
                $"{tier} requires a non-None referral category; use Green() for the no-signal case.",
                nameof(category));
        }

        // Enforce the tier-category pairing the enum docs assert: `Crisis` and
        // `EmergencyReferral` are always Red, `Injury` and `RedS` always Amber.
        // A mis-paired catalog rule fails construction rather than emitting a
        // contradictory classification.
        var validForTier = tier switch
        {
            SafetyTier.Red => category is ReferralCategory.Crisis or ReferralCategory.EmergencyReferral,
            SafetyTier.Amber => category is ReferralCategory.Injury or ReferralCategory.RedS,
            _ => false,
        };

        if (!validForTier)
        {
            throw new ArgumentException(
                $"{category} is not a valid referral category for {tier}.",
                nameof(category));
        }

        return new SafetyClassification(tier, category);
    }
}
