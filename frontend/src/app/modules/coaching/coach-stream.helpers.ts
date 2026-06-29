// Pure helpers for the streaming Q&A SSE wire (`POST /api/v1/conversation/messages`).
// `createSseDecoder` is an incremental parser that buffers across chunk
// boundaries, skips comment lines (the `: hb` heartbeat), and surfaces each
// completed `event:`/`data:` block once. `toCoachStreamFrame` narrows a raw
// event into the typed {@link CoachStreamFrame} union. Both are kept side-effect
// free so the hand-rolled `useCoachStream` fetch reader stays thin and testable.

import type {
  CardFrame,
  CoachStreamFrame,
  DoneFrame,
  ErrorFrame,
  SafetyFrame,
  TokenFrame,
} from '~/modules/coaching/models/coach-stream.model'

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

/**
 * Narrows a raw SSE event into a typed {@link CoachStreamFrame}, returning null
 * for an unknown event name or malformed JSON (`ping`/unknown SSE events are
 * tolerated by the caller).
 */
export const toCoachStreamFrame = (raw: SseEvent): CoachStreamFrame | null => {
  let data: unknown
  try {
    data = JSON.parse(raw.data)
  } catch {
    return null
  }

  switch (raw.event) {
    case 'token':
      return { event: 'token', ...(data as Omit<TokenFrame, 'event'>) }
    case 'safety':
      return { event: 'safety', ...(data as Omit<SafetyFrame, 'event'>) }
    case 'card':
      return { event: 'card', ...(data as Omit<CardFrame, 'event'>) }
    case 'error':
      return { event: 'error', ...(data as Omit<ErrorFrame, 'event'>) }
    case 'done':
      return { event: 'done', ...(data as Omit<DoneFrame, 'event'>) }
    default:
      return null
  }
}
