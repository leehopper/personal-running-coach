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
 * `OnboardingProgressDto.completedTopics` → `OnboardingProgressDto.completed`
 * is caught by `codegen:check` as a generated-output drift (git-diff) failure;
 * the resulting broken `.shape` access then surfaces as a TypeScript error
 * under `npm run build`. That regression class is exactly what Slice 1 bug #4
 * shipped undetected and what DEC-066 exists to prevent.
 *
 * Drift is caught bidirectionally for `SuggestedInputType`:
 *   - const ⊆ union: enforced by `satisfies Record<string, SuggestedInputType>`
 *     on the const object — a value not in the union is a compile error.
 *   - union ⊆ const: enforced by `_suggestedInputTypeExhaustivenessGuard`
 *     immediately below the const — a new backend variant that widens the
 *     Zod-inferred union without a matching entry in the const causes the
 *     guard to resolve to `never`, failing the build.
 */
import { z } from 'zod'

import { PostApiV1AuthRegisterBody } from './zod/auth/auth'
import { PostApiV1OnboardingTurnsResponse } from './zod/onboarding/onboarding'
import { PostApiV1WorkoutsLogsBody } from './zod/workout-logs/workout-logs'

// Auth schemas — migrated piecewise. RegisterRequest is the first; T03.2
// extends to OnboardingProgressDto + SuggestedInputType.
export const registerRequestSchema = PostApiV1AuthRegisterBody
export type RegisterRequest = z.infer<typeof registerRequestSchema>

// Onboarding schemas — the named components OnboardingProgressDto and
// SuggestedInputType live as `$ref` entries in swagger.json but Orval's
// `tags-split` mode inlines them into the parent endpoint envelopes. The
// extractions below source the canonical Zod node from
// `PostApiV1OnboardingTurnsResponse.shape.*`; the names are part of the
// barrel's contract and a `.shape` miss surfaces as a TS error.

export const onboardingProgressSchema = PostApiV1OnboardingTurnsResponse.shape.progress
export type OnboardingProgressDto = z.infer<typeof onboardingProgressSchema>

export const suggestedInputTypeSchema = PostApiV1OnboardingTurnsResponse.shape.suggestedInputType
export type SuggestedInputType = z.infer<typeof suggestedInputTypeSchema>

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

export const completionStatusSchema = createWorkoutLogRequestSchema.shape.completionStatus
export type CompletionStatus = z.infer<typeof completionStatusSchema>

/**
 * `as const` enum-shaped object paired with {@link SuggestedInputType}
 * so consumers can write `SuggestedInputType.Text` instead of the magic
 * integer literal `0`. The numeric values mirror the C# enum on the wire
 * (no `JsonStringEnumConverter` is registered for this controller); the
 * Zod schema above enforces the same integer set at the validation seam.
 * Adding a member here without the schema also accepting it would surface
 * as a Zod parse failure during integration tests, so the two stay in
 * lockstep with the backend.
 */
export const SuggestedInputType = {
  Text: 0,
  SingleSelect: 1,
  MultiSelect: 2,
  Numeric: 3,
  Date: 4,
} as const satisfies Record<string, SuggestedInputType>

// Exhaustiveness guard — union ⊆ const direction.
// `satisfies` above already ensures every value in the const is a member of
// the `SuggestedInputType` union (const ⊆ union). This assertion covers the
// other direction: if the Zod-inferred `SuggestedInputType` union gains a new
// member (e.g. the backend adds variant `5`), `_SuggestedInputTypeUnionMinusConst`
// becomes that literal rather than `never`, causing `_SuggestedInputTypeExhaustive`
// to resolve to `never` — making the `export const` assignment below fail with
// "Type 'true' is not assignable to type 'never'" (build error).
type _SuggestedInputTypeConstMembers = (typeof SuggestedInputType)[keyof typeof SuggestedInputType]
type _SuggestedInputTypeUnionMinusConst = Exclude<
  SuggestedInputType,
  _SuggestedInputTypeConstMembers
>
type _SuggestedInputTypeExhaustive = _SuggestedInputTypeUnionMinusConst extends never ? true : never
// Exported to satisfy `noUnusedLocals`; the assignment fails if a new backend
// variant widens `SuggestedInputType` without a matching entry in the const above.
export const _suggestedInputTypeExhaustivenessGuard: _SuggestedInputTypeExhaustive = true

/**
 * `as const` enum-shaped object paired with {@link CompletionStatus} so the
 * logging form and history surfaces read `CompletionStatus.Complete` instead of
 * the magic integer `0`. Values mirror the C# `CompletionStatus` enum on the
 * wire (no `JsonStringEnumConverter`); the Zod union above enforces the same
 * `0 | 1 | 2` set at the validation seam. Kept in lockstep with the backend by
 * the same bidirectional guard used for {@link SuggestedInputType}.
 */
export const CompletionStatus = {
  Complete: 0,
  Partial: 1,
  Skipped: 2,
} as const satisfies Record<string, CompletionStatus>

// Exhaustiveness guard — union ⊆ const direction (mirrors the SuggestedInputType
// guard above): a new backend `completionStatus` literal that widens the
// Zod-inferred union without a matching const entry resolves this to `never`,
// failing the build.
type _CompletionStatusConstMembers = (typeof CompletionStatus)[keyof typeof CompletionStatus]
type _CompletionStatusUnionMinusConst = Exclude<CompletionStatus, _CompletionStatusConstMembers>
type _CompletionStatusExhaustive = _CompletionStatusUnionMinusConst extends never ? true : never
export const _completionStatusExhaustivenessGuard: _CompletionStatusExhaustive = true
