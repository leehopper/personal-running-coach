using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Request payload for POST /api/v1/onboarding/answers — the deterministic, form-first onboarding
/// intake (DEC-086). Carries a client-minted idempotency key plus a nullable, whole-record slot
/// per topic; a present slot is that topic's complete answer. The single-page form submits every
/// completed topic in one request. Each slot is a loosened, non-throwing input shape (see
/// <see cref="PrimaryGoalInputDto"/>); the controller validates them deterministically and
/// originates one <see cref="AnswerCaptured"/> event per submitted topic — no onboarding-time LLM
/// call.
/// </summary>
/// <param name="IdempotencyKey">Client-generated idempotency key (typically a <c>crypto.randomUUID()</c>) re-sent on retry.</param>
/// <param name="PrimaryGoal">PrimaryGoal answer, or null if not submitted this request.</param>
/// <param name="TargetEvent">TargetEvent answer (only valid with a race-training PrimaryGoal), or null.</param>
/// <param name="CurrentFitness">CurrentFitness answer, or null.</param>
/// <param name="WeeklySchedule">WeeklySchedule answer, or null.</param>
/// <param name="InjuryHistory">InjuryHistory answer, or null.</param>
/// <param name="Preferences">Preferences answer, or null.</param>
public sealed record SubmitStructuredAnswersRequestDto(
    [property: JsonRequired] Guid IdempotencyKey,
    PrimaryGoalInputDto? PrimaryGoal,
    TargetEventInputDto? TargetEvent,
    CurrentFitnessInputDto? CurrentFitness,
    WeeklyScheduleInputDto? WeeklySchedule,
    InjuryHistoryInputDto? InjuryHistory,
    PreferencesInputDto? Preferences);
