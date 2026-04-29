namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Stream-creation event. Emitted exactly once per user when the first turn lands.
/// </summary>
/// <param name="UserId">The authenticated user's id; doubles as the per-user stream id.</param>
/// <param name="StartedAt">Wall-clock time the stream was opened.</param>
public sealed record OnboardingStarted(
    Guid UserId,
    DateTimeOffset StartedAt);
