import { configureStore } from '@reduxjs/toolkit'
import { act, renderHook, waitFor } from '@testing-library/react'
import { createElement, type ReactNode } from 'react'
import { Provider } from 'react-redux'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { apiSlice } from '~/api/api-slice'
import { conversationApi } from '~/api/conversation.api'
import { clearXsrfCookie, PatchedRequest, seedXsrfCookie } from '~/api/test-helpers'
import { authReducer } from '~/modules/auth/store/auth.slice'

import { useCoachStream } from './use-coach-stream.hooks'

const encoder = new TextEncoder()
const frame = (event: string, data: unknown): string =>
  `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`
const heartbeat = ': hb\n\n'

const sseResponse = (chunks: string[]): Response =>
  new Response(
    new ReadableStream<Uint8Array>({
      start(controller) {
        for (const chunk of chunks) controller.enqueue(encoder.encode(chunk))
        controller.close()
      },
    }),
    { status: 200, headers: { 'Content-Type': 'text/event-stream' } },
  )

const makeControlledStream = () => {
  let controller!: ReadableStreamDefaultController<Uint8Array>
  const stream = new ReadableStream<Uint8Array>({
    start(c) {
      controller = c
    },
  })
  return {
    response: new Response(stream, {
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' },
    }),
    enqueue: (text: string): void => controller.enqueue(encoder.encode(text)),
    close: (): void => controller.close(),
  }
}

const makeStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer, auth: authReducer },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(apiSlice.middleware),
  })

type Store = ReturnType<typeof makeStore>

const makeWrapper =
  (store: Store) =>
  ({ children }: { children: ReactNode }) =>
    createElement(Provider, { store, children })

const timelineTurns = (store: Store) =>
  conversationApi.endpoints.getConversationTimeline.select(undefined)(store.getState()).data
    ?.turns ?? []

// Establish a live timeline cache entry so `updateQueryData` has something to
// patch (the panel mounts the query in production).
const primeTimeline = async (store: Store): Promise<void> => {
  await store.dispatch(conversationApi.endpoints.getConversationTimeline.initiate(undefined))
}

const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

// The hand-rolled hook calls `fetch(stringUrl, init)`; RTK's `fetchBaseQuery`
// (the timeline prime) calls `fetch(new Request(...))`. Normalize both.
const urlOf = (input: unknown): string =>
  typeof input === 'string' ? input : (input as Request).url

const requestBody = (call: unknown[]): { message: string; clientMessageId: string } =>
  JSON.parse((call[1] as RequestInit).body as string)

describe('useCoachStream', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    seedXsrfCookie()
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  const routeFetch = (messagesResponse: Response): void => {
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      if (url.includes('/conversation/messages')) return Promise.resolve(messagesResponse)
      return Promise.resolve(jsonResponse({}))
    })
  }

  it('accumulates token deltas in local state without writing the cache per token, then reconciles once on done', async () => {
    const controlled = makeControlledStream()
    routeFetch(controlled.response)
    const store = makeStore()
    await primeTimeline(store)

    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    let sendPromise!: Promise<void>
    act(() => {
      sendPromise = result.current.send('how was my run?')
    })

    controlled.enqueue(heartbeat)
    controlled.enqueue(frame('token', { delta: 'You ' }))
    controlled.enqueue(frame('token', { delta: 'ran ' }))
    controlled.enqueue(frame('token', { delta: 'well.' }))

    await waitFor(() => expect(result.current.streamingText).toBe('You ran well.'))
    expect(result.current.isStreaming).toBe(true)
    // No per-token cache write — the timeline stays empty until `done`.
    expect(timelineTurns(store)).toHaveLength(0)

    controlled.enqueue(frame('done', { turnId: 'coach-turn-1' }))
    controlled.close()
    await act(async () => {
      await sendPromise
    })

    expect(result.current.isStreaming).toBe(false)
    expect(result.current.streamingText).toBe('')
    const coachTurns = timelineTurns(store).filter(
      (turn) => turn.kind === 1 && turn.interactive?.content === 'You ran well.',
    )
    expect(coachTurns).toHaveLength(1)
    expect(coachTurns[0]?.turnId).toBe('coach-turn-1')
    // The user message is reconciled too, before the coach reply.
    expect(timelineTurns(store).some((turn) => turn.kind === 0)).toBe(true)
  })

  it('surfaces an error frame and retries with a fresh clientMessageId', async () => {
    routeFetch(
      sseResponse([
        heartbeat,
        frame('error', { message: 'My end broke.', retryable: true, retryAfterSeconds: 5 }),
      ]),
    )
    const store = makeStore()
    await primeTimeline(store)

    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('how was my run?')
    })

    expect(result.current.error).toEqual({
      message: 'My end broke.',
      retryable: true,
      retryAfterSeconds: 5,
    })
    expect(result.current.isStreaming).toBe(false)

    const firstId = requestBody(
      fetchMock.mock.calls.filter((c) => urlOf(c[0]).includes('/messages'))[0],
    ).clientMessageId

    await act(async () => {
      await result.current.retry()
    })

    const messageCalls = fetchMock.mock.calls.filter((c) => urlOf(c[0]).includes('/messages'))
    expect(messageCalls).toHaveLength(2)
    const secondId = requestBody(messageCalls[1]).clientMessageId
    expect(secondId).not.toBe(firstId)
  })

  it('attaches credentials and the antiforgery header, and lazily seeds the token on a cold submit', async () => {
    clearXsrfCookie() // cold start — no SPA-readable cookie yet
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/auth/xsrf')) {
        seedXsrfCookie('seeded-token')
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      return Promise.resolve(sseResponse([heartbeat, frame('done', { turnId: 't' })]))
    })
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('hi')
    })

    const seedCall = fetchMock.mock.calls.find((c) => urlOf(c[0]).includes('/auth/xsrf'))
    expect(seedCall).toBeDefined()
    const messageCall = fetchMock.mock.calls.filter((c) => urlOf(c[0]).includes('/messages'))[0]
    const init = messageCall[1] as RequestInit
    expect(init.credentials).toBe('include')
    expect(new Headers(init.headers).get('X-XSRF-TOKEN')).toBe('seeded-token')
  })

  it('dispatches loggedOut on a 401 from the streaming POST', async () => {
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      if (url.includes('/messages')) return Promise.resolve(new Response(null, { status: 401 }))
      return Promise.resolve(jsonResponse({}))
    })
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('hi')
    })

    expect(store.getState().auth.status).toBe('unauthenticated')
  })

  it('surfaces a confirmation card carrying the request clientMessageId, and dismissCard clears it', async () => {
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
    routeFetch(sseResponse([heartbeat, frame('card', { draft, prescription: null })]))
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('ran 5k this morning')
    })

    const sentId = requestBody(
      fetchMock.mock.calls.filter((c) => urlOf(c[0]).includes('/messages'))[0],
    ).clientMessageId
    expect(result.current.card).toEqual({ draft, prescription: null, clientMessageId: sentId })
    expect(result.current.isStreaming).toBe(false)

    act(() => {
      result.current.dismissCard()
    })
    expect(result.current.card).toBeNull()
  })

  it('surfaces a safety notice from a safety frame', async () => {
    routeFetch(
      sseResponse([
        heartbeat,
        frame('safety', { content: 'Call 988', tier: 2, category: 1 }),
        frame('done', { turnId: 's' }),
      ]),
    )
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('I want to hurt myself')
    })

    expect(result.current.safety).toEqual({ content: 'Call 988', tier: 2, category: 1 })
  })
})
