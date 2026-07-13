import type { ReactElement } from 'react'
import { cn } from '@/lib/utils'
import { MonoLabel } from '~/modules/common/components/mono-label/mono-label.component'
import { Wordmark } from '~/modules/common/components/wordmark/wordmark.component'

/** Props for {@link TodayHeader}. */
export interface TodayHeaderProps {
  /** 1-based current training week, from `resolveCurrentWeek`. */
  weekNumber: number
  /** `Macro.TotalWeeks`, or `null` when `plan.macro === null`. */
  totalWeeks: number | null
  /** Friendly phase label for `weekNumber` (`labelForPhase(phaseForWeek(...))`), or `null` when `plan.macro === null`. */
  phaseLabel: string | null
  className?: string
}

/**
 * Today screen's page header: the `SPLIT/` wordmark (its first production
 * mount point), a 2px section rule, and `WEEK N OF M — PHASE` in mono. Falls
 * back to `WEEK N` alone when `totalWeeks`/`phaseLabel` are both `null` (the
 * `plan.macro === null` defensive case) — never crashes.
 */
export const TodayHeader = ({
  weekNumber,
  totalWeeks,
  phaseLabel,
  className,
}: TodayHeaderProps): ReactElement => {
  const weekLine =
    totalWeeks === null || phaseLabel === null
      ? `Week ${weekNumber}`
      : `Week ${weekNumber} of ${totalWeeks} — ${phaseLabel}`

  return (
    <header data-testid="today-header" className={cn('flex flex-col gap-2.5', className)}>
      <div className="flex items-baseline justify-between">
        <Wordmark />
        <MonoLabel>{weekLine}</MonoLabel>
      </div>
      {/* The header's own 2px rule — a plain sibling div per the design mock's
       * DOM shape, deliberately NOT `SectionRule`'s border-top-on-label-row
       * pattern (visually identical, structurally distinct). */}
      <div className="h-0.5 bg-rule" />
    </header>
  )
}
