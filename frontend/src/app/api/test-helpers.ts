import { XSRF_COOKIE_NAME } from './base-query'

// Shared spec helpers for the RTK Query api layer. The api specs dispatch
// endpoint thunks directly with `fetch` stubbed at the global level so the
// real `query: () => ({...})` factories run through `fetchBaseQuery`. Two
// jsdom/undici friction points are handled here so each spec no longer
// re-declares them:
//
//   1. `PatchedRequest` — `fetchBaseQuery({ baseUrl: '/api' })` joins the
//      relative base URL with each endpoint's `url` and hands the result to
//      `new Request(...)`. Undici's Request constructor (node's fetch in the
//      test environment) rejects relative URLs even though a real browser
//      resolves them against `window.location`, so absolutize any
//      leading-slash URL against the jsdom origin (vite.config.ts pins it to
//      `https://localhost:5173/`).
//
//   2. XSRF cookie seed/clear — jsdom honors the `__Host-` prefix only with
//      `Secure` + `Path=/` + no `Domain`; without `Secure` the cookie jar
//      silently drops the write. Seeding goes through the real
//      `XSRF_COOKIE_NAME` exported from `base-query.ts`, so a backend rename
//      can never leave these seeds inert while the lazy seed quietly fires.

const OriginalRequest = globalThis.Request

export class PatchedRequest extends OriginalRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    if (typeof input === 'string' && input.startsWith('/')) {
      super(new URL(input, 'https://localhost:5173').toString(), init)
      return
    }
    super(input, init)
  }
}

/**
 * Seeds the SPA-readable antiforgery cookie so the base query's lazy XSRF
 * seed stays quiet and the mutation under test is the only request observed.
 */
export const seedXsrfCookie = (value = 'test-xsrf'): void => {
  document.cookie = `${XSRF_COOKIE_NAME}=${value}; path=/; Secure`
}

/** Clears the antiforgery cookie (max-age=0) for test isolation. */
export const clearXsrfCookie = (): void => {
  document.cookie = `${XSRF_COOKIE_NAME}=; path=/; Secure; max-age=0`
}
