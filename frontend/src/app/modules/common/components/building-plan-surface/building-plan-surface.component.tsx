import { cn } from '@/lib/utils'

export interface BuildingPlanSurfaceProps {
  /**
   * Mono status line under the progress indicator, e.g. "The coach drafts
   * 12 weeks in about 30 seconds."
   */
  statusLine?: string
  className?: string
}

const DEFAULT_STATUS_LINE = 'The coach drafts 12 weeks in about 30 seconds.'

/**
 * Full-surface "plan is building" state. Replaces the whole screen while the
 * coach composes a plan - shared by the onboarding submit flow and the
 * settings regenerate flow, so it carries no feature-specific wiring of its
 * own beyond the status copy.
 *
 * The progress indicator is deliberately indeterminate: plan generation has
 * no real percent-complete signal to report, so a partial-width clay bar
 * travels the length of its track on a loop (`animate-indeterminate`,
 * index.css) instead of sitting at a fixed position — a fixed bar that only
 * pulses opacity reads as "stalled," not "working." Reduced motion swaps
 * the travel for a stationary opacity pulse (no positional movement).
 * `role="status"`/`aria-live="polite"` on the wrapper means the heading and
 * status line are announced to assistive tech without needing a numeric
 * `aria-valuenow` on the (decorative) bar.
 */
export const BuildingPlanSurface = ({
  statusLine = DEFAULT_STATUS_LINE,
  className,
}: BuildingPlanSurfaceProps) => {
  return (
    <div
      data-slot="building-plan-surface"
      role="status"
      aria-live="polite"
      className={cn(
        'screen-gutter flex min-h-screen flex-col items-center justify-center gap-6 bg-background text-center',
        className,
      )}
    >
      <p className="t-screen-title text-foreground">BUILDING YOUR PLAN</p>

      <div
        aria-hidden="true"
        className="relative h-1.5 w-48 max-w-full overflow-hidden rounded-full bg-secondary"
      >
        <div className="absolute inset-y-0 left-0 w-2/5 animate-indeterminate rounded-full bg-primary motion-reduce:translate-x-0 motion-reduce:animate-pulse" />
      </div>

      <p className="font-mono text-sm text-muted-foreground">{statusLine}</p>
    </div>
  )
}
