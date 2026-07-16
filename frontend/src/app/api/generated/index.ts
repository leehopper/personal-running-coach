/**
 * Hand-maintained barrel for the codegen pipeline (DEC-066 / R-071).
 *
 * Re-exports the generated artifacts under project-conventional camelCase
 * names so consumers reference `registerRequestSchema` rather than the
 * generator's `PostApiV1AuthRegisterBody`. Renames of the per-feature
 * generated files (e.g. Orval's `auth/auth.ts` → `auth/v2.ts`) are absorbed
 * here so the consuming code doesn't ripple.
 *
 * This file is NOT generated — it is the single seam between the
 * generated output (committed and gated by `codegen:check`) and the rest of
 * the app. Each migrated schema or hook gets one line here per direction
 * (schema + inferred type).
 *
 * Orval's `tags-split` mode inlines `$ref`-named component schemas directly
 * into the endpoint envelopes rather than exporting them as standalone
 * schemas. The named-component extractions below pull the sub-schemas via
 * Zod's `.shape` accessor so a backend rename of (say)
 * `CreateWorkoutLogRequest.completionStatus` is caught by `codegen:check` as a
 * generated-output drift (git-diff) failure; the resulting broken `.shape`
 * access then surfaces as a TypeScript error under `npm run build`. That
 * regression class is exactly what Slice 1 bug #4 shipped undetected and what
 * DEC-066 exists to prevent.
 *
 * Drift is caught bidirectionally for the numeric enums (`CompletionStatus`,
 * `PreferredUnits`):
 *   - const ⊆ union: enforced by `satisfies Record<string, T>` on the const
 *     object — a value not in the union is a compile error.
 *   - union ⊆ const: enforced by the `_*ExhaustivenessGuard` const immediately
 *     below each — a new backend variant that widens the Zod-inferred union
 *     without a matching entry in the const causes the guard to resolve to
 *     `never`, failing the build.
 */
import { z } from 'zod'

import { PostApiV1AuthRegisterBody } from './zod/auth/auth'
import { PostApiV1WorkoutsLogsBody } from './zod/workout-logs/workout-logs'

// Auth schemas — migrated piecewise.
export const registerRequestSchema = PostApiV1AuthRegisterBody
export type RegisterRequest = z.infer<typeof registerRequestSchema>

// Workout-log schemas (slice-2b). The create-request body is the drift seam
// the `/log` form derives from via `.pick().extend()` (DEC-075): the form keeps
// `occurredOn`'s ISO-date format and `completionStatus`'s `0|1|2` enum honest
// against the backend contract. A backend rename is caught by `codegen:check`
// as a generated-output drift (git-diff) failure; the resulting broken `.shape`
// access then surfaces as a TypeScript error under `npm run build`.
export const createWorkoutLogRequestSchema = PostApiV1WorkoutsLogsBody
export type CreateWorkoutLogRequest = z.infer<typeof createWorkoutLogRequestSchema>

// The create response has no Zod schema to derive from — Swashbuckle emits no
// 201 body schema, so the generated Zod response is `z.void()`. Re-export the
// generated RTK *type* (type-only, so the rtk module's runtime self-injection is
// not pulled in) so the response wire shape stays generated, not hand-mirrored.
export type { CreateWorkoutLogResponseDto } from './rtk/api'

// History read DTOs (slice-2b PR7). These are response/request *types* the
// history surface consumes; re-exported type-only (same reasoning as
// `CreateWorkoutLogResponseDto`) so the wire shape stays generated, not
// hand-mirrored. A backend rename ripples here via `codegen:check` + `tsc`.
export type {
  QueryWorkoutLogsRequestDto,
  QueryWorkoutLogsResponseDto,
  WorkoutLogDto,
  WorkoutLogSplitDto,
} from './rtk/api'

// SPLIT/Alpine Slice 4 PR-A — the prescribed-workout summary `GET
// /workouts/logs/prescribed` (D1) resolves for the log form's banner.
// Re-exported type-only (same reasoning as the workout-log DTOs above) so the
// wire shape stays generated, not hand-mirrored; a backend rename ripples
// here via `codegen:check` + `tsc`.
export type { PrescribedWorkoutDto } from './rtk/api'

// Conversation read DTOs (slice-3 Unit 2, PR3). The read-only "Explain-the-change"
// panel (PR7) consumes the response envelope + turn shape, the adaptation/safety
// enums (numeric on the wire — no JsonStringEnumConverter, matching CompletionStatus),
// and the structured before/after diff. Re-exported type-only (same reasoning as the
// workout-log DTOs) so the wire shape stays generated, not hand-mirrored; a backend
// rename ripples here via `codegen:check` + `tsc`.
//
// CAVEAT for PR7: `escalationLevel`, `adaptationKind`, and `diff` are nullable on the
// backend (null for `role === SystemSafety` safety turns) but the generated type
// marks them present — the repo-wide `$ref`-property limitation of
// RequireNonNullablePropertiesSchemaFilter (e.g. MesoDaySlotOutput.workoutType is
// likewise typed non-null yet null on Rest days). Narrow on `role` and hand-write an
// accurate discriminated domain model, mirroring `plan/models/plan.model.ts`'s
// `MesoDaySlot` union — do not read those fields unguarded.
export type {
  ConversationTurnsResponseDto,
  ConversationTurnDto,
  ConversationRole,
  EscalationLevel,
  SafetyTier,
  ReferralCategory,
  AdaptationKind,
  PlanAdaptationDiff,
  WorkoutChange,
  WeeklyTargetChange,
} from './rtk/api'

// Slice 4B conversational-logging + streaming DTOs (Units 4/5/6). The intent
// classifier's structured draft, the confirm-then-commit request/response, the
// adaptation envelope, and the SSE message request body. Re-exported type-only
// (same reasoning as the workout-log DTOs) so the wire shapes stay generated; a
// backend rename ripples here via `codegen:check` + `tsc`. The hand-rolled SSE
// frame union and the accurate discriminated timeline model live in
// `coaching/models` — the generated `ConversationTimelineTurnDto` marks
// `interactive`/`proactive` both required despite C# nullability (the same
// `$ref` limitation as the conversation read DTOs above).
export type {
  StructuredLogDraft,
  RunnerDistanceUnit,
  ConfirmConversationalLogRequestDto,
  ConfirmConversationalLogResponseDto,
  AdaptationResponseDto,
  AdaptationResponseKind,
  ConversationMessageRequestDto,
} from './rtk/api'

// Slice 4C units — the unit-preference DTO consumed by the settings RTK slice
// (`settings.api.ts`). Re-exported type-only (same reasoning as the workout-log
// DTOs above) so the wire shape stays generated, not hand-mirrored; a backend
// rename ripples here via `codegen:check` + `tsc`. (`PreferredUnits` itself is
// re-exported below, paired with its `as const` enum.)
export type { UnitPreferenceDto } from './rtk/api'

// Slice 4C onboarding — the form-first intake DTOs (PR-C `POST /answers`). The
// six loosened per-topic *input* shapes the form maps down to, plus the six
// concrete captured-*answer* shapes the resume-hydrate reads off `GET /state`.
// Re-exported type-only (same reasoning as the workout-log DTOs above) so the
// wire shapes stay generated, not hand-mirrored; a backend rename ripples here
// via `codegen:check` + `tsc`. The accurate request envelope
// (`SubmitStructuredAnswersRequest`, with a nullable `targetEvent`) and the
// nullable-slot state model are hand-written in `onboarding/models/onboarding.model.ts`,
// because `RequireNonNullablePropertiesSchemaFilter` marks every `$ref` property
// required even where the C# record is nullable (the same limitation noted for
// the conversation/timeline DTOs above).
export type {
  PrimaryGoalInputDto,
  TargetEventInputDto,
  CurrentFitnessInputDto,
  WeeklyScheduleInputDto,
  InjuryHistoryInputDto,
  PreferencesInputDto,
  PrimaryGoalAnswer,
  TargetEventAnswer,
  CurrentFitnessAnswer,
  WeeklyScheduleAnswer,
  InjuryHistoryAnswer,
  PreferencesAnswer,
} from './rtk/api'

export const completionStatusSchema = createWorkoutLogRequestSchema.shape.completionStatus
export type CompletionStatus = z.infer<typeof completionStatusSchema>

/**
 * `as const` enum-shaped object paired with {@link CompletionStatus} so the
 * logging form and history surfaces read `CompletionStatus.Complete` instead of
 * the magic integer `0`. Values mirror the C# `CompletionStatus` enum on the
 * wire (no `JsonStringEnumConverter`); the Zod union above enforces the same
 * `0 | 1 | 2` set at the validation seam. Kept in lockstep with the backend by
 * the same bidirectional guard used for {@link PreferredUnits}.
 */
export const CompletionStatus = {
  Complete: 0,
  Partial: 1,
  Skipped: 2,
} as const satisfies Record<string, CompletionStatus>

// Exhaustiveness guard — union ⊆ const direction (same pattern as the PreferredUnits
// guard below): a new backend `completionStatus` literal that widens the
// Zod-inferred union without a matching const entry resolves this to `never`,
// failing the build.
type _CompletionStatusConstMembers = (typeof CompletionStatus)[keyof typeof CompletionStatus]
type _CompletionStatusUnionMinusConst = Exclude<CompletionStatus, _CompletionStatusConstMembers>
type _CompletionStatusExhaustive = _CompletionStatusUnionMinusConst extends never ? true : never
export const _completionStatusExhaustivenessGuard: _CompletionStatusExhaustive = true

// Settings km/miles display preference (slice 4C-units). Re-export the generated
// wire *type* (`0 | 1`, numeric on the wire — no `JsonStringEnumConverter`) and
// pair it with an `as const` enum-shaped object so the shared unit-format module
// and the Settings toggle read `PreferredUnits.Miles` instead of the magic
// integer `1`. Kept in lockstep with the backend by the same bidirectional guard
// used for {@link CompletionStatus}: `satisfies` covers const ⊆ union, and the
// guard below covers union ⊆ const.
export type PreferredUnits = import('./rtk/api').PreferredUnits

export const PreferredUnits = {
  Kilometers: 0,
  Miles: 1,
} as const satisfies Record<string, PreferredUnits>

type _PreferredUnitsConstMembers = (typeof PreferredUnits)[keyof typeof PreferredUnits]
type _PreferredUnitsUnionMinusConst = Exclude<PreferredUnits, _PreferredUnitsConstMembers>
type _PreferredUnitsExhaustive = _PreferredUnitsUnionMinusConst extends never ? true : never
export const _preferredUnitsExhaustivenessGuard: _PreferredUnitsExhaustive = true
