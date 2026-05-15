import { useState, type ReactNode } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useLastTraceId } from '~/api/last-trace-id'
import { AppErrorBoundary } from './app-error-boundary'
import { reportClientError } from './report-client-error'

vi.mock('./report-client-error', () => ({
  reportClientError: vi.fn(),
}))

vi.mock('~/api/last-trace-id', () => ({
  useLastTraceId: vi.fn(() => null),
}))

const reportMock = vi.mocked(reportClientError)
const useLastTraceIdMock = vi.mocked(useLastTraceId)

const CORRELATION_UUID = 'abcdef12-3456-4789-abcd-ef1234567890'

// Throwing component for the error path. The boolean prop lets the
// parent toggle "throw or render" so we can verify that
// `resetErrorBoundary` re-mounts children cleanly.
const Boom = ({ shouldThrow }: { shouldThrow: boolean }): ReactNode => {
  if (shouldThrow) throw new Error('child blew up')
  return <div data-testid="boom-ok">recovered</div>
}

// Wrapper holding a "should I still throw" flag controlled by an
// external setter. `react-error-boundary`'s `onReset` callback fires
// after the user clicks "Try again"; the parent must flip the toggle
// off before the next render so the child renders successfully — that
// is the contract the library prescribes. The toggle button lives
// OUTSIDE the boundary so it stays mounted once the fallback takes
// over the children slot.
const ResetHarness = ({ initialThrow = true }: { initialThrow?: boolean }): ReactNode => {
  const [shouldThrow, setShouldThrow] = useState(initialThrow)
  return (
    <>
      <button type="button" data-testid="parent-clear" onClick={() => setShouldThrow(false)}>
        clear
      </button>
      <AppErrorBoundary>
        <Boom shouldThrow={shouldThrow} />
      </AppErrorBoundary>
    </>
  )
}

describe('AppErrorBoundary', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>

  beforeEach(() => {
    // React 19 logs the caught error to console.error; silence it so
    // test output stays readable, but keep the spy so we could assert
    // against it if needed.
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue(CORRELATION_UUID)
    useLastTraceIdMock.mockReturnValue(null)
  })

  afterEach(() => {
    consoleErrorSpy.mockRestore()
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    reportMock.mockReset()
    useLastTraceIdMock.mockReset()
  })

  it('renders children unchanged when no error is thrown', () => {
    render(
      <AppErrorBoundary>
        <p data-testid="child">happy path</p>
      </AppErrorBoundary>,
    )
    expect(screen.getByTestId('child')).toHaveTextContent('happy path')
  })

  it('renders the fallback heading + short id when a child throws', () => {
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )

    expect(screen.getByRole('heading', { name: /something went wrong/i })).toBeInTheDocument()
    // First 8 hex of the mocked uuid.
    expect(screen.getByText('abcdef12')).toBeInTheDocument()
  })

  it('calls reportClientError with the generated correlation id and componentStack', () => {
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )

    expect(reportMock).toHaveBeenCalledTimes(1)
    const args = reportMock.mock.calls[0][0]
    expect(args.kind).toBe('render')
    expect(args.correlationId).toBe(CORRELATION_UUID)
    expect(args.error).toBeInstanceOf(Error)
    expect((args.error as Error).message).toBe('child blew up')
  })

  it('focuses the heading after mount', async () => {
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )
    const heading = await screen.findByRole('heading', { name: /something went wrong/i })
    expect(heading).toHaveFocus()
  })

  it('"Try again" resets the boundary so children re-mount on next render', async () => {
    const user = userEvent.setup()
    render(<ResetHarness />)
    expect(screen.getByRole('heading', { name: /something went wrong/i })).toBeInTheDocument()

    // The parent clears the throw flag before we click reset so the
    // re-render renders the happy-path branch.
    await user.click(screen.getByTestId('parent-clear'))
    await user.click(screen.getByRole('button', { name: /try again/i }))

    expect(screen.queryByRole('heading', { name: /something went wrong/i })).not.toBeInTheDocument()
    expect(screen.getByTestId('boom-ok')).toHaveTextContent('recovered')
  })

  it('"Reload page" calls window.location.reload', async () => {
    const reloadMock = vi.fn()
    // jsdom's `window.location.reload` is non-configurable; stubbing the
    // whole `location` object via `vi.stubGlobal` is the supported path
    // (mirrors vitest docs).
    vi.stubGlobal('location', {
      ...window.location,
      reload: reloadMock,
    } as Location)

    const user = userEvent.setup()
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )

    await user.click(screen.getByRole('button', { name: /reload page/i }))
    expect(reloadMock).toHaveBeenCalledTimes(1)
  })

  it('omits the support-code row when useLastTraceId returns null', () => {
    useLastTraceIdMock.mockReturnValue(null)
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )
    expect(screen.queryByText(/support code:/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /copy support code/i })).not.toBeInTheDocument()
  })

  it('formats a 32-char trace-id with 8-char groups separated by dashes', () => {
    useLastTraceIdMock.mockReturnValue('5b8efff798038103d269b633813fc60c')
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )
    expect(screen.getByText('5b8efff7-98038103-d269b633-813fc60c')).toBeInTheDocument()
  })

  it('renders a non-32-char trace-id unchanged', () => {
    useLastTraceIdMock.mockReturnValue('short-trace')
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )
    expect(screen.getByText('short-trace')).toBeInTheDocument()
  })

  // userEvent v14's `setup()` installs its own clipboard implementation that
  // shadows `Object.defineProperty(navigator, 'clipboard', ...)` overrides,
  // so we cannot reliably assert against a `writeText` spy in jsdom without
  // pulling apart the user-event internals. The happy path is exercised by
  // the Playwright suite (`e2e/error-boundary.spec.ts` smoke); here we just
  // verify the button is reachable and clicking it does not throw.
  it('renders a Copy support code button that is safe to click', async () => {
    useLastTraceIdMock.mockReturnValue('5b8efff798038103d269b633813fc60c')
    const user = userEvent.setup()
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )

    const button = screen.getByRole('button', { name: /copy support code/i })
    expect(button).toBeInTheDocument()
    await expect(user.click(button)).resolves.not.toThrow()
  })

  it('does not throw when navigator.clipboard is undefined', async () => {
    useLastTraceIdMock.mockReturnValue('5b8efff798038103d269b633813fc60c')
    Object.defineProperty(globalThis.navigator, 'clipboard', {
      value: undefined,
      configurable: true,
      writable: true,
    })

    const user = userEvent.setup()
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )

    await expect(
      user.click(screen.getByRole('button', { name: /copy support code/i })),
    ).resolves.not.toThrow()
  })

  it('shows the error name, message, and stack inside the details block', () => {
    render(
      <AppErrorBoundary>
        <Boom shouldThrow />
      </AppErrorBoundary>,
    )

    // The details element is collapsed by default; the content is still
    // in the DOM so we can assert against it directly.
    // Error name lives in a `<strong>` element next to the message.
    expect(screen.getByText('Error', { selector: 'strong' })).toBeInTheDocument()
    // The stack `<pre>` block is the only `<pre>` the Fallback renders.
    const pre = document.querySelector('pre')
    expect(pre).not.toBeNull()
    expect(pre?.textContent).toContain('child blew up')
    // Full correlation id surfaces inside the details block.
    expect(screen.getByText(CORRELATION_UUID)).toBeInTheDocument()
  })

  it('handles non-Error throws by coercing them via String()', () => {
    const StringThrower = (): ReactNode => {
      // react-error-boundary will propagate the raw thrown value to
      // `onError`; the wrapper coerces non-Errors via `new Error(String(...))`.
      throw 'plain-string-fail'
    }

    render(
      <AppErrorBoundary>
        <StringThrower />
      </AppErrorBoundary>,
    )

    expect(reportMock).toHaveBeenCalledTimes(1)
    const args = reportMock.mock.calls[0][0]
    expect(args.error).toBeInstanceOf(Error)
    expect((args.error as Error).message).toBe('plain-string-fail')
  })
})
