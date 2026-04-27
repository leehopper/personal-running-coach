using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
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
    IDocumentSession session,
    ILogger<OnboardingController> logger) : ControllerBase
{
    private const string AlreadyCompleteType = "https://runcoach.app/problems/onboarding-already-complete";
    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
            var response = await bus
                .InvokeAsync<OnboardingTurnResponseDto>(
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

        var view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            return NotFound();
        }

        var (completed, total) = OnboardingCompletionGate.Progress(view);
        var dto = new OnboardingStateDto(
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
        return Ok(dto);
    }

    /// <summary>POST /api/v1/onboarding/answers/revise — overwrite a captured answer.</summary>
    /// <remarks>
    /// Appends a fresh <see cref="AnswerCaptured"/> event to the runner's
    /// onboarding stream so the audit trail is preserved (the prior captured
    /// answer remains addressable in the event log). Returns the updated view.
    /// Validates <see cref="ReviseAnswerRequestDto.NormalizedValue"/> against
    /// the topic-specific answer DTO before appending; returns 400 if the
    /// payload is malformed or does not match the expected schema, preventing
    /// corrupt events from landing in the durable event stream.
    /// </remarks>
    [HttpPost("answers/revise")]
    [Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryToken]
    [ProducesResponseType(typeof(OnboardingStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviseAnswer(
        [FromBody] ReviseAnswerRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        var view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            return NotFound();
        }

        var validationError = ValidateNormalizedValue(request.Topic, request.NormalizedValue);
        if (validationError is not null)
        {
            return BadRequest(new ValidationProblemDetails(validationError));
        }

        var now = DateTimeOffset.UtcNow;
        session.Events.Append(
            userId,
            new AnswerCaptured(request.Topic, request.NormalizedValue, Confidence: 1.0, CapturedAt: now));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // Re-load post-revise so the response reflects the freshly applied
        // answer (the inline projection materializes alongside the append).
        view = await session.LoadAsync<OnboardingView>(userId, ct).ConfigureAwait(false);
        if (view is null)
        {
            return NotFound();
        }

        var (completed, total) = OnboardingCompletionGate.Progress(view);
        var dto = new OnboardingStateDto(
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
        return Ok(dto);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Onboarding turn rejected: stream already complete user={UserId}")]
    private static partial void LogAlreadyComplete(ILogger logger, Guid userId);

    /// <summary>
    /// Deserializes <paramref name="payload"/> against the topic-specific answer DTO
    /// and returns a non-null error dictionary when the payload is malformed or does
    /// not conform to the expected schema. Returns <c>null</c> when validation succeeds.
    /// Validates that the payload root is an object and that all required properties
    /// for the topic-specific DTO are present and non-null.
    /// </summary>
    private static Dictionary<string, string[]>? ValidateNormalizedValue(
        OnboardingTopic topic,
        JsonDocument payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string[]>
            {
                ["normalizedValue"] = [$"Payload must be a JSON object for topic '{topic}'."],
            };
        }

        try
        {
            var json = payload.RootElement.GetRawText();
            var isValid = topic switch
            {
                OnboardingTopic.PrimaryGoal => TryDeserializeValid<PrimaryGoalAnswer>(json),
                OnboardingTopic.TargetEvent => TryDeserializeValid<TargetEventAnswer>(json),
                OnboardingTopic.CurrentFitness => TryDeserializeValid<CurrentFitnessAnswer>(json),
                OnboardingTopic.WeeklySchedule => TryDeserializeValid<WeeklyScheduleAnswer>(json),
                OnboardingTopic.InjuryHistory => TryDeserializeValid<InjuryHistoryAnswer>(json),
                OnboardingTopic.Preferences => TryDeserializeValid<PreferencesAnswer>(json),
                _ => false,
            };

            if (!isValid)
            {
                return new Dictionary<string, string[]>
                {
                    ["normalizedValue"] = [$"Payload does not match the expected schema for topic '{topic}'."],
                };
            }

            return null;
        }
        catch (JsonException ex)
        {
            return new Dictionary<string, string[]>
            {
                ["normalizedValue"] = [$"Payload JSON is malformed for topic '{topic}': {ex.Message}"],
            };
        }
    }

    private static bool TryDeserializeValid<T>(string json)
        where T : class
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions);
            return result is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

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
