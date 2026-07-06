// Onboarding wire-format types — paired 1:1 with the backend records under
// `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/Models/`. ASP.NET MVC
// serializes properties as camelCase and enums as their numeric value (no
// global JsonStringEnumConverter is registered for the controller layer); this
// file therefore keeps integer enums whose numeric values exactly mirror the C#
// `enum` declarations. Renaming or reordering members on either side of the wire
// requires a paired change here.
//
// Slice 4C-onboarding replaced the turn-based chat with the form-first intake
// (DEC-086): the per-turn request/response DTOs were removed. The per-topic
// answer/input shapes now come from the codegen barrel (`~/api/generated`); this
// file hand-writes the two shapes the generator gets wrong — the resume-state
// envelope (whose slots are nullable in C# but marked required by
// `RequireNonNullablePropertiesSchemaFilter`) and the submit request (whose
// `targetEvent` is nullable for a non-race goal).

import type {
  CurrentFitnessAnswer,
  CurrentFitnessInputDto,
  InjuryHistoryAnswer,
  InjuryHistoryInputDto,
  PreferencesAnswer,
  PreferencesInputDto,
  PrimaryGoalAnswer,
  PrimaryGoalInputDto,
  TargetEventAnswer,
  TargetEventInputDto,
  WeeklyScheduleAnswer,
  WeeklyScheduleInputDto,
} from '~/api/generated'

/**
 * Lifecycle status of the per-user onboarding stream. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.OnboardingStatus`.
 */
export const OnboardingStatus = {
  NotStarted: 0,
  InProgress: 1,
  Completed: 2,
} as const
export type OnboardingStatus = (typeof OnboardingStatus)[keyof typeof OnboardingStatus]

/**
 * The canonical six-topic onboarding state machine per DEC-047. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.OnboardingTopic`. Retained as plain
 * section-ordering constants for the form.
 */
export const OnboardingTopic = {
  PrimaryGoal: 0,
  TargetEvent: 1,
  CurrentFitness: 2,
  WeeklySchedule: 3,
  InjuryHistory: 4,
  Preferences: 5,
} as const
export type OnboardingTopic = (typeof OnboardingTopic)[keyof typeof OnboardingTopic]

/**
 * The runner's primary training goal. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.PrimaryGoal`. Drives the goal
 * select and the conditional TargetEvent reveal.
 */
export const PrimaryGoal = {
  RaceTraining: 0,
  GeneralFitness: 1,
  ReturnToRunning: 2,
  BuildVolume: 3,
  BuildSpeed: 4,
} as const
export type PrimaryGoal = (typeof PrimaryGoal)[keyof typeof PrimaryGoal]

/**
 * `GET /api/v1/onboarding/state` response payload. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.OnboardingStateDto`.
 *
 * The six captured-answer slots are `null` until the runner has submitted that
 * topic — the C# record types them `TAnswer?`, but the generated OpenAPI type
 * marks every `$ref` property required (`RequireNonNullablePropertiesSchemaFilter`,
 * repo-wide; see the `~/api/generated` barrel). This hand-written shape restores
 * the honest nullability the resume-hydrate depends on, over the concrete
 * generated answer types.
 */
export interface OnboardingStateDto {
  userId: string
  status: OnboardingStatus
  currentTopic: OnboardingTopic | null
  completedTopics: number
  totalTopics: number
  isComplete: boolean
  outstandingClarifications: OnboardingTopic[]
  primaryGoal: PrimaryGoalAnswer | null
  targetEvent: TargetEventAnswer | null
  currentFitness: CurrentFitnessAnswer | null
  weeklySchedule: WeeklyScheduleAnswer | null
  injuryHistory: InjuryHistoryAnswer | null
  preferences: PreferencesAnswer | null
  currentPlanId: string | null
}

/**
 * `POST /api/v1/onboarding/answers` request payload (the form-first intake,
 * DEC-086 / DP-5). Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.SubmitStructuredAnswersRequestDto`,
 * but types `targetEvent` as nullable: the C# slot is `TargetEventInputDto?` and
 * the handler enforces `TargetEvent ⇒ RaceTraining`, so a non-race submission
 * sends `null` — whereas the generated `SubmitStructuredAnswersRequestDto` marks
 * every slot required (the same `$ref` limitation as `OnboardingStateDto`).
 */
export interface SubmitStructuredAnswersRequest {
  /** Client-generated UUID (`crypto.randomUUID()`), re-sent on retry for idempotency. */
  idempotencyKey: string
  primaryGoal: PrimaryGoalInputDto
  targetEvent: TargetEventInputDto | null
  currentFitness: CurrentFitnessInputDto
  weeklySchedule: WeeklyScheduleInputDto
  injuryHistory: InjuryHistoryInputDto
  preferences: PreferencesInputDto
}
