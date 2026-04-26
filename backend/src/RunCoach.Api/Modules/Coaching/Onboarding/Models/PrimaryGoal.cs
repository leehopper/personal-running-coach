namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// The runner's primary training goal. Values are explicitly numbered so reordering
/// members in source does not change Marten event payloads or JSON wire encoding.
/// </summary>
public enum PrimaryGoal
{
    /// <summary>Training for a specific race or event (paired with a TargetEvent answer).</summary>
    RaceTraining = 0,

    /// <summary>Building or maintaining general aerobic fitness without a specific race.</summary>
    GeneralFitness = 1,

    /// <summary>Returning to running after a layoff (injury, life event, off-season).</summary>
    ReturnToRunning = 2,

    /// <summary>Increasing weekly volume or comfort with longer distances.</summary>
    BuildVolume = 3,

    /// <summary>Improving speed at a chosen distance without a specific event date.</summary>
    BuildSpeed = 4,
}
