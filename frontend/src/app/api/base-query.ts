import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react'
import { loggedOut } from '~/modules/auth/store/auth.slice'

// The SPA-readable half of the antiforgery double-submit pair (DEC-054).
// Backend source of truth: `AuthCookieNames.AntiforgeryRequest`. The
// HttpOnly companion cookie `__Host-Xsrf` is unreadable here by design.
const XSRF_COOKIE_NAME = '__Host-Xsrf-Request'
const XSRF_HEADER_NAME = 'X-XSRF-TOKEN'

// Reads the single cookie segment whose name matches XSRF_COOKIE_NAME and
// URL-decodes the value. Returns null when the cookie is absent (the
// app-boot `GET /xsrf` seeds it before any mutation fires) or when the
// stored value is malformed — a `URIError` from `decodeURIComponent` must
// not escape into `prepareHeaders` and block every mutation. Splits on
// `;` with per-segment `trim()` rather than `"; "` to stay robust against
// browsers that produce spaceless separators.
const readXsrfCookie = (): string | null => {
  if (typeof document === 'undefined') return null
  const raw = document.cookie
  if (raw.length === 0) return null
  const segments = raw.split(';').map((segment) => segment.trim())
  const prefix = `${XSRF_COOKIE_NAME}=`
  const hit = segments.find((segment) => segment.startsWith(prefix))
  if (hit === undefined) return null
  try {
    return decodeURIComponent(hit.slice(prefix.length))
  } catch {
    return null
  }
}

export const rawBaseQuery = fetchBaseQuery({
  baseUrl: '/api',
  credentials: 'include',
  prepareHeaders: (headers, { type }) => {
    if (type === 'mutation') {
      const xsrf = readXsrfCookie()
      if (xsrf !== null) headers.set(XSRF_HEADER_NAME, xsrf)
    }
    return headers
  },
})

// 401 posture (spec §Unit 3 line 116, DEC-054 / R-058):
//   - Dispatch `loggedOut` so every consumer of the auth slice flips to
//     `unauthenticated` in the same tick as the failed response.
//   - Navigation to `/login` is intentionally left to `<RequireAuth>` in
//     T03.2 rather than forcing `window.location.replace(...)` here. The
//     slice transition is the single source of truth; the view layer reads
//     it and renders the right thing (`Navigate` for protected routes;
//     no-op on `/login` and `/register`, which are not wrapped in
//     RequireAuth).
//   - No `baseQueryWithReauth` mutex / refresh-retry (DEC-054): sliding
//     expiration renews the cookie on every honored request, so 401 means
//     the session is genuinely gone, not "access token is stale."
export const baseQueryWith401Handler: BaseQueryFn<
  string | FetchArgs,
  unknown,
  FetchBaseQueryError
> = async (args, api, extraOptions) => {
  const result = await rawBaseQuery(args, api, extraOptions)
  if (result.error?.status === 401) {
    api.dispatch(loggedOut())
  }
  return result
}
