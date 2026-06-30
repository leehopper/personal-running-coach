import { describe, expect, it } from 'vitest'

import { createSseDecoder, toCoachStreamFrame } from './coach-stream.helpers'

// The backend `SseWriter` emits typed frames as `event: {name}\ndata: {compactJson}\n\n`
// (LF line endings) and an opening heartbeat comment `: hb\n\n`. The decoder must
// buffer across chunk boundaries, skip comment lines, and surface each completed
// event exactly once.

describe('createSseDecoder', () => {
  it('decodes a single complete frame into one event', () => {
    const decoder = createSseDecoder()

    const events = decoder.push('event: token\ndata: {"delta":"hi"}\n\n')

    expect(events).toEqual([{ event: 'token', data: '{"delta":"hi"}' }])
  })

  it('skips the opening heartbeat comment and only surfaces real frames', () => {
    const decoder = createSseDecoder()

    const events = decoder.push(': hb\n\nevent: done\ndata: {"turnId":"abc"}\n\n')

    expect(events).toEqual([{ event: 'done', data: '{"turnId":"abc"}' }])
  })

  it('buffers a frame split across two chunks and emits it once on completion', () => {
    const decoder = createSseDecoder()

    const first = decoder.push('event: token\ndata: {"del')
    const second = decoder.push('ta":"run"}\n\n')

    expect(first).toEqual([])
    expect(second).toEqual([{ event: 'token', data: '{"delta":"run"}' }])
  })

  it('returns multiple frames from a single chunk in order', () => {
    const decoder = createSseDecoder()

    const events = decoder.push(
      'event: token\ndata: {"delta":"a"}\n\nevent: token\ndata: {"delta":"b"}\n\n',
    )

    expect(events).toEqual([
      { event: 'token', data: '{"delta":"a"}' },
      { event: 'token', data: '{"delta":"b"}' },
    ])
  })

  it('does not emit an incomplete trailing frame', () => {
    const decoder = createSseDecoder()

    const events = decoder.push('event: token\ndata: {"delta":"a"}\n\nevent: done\ndata: {')

    expect(events).toEqual([{ event: 'token', data: '{"delta":"a"}' }])
  })
})

describe('toCoachStreamFrame', () => {
  it('parses a token event into a typed token frame', () => {
    expect(toCoachStreamFrame({ event: 'token', data: '{"delta":"hi"}' })).toEqual({
      event: 'token',
      delta: 'hi',
    })
  })

  it('parses a done event into a typed done frame', () => {
    expect(toCoachStreamFrame({ event: 'done', data: '{"turnId":"t-1"}' })).toEqual({
      event: 'done',
      turnId: 't-1',
    })
  })

  it('parses a card event with a null prescription', () => {
    const draft = {
      occurredOn: '2026-06-29',
      distanceValue: 5,
      distanceUnit: 0,
      durationHours: 0,
      durationMinutes: 25,
      durationSeconds: 0,
      completionStatus: 0,
      notes: null,
    }

    expect(
      toCoachStreamFrame({
        event: 'card',
        data: JSON.stringify({ draft, prescription: null }),
      }),
    ).toEqual({ event: 'card', draft, prescription: null })
  })

  it('parses a safety event with integer tier and category', () => {
    expect(
      toCoachStreamFrame({
        event: 'safety',
        data: '{"content":"Call 988","tier":2,"category":1}',
      }),
    ).toEqual({ event: 'safety', content: 'Call 988', tier: 2, category: 1 })
  })

  it('parses an error event preserving a null retryAfterSeconds', () => {
    expect(
      toCoachStreamFrame({
        event: 'error',
        data: '{"message":"oops","retryable":true,"retryAfterSeconds":null}',
      }),
    ).toEqual({ event: 'error', message: 'oops', retryable: true, retryAfterSeconds: null })
  })

  it('returns null for an unknown event name', () => {
    expect(toCoachStreamFrame({ event: 'ping', data: '{}' })).toBeNull()
  })

  it('returns null for malformed JSON data', () => {
    expect(toCoachStreamFrame({ event: 'token', data: '{not json' })).toBeNull()
  })

  it('returns null for a token frame missing its delta (guards against "undefined" text)', () => {
    expect(toCoachStreamFrame({ event: 'token', data: '{"foo":"bar"}' })).toBeNull()
  })

  it('returns null for a safety frame whose tier is not a number', () => {
    expect(
      toCoachStreamFrame({ event: 'safety', data: '{"content":"x","tier":"red","category":1}' }),
    ).toBeNull()
  })

  it('returns null for a card frame missing its draft', () => {
    expect(toCoachStreamFrame({ event: 'card', data: '{"prescription":null}' })).toBeNull()
  })

  it('returns null for a done frame missing its turnId', () => {
    expect(toCoachStreamFrame({ event: 'done', data: '{}' })).toBeNull()
  })

  it('coerces a missing retryAfterSeconds on an error frame to null', () => {
    expect(
      toCoachStreamFrame({ event: 'error', data: '{"message":"x","retryable":true}' }),
    ).toEqual({
      event: 'error',
      message: 'x',
      retryable: true,
      retryAfterSeconds: null,
    })
  })
})
