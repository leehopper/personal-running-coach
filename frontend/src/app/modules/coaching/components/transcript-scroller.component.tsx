import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ReactElement,
  type ReactNode,
} from 'react'

export interface TranscriptScrollerProps {
  // `turnCount` is the trigger for the auto-scroll effect. The scroller is
  // intentionally agnostic about WHAT the turns are (text, structured
  // input, tool_use opaque blocks) — any change in count is treated as a
  // new turn arriving and pulls the viewport down to the latest message
  // unless the user has scrolled up to read history.
  turnCount: number
  children: ReactNode
  className?: string
  // Pixel tolerance for "is the user at the bottom?". Wrapped components
  // sometimes have sub-pixel scroll positions due to flexbox / motion
  // layouts; an 8px slop window keeps the auto-scroll behavior stable
  // without resorting to fragile equality.
  bottomThresholdPx?: number
}

const isAtBottom = (element: HTMLElement, thresholdPx: number): boolean => {
  const distanceFromBottom = element.scrollHeight - element.clientHeight - element.scrollTop
  return distanceFromBottom <= thresholdPx
}

// `TranscriptScroller` owns one piece of UI behavior: pin the viewport to
// the latest message UNLESS the user has scrolled up, in which case
// preserve their position so they can read history without being yanked
// back to the bottom every time a streaming token arrives.
//
// The `role="log"` + `aria-live="polite"` pair lets screen readers
// announce new assistant turns without stealing focus, satisfying spec
// § Unit 3 R03.8.
export const TranscriptScroller = ({
  turnCount,
  children,
  className,
  bottomThresholdPx = 8,
}: TranscriptScrollerProps): ReactElement => {
  const containerRef = useRef<HTMLDivElement>(null)
  // `shouldAutoScroll` mirrors "user is currently parked at the bottom".
  // It flips to false the moment the user scrolls up, and back to true
  // when they scroll back down to the bottom. The auto-scroll effect
  // honors it strictly.
  const [shouldAutoScroll, setShouldAutoScroll] = useState(true)

  // useLayoutEffect rather than useEffect: the scroll has to land in the
  // same paint as the new content so users never see a one-frame flash of
  // mid-scrolled content before snapping to the bottom.
  useLayoutEffect(() => {
    const container = containerRef.current
    if (container === null) {
      return
    }
    if (shouldAutoScroll) {
      container.scrollTop = container.scrollHeight
    }
    // `turnCount` drives the effect — `shouldAutoScroll` is read but is
    // not the trigger; we don't want to slam back to the bottom the
    // instant the user reaches it via their own scroll.
  }, [turnCount, shouldAutoScroll])

  useEffect(() => {
    const container = containerRef.current
    if (container === null) {
      return
    }
    const handleScroll = () => {
      setShouldAutoScroll(isAtBottom(container, bottomThresholdPx))
    }
    container.addEventListener('scroll', handleScroll, { passive: true })
    return () => {
      container.removeEventListener('scroll', handleScroll)
    }
  }, [bottomThresholdPx])

  return (
    <div
      ref={containerRef}
      role="log"
      aria-live="polite"
      aria-relevant="additions"
      data-testid="transcript-scroller"
      data-auto-scroll={shouldAutoScroll ? 'true' : 'false'}
      className={`flex flex-col gap-3 overflow-y-auto ${className ?? ''}`}
    >
      {children}
    </div>
  )
}
