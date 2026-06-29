import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import type { ConfirmConversationalLogRequestDto, StructuredLogDraft } from '~/api/generated'
import type {
  ConversationTimelineDto,
  ConversationTurnsResponseDto,
} from '~/modules/coaching/models/conversation.model'
import { conversationApi } from './conversation.api'
import { planApi } from './plan.api'
import { clearXsrfCookie, PatchedRequest, seedXsrfCookie } from './test-helpers'

// Dispatch the endpoint thunk directly with `fetch` stubbed at the global level
// so the `query: () => ({...})` factory actually executes (the page spec mocks
// the hook and never reaches the factory). The `PatchedRequest` jsdom
// relative-URL workaround is shared via `test-helpers.ts`.
const jsonResponse = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const makeStore = () =>
  configureStore({
    reducer: { [apiSlice.reducerPath]: apiSlice.reducer },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(apiSlice.middleware),
  })

const EMPTY_RESPONSE: ConversationTurnsResponseDto = { turns: [] }
const EMPTY_TIMELINE: ConversationTimelineDto = { turns: [] }

const SAMPLE_DRAFT: StructuredLogDraft = {
  occurredOn: '2026-06-20',
  distanceValue: 5,
  distanceUnit: 0,
  durationHours: 0,
  durationMinutes: 25,
  durationSeconds: 0,
  completionStatus: 0,
  notes: null,
}

const SAMPLE_CONFIRM_BODY: ConfirmConversationalLogRequestDto = {
  draft: SAMPLE_DRAFT,
  clientMessageId: '00000000-0000-0000-0000-0000000000c1',
}

describe('conversationApi.getConversationTurns query factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(jsonResponse(EMPTY_RESPONSE))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('issues a GET to /api/v1/conversation/turns and surfaces the turns payload', async () => {
    const store = makeStore()
    const result = await store.dispatch(
      conversationApi.endpoints.getConversationTurns.initiate(undefined),
    )

    expect(result.data).toEqual(EMPTY_RESPONSE)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request).toBeInstanceOf(Request)
    expect(request.method).toBe('GET')
    expect(request.url).toContain('/api/v1/conversation/turns')
  })
})

describe('conversationApi.getConversationTimeline query factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(jsonResponse(EMPTY_TIMELINE))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('issues a GET to /api/v1/conversation/timeline and surfaces the timeline payload', async () => {
    const store = makeStore()
    const result = await store.dispatch(
      conversationApi.endpoints.getConversationTimeline.initiate(undefined),
    )

    expect(result.data).toEqual(EMPTY_TIMELINE)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('GET')
    expect(request.url).toContain('/api/v1/conversation/timeline')
  })
})

describe('conversationApi.confirmConversationalLog mutation factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        workoutLogId: 'log-7',
        adaptation: { kind: 0, adaptationKind: 0, retryable: false },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
    seedXsrfCookie()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('issues a POST to /api/v1/conversation/logs/confirm with the draft + clientMessageId', async () => {
    const store = makeStore()
    const result = await store.dispatch(
      conversationApi.endpoints.confirmConversationalLog.initiate(SAMPLE_CONFIRM_BODY),
    )

    expect('data' in result && result.data).toEqual({
      workoutLogId: 'log-7',
      adaptation: { kind: 0, adaptationKind: 0, retryable: false },
    })
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('POST')
    expect(request.url).toContain('/api/v1/conversation/logs/confirm')
    const sentBody = await request.clone().json()
    expect(sentBody).toEqual(SAMPLE_CONFIRM_BODY)
  })
})

describe('confirmConversationalLog cache invalidation', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  const callsTo = (path: string): number =>
    fetchMock.mock.calls.filter((call) => (call[0] as Request).url.includes(path)).length

  const stubRoutes = (confirmResponse: Response) =>
    vi.fn().mockImplementation((input: Request) => {
      if (input.url.includes('/api/v1/conversation/timeline')) {
        return Promise.resolve(jsonResponse(EMPTY_TIMELINE))
      }
      if (input.url.includes('/api/v1/plan/current')) {
        return Promise.resolve(jsonResponse({ planId: 'plan-1' }))
      }
      return Promise.resolve(confirmResponse)
    })

  beforeEach(() => {
    seedXsrfCookie()
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('refetches subscribed Plan and Conversation queries after a successful confirm', async () => {
    fetchMock = stubRoutes(
      jsonResponse({
        workoutLogId: 'log-7',
        adaptation: { kind: 0, adaptationKind: 0, retryable: false },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)
    const store = makeStore()

    await store.dispatch(planApi.endpoints.getCurrentPlan.initiate(undefined))
    await store.dispatch(conversationApi.endpoints.getConversationTimeline.initiate(undefined))
    expect(callsTo('/api/v1/plan/current')).toBe(1)
    expect(callsTo('/api/v1/conversation/timeline')).toBe(1)

    await store.dispatch(
      conversationApi.endpoints.confirmConversationalLog.initiate(SAMPLE_CONFIRM_BODY),
    )

    await vi.waitFor(() => {
      expect(callsTo('/api/v1/plan/current')).toBe(2)
      expect(callsTo('/api/v1/conversation/timeline')).toBe(2)
    })
  })

  it('does not refetch Plan or Conversation when the confirm fails', async () => {
    fetchMock = stubRoutes(jsonResponse({ title: 'bad request' }, 400))
    vi.stubGlobal('fetch', fetchMock)
    const store = makeStore()

    await store.dispatch(planApi.endpoints.getCurrentPlan.initiate(undefined))
    await store.dispatch(conversationApi.endpoints.getConversationTimeline.initiate(undefined))
    expect(callsTo('/api/v1/plan/current')).toBe(1)
    expect(callsTo('/api/v1/conversation/timeline')).toBe(1)

    const result = await store.dispatch(
      conversationApi.endpoints.confirmConversationalLog.initiate(SAMPLE_CONFIRM_BODY),
    )
    expect('error' in result).toBe(true)

    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(callsTo('/api/v1/plan/current')).toBe(1)
    expect(callsTo('/api/v1/conversation/timeline')).toBe(1)
  })
})
