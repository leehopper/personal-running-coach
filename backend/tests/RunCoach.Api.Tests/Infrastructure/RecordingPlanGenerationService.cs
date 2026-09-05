using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Test decorator that records the onboarding view supplied to plan generation and forwards the
/// call to the wrapped deterministic service.
/// </summary>
public sealed class RecordingPlanGenerationService(IPlanGenerationService inner) : IPlanGenerationService
{
    private readonly IPlanGenerationService _inner = inner;

    public OnboardingView? LastOnboardingView { get; private set; }

    public Task<PlanEventSequence> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct)
    {
        LastOnboardingView = profileSnapshot;
        return _inner.GeneratePlanAsync(profileSnapshot, userId, planId, intent, previousPlanId, ct);
    }

    public Task<WeekGenerationResult> GenerateWeekAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        MacroPlanOutput macro,
        DateOnly planStartDate,
        DateOnly? targetEventDate,
        int targetWeekIndex,
        MesoWeekOutput? existingMesoWeek,
        CancellationToken ct)
    {
        return _inner.GenerateWeekAsync(
            profileSnapshot,
            userId,
            planId,
            macro,
            planStartDate,
            targetEventDate,
            targetWeekIndex,
            existingMesoWeek,
            ct);
    }
}
