import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import type { CreateWorkoutLogRequest } from '~/api/generated'
import { conversationApi } from './conversation.api'
import { planApi } from './plan.api'
import { clearXsrfCookie, PatchedRequest, seedXsrfCookie } from './test-helpers'
import { workoutLogApi, WORKOUT_HISTORY_PAGE_SIZE } from './workout-log.api'

// Dispatch the endpoint thunk directly with `fetch` stubbed at the global level
// so the `query: (body) => ({...})` factory actually executes (the page spec
// mocks the hook and never reaches the factory). The `PatchedRequest` jsdom
// relative-URL workaround and the XSRF cookie seed/clear pair are shared via
// `test-helpers.ts`.
const jsonResponse = (body: Record<string, unknown>, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const makeStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(apiSlice.middleware),
  })

const SAMPLE_BODY: CreateWorkoutLogRequest = {
  idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
  occurredOn: '2026-06-06',
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: 0,
  metrics: { rpe: 6, hrAvg: 142 },
}

describe('workoutLogApi.createWorkoutLog query factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(jsonResponse({ workoutLogId: 'log-1' }, 201))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
    // Mirror the booted runtime: the antiforgery cookie is present, so the
    // base query's lazy XSRF seed (base-query.ts) stays quiet and the
    // mutation is the only request the factory assertions see.
    seedXsrfCookie()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('issues a POST to /api/v1/workouts/logs with the supplied body', async () => {
    const store = makeStore()
    const result = await store.dispatch(
      workoutLogApi.endpoints.createWorkoutLog.initiate(SAMPLE_BODY),
    )

    expect(result.data).toEqual({ workoutLogId: 'log-1' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request).toBeInstanceOf(Request)
    expect(request.method).toBe('POST')
    expect(request.url).toContain('/api/v1/workouts/logs')
    const sentBody = await request.clone().json()
    expect(sentBody).toEqual(SAMPLE_BODY)
  })
})

describe('workoutLogApi.getWorkoutLogHistory pagination contract', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  const historyPage = (logs: Array<Record<string, unknown>>, nextCursor: string | null): Response =>
    jsonResponse({ logs, nextCursor })

  const sentBody = async (callIndex: number): Promise<Record<string, unknown>> => {
    const request = fetchMock.mock.calls[callIndex][0] as Request
    return request.clone().json()
  }

  beforeEach(() => {
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('threads the keyset cursor: first page sends cursor null, the next page sends the prior nextCursor', async () => {
    fetchMock
      .mockResolvedValueOnce(historyPage([{ workoutLogId: 'w1' }], 'cursor-1'))
      .mockResolvedValueOnce(historyPage([{ workoutLogId: 'w2' }], null))
    const store = makeStore()

    // First (newest) page — initialPageParam is null.
    await store.dispatch(workoutLogApi.endpoints.getWorkoutLogHistory.initiate(undefined))
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(await sentBody(0)).toEqual({ limit: WORKOUT_HISTORY_PAGE_SIZE, cursor: null })

    // "Load older": getNextPageParam returns the prior page's nextCursor, threaded
    // into the next request body.
    await store.dispatch(
      workoutLogApi.endpoints.getWorkoutLogHistory.initiate(undefined, { direction: 'forward' }),
    )
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(await sentBody(1)).toEqual({ limit: WORKOUT_HISTORY_PAGE_SIZE, cursor: 'cursor-1' })
  })

  it('stops paginating once nextCursor is null (getNextPageParam returns undefined)', async () => {
    fetchMock.mockResolvedValue(historyPage([{ workoutLogId: 'w1' }], null))
    const store = makeStore()

    await store.dispatch(workoutLogApi.endpoints.getWorkoutLogHistory.initiate(undefined))
    expect(fetchMock).toHaveBeenCalledTimes(1)

    // The first page exhausted the cursor, so a forward fetch must be a no-op.
    await store.dispatch(
      workoutLogApi.endpoints.getWorkoutLogHistory.initiate(undefined, { direction: 'forward' }),
    )
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})

describe('createWorkoutLog cache invalidation (spec 17 § Unit 7)', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  const callsTo = (path: string): number =>
    fetchMock.mock.calls.filter((call) => (call[0] as Request).url.includes(path)).length

  beforeEach(() => {
    // URL-discriminating stub: a create can synchronously adapt the plan and
    // append a conversation turn, so the subscribed plan + conversation
    // queries must refetch after a successful create.
    fetchMock = vi.fn().mockImplementation((input: Request) => {
      if (input.url.includes('/api/v1/conversation/timeline')) {
        return Promise.resolve(jsonResponse({ turns: [] }))
      }
      if (input.url.includes('/api/v1/plan/current')) {
        return Promise.resolve(jsonResponse({ planId: 'plan-1' }))
      }
      return Promise.resolve(jsonResponse({ workoutLogId: 'log-1' }, 201))
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
    // Mirror the booted runtime: the antiforgery cookie is present, so the
    // base query's lazy XSRF seed (base-query.ts) stays quiet and the
    // call counts below track only the plan/conversation/create requests.
    seedXsrfCookie()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('refetches subscribed Plan and Conversation queries after a successful create', async () => {
    const store = makeStore()

    // Mount-equivalent subscriptions: home renders the plan view + the
    // timeline-backed coach chat, each holding a live query subscription.
    await store.dispatch(planApi.endpoints.getCurrentPlan.initiate(undefined))
    await store.dispatch(conversationApi.endpoints.getConversationTimeline.initiate(undefined))
    expect(callsTo('/api/v1/plan/current')).toBe(1)
    expect(callsTo('/api/v1/conversation/timeline')).toBe(1)

    await store.dispatch(workoutLogApi.endpoints.createWorkoutLog.initiate(SAMPLE_BODY))

    // The `invalidatesTags` callback returns the tags only when `error` is
    // undefined; both subscribed queries refetch in the same interaction —
    // the plan re-renders and the timeline picks up the new turn.
    await vi.waitFor(() => {
      expect(callsTo('/api/v1/plan/current')).toBe(2)
      expect(callsTo('/api/v1/conversation/timeline')).toBe(2)
    })
  })

  it('does not refetch Plan or Conversation when the create fails', async () => {
    // RTK Query applies a *static* `invalidatesTags` array on rejected-with-value
    // mutations too, so the callback form must return `[]` on error. A failed
    // create must not refetch the plan view or replay a stale timeline.
    fetchMock.mockImplementation((input: Request) => {
      if (input.url.includes('/api/v1/conversation/timeline')) {
        return Promise.resolve(jsonResponse({ turns: [] }))
      }
      if (input.url.includes('/api/v1/plan/current')) {
        return Promise.resolve(jsonResponse({ planId: 'plan-1' }))
      }
      // The create rejects with a 400 (e.g. an antiforgery failure) — a
      // rejected-with-value base-query error, not a thrown exception.
      return Promise.resolve(jsonResponse({ title: 'bad request' }, 400))
    })
    const store = makeStore()

    await store.dispatch(planApi.endpoints.getCurrentPlan.initiate(undefined))
    await store.dispatch(conversationApi.endpoints.getConversationTimeline.initiate(undefined))
    expect(callsTo('/api/v1/plan/current')).toBe(1)
    expect(callsTo('/api/v1/conversation/timeline')).toBe(1)

    const result = await store.dispatch(
      workoutLogApi.endpoints.createWorkoutLog.initiate(SAMPLE_BODY),
    )
    expect('error' in result).toBe(true)

    // Give any (incorrect) invalidation a chance to fire before asserting the
    // counts held — the failed create must leave both subscriptions untouched.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(callsTo('/api/v1/plan/current')).toBe(1)
    expect(callsTo('/api/v1/conversation/timeline')).toBe(1)
  })
})
