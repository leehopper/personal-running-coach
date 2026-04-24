import { useEffect } from 'react'
import { useDispatch, useSelector } from 'react-redux'
import { authApi } from '~/api/auth.api'
import { subscribeLogoutBroadcast } from '~/modules/auth/lib/broadcast-auth'
import type { AuthState } from '~/modules/auth/models/auth.model'
import { loggedOut, sessionVerified } from '~/modules/auth/store/auth.slice'
import type { AppDispatch, RootState } from '~/modules/app/app.store'

export interface UseAuthReturn {
  status: AuthState['status']
  user: AuthState['user']
  isAuthenticated: boolean
  isUnknown: boolean
  isUnauthenticated: boolean
}

export const useAuth = (): UseAuthReturn => {
  const { status, user } = useSelector((state: RootState) => state.auth)
  return {
    status,
    user,
    isAuthenticated: status === 'authenticated',
    isUnknown: status === 'unknown',
    isUnauthenticated: status === 'unauthenticated',
  }
}

// App-boot sequence (spec §Unit 3 lines 114–115):
//   1. `GET /xsrf` seeds the antiforgery double-submit pair before any
//      state-changing request can fire.
//   2. `GET /me` determines whether a live session exists. 200 →
//      `sessionVerified`; non-200 → `loggedOut`.
//
// Run once per app mount. Both calls are fire-and-forget from the UI
// perspective — failures flow into the auth slice, not into a loading
// spinner this hook owns.
export const useAuthBootstrap = (): void => {
  const dispatch = useDispatch<AppDispatch>()

  useEffect(() => {
    let cancelled = false

    const bootstrap = async (): Promise<void> => {
      const xsrfPromise = dispatch(authApi.endpoints.xsrf.initiate(undefined))
      try {
        await xsrfPromise.unwrap()
      } catch {
        // Antiforgery seed fires-and-forgets: mutations read the cookie
        // at send time; if the seed failed the first mutation will.
      } finally {
        xsrfPromise.unsubscribe()
      }

      const mePromise = dispatch(authApi.endpoints.me.initiate(undefined))
      try {
        const me = await mePromise.unwrap()
        if (!cancelled) {
          dispatch(sessionVerified({ userId: me.userId, email: me.email }))
        }
      } catch {
        if (!cancelled) dispatch(loggedOut())
      } finally {
        mePromise.unsubscribe()
      }
    }

    void bootstrap()

    return () => {
      cancelled = true
    }
  }, [dispatch])
}

// Cross-tab logout listener (spec §Unit 3 line 118, optional). When tab A
// posts a logout message, every other tab listening on the `auth`
// BroadcastChannel flips to `unauthenticated` immediately rather than
// waiting for its next network call to 401.
export const useAuthBroadcastListener = (): void => {
  const dispatch = useDispatch<AppDispatch>()
  useEffect(() => subscribeLogoutBroadcast(() => dispatch(loggedOut())), [dispatch])
}
