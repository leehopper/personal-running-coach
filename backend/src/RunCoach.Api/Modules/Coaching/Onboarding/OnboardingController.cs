using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Training.Plan;
using Wolverine;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Onboarding endpoints — read current state and submit the structured form
/// answers (DEC-086 form-first intake). Per Slice 1 § Unit 1 / DEC-055
/// every state-changing route is gated
/// by both <see cref="AuthPolicies.CookieOrBearer"/> and
/// <see cref="Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute"/>;
/// the read endpoint requires auth but no antiforgery token (idempotent GETs
/// are out of scope for the framework's antiforgery middleware).
/// </summary>
[ApiController]
[Route("api/v1/onboarding")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed partial class OnboardingController(
    IMessageBus bus,
    IDocumentStore store,
    ILogger<OnboardingController> logger) : ControllerBase
{
    private const string AlreadyCompleteType = "https://runcoach.app/problems/onboarding-already-complete";
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";
    private const string InvalidAnswersType = "https://runcoach.app/problems/invalid-onboarding-answers";
    private const string PlanGenerationFailedType = "https://runcoach.app/problems/onboarding-plan-generation-failed";
    private const string StateUnavailableType = "https://runcoach.app/problems/onboarding-state-unavailable";

    /// <summary>GET /api/v1/onboarding/state — read the current onboarding view.</summary>
    /// <remarks>
    /// Returns 404 before the runner has submitted any turn (no stream); 200
    /// with the projection otherwise. The completion gate's outcome is
    /// surfaced as <c>isComplete</c> on the response so the chat UI can
    /// distinguish "all six topics captured but a clarification is still
    /// outstanding" from "everything captured AND no outstanding clarifications".
    /// </remarks>
    [HttpGet("state")]
    [ProducesResponseType(typeof(OnboardingStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetState(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Marten is configured with TenancyStyle.Conjoined; the OnboardingView
        // and the EF projection are both tenant-scoped to the runner's user id.
        // Open a per-request tenanted session — Wolverine middleware does the
        // same thing in the SubmitAnswers (POST /answers) dispatch path, but
        // GetState reads directly off Marten and has to set the tenant explicitly.
        await using var session = store.LightweightSession(userId.ToString());
        var view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            return NotFound();
        }

        return Ok(BuildStateDto(view));
    }

    /// <summary>POST /api/v1/onboarding/answers — submit structured onboarding answers (form-first intake).</summary>
    /// <remarks>
    /// The deterministic, form-first intake (DEC-086 D1). Validates the submitted topics, dispatches a
    /// <see cref="SubmitStructuredAnswers"/> command to <see cref="SubmitStructuredAnswersHandler"/>
    /// (one <see cref="AnswerCaptured"/> per topic, gate, inline plan generation — no LLM call), then
    /// returns the reloaded onboarding view. When the completion gate is satisfied the response carries
    /// the generated plan id and <c>isComplete = true</c>; otherwise it reflects partial progress and
    /// the client stays on the form (<c>GET /state</c> remains the resume contract).
    /// </remarks>
    [HttpPost("answers")]
    [Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryToken]
    [ProducesResponseType(typeof(OnboardingStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitAnswers(
        [FromBody] SubmitStructuredAnswersRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Deterministic validation of untrusted input before any transaction opens. The loosened wire
        // DTOs bind without throwing, so an out-of-range numeric surfaces here as a 400 rather than an
        // uncatchable 500 during model binding.
        if (!SubmitStructuredAnswersRequestMapper.TryMap(request, userId, out var command, out var validationError))
        {
            return Problem(
                type: InvalidAnswersType,
                title: "The submitted onboarding answers are invalid.",
                detail: validationError,
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            _ = await bus
                .InvokeForTenantAsync<SubmitStructuredAnswersResult>(userId.ToString(), command!, ct)
                .ConfigureAwait(false);
        }
        catch (OnboardingAlreadyCompleteException ex)
        {
            LogAlreadyComplete(logger, ex.UserId);
            return Problem(
                type: AlreadyCompleteType,
                title: "Onboarding is already complete.",
                detail: "Regenerate your plan via Settings → Plan instead of resubmitting the onboarding form.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (PlanGenerationRejectedException ex)
        {
            // The gate was satisfied but the generated macro plan failed validation (F3 / DEC-082).
            // Wolverine aborted the transaction — nothing staged, the form is re-submittable. Surface a
            // handled 422 with a retry affordance rather than a 500 so monitoring sees no unhandled
            // exception.
            LogPlanGenerationRejected(logger, userId, ex.Violation);
            return PlanCouldNotBeBuilt();
        }
        catch (MesoMicroConsistencyRejectedException ex)
        {
            // Same terminal-rejection posture as the macro case, but the generated week-1 workouts
            // disagreed with the meso week template after the bounded consistency retry (F-LIVE-2 /
            // DEC-088). Nothing staged, the form is re-submittable — surface the identical handled 422.
            LogPlanConsistencyRejected(logger, userId, ex.Violation);
            return PlanCouldNotBeBuilt();
        }

        // Reload the committed projection so the response reflects authoritative state — the
        // handler always bootstraps + appends, so the view must exist post-commit.
        await using var session = store.LightweightSession(userId.ToString());
        var view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            LogAnswersStateUnavailable(logger, userId);
            return Problem(
                type: StateUnavailableType,
                title: "Onboarding state could not be read after submission.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Ok(BuildStateDto(view));
    }

    private static OnboardingStateDto BuildStateDto(OnboardingView view)
    {
        var (completed, total) = OnboardingCompletionGate.Progress(view);
        return new OnboardingStateDto(
            UserId: view.UserId,
            Status: view.Status,
            CurrentTopic: view.CurrentTopic,
            CompletedTopics: completed,
            TotalTopics: total,
            IsComplete: OnboardingCompletionGate.IsSatisfied(view) && view.Status == OnboardingStatus.Completed,
            OutstandingClarifications: view.OutstandingClarifications,
            PrimaryGoal: view.PrimaryGoal,
            TargetEvent: view.TargetEvent,
            CurrentFitness: view.CurrentFitness,
            WeeklySchedule: view.WeeklySchedule,
            InjuryHistory: view.InjuryHistory,
            Preferences: view.Preferences,
            CurrentPlanId: view.CurrentPlanId);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Onboarding submission rejected: stream already complete user={UserId}")]
    private static partial void LogAlreadyComplete(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Plan generation rejected during onboarding completion: UserId={UserId} Violation={Violation}")]
    private static partial void LogPlanGenerationRejected(ILogger logger, Guid userId, MacroPlanOutputValidationViolation violation);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Plan generation rejected during onboarding completion (meso/micro consistency): UserId={UserId} Violation={Violation}")]
    private static partial void LogPlanConsistencyRejected(ILogger logger, Guid userId, MesoMicroConsistencyViolation violation);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Onboarding view was null after a successful structured-answers submission: UserId={UserId}")]
    private static partial void LogAnswersStateUnavailable(ILogger logger, Guid userId);

    /// <summary>
    /// The 422 returned when plan generation is terminally rejected during onboarding completion —
    /// shared by the macro-validation (<see cref="PlanGenerationRejectedException"/>) and the
    /// meso/micro consistency (<see cref="MesoMicroConsistencyRejectedException"/>) rejection paths,
    /// which surface an identical re-submittable envelope.
    /// </summary>
    private ObjectResult PlanCouldNotBeBuilt() =>
        Problem(
            type: PlanGenerationFailedType,
            title: "We couldn't build a plan from your answers.",
            detail: "Please submit again.",
            statusCode: StatusCodes.Status422UnprocessableEntity);

    private bool TryGetUserId(out Guid userId)
    {
        // Cookie auth writes ClaimTypes.NameIdentifier; raw JWTs carry the
        // subject as `sub`. Program.cs disables inbound claim mapping, so the
        // bearer-authenticated branch lands on `sub` only. See AuthController
        // for the full rationale.
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out userId))
        {
            userId = Guid.Empty;
            return false;
        }

        return true;
    }

    private ObjectResult MissingUserClaim() =>
        Problem(
            type: MissingUserType,
            title: "Authenticated user id was missing or malformed.",
            statusCode: StatusCodes.Status401Unauthorized);
}
