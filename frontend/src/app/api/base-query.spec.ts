import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { BaseQueryApi } from '@reduxjs/toolkit/query'
import { loggedOut } from '~/modules/auth/store/auth.slice'
import { baseQueryWith401Handler, rawBaseQuery } from './base-query'

// The cookie name the SPA reads is the double-submit companion of the
// HttpOnly backend cookie (DEC-054). Kept in lockstep with `base-query.ts`.
const XSRF_COOKIE_NAME = '__Host-Xsrf-Request'
const XSRF_HEADER_NAME = 'X-XSRF-TOKEN'

// BaseQueryApi is a structural contract; tests only exercise `dispatch` and
// the discriminator `type`. The signal and the other fields are filled with
// no-op defaults so fetchBaseQuery has a valid shape to destructure.
const makeApi = (type: 'query' | 'mutation', dispatch = vi.fn()): BaseQueryApi => ({
  signal: new AbortController().signal,
  abort: vi.fn(),
  dispatch,
  getState: vi.fn(() => ({})),
  extra: undefined,
  endpoint: 'test',
  type,
  forced: false,
})

const okResponse = (body: Record<string, unknown> = { ok: true }): Response =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })

const statusResponse = (status: number): Response =>
  new Response(JSON.stringify({ title: 'error' }), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const extractRequest = (fetchMock: ReturnType<typeof vi.fn>): Request => {
  expect(fetchMock).toHaveBeenCalledTimes(1)
  const firstArg = fetchMock.mock.calls[0][0]
  expect(firstArg).toBeInstanceOf(Request)
  return firstArg as Request
}

// RTK Query hands `fetchBaseQuery` an absolute URL through the constructor's
// `new Request(...)` — jsdom / undici reject relative URLs there even though
// real browsers would resolve them against `window.location`. Tests pass an
// absolute URL in `args.url`; the slash-prefixed `baseUrl` inside
// `base-query.ts` is short-circuited by RTK Query's own `joinUrls` (absolute
// URLs in `url` bypass the base-url join). The header + credentials
// behavior we are exercising is independent of which path/host we use.
const ABS_URL = 'https://localhost:5173/api/endpoint'

// Helper for seeding the XSRF cookie inside jsdom. jsdom honors Set-Cookie
// semantics via `document.cookie =` — the `__Host-` prefix additionally
// requires `Secure` + `Path=/` + no `Domain`; without Secure the cookie jar
// silently drops the assignment and the next read returns empty. The vitest
// environment is configured to `https://localhost:5173/` so the Secure
// attribute is honored.
const setCookie = (name: string, value: string): void => {
  document.cookie = `${name}=${value}; path=/; Secure`
}

const clearCookie = (name: string): void => {
  document.cookie = `${name}=; path=/; Secure; max-age=0`
}

describe('rawBaseQuery', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(okResponse())
    vi.stubGlobal('fetch', fetchMock)
    clearCookie(XSRF_COOKIE_NAME)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearCookie(XSRF_COOKIE_NAME)
  })

  describe('credentials: "include" is set on every call', () => {
    it.each([
      ['GET', 'query'],
      ['POST', 'mutation'],
      ['PUT', 'mutation'],
      ['PATCH', 'mutation'],
      ['DELETE', 'mutation'],
    ] as const)('%s request honors credentials: include', async (method, type) => {
      await rawBaseQuery({ url: ABS_URL, method }, makeApi(type), {})
      const request = extractRequest(fetchMock)
      expect(request.credentials).toBe('include')
    })
  })

  describe('X-XSRF-TOKEN header on mutation verbs', () => {
    beforeEach(() => {
      setCookie(XSRF_COOKIE_NAME, 'xsrf-plain-value')
    })

    it.each(['POST', 'PUT', 'PATCH', 'DELETE'] as const)(
      'adds X-XSRF-TOKEN on %s requests',
      async (method) => {
        await rawBaseQuery({ url: ABS_URL, method }, makeApi('mutation'), {})
        const request = extractRequest(fetchMock)
        expect(request.headers.get(XSRF_HEADER_NAME)).toBe('xsrf-plain-value')
      },
    )
  })

  describe('X-XSRF-TOKEN header NOT added on GET', () => {
    it('omits the XSRF header on GET requests even when the cookie is present', async () => {
      setCookie(XSRF_COOKIE_NAME, 'xsrf-plain-value')
      await rawBaseQuery({ url: ABS_URL, method: 'GET' }, makeApi('query'), {})
      const request = extractRequest(fetchMock)
      expect(request.headers.get(XSRF_HEADER_NAME)).toBeNull()
    })

    it('omits the XSRF header on GET requests when the cookie is absent', async () => {
      await rawBaseQuery({ url: ABS_URL, method: 'GET' }, makeApi('query'), {})
      const request = extractRequest(fetchMock)
      expect(request.headers.get(XSRF_HEADER_NAME)).toBeNull()
    })
  })

  describe('XSRF header value is URL-decoded', () => {
    it('decodes percent-encoded characters from the cookie before setting the header', async () => {
      // Raw cookie holds the percent-encoded form; the header must carry the
      // decoded value. Backend DataProtection-wrapped XSRF tokens routinely
      // contain `+`, `/`, `=` — base64 characters that safely pass through
      // encodeURIComponent / decodeURIComponent round-trips.
      const decoded = 'CfDJ8+abc/def=='
      const encoded = encodeURIComponent(decoded)
      setCookie(XSRF_COOKIE_NAME, encoded)

      await rawBaseQuery({ url: ABS_URL, method: 'POST' }, makeApi('mutation'), {})
      const request = extractRequest(fetchMock)
      expect(request.headers.get(XSRF_HEADER_NAME)).toBe(decoded)
    })
  })

  describe('XSRF header omitted when cookie is missing on a mutation', () => {
    it('skips the header when no XSRF cookie is set', async () => {
      await rawBaseQuery({ url: ABS_URL, method: 'POST' }, makeApi('mutation'), {})
      const request = extractRequest(fetchMock)
      expect(request.headers.get(XSRF_HEADER_NAME)).toBeNull()
    })
  })

  describe('malformed cookie values do not block mutations', () => {
    // `decodeURIComponent` throws `URIError` on a lone `%` or other invalid
    // percent-encoding. That error must not escape `prepareHeaders` — the
    // right posture is to drop the token and let the backend's antiforgery
    // filter 400 the request with a proper `ProblemDetails` response.
    it('returns null from the helper (omits the header) when the cookie holds invalid percent-encoding', async () => {
      setCookie(XSRF_COOKIE_NAME, 'broken%ZZ')
      await rawBaseQuery({ url: ABS_URL, method: 'POST' }, makeApi('mutation'), {})
      const request = extractRequest(fetchMock)
      expect(request.headers.get(XSRF_HEADER_NAME)).toBeNull()
    })
  })
})

describe('baseQueryWith401Handler', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('dispatches loggedOut when the response is 401', async () => {
    fetchMock.mockResolvedValue(statusResponse(401))
    const dispatch = vi.fn()
    await baseQueryWith401Handler({ url: ABS_URL, method: 'GET' }, makeApi('query', dispatch), {})
    expect(dispatch).toHaveBeenCalledWith(loggedOut())
  })

  it('does not dispatch loggedOut on success responses', async () => {
    fetchMock.mockResolvedValue(okResponse())
    const dispatch = vi.fn()
    await baseQueryWith401Handler({ url: ABS_URL, method: 'GET' }, makeApi('query', dispatch), {})
    expect(dispatch).not.toHaveBeenCalled()
  })

  it('does not dispatch loggedOut on non-401 error responses', async () => {
    fetchMock.mockResolvedValue(statusResponse(500))
    const dispatch = vi.fn()
    await baseQueryWith401Handler({ url: ABS_URL, method: 'GET' }, makeApi('query', dispatch), {})
    expect(dispatch).not.toHaveBeenCalled()
  })
})

describe('baseQueryWith401Handler — lazy antiforgery seed', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  // The seed call uses a relative `/v1/auth/xsrf` joined onto the `/api`
  // base URL; jsdom's Request constructor rejects relative URLs (see the
  // ABS_URL note above), so absolutize them the same way the api specs do.
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

  const requestedUrls = (): string[] => fetchMock.mock.calls.map((call) => (call[0] as Request).url)

  beforeEach(() => {
    fetchMock = vi.fn().mockImplementation((input: Request) => {
      if (input.url.includes('/api/v1/auth/xsrf')) {
        // Model the backend's 204 + Set-Cookie: the browser would store the
        // pair; jsdom does not honor Set-Cookie from fetch, so write the
        // SPA-readable half directly.
        setCookie(XSRF_COOKIE_NAME, 'seeded-by-lazy-call')
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      return Promise.resolve(okResponse())
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('Request', PatchedRequest)
    clearCookie(XSRF_COOKIE_NAME)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    clearCookie(XSRF_COOKIE_NAME)
  })

  it('seeds the antiforgery pair before a mutation when the cookie is absent', async () => {
    // The app-boot `GET /xsrf` is fire-and-forget, so a fast first submit
    // can outrun it; the wrapper must seed inline rather than send the
    // mutation without the double-submit pair.
    await baseQueryWith401Handler({ url: ABS_URL, method: 'POST' }, makeApi('mutation'), {})

    const urls = requestedUrls()
    expect(urls).toHaveLength(2)
    expect(urls[0]).toContain('/api/v1/auth/xsrf')
    expect(urls[1]).toBe(ABS_URL)
    // The mutation that follows the seed carries the freshly-seeded header.
    const mutationRequest = fetchMock.mock.calls[1][0] as Request
    expect(mutationRequest.headers.get(XSRF_HEADER_NAME)).toBe('seeded-by-lazy-call')
  })

  it('does not re-seed when the cookie is already present', async () => {
    setCookie(XSRF_COOKIE_NAME, 'already-seeded')
    await baseQueryWith401Handler({ url: ABS_URL, method: 'POST' }, makeApi('mutation'), {})

    const urls = requestedUrls()
    expect(urls).toEqual([ABS_URL])
  })

  it('never seeds for queries', async () => {
    await baseQueryWith401Handler({ url: ABS_URL, method: 'GET' }, makeApi('query'), {})

    const urls = requestedUrls()
    expect(urls).toEqual([ABS_URL])
  })

  it('falls through to the mutation when the seed call itself fails', async () => {
    fetchMock.mockImplementation((input: Request) => {
      if (input.url.includes('/api/v1/auth/xsrf')) {
        return Promise.resolve(statusResponse(500))
      }
      return Promise.resolve(okResponse())
    })

    const result = await baseQueryWith401Handler(
      { url: ABS_URL, method: 'POST' },
      makeApi('mutation'),
      {},
    )

    // The mutation still goes out (and surfaces its own outcome) — a failed
    // seed must not dead-end the request.
    expect(requestedUrls()).toHaveLength(2)
    expect(result.error).toBeUndefined()
  })
})
