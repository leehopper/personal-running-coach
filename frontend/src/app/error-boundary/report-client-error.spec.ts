import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { reportClientError } from './report-client-error'

// `report-client-error.ts` is a pure module — no React, no store, no
// imports from the rest of the app. Tests pin it down to the public
// contract: fire `fetch` first, fall back to `sendBeacon` on rejection,
// and swallow every conceivable throw on top. Each test stubs only the
// global it touches and restores everything in `afterEach` so leaks
// cannot bleed into sibling specs (the `unhandledrejection` listener
// installed by the hook spec is sensitive to stray fetch rejections).
const ENDPOINT = '/api/v1/client-errors'

const buildError = (overrides: Partial<Error> = {}): Error => {
  const error = new Error(overrides.message ?? 'boom')
  if (overrides.name !== undefined) error.name = overrides.name
  if (overrides.stack !== undefined) error.stack = overrides.stack
  return error
}

describe('reportClientError', () => {
  let fetchMock: ReturnType<typeof vi.fn>
  let sendBeaconMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    sendBeaconMock = vi.fn().mockReturnValue(true)
    vi.stubGlobal('navigator', {
      ...globalThis.navigator,
      sendBeacon: sendBeaconMock,
      userAgent: 'vitest-jsdom',
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('POSTs the payload to /api/v1/client-errors with the expected fetch options', async () => {
    const error = buildError({ name: 'RenderError', message: 'kaboom', stack: 'stack-trace' })

    reportClientError({
      kind: 'render',
      correlationId: '00000000-0000-4000-8000-000000000001',
      error,
      componentStack: '<App>',
    })

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe(ENDPOINT)
    expect(init.method).toBe('POST')
    expect(init.credentials).toBe('include')
    expect(init.keepalive).toBe(true)
    expect(init.headers).toEqual({ 'Content-Type': 'application/json' })

    const body = JSON.parse(init.body as string)
    expect(body).toMatchObject({
      kind: 'render',
      correlationId: '00000000-0000-4000-8000-000000000001',
      errorName: 'RenderError',
      message: 'kaboom',
      stack: 'stack-trace',
      componentStack: '<App>',
    })
    expect(typeof body.occurredAt).toBe('string')
    expect(() => new Date(body.occurredAt as string).toISOString()).not.toThrow()
    // Default jsdom URL (https://localhost:5173/) has no query/fragment, so
    // origin+pathname === href — the redaction contract is not exercised here.
    // A dedicated test below covers it; here we just confirm url is present.
    expect(typeof body.url).toBe('string')
    expect(body.userAgent).toBe('vitest-jsdom')
    expect(typeof body.appVersion).toBe('string')
  })

  it('defaults correlationId to crypto.randomUUID when none provided', () => {
    const uuid = '11111111-2222-3333-4444-555555555555'
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue(uuid)

    reportClientError({
      kind: 'window-error',
      error: buildError(),
    })

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    expect(body.correlationId).toBe(uuid)
  })

  it('emits empty strings for stack and componentStack when absent', () => {
    const error = buildError()
    error.stack = undefined

    reportClientError({
      kind: 'unhandled-rejection',
      error,
    })

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)
    expect(body.stack).toBe('')
    expect(body.componentStack).toBe('')
  })

  it('falls back to navigator.sendBeacon when fetch rejects', async () => {
    const error = buildError({ message: 'net' })
    fetchMock.mockRejectedValueOnce(new Error('network down'))

    reportClientError({
      kind: 'render',
      correlationId: 'cid',
      error,
    })

    // The fetch promise rejection routes synchronously through the
    // `.then` rejection handler; awaiting a microtask lets that chain
    // settle before we assert against the beacon mock.
    await Promise.resolve()
    await Promise.resolve()

    expect(sendBeaconMock).toHaveBeenCalledTimes(1)
    const [beaconUrl, blob] = sendBeaconMock.mock.calls[0]
    expect(beaconUrl).toBe(ENDPOINT)
    expect(blob).toBeInstanceOf(Blob)
    expect((blob as Blob).type).toBe('application/json')
    const text = await (blob as Blob).text()
    const parsed = JSON.parse(text)
    expect(parsed.correlationId).toBe('cid')
    expect(parsed.message).toBe('net')
  })

  it('swallows a sendBeacon throw on the fallback path', async () => {
    sendBeaconMock.mockImplementation(() => {
      throw new Error('beacon-broken')
    })
    fetchMock.mockRejectedValueOnce(new Error('network down'))

    expect(() =>
      reportClientError({
        kind: 'render',
        correlationId: 'cid',
        error: buildError(),
      }),
    ).not.toThrow()

    await Promise.resolve()
    await Promise.resolve()
    expect(sendBeaconMock).toHaveBeenCalledTimes(1)
  })

  it('swallows a JSON.stringify throw (outer try/catch)', () => {
    // Force the body builder to blow up by replacing `crypto.randomUUID`
    // with something the JSON serialiser cannot encode — a circular ref
    // wrapped in a `toJSON` that itself throws.
    vi.spyOn(globalThis.crypto, 'randomUUID').mockImplementation(() => {
      throw new Error('uuid-broken')
    })

    expect(() =>
      reportClientError({
        kind: 'render',
        error: buildError(),
      }),
    ).not.toThrow()
    // fetch must never fire when body construction throws.
    expect(fetchMock).not.toHaveBeenCalled()
    expect(sendBeaconMock).not.toHaveBeenCalled()
  })

  it('redacts query string and fragment from body.url', () => {
    // Stub window.location with a URL that carries PII-like values in both
    // the query string and the fragment. The production code must send only
    // origin+pathname, stripping everything after the path.
    vi.stubGlobal('location', {
      origin: 'https://app.runcoach.io',
      pathname: '/onboarding',
      href: 'https://app.runcoach.io/onboarding?token=secret123&email=u@x.com#section',
    })

    reportClientError({
      kind: 'render',
      correlationId: 'redact-test',
      error: buildError(),
    })

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const body = JSON.parse(fetchMock.mock.calls[0][1].body as string)

    // Only origin+pathname — no query string, no fragment.
    expect(body.url).toBe('https://app.runcoach.io/onboarding')

    // Belt-and-suspenders: the raw JSON must not contain any PII token.
    const raw = JSON.stringify(body)
    expect(raw).not.toContain('secret123')
    expect(raw).not.toContain('u@x.com')
    expect(raw).not.toContain('#section')
  })

  it('does not throw and does not call fetch again when sendBeacon returns false', async () => {
    // `sendBeacon` returns `false` when the queue is full or the body exceeds
    // the 64 KB limit. The contract is silent-drop: no throw, no retry.
    sendBeaconMock.mockReturnValue(false)
    fetchMock.mockRejectedValueOnce(new Error('network down'))

    expect(() =>
      reportClientError({
        kind: 'render',
        correlationId: 'beacon-false',
        error: buildError(),
      }),
    ).not.toThrow()

    await Promise.resolve()
    await Promise.resolve()

    // sendBeacon was still called (attempted), but returned false — that is
    // allowed. No second fetch call must be made.
    expect(sendBeaconMock).toHaveBeenCalledTimes(1)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('does not throw when sendBeacon is undefined (missing polyfill)', async () => {
    // If the polyfill is absent, `navigator.sendBeacon` is undefined and the
    // call site throws a TypeError. The outer try/catch on sendBeaconFallback
    // must swallow it so the error boundary can still render the fallback card.
    vi.stubGlobal('navigator', {
      ...globalThis.navigator,
      sendBeacon: undefined,
      userAgent: 'vitest-jsdom',
    })
    fetchMock.mockRejectedValueOnce(new Error('network down'))

    expect(() =>
      reportClientError({
        kind: 'render',
        correlationId: 'no-beacon',
        error: buildError(),
      }),
    ).not.toThrow()

    await Promise.resolve()
    await Promise.resolve()
    // No assertion on sendBeacon — it is undefined and must not be called.
    // The test passes as long as no exception propagates.
  })
})
