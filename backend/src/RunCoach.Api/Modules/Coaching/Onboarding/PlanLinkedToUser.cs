namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// DEC-060 / R-069 event that drives the EF UserProfile.CurrentPlanId update via the
/// UserProfileFromOnboardingProjection apply method. Appended to the onboarding stream by
/// the terminal-branch handler immediately after the new Plan stream is staged, and again
/// by the regenerate handler each time a fresh plan is generated.
/// </summary>
/// <param name="UserId">The authenticated user's id.</param>
/// <param name="PlanId">The newly generated plan's id.</param>
public sealed record PlanLinkedToUser(
    Guid UserId,
    Guid PlanId);
