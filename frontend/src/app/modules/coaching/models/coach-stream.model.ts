// SSE frame wire-format types for the streaming Q&A endpoint
// (`POST /api/v1/conversation/messages`). Paired 1:1 with the backend frame
// records in `RunCoach.Api.Modules.Coaching.Conversation.Streaming` — the
// `SseWriter` emits each as `event: {name}\ndata: {compactJson}\n\n` with an
// opening `: hb\n\n` heartbeat comment. The frame's `EventName` is an explicit
// C# interface member, so it never appears inside the `data` JSON — the event
// name lives only on the SSE `event:` line and is folded into the discriminant
// here by `toCoachStreamFrame`.
//
// Conversation enums (`SafetyTier`, `ReferralCategory`) cross the wire as
// integers — see `conversation.model.ts`. Renaming a frame member requires a
// paired change in the backend record.

import type { StructuredLogDraft } from '~/api/generated'
import type { ReferralCategory, SafetyTier } from '~/modules/coaching/models/conversation.model'

/**
 * The on-plan prescription a parsed workout was matched against (null for an
 * off-plan run). Mirrors `RunCoach...CandidatePrescriptionDto` — hand-written
 * because the SSE `card` frame is outside the OpenAPI surface (the messages
 * endpoint's generated response is `string`).
 */
export interface CandidatePrescriptionDto {
  workoutType: string
  distanceMeters: number
  durationSeconds: number
  paceFastSecPerKm: number
  paceEasySecPerKm: number
}

/** A chunk of the coach's streamed answer text. */
export interface TokenFrame {
  event: 'token'
  delta: string
}

/** A deterministic safety message emitted before any LLM output. */
export interface SafetyFrame {
  event: 'safety'
  content: string
  tier: SafetyTier
  category: ReferralCategory
}

/** A parsed workout-log draft awaiting an explicit Confirm (no `done` follows). */
export interface CardFrame {
  event: 'card'
  draft: StructuredLogDraft
  prescription: CandidatePrescriptionDto | null
}

/** A terminal failure — the partial answer is discarded; retry with a fresh id. */
export interface ErrorFrame {
  event: 'error'
  message: string
  retryable: boolean
  retryAfterSeconds: number | null
}

/** The terminal success frame carrying the persisted coach turn's id. */
export interface DoneFrame {
  event: 'done'
  turnId: string
}

/** A typed SSE frame, discriminated on `event`. */
export type CoachStreamFrame = TokenFrame | SafetyFrame | CardFrame | ErrorFrame | DoneFrame
