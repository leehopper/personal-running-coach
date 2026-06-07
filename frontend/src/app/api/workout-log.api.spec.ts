import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import type { CreateWorkoutLogRequest } from '~/api/generated'
import { workoutLogApi } from './workout-log.api'

// Dispatch the endpoint thunk directly with `fetch` stubbed at the global level
// so the `query: (body) => ({...})` factory actually executes (the page spec
// mocks the hook and never reaches the factory). Mirrors plan.api.spec.ts.
const OriginalRequest = globalThis.Request
class PatchedRequest extends OriginalRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    if (typeof input === 'string' && input.startsWith('/')) {
      super(new URL(input, 'https://localhost:5173').toString(), init)
      return
    }
    super(input, init)
  }
}

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
  })

  afterEach(() => {
    vi.unstubAllGlobals()
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
