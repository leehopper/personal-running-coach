import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import { planApi } from './plan.api'
import { clearXsrfCookie, PatchedRequest, seedXsrfCookie } from './test-helpers'

// Build a real store wired to the shared `apiSlice` so the `regeneratePlan`
// mutation's `query: (body) => ({...})` factory and the `getCurrentPlan`
// query factory actually execute. The dialog spec mocks
// `useRegeneratePlanMutation` and so never reaches the factory; this spec
// dispatches the endpoint thunks directly with `fetch` stubbed at the global
// level (matching the pattern in `base-query.spec.ts`). The `PatchedRequest`
// jsdom relative-URL workaround and the XSRF cookie seed/clear pair are shared
// via `test-helpers.ts`.
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

describe('planApi.regeneratePlan query factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(jsonResponse({ planId: 'plan-1', status: 'generated' }))
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

  it('issues a POST to /api/v1/plan/regenerate with the supplied body', async () => {
    const store = makeStore()
    const result = await store.dispatch(
      planApi.endpoints.regeneratePlan.initiate({
        idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
        intent: { freeText: 'less volume' },
      }),
    )

    expect(result.data).toEqual({ planId: 'plan-1', status: 'generated' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request).toBeInstanceOf(Request)
    expect(request.method).toBe('POST')
    expect(request.url).toContain('/api/v1/plan/regenerate')
    const sentBody = await request.clone().json()
    expect(sentBody).toEqual({
      idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
      intent: { freeText: 'less volume' },
    })
  })

  it('serializes a body without the optional intent block when omitted', async () => {
    const store = makeStore()
    await store.dispatch(
      planApi.endpoints.regeneratePlan.initiate({
        idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
      }),
    )

    const request = fetchMock.mock.calls[0][0] as Request
    const sentBody = await request.clone().json()
    expect(sentBody).toEqual({ idempotencyKey: '00000000-0000-0000-0000-00000000abcd' })
  })
})

describe('planApi.getCurrentPlan query factory', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        planId: 'plan-1',
        userId: 'user-1',
        generatedAt: '2026-04-25T15:00:00.000Z',
        planStartDate: '2026-04-19',
        previousPlanId: null,
        promptVersion: 'coaching-v1',
        modelId: 'claude-sonnet-4-6',
        macro: null,
        mesoWeeks: [],
        microWorkoutsByWeek: {},
      }),
    )
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('issues a GET to /api/v1/plan/current', async () => {
    const store = makeStore()
    const result = await store.dispatch(planApi.endpoints.getCurrentPlan.initiate(undefined))

    expect(result.data?.planId).toBe('plan-1')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('GET')
    expect(request.url).toContain('/api/v1/plan/current')
  })
})
