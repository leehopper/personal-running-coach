// Pure helpers for the streaming Q&A SSE wire (`POST /api/v1/conversation/messages`).
// `createSseDecoder` is an incremental parser that buffers across chunk
// boundaries, skips comment lines (the `: hb` heartbeat), and surfaces each
// completed `event:`/`data:` block once. `toCoachStreamFrame` maps a raw event
// into the typed {@link CoachStreamFrame} union, validating the discriminant
// primitive fields at the stream boundary so a malformed frame is dropped rather
// than producing an object with `undefined` members. Both are kept side-effect
// free so the hand-rolled `useCoachStream` fetch reader stays thin and testable.

import type {
  CandidatePrescriptionDto,
  CardFrame,
  CoachStreamFrame,
  DoneFrame,
  ErrorFrame,
  SafetyFrame,
  TokenFrame,
} from '~/modules/coaching/models/coach-stream.model'
import type { ReferralCategory, SafetyTier } from '~/modules/coaching/models/conversation.model'
import type { StructuredLogDraft } from '~/api/generated'

/** A decoded SSE block: the `event:` name (default `message`) and joined `data:`. */
export interface SseEvent {
  event: string
  data: string
}

/** A stateful incremental SSE block decoder. */
export interface SseDecoder {
  push: (chunk: string) => SseEvent[]
}

const FRAME_BOUNDARY = '\n\n'

/**
 * Creates a decoder that accepts text chunks and returns the SSE events
 * completed by each chunk. Frames split across chunk boundaries are buffered
 * until their terminating blank line arrives; comment-only blocks (the
 * heartbeat) yield nothing.
 */
export const createSseDecoder = (): SseDecoder => {
  let buffer = ''

  return {
    push: (chunk: string): SseEvent[] => {
      buffer += chunk
      const events: SseEvent[] = []
      let boundary = buffer.indexOf(FRAME_BOUNDARY)
      while (boundary !== -1) {
        const block = buffer.slice(0, boundary)
        buffer = buffer.slice(boundary + FRAME_BOUNDARY.length)
        const event = parseBlock(block)
        if (event !== null) events.push(event)
        boundary = buffer.indexOf(FRAME_BOUNDARY)
      }
      return events
    },
  }
}

const parseBlock = (block: string): SseEvent | null => {
  let eventName = 'message'
  const dataLines: string[] = []

  for (const rawLine of block.split('\n')) {
    const line = rawLine.endsWith('\r') ? rawLine.slice(0, -1) : rawLine
    if (line.length === 0 || line.startsWith(':')) continue

    const colon = line.indexOf(':')
    const field = colon === -1 ? line : line.slice(0, colon)
    const rawValue = colon === -1 ? '' : line.slice(colon + 1)
    const value = rawValue.startsWith(' ') ? rawValue.slice(1) : rawValue

    if (field === 'event') eventName = value
    else if (field === 'data') dataLines.push(value)
  }

  if (dataLines.length === 0) return null
  return { event: eventName, data: dataLines.join('\n') }
}

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null

const parseToken = (d: Record<string, unknown>): TokenFrame | null =>
  typeof d.delta === 'string' ? { event: 'token', delta: d.delta } : null

const parseSafety = (d: Record<string, unknown>): SafetyFrame | null =>
  typeof d.content === 'string' && typeof d.tier === 'number' && typeof d.category === 'number'
    ? {
        event: 'safety',
        content: d.content,
        tier: d.tier as SafetyTier,
        category: d.category as ReferralCategory,
      }
    : null

const parseCard = (d: Record<string, unknown>): CardFrame | null =>
  isObject(d.draft)
    ? {
        event: 'card',
        draft: d.draft as unknown as StructuredLogDraft,
        prescription: isObject(d.prescription)
          ? (d.prescription as unknown as CandidatePrescriptionDto)
          : null,
      }
    : null

const parseError = (d: Record<string, unknown>): ErrorFrame | null =>
  typeof d.message === 'string' && typeof d.retryable === 'boolean'
    ? {
        event: 'error',
        message: d.message,
        retryable: d.retryable,
        retryAfterSeconds: typeof d.retryAfterSeconds === 'number' ? d.retryAfterSeconds : null,
      }
    : null

const parseDone = (d: Record<string, unknown>): DoneFrame | null =>
  typeof d.turnId === 'string' ? { event: 'done', turnId: d.turnId } : null

/**
 * Maps a raw SSE event to a typed {@link CoachStreamFrame}. Returns null for an
 * unknown event name, malformed JSON, or a frame missing its required primitive
 * fields (the discriminant fields are validated; the nested `draft` /
 * `prescription` payloads are server-trusted and rendered field-by-field with
 * safe formatting). The caller tolerates a null (it skips `ping`/unknown SSE
 * events the same way), so a bad frame is dropped rather than producing an
 * object with `undefined` members.
 */
export const toCoachStreamFrame = (raw: SseEvent): CoachStreamFrame | null => {
  let data: unknown
  try {
    data = JSON.parse(raw.data)
  } catch {
    return null
  }
  if (!isObject(data)) return null

  switch (raw.event) {
    case 'token':
      return parseToken(data)
    case 'safety':
      return parseSafety(data)
    case 'card':
      return parseCard(data)
    case 'error':
      return parseError(data)
    case 'done':
      return parseDone(data)
    default:
      return null
  }
}
