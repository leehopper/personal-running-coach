import { act, renderHook, waitFor } from '@testing-library/react'
import { configureStore } from '@reduxjs/toolkit'
import type { ReactNode } from 'react'
import { Provider } from 'react-redux'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { apiSlice } from '~/api/api-slice'
import { authApi } from '~/api/auth.api'
import { PatchedRequest, clearXsrfCookie, seedXsrfCookie } from '~/api/test-helpers'
import { postLogoutBroadcast } from '~/modules/auth/lib/broadcast-auth'
import type { AuthState } from '~/modules/auth/models/auth.model'
import { authSlice } from '~/modules/auth/store/auth.slice'
import { useAuthBroadcastListener, useSignOut } from './auth.hooks'

const { reportClientErrorMock } = vi.hoisted(() => ({
  reportClientErrorMock: vi.fn(),
}))

vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: reportClientErrorMock,
}))

class InMemoryBroadcastChannel {
  private static readonly channels = new Map<string, Set<InMemoryBroadcastChannel>>()
  private readonly name: string
  onmessage: ((event: MessageEvent) => void) | null = null

  constructor(name: string) {
    this.name = name
    const channels = InMemoryBroadcastChannel.channels.get(name) ?? new Set()
    channels.add(this)
    InMemoryBroadcastChannel.channels.set(name, channels)
  }

  postMessage(data: unknown): void {
    const channels = InMemoryBroadcastChannel.channels.get(this.name)
    channels?.forEach((channel) => {
      if (channel !== this) channel.onmessage?.(new MessageEvent('message', { data }))
    })
  }

  close(): void {
    InMemoryBroadcastChannel.channels.get(this.name)?.delete(this)
  }

  static reset(): void {
    InMemoryBroadcastChannel.channels.clear()
  }
}

const makeStore = (
  auth: AuthState = {
    status: 'authenticated',
    user: { userId: 'u1', email: 'runner@example.com' },
  },
) =>
  configureStore({
    reducer: {
      [authSlice.name]: authSlice.reducer,
      [apiSlice.reducerPath]: apiSlice.reducer,
    },
    middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(apiSlice.middleware),
    preloadedState: {
      [authSlice.name]: auth,
    },
  })

type Store = ReturnType<typeof makeStore>

const makeWrapper =
  (store: Store) =>
  ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>

const originalBroadcastChannel = globalThis.BroadcastChannel
const originalRequest = globalThis.Request
const originalFetch = globalThis.fetch

const makeResponse = (body: unknown, status = 200): Response =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  })

const requestUrl = (input: RequestInfo | URL): string => {
  if (typeof input === 'string') return input
  if (input instanceof URL) return input.toString()
  return input.url
}

describe('useSignOut', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    vi.stubGlobal('BroadcastChannel', InMemoryBroadcastChannel)
    vi.stubGlobal('Request', PatchedRequest)
    seedXsrfCookie()
    reportClientErrorMock.mockReset()
    fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = requestUrl(input)
      if (url.includes('/v1/auth/logout')) return Promise.resolve(makeResponse(undefined, 204))
      if (url.includes('/v1/auth/me')) {
        return Promise.resolve(makeResponse({ userId: 'u1', email: 'runner@example.com' }))
      }
      return Promise.resolve(makeResponse(undefined, 404))
    })
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    InMemoryBroadcastChannel.reset()
    vi.stubGlobal('BroadcastChannel', originalBroadcastChannel)
    vi.stubGlobal('Request', originalRequest)
    vi.stubGlobal('fetch', originalFetch)
    clearXsrfCookie()
  })

  it('posts logout before marking the store unauthenticated', async () => {
    const store = makeStore()
    let statusDuringPost: AuthState['status'] | undefined
    fetchMock.mockImplementationOnce((input: RequestInfo | URL) => {
      const url = requestUrl(input)
      if (url.includes('/v1/auth/logout')) {
        statusDuringPost = store.getState().auth.status
        return Promise.resolve(makeResponse(undefined, 204))
      }
      return Promise.resolve(makeResponse(undefined, 404))
    })
    const { result } = renderHook(() => useSignOut(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.signOut()
    })

    expect(statusDuringPost).toBe('authenticated')
    expect(store.getState().auth.status).toBe('unauthenticated')
  })

  it('purges subscribed RTK Query state and refetches on resubscribe', async () => {
    const store = makeStore()
    const subscription = store.dispatch(authApi.endpoints.me.initiate(undefined))
    const countMeRequests = (): number =>
      fetchMock.mock.calls.filter(([input]) => {
        const url = requestUrl(input)
        return url.includes('/v1/auth/me')
      }).length
    await waitFor(() => expect(countMeRequests()).toBe(1))
    expect(Object.keys(store.getState().api.queries)).not.toHaveLength(0)

    const { result } = renderHook(() => useSignOut(), { wrapper: makeWrapper(store) })
    await act(async () => {
      await result.current.signOut()
    })
    expect(store.getState().api.queries).toEqual({})

    subscription.unsubscribe()
    const secondSubscription = store.dispatch(authApi.endpoints.me.initiate(undefined))
    await waitFor(() => expect(countMeRequests()).toBe(2))
    secondSubscription.unsubscribe()
  })

  it('broadcasts the exact logout message on auth', async () => {
    const observer = new BroadcastChannel('auth')
    const messageMock = vi.fn()
    observer.onmessage = messageMock
    const store = makeStore()
    const { result } = renderHook(() => useSignOut(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.signOut()
    })

    expect(messageMock).toHaveBeenCalledTimes(1)
    expect(messageMock.mock.calls[0][0].data).toBe('logout')
    observer.close()
  })

  it('the receiver purges a second store after logout', async () => {
    const receiverStore = makeStore()
    const subscription = receiverStore.dispatch(authApi.endpoints.me.initiate(undefined))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    expect(Object.keys(receiverStore.getState().api.queries)).not.toHaveLength(0)
    const { unmount } = renderHook(() => useAuthBroadcastListener(), {
      wrapper: makeWrapper(receiverStore),
    })

    postLogoutBroadcast()
    await waitFor(() => expect(receiverStore.getState().auth.status).toBe('unauthenticated'))
    expect(receiverStore.getState().api.queries).toEqual({})
    subscription.unsubscribe()
    unmount()
  })

  it('reports a rejected logout while still purging and broadcasting', async () => {
    const observer = new BroadcastChannel('auth')
    const messageMock = vi.fn()
    observer.onmessage = messageMock
    const store = makeStore()
    const subscription = store.dispatch(authApi.endpoints.me.initiate(undefined))
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
    fetchMock.mockImplementationOnce(() => Promise.resolve(makeResponse({ title: 'failed' }, 500)))
    const { result } = renderHook(() => useSignOut(), { wrapper: makeWrapper(store) })

    await act(async () => {
      await result.current.signOut()
    })

    expect(reportClientErrorMock).toHaveBeenCalledWith(
      expect.objectContaining({ kind: 'unhandled-rejection' }),
    )
    expect(store.getState().auth.status).toBe('unauthenticated')
    expect(store.getState().api.queries).toEqual({})
    expect(messageMock).toHaveBeenCalledTimes(1)
    subscription.unsubscribe()
    observer.close()
  })
})
