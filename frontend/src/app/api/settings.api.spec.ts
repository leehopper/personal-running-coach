import { configureStore } from '@reduxjs/toolkit'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import type { UnitPreferenceDto } from '~/api/generated'
import { clearXsrfCookie, PatchedRequest, seedXsrfCookie } from './test-helpers'
import { settingsApi } from './settings.api'

// Dispatch the endpoint thunks directly with `fetch` stubbed at the global
// level so the real `query: () => ({...})` factories and the success-gated
// `invalidatesTags` callback execute (the component spec mocks the hooks and
// never reaches them). Mirrors `workout-log.api.spec.ts`.
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

describe('settingsApi query factories', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    // A fresh `Response` per call — the same object cannot be `.clone()`d twice
    // (undici: "Body has already been consumed").
    fetchMock = vi.fn().mockImplementation(() => jsonResponse({ preferredUnits: 1 }))
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
    // Mirror the booted runtime: the antiforgery cookie is present, so the base
    // query's lazy XSRF seed stays quiet and the assertions see only the request
    // under test.
    seedXsrfCookie()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearXsrfCookie()
  })

  it('issues a GET to /api/v1/settings/units', async () => {
    const store = makeStore()
    const result = await store.dispatch(settingsApi.endpoints.getUnitPreference.initiate(undefined))

    expect(result.data).toEqual({ preferredUnits: 1 })
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('GET')
    expect(request.url).toContain('/api/v1/settings/units')
  })

  it('issues a PUT to /api/v1/settings/units with the preferred-units body', async () => {
    const store = makeStore()
    const body: UnitPreferenceDto = { preferredUnits: 1 }

    await store.dispatch(settingsApi.endpoints.putUnitPreference.initiate(body))

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const request = fetchMock.mock.calls[0][0] as Request
    expect(request.method).toBe('PUT')
    expect(request.url).toContain('/api/v1/settings/units')
    expect(await request.clone().json()).toEqual(body)
  })
})

describe('putUnitPreference cache invalidation', () => {
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

  it('refetches the subscribed unit preference after a successful save', async () => {
    fetchMock = vi.fn().mockImplementation(() => jsonResponse({ preferredUnits: 1 }))
    vi.stubGlobal('fetch', fetchMock)
    const store = makeStore()

    // A live subscription on the GET, as the Units toggle holds while mounted.
    await store.dispatch(settingsApi.endpoints.getUnitPreference.initiate(undefined))
    expect(callsTo('GET')).toBe(1)

    await store.dispatch(settingsApi.endpoints.putUnitPreference.initiate({ preferredUnits: 1 }))

    // The success-gated `invalidatesTags` callback returns ['UserSettings'], so
    // the subscribed GET refetches in the same interaction.
    await vi.waitFor(() => expect(callsTo('GET')).toBe(2))
  })

  it('does not refetch the unit preference when the save fails', async () => {
    // RTK Query applies a *static* `invalidatesTags` array on rejected-with-value
    // mutations too, so the callback form must return `[]` on error. A failed PUT
    // must not refetch the preference.
    fetchMock = vi
      .fn()
      .mockImplementation((input: Request) =>
        input.method === 'PUT'
          ? Promise.resolve(jsonResponse({ title: 'bad request' }, 400))
          : Promise.resolve(jsonResponse({ preferredUnits: 0 })),
      )
    vi.stubGlobal('fetch', fetchMock)
    const store = makeStore()

    await store.dispatch(settingsApi.endpoints.getUnitPreference.initiate(undefined))
    expect(callsTo('GET')).toBe(1)

    const result = await store.dispatch(
      settingsApi.endpoints.putUnitPreference.initiate({ preferredUnits: 1 }),
    )
    expect('error' in result).toBe(true)

    // Give any (incorrect) invalidation a chance to fire before asserting the
    // count held — the failed save must leave the subscription untouched.
    await new Promise((resolve) => setTimeout(resolve, 50))
    expect(callsTo('GET')).toBe(1)
  })
})
