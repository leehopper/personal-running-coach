// Conversation wire-format types — paired 1:1 with the backend records in
// `RunCoach.Api.Modules.Coaching.Conversation` (`ConversationTurnDto` /
// `ConversationTurnsResponseDto`) rendered by the read-only
// "Explain-the-change" panel via `GET /api/v1/conversation/turns`.
//
// Unlike the plan module's structured-output enums (which carry per-type
// `JsonStringEnumConverter` attributes and cross the wire as strings), the
// conversation enums serialize as **integers** — each backend member is
// explicitly numbered so the encoding is stable. They are declared here as
// numeric literal unions with companion constant maps so consumers switch on
// named members, never bare integers.
//
// The generated Zod schema (`api/generated/zod/conversation`) marks every
// member required because the backend schema filter cannot express C#
// nullability on `$ref` properties; THIS file is the accurate contract:
// `escalationLevel`, `adaptationKind`, and `diff` are null on safety turns
// and always present on adaptation turns, which makes `role` a discriminant
// (mirrors `ConversationTurnView.FromAdaptation` / `FromSafety`).
//
// Renaming or reordering members on either side of the wire requires a paired
// change in this file plus the backend record. Per the trademark rule in the
// root `CLAUDE.md`, every user-facing string this module renders must use
// "Daniels-Gilbert zones" / "pace-zone index" terminology.

import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'

/**
 * The author/kind of a turn. Mirrors
 * `RunCoach.Api.Modules.Coaching.Conversation.ConversationRole`.
 */
export type ConversationRole = 0 | 1

/** Named members of {@link ConversationRole}. */
export const CONVERSATION_ROLE = {
  assistantAdaptation: 0,
  systemSafety: 1,
} as const satisfies Record<string, ConversationRole>

/**
 * The DEC-012 escalation-ladder level (0-indexed canon). Mirrors
 * `RunCoach.Api.Modules.Training.Adaptation.EscalationLevel`.
 */
export type EscalationLevel = 0 | 1 | 2 | 3 | 4

/** Named members of {@link EscalationLevel}. */
export const ESCALATION_LEVEL = {
  absorb: 0,
  microAdjust: 1,
  restructure: 2,
  phaseReconsider: 3,
  planOverhaul: 4,
} as const satisfies Record<string, EscalationLevel>

/**
 * The deterministic safety tier resolved for the triggering log. Mirrors
 * `RunCoach.Api.Modules.Training.Safety.SafetyTier`.
 */
export type SafetyTier = 0 | 1 | 2

/** Named members of {@link SafetyTier}. */
export const SAFETY_TIER = {
  green: 0,
  amber: 1,
  red: 2,
} as const satisfies Record<string, SafetyTier>

/**
 * The kind of safety signal behind a non-Green tier. Mirrors
 * `RunCoach.Api.Modules.Training.Safety.ReferralCategory`. Adaptation turns
 * always carry `none`.
 */
export type ReferralCategory = 0 | 1 | 2 | 3 | 4

/** Named members of {@link ReferralCategory}. */
export const REFERRAL_CATEGORY = {
  none: 0,
  crisis: 1,
  emergencyReferral: 2,
  injury: 3,
  redS: 4,
} as const satisfies Record<string, ReferralCategory>

/**
 * The kind of plan change an adaptation applied — drives the panel render
 * style (absorb never persists a turn; nudge renders inline; restructure
 * renders as an expandable block). Mirrors
 * `RunCoach.Api.Modules.Training.Adaptation.AdaptationKind`.
 */
export type AdaptationKind = 0 | 1 | 2

/** Named members of {@link AdaptationKind}. */
export const ADAPTATION_KIND = {
  absorb: 0,
  nudge: 1,
  restructure: 2,
} as const satisfies Record<string, AdaptationKind>

/**
 * A single before/after change to one micro-week workout, keyed by 1-based
 * `weekNumber` and `dayOfWeek` (0 = Sunday … 6 = Saturday). Mirrors
 * `RunCoach.Api.Modules.Training.Adaptation.WorkoutChange` — the `before` /
 * `after` payloads are the same `WorkoutOutput` wire shape the plan module
 * renders, so {@link MicroWorkoutCardDto} is reused. A null `before` denotes
 * an added workout; a null `after` a removed one.
 */
export interface WorkoutChangeDto {
  weekNumber: number
  dayOfWeek: number
  before: MicroWorkoutCardDto | null
  after: MicroWorkoutCardDto | null
}

/**
 * A before/after change to one meso week's volume target. Mirrors
 * `RunCoach.Api.Modules.Training.Adaptation.WeeklyTargetChange`.
 */
export interface WeeklyTargetChangeDto {
  weekNumber: number
  beforeWeeklyTargetKm: number
  afterWeeklyTargetKm: number
}

/**
 * The structured before/after payload an adaptation carries — the "Show what
 * changed" expander renders from this, never from parsed prose. Mirrors
 * `RunCoach.Api.Modules.Training.Adaptation.PlanAdaptationDiff`; both
 * collections are always present (possibly empty), never null.
 */
export interface PlanAdaptationDiffDto {
  workoutChanges: WorkoutChangeDto[]
  weeklyTargetChanges: WeeklyTargetChangeDto[]
}

/**
 * An adaptation-explanation turn projected from a `PlanAdaptedFromLog`
 * event: `escalationLevel`, `adaptationKind`, and `diff` are always present
 * and `referralCategory` is always `none`
 * (`ConversationTurnView.FromAdaptation`).
 */
export interface AdaptationTurnDto {
  triggeringPlanEventId: string
  role: typeof CONVERSATION_ROLE.assistantAdaptation
  content: string
  escalationLevel: EscalationLevel
  safetyTier: SafetyTier
  referralCategory: ReferralCategory
  adaptationKind: AdaptationKind
  diff: PlanAdaptationDiffDto
  triggeringWorkoutLogId: string
  createdAt: string
}

/**
 * A deterministic safety turn projected from a `SafetySignalRaised` event:
 * the adaptation-only members are always null and the scripted `content`
 * renders at full prominence regardless of any plan-change level
 * (`ConversationTurnView.FromSafety`).
 */
export interface SafetyTurnDto {
  triggeringPlanEventId: string
  role: typeof CONVERSATION_ROLE.systemSafety
  content: string
  escalationLevel: null
  safetyTier: SafetyTier
  referralCategory: ReferralCategory
  adaptationKind: null
  diff: null
  triggeringWorkoutLogId: string
  createdAt: string
}

/**
 * One turn in the read-only panel, discriminated on `role`. Narrow on
 * `role` (or the null-ness of `adaptationKind`) before reading the
 * adaptation-only members.
 */
export type ConversationTurnDto = AdaptationTurnDto | SafetyTurnDto

/**
 * GET /api/v1/conversation/turns response payload — the runner's turns for
 * the active plan, newest-first (`CreatedAt` desc, per-stream event version
 * as the tiebreaker). Mirrors
 * `RunCoach.Api.Modules.Coaching.Conversation.ConversationTurnsResponseDto`.
 */
export interface ConversationTurnsResponseDto {
  turns: ConversationTurnDto[]
}
