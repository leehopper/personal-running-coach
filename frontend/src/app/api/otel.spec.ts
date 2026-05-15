import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Span, SpanContext } from '@opentelemetry/api'
import * as lti from './last-trace-id'
import { applyCustomAttributesOnSpan, ignoreUrls } from './otel'

// Minimal `Span` stand-in. The production callback only uses
// `spanContext().traceId` and `setAttribute`; everything else stays a
// no-op cast — typing the stub against the full `Span` surface would
// bloat the test without exercising behaviour the production code
// actually depends on.
const makeSpan = (traceId: string): { span: Span; setAttribute: ReturnType<typeof vi.fn> } => {
  const setAttribute = vi.fn()
  const spanContext: SpanContext = {
    traceId,
    spanId: '0000000000000001',
    traceFlags: 1,
  }
  const span = {
    setAttribute,
    spanContext: () => spanContext,
  } as unknown as Span
  return { span, setAttribute }
}

const makeRequest = (url: string): Request => ({ url, method: 'GET' }) as unknown as Request

describe('otel `applyCustomAttributesOnSpan`', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('strips query string and fragment from `http.url` and `http.target`', () => {
    const { span, setAttribute } = makeSpan('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
    const request = makeRequest(
      'https://app.runcoach.io/onboarding?token=secret123&email=u@x.com#section',
    )

    applyCustomAttributesOnSpan(span, request)

    expect(setAttribute).toHaveBeenCalledWith('http.url', 'https://app.runcoach.io/onboarding')
    expect(setAttribute).toHaveBeenCalledWith('http.target', '/onboarding')

    // No setAttribute call may carry the secret, the email, or the fragment.
    for (const call of setAttribute.mock.calls) {
      for (const arg of call) {
        const serialised = typeof arg === 'string' ? arg : JSON.stringify(arg)
        expect(serialised).not.toContain('secret123')
        expect(serialised).not.toContain('u@x.com')
        expect(serialised).not.toContain('#section')
        expect(serialised).not.toContain('token=')
        expect(serialised).not.toContain('email=')
      }
    }
  })

  it('also strips query string and fragment when the input is a plain string', () => {
    const { span, setAttribute } = makeSpan('bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb')

    applyCustomAttributesOnSpan(span, 'https://app.runcoach.io/api/v1/plans?cursor=opaque#row=4')

    expect(setAttribute).toHaveBeenCalledWith('http.url', 'https://app.runcoach.io/api/v1/plans')
    expect(setAttribute).toHaveBeenCalledWith('http.target', '/api/v1/plans')
    for (const call of setAttribute.mock.calls) {
      for (const arg of call) {
        const serialised = typeof arg === 'string' ? arg : JSON.stringify(arg)
        expect(serialised).not.toContain('cursor=opaque')
        expect(serialised).not.toContain('#row=4')
      }
    }
  })

  it('records the span trace-id via `recordLastTraceId`', () => {
    const spy = vi.spyOn(lti, 'recordLastTraceId').mockImplementation(() => {})
    const { span } = makeSpan('cccccccccccccccccccccccccccccccc')

    applyCustomAttributesOnSpan(span, makeRequest('https://app.runcoach.io/onboarding'))

    expect(spy).toHaveBeenCalledTimes(1)
    expect(spy).toHaveBeenCalledWith('cccccccccccccccccccccccccccccccc')
  })

  it('swallows malformed URLs without throwing and leaves attributes untouched', () => {
    const { span, setAttribute } = makeSpan('dddddddddddddddddddddddddddddddd')

    // `new URL('http://')` throws TypeError — the production try/catch
    // must swallow it. The trace-id is still recorded (that runs before
    // the URL parse), but `setAttribute` is never called for
    // `http.url` / `http.target`.
    expect(() => {
      applyCustomAttributesOnSpan(span, 'http://')
    }).not.toThrow()

    expect(setAttribute).not.toHaveBeenCalled()
  })
})

describe('otel `ignoreUrls`', () => {
  const matches = (url: string): boolean => ignoreUrls.some((re) => re.test(url))

  it('matches `/v1/traces` in both path-only and origin+path forms', () => {
    expect(matches('/v1/traces')).toBe(true)
    expect(matches('http://localhost:4318/v1/traces')).toBe(true)
    // Trailing slash from a misconfigured collector URL.
    expect(matches('http://localhost:4318/v1/traces/')).toBe(true)
  })

  it('matches `/api/v1/client-errors` in both path-only and origin+path forms', () => {
    expect(matches('/api/v1/client-errors')).toBe(true)
    expect(matches('https://app.runcoach.io/api/v1/client-errors')).toBe(true)
    // Tolerate stray trailing slash.
    expect(matches('https://app.runcoach.io/api/v1/client-errors/')).toBe(true)
  })

  it('does not over-match unrelated URLs', () => {
    expect(matches('https://app.runcoach.io/api/v1/plans')).toBe(false)
    expect(matches('https://app.runcoach.io/onboarding')).toBe(false)
    expect(matches('https://app.runcoach.io/api/v1/client-errors-archive')).toBe(false)
  })
})
