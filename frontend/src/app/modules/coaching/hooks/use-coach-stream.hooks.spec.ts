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
    // Reject the in-flight `reader.read()` — what a signal-honouring `fetch`
    // raises once its AbortController fires (the stubbed fetch ignores the signal).
    error: (reason: unknown): void => controller.error(reason),
  }
}

const abortError = (): DOMException => new DOMException('The user aborted a request.', 'AbortError')

// Pulls the AbortSignal off a recorded `fetch(input, init)` call, narrowing at
// runtime rather than asserting a fixed call shape.
const messageSignal = (call: unknown[] | undefined): AbortSignal => {
  const init = call?.[1]
  if (
    typeof init === 'object' &&
    init !== null &&
    'signal' in init &&
    init.signal instanceof AbortSignal
  ) {
    return init.signal
  }
  throw new Error('expected the fetch call to carry an AbortSignal')
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

  it('surfaces a retry if the body closes after tokens with no terminal frame', async () => {
    // Defense in depth: the backend always emits a done/error frame on a live
    // connection, but a body that closes mid-stream must not freeze a partial.
    routeFetch(sseResponse([heartbeat, frame('token', { delta: 'partial answer' })]))
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('how was my run?')
    })

    expect(result.current.error?.retryable).toBe(true)
    expect(result.current.streamingText).toBe('')
    expect(result.current.pendingUserMessage).toBeNull()
    expect(result.current.isStreaming).toBe(false)
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

  it('aborts the live request when a new send starts, without duplicating the user turn', async () => {
    const first = makeControlledStream()
    const second = makeControlledStream()
    let messageCall = 0
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      if (url.includes('/messages')) {
        messageCall += 1
        return Promise.resolve(messageCall === 1 ? first.response : second.response)
      }
      return Promise.resolve(jsonResponse({}))
    })
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    let firstSend!: Promise<void>
    act(() => {
      firstSend = result.current.send('first question')
    })
    first.enqueue(frame('token', { delta: 'partial' }))
    await waitFor(() => expect(result.current.streamingText).toBe('partial'))

    // A second send before the first terminates must abort the first request.
    let secondSend!: Promise<void>
    act(() => {
      secondSend = result.current.send('second question')
    })
    await waitFor(() =>
      expect(fetchMock.mock.calls.filter((c) => urlOf(c[0]).includes('/messages'))).toHaveLength(2),
    )
    const messageCalls = fetchMock.mock.calls.filter((c) => urlOf(c[0]).includes('/messages'))
    expect(messageSignal(messageCalls[0]).aborted).toBe(true)

    // Let the aborted first reader unwind silently, then complete the second.
    first.error(abortError())
    await firstSend
    second.enqueue(frame('done', { turnId: 'coach-2' }))
    second.close()
    await act(async () => {
      await secondSend
    })

    const userTurns = timelineTurns(store).filter((turn) => turn.kind === 0)
    expect(userTurns).toHaveLength(1)
    expect(userTurns[0]?.interactive?.content).toBe('second question')
    expect(result.current.error).toBeNull()
  })

  it('aborts the in-flight reader on unmount and cancels silently', async () => {
    const controlled = makeControlledStream()
    routeFetch(controlled.response)
    const store = makeStore()
    await primeTimeline(store)
    const { result, unmount } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    let sendPromise!: Promise<void>
    act(() => {
      sendPromise = result.current.send('how was my run?')
    })
    controlled.enqueue(frame('token', { delta: 'partial' }))
    await waitFor(() => expect(result.current.streamingText).toBe('partial'))

    const signal = messageSignal(
      fetchMock.mock.calls.find((c) => urlOf(c[0]).includes('/messages')),
    )
    expect(signal.aborted).toBe(false)

    // Unmounting mid-stream aborts the controller; simulate the reader rejection
    // a signal-honouring fetch would then raise.
    unmount()
    expect(signal.aborted).toBe(true)
    controlled.error(abortError())
    await act(async () => {
      await sendPromise
    })

    // Silent cancel: no terminal error surfaced and no turn reconciled.
    expect(result.current.error).toBeNull()
    expect(timelineTurns(store)).toHaveLength(0)
  })

  it('surfaces a retryable error on a non-401 failure response and appends no turn', async () => {
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      if (url.includes('/messages'))
        return Promise.resolve(new Response('upstream unavailable', { status: 500 }))
      return Promise.resolve(jsonResponse({}))
    })
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('how was my run?')
    })

    expect(result.current.error?.retryable).toBe(true)
    expect(result.current.isStreaming).toBe(false)
    expect(store.getState().auth.status).not.toBe('unauthenticated')
    expect(timelineTurns(store)).toHaveLength(0)
  })

  it('surfaces a retryable error when a 200 response carries no body', async () => {
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      if (url.includes('/messages')) return Promise.resolve(new Response(null, { status: 200 }))
      return Promise.resolve(jsonResponse({}))
    })
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('how was my run?')
    })

    expect(result.current.error?.retryable).toBe(true)
    expect(result.current.isStreaming).toBe(false)
  })

  it('surfaces a retryable error when the network request rejects', async () => {
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline'))
        return Promise.resolve(jsonResponse({ turns: [] }))
      if (url.includes('/messages')) return Promise.reject(new TypeError('Failed to fetch'))
      return Promise.resolve(jsonResponse({}))
    })
    const store = makeStore()
    await primeTimeline(store)
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('how was my run?')
    })

    expect(result.current.error?.retryable).toBe(true)
    expect(result.current.isStreaming).toBe(false)
    expect(timelineTurns(store)).toHaveLength(0)
  })

  it('reconciles via cache invalidation when the optimistic push lands on a cold timeline', async () => {
    // Server truth the post-stream refetch returns — by `done` the backend has
    // persisted both turns. The mount GET is held pending through the stream so
    // the optimistic push has no cache `data` to patch.
    const serverTurns = [
      {
        kind: 0,
        turnId: 'u1',
        createdAt: '2026-06-30T00:00:00Z',
        interactive: { content: 'how was my run?', isErrored: false, loggedRun: null },
        proactive: null,
      },
      {
        kind: 1,
        turnId: 'coach-1',
        createdAt: '2026-06-30T00:00:01Z',
        interactive: { content: 'You ran well.', isErrored: false, loggedRun: null },
        proactive: null,
      },
    ]
    let timelineCalls = 0
    let resolveMountTimeline!: () => void
    const mountTimeline = new Promise<void>((resolve) => {
      resolveMountTimeline = resolve
    })
    fetchMock.mockImplementation((input: unknown) => {
      const url = urlOf(input)
      if (url.includes('/conversation/timeline')) {
        timelineCalls += 1
        // The mount GET stays pending through the stream; the post-invalidation
        // refetch returns the server-persisted turns.
        return timelineCalls === 1
          ? mountTimeline.then(() => jsonResponse({ turns: [] }))
          : Promise.resolve(jsonResponse({ turns: serverTurns }))
      }
      if (url.includes('/messages'))
        return Promise.resolve(
          sseResponse([
            heartbeat,
            frame('token', { delta: 'You ran well.' }),
            frame('done', { turnId: 'coach-1' }),
          ]),
        )
      return Promise.resolve(jsonResponse({}))
    })
    const store = makeStore()
    // Subscribe to the timeline (mirrors the mounted chat) but DO NOT await it —
    // the cache stays cold while the GET is in flight.
    const subscription = store.dispatch(
      conversationApi.endpoints.getConversationTimeline.initiate(undefined),
    )
    const { result } = renderHook(() => useCoachStream(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.send('how was my run?')
    })

    // The optimistic push silently no-op'd on the cold cache.
    expect(timelineTurns(store)).toHaveLength(0)

    // Settle the mount GET; the deferred invalidation now flushes and refetches.
    await act(async () => {
      resolveMountTimeline()
      await mountTimeline
    })
    await waitFor(() => expect(timelineCalls).toBeGreaterThanOrEqual(2))
    await waitFor(() =>
      expect(
        timelineTurns(store).some((turn) => turn.interactive?.content === 'You ran well.'),
      ).toBe(true),
    )

    subscription.unsubscribe()
  })
})
