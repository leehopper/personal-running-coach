namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Discriminator for <see cref="OnboardingTurnResponseDto"/> indicating whether the turn was
/// answered with another question ('Ask'), completed onboarding ('Complete'), or was terminally
/// rejected ('Error').
/// Values are explicitly numbered for stable JSON wire encoding.
/// </summary>
public enum OnboardingTurnKind
{
    /// <summary>The handler asked the runner another question; onboarding is not yet complete.</summary>
    Ask = 0,

    /// <summary>Onboarding is complete and a plan has been generated.</summary>
    Complete = 1,

    /// <summary>Plan generation was terminally rejected (F3); the error fields apply and nothing was staged.</summary>
    Error = 2,
}
