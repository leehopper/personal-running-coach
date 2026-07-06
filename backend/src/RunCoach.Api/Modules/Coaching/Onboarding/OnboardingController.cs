using System.Security.Claims;
using System.Text.Json;
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
/// Slice 1 onboarding endpoints — submit a turn, read current state, revise an
/// answer. Per Slice 1 § Unit 1 / DEC-055 every state-changing route is gated
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
    private const string InvalidTopicType = "https://runcoach.app/problems/invalid-onboarding-topic";
    private const string InvalidAnswersType = "https://runcoach.app/problems/invalid-onboarding-answers";
    private const string PlanGenerationFailedType = "https://runcoach.app/problems/onboarding-plan-generation-failed";
    private const string StateUnavailableType = "https://runcoach.app/problems/onboarding-state-unavailable";

    /// <summary>POST /api/v1/onboarding/turns — submit a single onboarding turn.</summary>
    /// <remarks>
    /// Dispatches a <see cref="SubmitUserTurn"/> command to the
    /// <see cref="OnboardingTurnHandler"/>. The handler runs inside one Marten
    /// transaction; on success the response carries either the next assistant
    /// turn (kind=Ask) or the completion signal with a generated plan id
    /// (kind=Complete). Idempotency is handled by the handler — sending the
    /// same <see cref="OnboardingTurnRequestDto.IdempotencyKey"/> twice returns
    /// the byte-identical prior response and appends nothing.
    /// </remarks>
    [HttpPost("turns")]
    [Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryToken]
    [ProducesResponseType(typeof(OnboardingTurnResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitTurn(
        [FromBody] OnboardingTurnRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        try
        {
            // InvokeForTenantAsync sets the user id as Marten's conjoined
            // tenant on the handler's auto-applied IDocumentSession. Without
            // it, the handler's session has no TenantId and every Marten
            // operation against an OnboardingView / IdempotencyMarker (both
            // multi-tenant under TenancyStyle.Conjoined) fails to resolve.
            var response = await bus
                .InvokeForTenantAsync<OnboardingTurnResponseDto>(
                    userId.ToString(),
                    new SubmitUserTurn(userId, request.IdempotencyKey, request.Text ?? string.Empty),
                    ct)
                .ConfigureAwait(false);
            return Ok(response);
        }
        catch (OnboardingAlreadyCompleteException ex)
        {
            LogAlreadyComplete(logger, ex.UserId);
            return Problem(
                type: AlreadyCompleteType,
                title: "Onboarding is already complete.",
                detail: "Submit a regenerate request via Settings → Plan instead of a fresh turn.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (PlanGenerationRejectedException ex)
        {
            // Terminal, expected rejection of a well-formed but invalid generated plan (F3): the
            // Wolverine transaction already aborted (nothing staged; the turn is re-submittable).
            // Surface an HTTP-200 error envelope rather than a 500 so the client renders the message
            // + a retry affordance, and monitoring does not see an unhandled exception.
            LogPlanGenerationRejected(logger, userId, ex.Violation);

            // Tailor the copy to the violation: only a HorizonMismatch is about the event date.
            // A PhaseSumMismatch is an internal model-output inconsistency unrelated to the date,
            // so the event-date wording would be misleading — fall back to a neutral message.
            var errorMessage = ex.Violation switch
            {
                MacroPlanOutputValidationViolation.HorizonMismatch =>
                    "We couldn't build a plan that fits your event date. Please try submitting again.",
                _ =>
                    "We hit a problem building your plan. Please try submitting again.",
            };
            return Ok(OnboardingTurnResponseDto.Error(errorMessage));
        }
    }

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
        // same thing in the SubmitTurn dispatch path, but GetState reads
        // directly off Marten and has to set the tenant explicitly.
        await using var session = store.LightweightSession(userId.ToString());
        var view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            return NotFound();
        }

        return Ok(BuildStateDto(view));
    }

    /// <summary>POST /api/v1/onboarding/answers/revise — overwrite a captured answer.</summary>
    /// <remarks>
    /// Appends a fresh <see cref="AnswerCaptured"/> event to the runner's
    /// onboarding stream so the audit trail is preserved (the prior captured
    /// answer remains addressable in the event log). Returns the updated view.
    /// </remarks>
    [HttpPost("answers/revise")]
    [Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryToken]
    [ProducesResponseType(typeof(OnboardingStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviseAnswer(
        [FromBody] ReviseAnswerRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // System.Text.Json deserializes int-backed enums without range checks,
        // so a payload with `topic: 99` lands here as an OnboardingTopic with
        // value 99. Reject explicitly with a 400 ProblemDetails before any
        // event is appended — otherwise the inline projection's switch falls
        // through to InvalidOperationException at apply time, surfacing as a
        // 500 with no actionable error code for the client.
        if (!Enum.IsDefined(request.Topic))
        {
            return Problem(
                type: InvalidTopicType,
                title: "The supplied onboarding topic is not a recognized value.",
                detail: $"Topic value '{(int)request.Topic}' is outside the OnboardingTopic enum range.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Per-request tenanted session — same rationale as GetState. The
        // append + SaveChanges + reload must all run on the same session so
        // the inline projection re-materializes before the read.
        await using var session = store.LightweightSession(userId.ToString());
        var view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;

        // AnswerCaptured carries the normalized payload as a JsonDocument so
        // both projections can call JsonDocument.Deserialize<T>(). Wrap the
        // serialize in a `using` so the rented ArrayPool buffers are returned
        // after SaveChangesAsync writes the event bytes — the prior code
        // rented and leaked. The await/save happens inside the using block
        // so the document is still live during Marten's serialization.
        using var normalizedPayload = JsonSerializer.SerializeToDocument(request.NormalizedValue);
        session.Events.Append(
            userId,
            new AnswerCaptured(request.Topic, normalizedPayload, Confidence: 1.0, CapturedAt: now));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // Re-load post-revise so the response reflects the freshly applied
        // answer (the inline projection materializes alongside the append).
        view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
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
            // The gate was satisfied but the generated plan failed validation (F3 / DEC-082). Wolverine
            // aborted the transaction — nothing staged, the form is re-submittable. Surface a handled 422
            // with a retry affordance rather than a 500 so monitoring sees no unhandled exception.
            LogPlanGenerationRejected(logger, userId, ex.Violation);
            return Problem(
                type: PlanGenerationFailedType,
                title: "We couldn't build a plan from your answers.",
                detail: "Please submit again.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        // Reload the committed projection so the response reflects authoritative state (mirrors
        // ReviseAnswer). The handler always bootstraps + appends, so the view must exist post-commit.
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
        Message = "Onboarding turn rejected: stream already complete user={UserId}")]
    private static partial void LogAlreadyComplete(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Plan generation rejected during onboarding completion: UserId={UserId} Violation={Violation}")]
    private static partial void LogPlanGenerationRejected(ILogger logger, Guid userId, MacroPlanOutputValidationViolation violation);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Onboarding view was null after a successful structured-answers submission: UserId={UserId}")]
    private static partial void LogAnswersStateUnavailable(ILogger logger, Guid userId);

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
