import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { apiSlice } from '~/api/api-slice'
import type { SubmitStructuredAnswersRequest } from '~/modules/onboarding/models/onboarding.model'
import { PreferredUnits } from '~/api/generated'
import { PrimaryGoal } from '~/modules/onboarding/models/onboarding.model'
import { clearXsrfCookie, PatchedRequest, seedXsrfCookie } from './test-helpers'
import { onboardingApi } from './onboarding.api'

// Dispatch the endpoint thunks directly with `fetch` stubbed globally so the
// real `query: () => ({...})` factory and the success-gated `invalidatesTags`
// callback execute (the component spec mocks the hooks and never reaches them).
// Mirrors `settings.api.spec.ts` / `workout-log.api.spec.ts`.

const completedState = {
  userId: 'u1',
  status: 2,
  currentTopic: null,
  completedTopics: 6,
  totalTopics: 6,
  isComplete: true,
  outstandingClarifications: [],
  primaryGoal: { goal: 0, description: '' },
  targetEvent: null,
  currentFitness: null,
  weeklySchedule: null,
  injuryHistory: null,
  preferences: null,
  currentPlanId: 'plan-1',
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

const sampleRequest = (): SubmitStructuredAnswersRequest => ({
  idempotencyKey: '11111111-1111-1111-1111-111111111111',
  primaryGoal: { goal: PrimaryGoal.GeneralFitness, description: '' },
  targetEvent: null,
  currentFitness: {
    typicalWeeklyKm: 40,
    longestRecentRunKm: 18,
    recentRaceDistanceKm: null,
    recentRaceTimeIso: null,
    description: '',
  },
  weeklySchedule: {
    maxRunDaysPerWeek: 4,
    typicalSessionMinutes: 45,
    monday: true,
    tuesday: false,
    wednesday: true,
    thursday: false,
    friday: false,
    saturday: true,
    sunday: false,
    description: '',
  },
  injuryHistory: { hasActiveInjury: false, activeInjuryDescription: '', pastInjurySummary: '' },
  preferences: {
    preferredUnits: PreferredUnits.Kilometers,
    preferTrail: false,
    comfortableWithIntensity: true,
    description: '',
  },
})

describe('onboardingApi query factories', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockImplementation(() => jsonResponse(completedState))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
    seedXsrfCookie()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('issues a GET to /api/v1/onboarding/state', async () => {
    const store = makeStore()
    await store.dispatch(onboardingApi.endpoints.getOnboardingState.initiate(undefined))

    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('GET')
    expect(request.url).toContain('/api/v1/onboarding/state')
  })

  it('posts the structured answers to /api/v1/onboarding/answers', async () => {
    const store = makeStore()
    const body = sampleRequest()

    await store.dispatch(onboardingApi.endpoints.submitStructuredAnswers.initiate(body))

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('POST')
    expect(request.url).toContain('/api/v1/onboarding/answers')
    expect(await request.clone().json()).toEqual(body)
  })
})

describe('submitStructuredAnswers cache invalidation', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  const callsTo = (method: string): number =>
    fetchMock.mock.calls.filter((call) => (call[0] as Request).method === method).length

  beforeEach(() => {
    vi.stubGlobal('Request', PatchedRequest)
    seedXsrfCookie()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('refetches the subscribed onboarding state after a successful submit', async () => {
    fetchMock = vi.fn().mockImplementation(() => jsonResponse(completedState))
    vi.stubGlobal('fetch', fetchMock)
    const store = makeStore()

    // A live subscription on the state query, as the redirect guard holds.
    await store.dispatch(onboardingApi.endpoints.getOnboardingState.initiate(undefined))
    expect(callsTo('GET')).toBe(1)

    await store.dispatch(onboardingApi.endpoints.submitStructuredAnswers.initiate(sampleRequest()))

    // The success-gated `invalidatesTags` returns ['Onboarding'], so the
    // subscribed state query refetches in the same interaction.
    await vi.waitFor(() => expect(callsTo('GET')).toBe(2))
  })

  it('does not refetch the onboarding state when the submit fails', async () => {
    fetchMock = vi
      .fn()
      .mockImplementation((input: Request) =>
        input.method === 'POST'
          ? Promise.resolve(jsonResponse({ title: 'invalid answers' }, 400))
          : Promise.resolve(jsonResponse(completedState)),
      )
    vi.stubGlobal('fetch', fetchMock)
    const store = makeStore()

    await store.dispatch(onboardingApi.endpoints.getOnboardingState.initiate(undefined))
    expect(callsTo('GET')).toBe(1)

    const result = await store.dispatch(
      onboardingApi.endpoints.submitStructuredAnswers.initiate(sampleRequest()),
    )
    expect('error' in result).toBe(true)

    // Give any (incorrect) invalidation a chance to fire before asserting the
    // count held — a rejected submit must leave the subscription untouched.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(callsTo('GET')).toBe(1)
  })
})
