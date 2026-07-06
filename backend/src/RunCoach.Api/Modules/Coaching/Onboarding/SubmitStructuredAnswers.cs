using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Wolverine command dispatched by <c>OnboardingController.SubmitAnswers</c> for the form-first
/// onboarding intake (DEC-086 D1). Carries the authenticated user's id (the per-user onboarding
/// stream identity), the client-supplied idempotency key, and the already-validated canonical
/// answer records for each submitted topic. Per DP-2 the matching handler is
/// <see cref="SubmitStructuredAnswersHandler"/> — a plain static Wolverine handler (no
/// <c>[AggregateHandler]</c> attribute) that appends one whole-record <see cref="AnswerCaptured"/>
/// per present slot, evaluates the deterministic completion gate, and runs the existing inline
/// plan-generation terminal branch — all through a single Marten <c>IDocumentSession</c>, with no
/// onboarding-time LLM call.
/// </summary>
/// <remarks>
/// The slots carry the canonical <c>*Answer</c> records rather than the loosened wire input DTOs:
/// the controller performs deterministic validation and constructs these records (whose <c>init</c>
/// accessors enforce numeric ranges) before dispatch, so an invalid submission is rejected with a
/// 400 before any transaction opens.
/// </remarks>
/// <param name="UserId">The authenticated runner's id; doubles as the onboarding stream id (DEC-047).</param>
/// <param name="IdempotencyKey">Client-generated idempotency key; the handler short-circuits duplicates via <c>IIdempotencyStore.SeenAsync</c>.</param>
/// <param name="PrimaryGoal">Validated PrimaryGoal answer, or null if not submitted.</param>
/// <param name="TargetEvent">Validated TargetEvent answer, or null.</param>
/// <param name="CurrentFitness">Validated CurrentFitness answer, or null.</param>
/// <param name="WeeklySchedule">Validated WeeklySchedule answer, or null.</param>
/// <param name="InjuryHistory">Validated InjuryHistory answer, or null.</param>
/// <param name="Preferences">Validated Preferences answer, or null.</param>
public sealed record SubmitStructuredAnswers(
    Guid UserId,
    Guid IdempotencyKey,
    PrimaryGoalAnswer? PrimaryGoal,
    TargetEventAnswer? TargetEvent,
    CurrentFitnessAnswer? CurrentFitness,
    WeeklyScheduleAnswer? WeeklySchedule,
    InjuryHistoryAnswer? InjuryHistory,
    PreferencesAnswer? Preferences);
