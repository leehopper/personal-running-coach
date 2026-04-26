// Onboarding wire-format types — paired 1:1 with the backend records under
// `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/Models/`. ASP.NET MVC
// serializes properties as camelCase and enums as their numeric value (no
// global JsonStringEnumConverter is registered for the controller layer);
// this file therefore keeps integer enums whose numeric values exactly mirror
// the C# `enum` declarations. Renaming or reordering members on either side
// of the wire requires a paired change here. Schema validation lives in
// `../schemas/onboarding-turn-response.schema.ts`.

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
 * The canonical six-topic onboarding state machine per DEC-047.
 * Mirrors `RunCoach.Api.Modules.Coaching.Onboarding.OnboardingTopic`.
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
 * Discriminator for `OnboardingTurnResponse`. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.OnboardingTurnKind`.
 * `Error` is a synthetic client-side variant produced when the local Zod
 * schema rejects a server payload — the backend itself only emits
 * `Ask` (0) or `Complete` (1).
 */
export const OnboardingTurnKind = {
  Ask: 0,
  Complete: 1,
  Error: -1,
} as const
export type OnboardingTurnKind = (typeof OnboardingTurnKind)[keyof typeof OnboardingTurnKind]

/**
 * Frontend input control hint paired with each `Ask` turn. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.SuggestedInputType`.
 */
export const SuggestedInputType = {
  Text: 0,
  SingleSelect: 1,
  MultiSelect: 2,
  Numeric: 3,
  Date: 4,
} as const
export type SuggestedInputType = (typeof SuggestedInputType)[keyof typeof SuggestedInputType]

/**
 * Anthropic content block discriminator. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.AnthropicContentBlockType`.
 * Slice 1 produces only `Text`; `Thinking` blocks pass through opaquely
 * and must NEVER be rendered as runner-visible text (R-065).
 */
export const AnthropicContentBlockType = {
  Text: 0,
  Thinking: 1,
} as const
export type AnthropicContentBlockType =
  (typeof AnthropicContentBlockType)[keyof typeof AnthropicContentBlockType]

/**
 * Closed-shape Anthropic content block. Mirrors the Pattern B record in
 * `RunCoach.Api.Modules.Coaching.Onboarding.AnthropicContentBlock`.
 */
export interface AnthropicContentBlock {
  type: AnthropicContentBlockType
  text: string
}

/**
 * Topic-completion progress for the chat UI's six-segment indicator.
 */
export interface OnboardingProgressDto {
  completedTopics: number
  totalTopics: number
}

/**
 * The runner's primary training goal. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.PrimaryGoal`.
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
 * Captured PrimaryGoal answer payload.
 */
export interface PrimaryGoalAnswerDto {
  goal: PrimaryGoal
  description: string
}

/**
 * Captured TargetEvent answer payload (race name + date + distance hint).
 * Shapes are intentionally permissive (`unknown`) on the frontend so a
 * minor backend additive change does not block the chat UI from resuming
 * — the submit/revise endpoints stay typed.
 */
export interface TargetEventAnswerDto {
  [key: string]: unknown
}

export interface CurrentFitnessAnswerDto {
  [key: string]: unknown
}

export interface WeeklyScheduleAnswerDto {
  [key: string]: unknown
}

export interface InjuryHistoryAnswerDto {
  [key: string]: unknown
}

export interface PreferencesAnswerDto {
  [key: string]: unknown
}

/**
 * GET /api/v1/onboarding/state response payload. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.OnboardingStateDto`.
 */
export interface OnboardingStateDto {
  userId: string
  status: OnboardingStatus
  currentTopic: OnboardingTopic | null
  completedTopics: number
  totalTopics: number
  isComplete: boolean
  outstandingClarifications: OnboardingTopic[]
  primaryGoal: PrimaryGoalAnswerDto | null
  targetEvent: TargetEventAnswerDto | null
  currentFitness: CurrentFitnessAnswerDto | null
  weeklySchedule: WeeklyScheduleAnswerDto | null
  injuryHistory: InjuryHistoryAnswerDto | null
  preferences: PreferencesAnswerDto | null
  currentPlanId: string | null
}

/**
 * POST /api/v1/onboarding/turns request payload. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.OnboardingTurnRequestDto`.
 */
export interface OnboardingTurnRequestDto {
  /** Client-generated UUID (typically `crypto.randomUUID()`). */
  idempotencyKey: string
  text: string
}

/**
 * POST /api/v1/onboarding/answers/revise request payload. Mirrors
 * `RunCoach.Api.Modules.Coaching.Onboarding.Models.ReviseAnswerRequestDto`.
 * `normalizedValue` shape is per-topic — the chat UI dispatches based on
 * `topic` so the value type stays `unknown` here.
 */
export interface ReviseAnswerRequestDto {
  topic: OnboardingTopic
  normalizedValue: unknown
}

/**
 * Common fields across every `OnboardingTurnResponse` variant.
 */
export interface OnboardingTurnResponseBase {
  assistantBlocks: AnthropicContentBlock[]
  progress: OnboardingProgressDto
}

/**
 * Server asked the runner another question — onboarding is not yet complete.
 */
export interface OnboardingTurnAskResponse extends OnboardingTurnResponseBase {
  kind: typeof OnboardingTurnKind.Ask
  topic: OnboardingTopic
  suggestedInputType: SuggestedInputType
  planId: null
}

/**
 * Onboarding completed and a plan was generated.
 */
export interface OnboardingTurnCompleteResponse extends OnboardingTurnResponseBase {
  kind: typeof OnboardingTurnKind.Complete
  topic: null
  suggestedInputType: null
  planId: string
}

/**
 * Synthetic client-side variant emitted when the local Zod schema rejects
 * a server payload. The backend itself never serializes this kind.
 */
export interface OnboardingTurnErrorResponse {
  kind: typeof OnboardingTurnKind.Error
  message: string
}

/**
 * Discriminated union of every onboarding turn response shape — narrow on
 * `kind` to access variant-specific fields.
 */
export type OnboardingTurnResponse =
  | OnboardingTurnAskResponse
  | OnboardingTurnCompleteResponse
  | OnboardingTurnErrorResponse
