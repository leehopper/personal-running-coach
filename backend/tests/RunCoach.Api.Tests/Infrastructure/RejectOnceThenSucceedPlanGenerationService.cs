using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Test <see cref="IPlanGenerationService"/> that throws a
/// <see cref="PlanGenerationRejectedException"/> on its FIRST
/// <see cref="GeneratePlanAsync"/> call and delegates to a wrapped success-path service on every
/// subsequent call. Used by <c>OnboardingCompletionRejectionIntegrationTests</c> to prove the
/// onboarding completion turn maps a terminal plan-generation rejection to an HTTP-200 error
/// envelope (with nothing staged) AND that the same turn is re-submittable to succeed.
/// </summary>
/// <remarks>
/// Registered as a SINGLETON so the call counter persists across the two HTTP requests; a scoped
/// registration would reset the counter per request and never reach the success branch.
/// </remarks>
public sealed class RejectOnceThenSucceedPlanGenerationService(IPlanGenerationService success)
    : IPlanGenerationService
{
    private readonly IPlanGenerationService _success = success;
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
            throw new PlanGenerationRejectedException(MacroPlanOutputValidationViolation.HorizonMismatch);
        }

        return _success.GeneratePlanAsync(profileSnapshot, userId, planId, intent, previousPlanId, ct);
    }
}
