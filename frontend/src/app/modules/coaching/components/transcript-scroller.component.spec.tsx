import { act, fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { TranscriptScroller } from './transcript-scroller.component'

// jsdom does not run layout, so scrollHeight / clientHeight / scrollTop
// stay at 0 unless we stub them. The harness below installs configurable
// getters on the prototype-side properties we read in the component, then
// dispatches `scroll` events to drive the listener. Each test resets the
// values it cares about.
const stubScrollMetrics = (
  element: HTMLElement,
  values: { scrollHeight: number; clientHeight: number; scrollTop: number },
) => {
  Object.defineProperty(element, 'scrollHeight', {
    configurable: true,
    get: () => values.scrollHeight,
  })
  Object.defineProperty(element, 'clientHeight', {
    configurable: true,
    get: () => values.clientHeight,
  })
  Object.defineProperty(element, 'scrollTop', {
    configurable: true,
    get: () => values.scrollTop,
    set: (next: number) => {
      values.scrollTop = next
    },
  })
}

interface HarnessProps {
  initialMessages?: string[]
  bottomThresholdPx?: number
}

// `Harness` mirrors how the real onboarding chat will use the scroller:
// state for the list of turns, `turnCount` driven from `messages.length`.
const Harness = ({ initialMessages = ['one'], bottomThresholdPx }: HarnessProps) => {
  const [messages, setMessages] = useState(initialMessages)
  return (
    <div>
      <button data-testid="add-turn" onClick={() => setMessages((prev) => [...prev, 'next'])}>
        add
      </button>
      <TranscriptScroller turnCount={messages.length} bottomThresholdPx={bottomThresholdPx}>
        {messages.map((m, i) => (
          <div key={`${m}-${i}`}>{m}</div>
        ))}
      </TranscriptScroller>
    </div>
  )
}

describe('TranscriptScroller', () => {
  it('renders with role="log" and aria-live="polite" so screen readers announce new turns', () => {
    render(<Harness />)
    const scroller = screen.getByTestId('transcript-scroller')
    expect(scroller).toHaveAttribute('role', 'log')
    expect(scroller).toHaveAttribute('aria-live', 'polite')
    expect(scroller).toHaveAttribute('aria-relevant', 'additions')
  })

  it('renders children passed in', () => {
    render(<Harness initialMessages={['hello', 'world']} />)
    expect(screen.getByText('hello')).toBeInTheDocument()
    expect(screen.getByText('world')).toBeInTheDocument()
  })

  it('auto-scrolls to bottom when a new turn arrives and the user is parked at the bottom', () => {
    render(<Harness />)
    const scroller = screen.getByTestId('transcript-scroller')
    const metrics = { scrollHeight: 1000, clientHeight: 400, scrollTop: 600 }
    stubScrollMetrics(scroller, metrics)

    act(() => {
      fireEvent.click(screen.getByTestId('add-turn'))
    })

    expect(metrics.scrollTop).toBe(metrics.scrollHeight)
    expect(scroller.dataset.autoScroll).toBe('true')
  })

  it('preserves scroll position when the user has scrolled up before a new turn arrives', () => {
    render(<Harness />)
    const scroller = screen.getByTestId('transcript-scroller')
    const metrics = { scrollHeight: 1000, clientHeight: 400, scrollTop: 100 }
    stubScrollMetrics(scroller, metrics)

    // Simulate the user scrolling up — fires the scroll listener which
    // recomputes shouldAutoScroll against the current metrics.
    act(() => {
      fireEvent.scroll(scroller)
    })

    expect(scroller.dataset.autoScroll).toBe('false')

    // New turn arrives; auto-scroll is suppressed.
    metrics.scrollHeight = 1400
    act(() => {
      fireEvent.click(screen.getByTestId('add-turn'))
    })

    expect(metrics.scrollTop).toBe(100)
  })

  it('resumes auto-scroll once the user scrolls back to the bottom', () => {
    render(<Harness />)
    const scroller = screen.getByTestId('transcript-scroller')
    const metrics = { scrollHeight: 1000, clientHeight: 400, scrollTop: 100 }
    stubScrollMetrics(scroller, metrics)

    act(() => {
      fireEvent.scroll(scroller)
    })
    expect(scroller.dataset.autoScroll).toBe('false')

    metrics.scrollTop = 600
    act(() => {
      fireEvent.scroll(scroller)
    })
    expect(scroller.dataset.autoScroll).toBe('true')

    metrics.scrollHeight = 1400
    act(() => {
      fireEvent.click(screen.getByTestId('add-turn'))
    })
    expect(metrics.scrollTop).toBe(metrics.scrollHeight)
  })

  it('treats a position within the threshold as "at the bottom"', () => {
    render(<Harness bottomThresholdPx={16} />)
    const scroller = screen.getByTestId('transcript-scroller')
    // scrollHeight - clientHeight - scrollTop = 1000 - 400 - 590 = 10 (within 16px threshold).
    const metrics = { scrollHeight: 1000, clientHeight: 400, scrollTop: 590 }
    stubScrollMetrics(scroller, metrics)

    act(() => {
      fireEvent.scroll(scroller)
    })

    expect(scroller.dataset.autoScroll).toBe('true')
  })
})
