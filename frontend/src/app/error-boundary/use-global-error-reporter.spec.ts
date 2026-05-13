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

    const events = addSpy.mock.calls.map((call: unknown[]) => call[0] as string)
    expect(events).toContain('error')
    expect(events).toContain('unhandledrejection')

    unmount()

    const removed = removeSpy.mock.calls.map((call: unknown[]) => call[0] as string)
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

  // jsdom v29 reliably ships `PromiseRejectionEvent`. We pre-attach a
  // swallow handler to the rejected promise we feed into the event so
  // jsdom does not surface it as an unhandled rejection in test output.
  const buildRejectionEvent = (reason: unknown): PromiseRejectionEvent => {
    const promise = Promise.reject(reason)
    promise.catch(() => {
      /* swallow synthetic rejection */
    })
    return new PromiseRejectionEvent('unhandledrejection', { promise, reason })
  }

  it('forwards an Error reason from a PromiseRejectionEvent as kind="unhandled-rejection"', () => {
    renderHook(() => useGlobalErrorReporter())
    const reason = new Error('rejected')
    const event = buildRejectionEvent(reason)

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    expect(reportMock).toHaveBeenCalledWith({ kind: 'unhandled-rejection', error: reason })
  })

  it('wraps a primitive reason via String(reason) when the reason is not an Error', () => {
    renderHook(() => useGlobalErrorReporter())
    const event = buildRejectionEvent('plain-string')

    window.dispatchEvent(event)

    expect(reportMock).toHaveBeenCalledTimes(1)
    const args = reportMock.mock.calls[0][0]
    expect(args.kind).toBe('unhandled-rejection')
    expect(args.error).toBeInstanceOf(Error)
    expect(args.error.message).toBe('plain-string')
  })
})
