using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Test <see cref="IPlanGenerationService"/> that throws a terminal plan-generation rejection on its
/// FIRST <see cref="GeneratePlanAsync"/> call and delegates to a wrapped success-path service on
/// every subsequent call. Used by the onboarding endpoint integration tests to prove the onboarding
/// completion turn maps a terminal plan-generation rejection to a handled 422 (with nothing staged)
/// AND that the same turn is re-submittable to succeed. The rejection defaults to a macro-validation
/// <see cref="PlanGenerationRejectedException"/> (F3 / DEC-082); pass an explicit exception to
/// exercise the sibling meso/micro consistency rejection path (F-LIVE-2 / DEC-088).
/// </summary>
/// <remarks>
/// Registered as a SINGLETON so the call counter persists across the two HTTP requests; a scoped
/// registration would reset the counter per request and never reach the success branch.
/// </remarks>
public sealed class RejectOnceThenSucceedPlanGenerationService(
    IPlanGenerationService success,
    Exception? rejection = null)
    : IPlanGenerationService
{
    private readonly IPlanGenerationService _success = success;
    private readonly Exception _rejection =
        rejection ?? new PlanGenerationRejectedException(MacroPlanOutputValidationViolation.HorizonMismatch);

    private int _calls;

    public Task<PlanEventSequence> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct)
    {
        if (Interlocked.Increment(ref _calls) == 1)
        {
            throw _rejection;
        }

        return _success.GeneratePlanAsync(profileSnapshot, userId, planId, intent, previousPlanId, ct);
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
        throw new NotSupportedException(
            "GenerateWeekAsync (the rolling-horizon extension seam, DEC-090) is not driven through " +
            "the Wolverine bus until PR2; this stub covers only the bootstrap/regenerate flows " +
            "exercised via GeneratePlanAsync.");
    }
}
