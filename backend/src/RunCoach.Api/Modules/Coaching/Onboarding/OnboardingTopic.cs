namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// The canonical six-topic onboarding state machine per DEC-047.
/// Topic order is fixed: PrimaryGoal → TargetEvent (skipped if PrimaryGoal != RaceTraining) →
/// CurrentFitness → WeeklySchedule → InjuryHistory → Preferences.
/// </summary>
/// <remarks>
/// The enum values are explicitly numbered so reordering members in source does not change
/// the on-the-wire integer representation used by Marten event payloads or System.Text.Json.
/// </remarks>
public enum OnboardingTopic
{
    /// <summary>The runner's primary training goal (race, general fitness, return-to-running, etc.).</summary>
    PrimaryGoal = 0,

    /// <summary>A specific target race event when the primary goal is race training.</summary>
    TargetEvent = 1,

    /// <summary>The runner's current fitness level (recent volume, longest run, recent race results).</summary>
    CurrentFitness = 2,

    /// <summary>The runner's available weekly training schedule (days, time per day, preferred slots).</summary>
    WeeklySchedule = 3,

    /// <summary>Injury history and current physical limitations.</summary>
    InjuryHistory = 4,

    /// <summary>Runner preferences (preferred terrain, units, intensity tolerance, etc.).</summary>
    Preferences = 5,
}
