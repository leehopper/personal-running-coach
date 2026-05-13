import { renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useGlobalErrorReporter } from './use-global-error-reporter'
import { reportClientError } from './report-client-error'

// Mock the reporter — we only care that the hook translates DOM events
// into the right `reportClientError` calls. The reporter itself has its
// own spec.
vi.mock('./report-client-error', () => ({
  reportClientError: vi.fn(),
}))

const reportMock = vi.mocked(reportClientError)

describe('useGlobalErrorReporter', () => {
  let addSpy: ReturnType<typeof vi.spyOn>
  let removeSpy: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    addSpy = vi.spyOn(window, 'addEventListener')
    removeSpy = vi.spyOn(window, 'removeEventListener')
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    reportMock.mockReset()
  })

  it('attaches error + unhandledrejection listeners on mount and removes them on unmount', () => {
    const { unmount } = renderHook(() => useGlobalErrorReporter())

    const events = addSpy.mock.calls.map((call) => call[0])
    expect(events).toContain('error')
    expect(events).toContain('unhandledrejection')

    unmount()

    const removed = removeSpy.mock.calls.map((call) => call[0])
    expect(removed).toContain('error')
    expect(removed).toContain('unhandledrejection')
  })

  it('forwards an Error from an ErrorEvent as kind="window-error"', () => {
    renderHook(() => useGlobalErrorReporter())
    const error = new Error('boom')
    const event = new ErrorEvent('error', { message: 'boom', error })

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    expect(reportMock).toHaveBeenCalledWith({ kind: 'window-error', error })
  })

  it('wraps an ErrorEvent without `event.error` and a non-empty message into a new Error', () => {
    renderHook(() => useGlobalErrorReporter())
    const event = new ErrorEvent('error', { message: 'script failure' })

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    const args = reportMock.mock.calls[0][0]
    expect(args.kind).toBe('window-error')
    expect(args.error).toBeInstanceOf(Error)
    expect(args.error.message).toBe('script failure')
  })

  it('wraps an ErrorEvent with empty message into a new Error("window error")', () => {
    renderHook(() => useGlobalErrorReporter())
    const event = new ErrorEvent('error', { message: '' })

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    const args = reportMock.mock.calls[0][0]
    expect(args.error).toBeInstanceOf(Error)
    expect(args.error.message).toBe('window error')
  })

  it('forwards an Error reason from a PromiseRejectionEvent as kind="unhandled-rejection"', () => {
    renderHook(() => useGlobalErrorReporter())
    const reason = new Error('rejected')
    // jsdom v29 ships `PromiseRejectionEvent`; if it ever regresses we
    // fall back to a generic `Event` with the needed shape — the hook
    // only reads `.reason`, never the prototype identity.
    const ctor = (globalThis as { PromiseRejectionEvent?: typeof PromiseRejectionEvent })
      .PromiseRejectionEvent
    const event =
      ctor !== undefined
        ? new ctor('unhandledrejection', { promise: Promise.reject(reason), reason })
        : Object.assign(new Event('unhandledrejection'), { reason })
    // Pre-attach a swallow handler so jsdom does not surface the
    // rejection from the synthetic promise above.
    event.promise?.catch(() => {
      /* swallow */
    })

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    expect(reportMock).toHaveBeenCalledWith({ kind: 'unhandled-rejection', error: reason })
  })

  it('wraps a primitive reason via String(reason) when the reason is not an Error', () => {
    renderHook(() => useGlobalErrorReporter())
    const ctor = (globalThis as { PromiseRejectionEvent?: typeof PromiseRejectionEvent })
      .PromiseRejectionEvent
    const event =
      ctor !== undefined
        ? new ctor('unhandledrejection', {
            promise: Promise.reject('plain-string'),
            reason: 'plain-string',
          })
        : Object.assign(new Event('unhandledrejection'), { reason: 'plain-string' })
    event.promise?.catch(() => {
      /* swallow */
    })

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    const args = reportMock.mock.calls[0][0]
    expect(args.kind).toBe('unhandled-rejection')
    expect(args.error).toBeInstanceOf(Error)
    expect(args.error.message).toBe('plain-string')
  })
})
