import type { ReactElement } from 'react'
import { CheckIcon } from 'lucide-react'
import { cn } from '@/lib/utils'
import { PreferredUnits } from '~/api/generated'
import type { WorkoutLogDto } from '~/api/generated'
import { SectionRule } from '~/modules/common/components/section-rule/section-rule.component'
import type { MesoWeekTemplateDto } from '~/modules/plan/models/plan.model'
import { DAY_OF_WEEK_LABELS } from './plan-display.helpers'
import {
  type DayCellState,
  formatWeekProgress,
  formatWeekProgressUnknown,
  resolveDayCells,
  weekLoggedKm,
} from './the-week.helpers'

/** Props for {@link TheWeek}. */
export interface TheWeekProps {
  currentWeek: MesoWeekTemplateDto | undefined
  currentWeekNumber: number
  planStartDate: string
  /** Full fetched log list — the component filters internally to this week's span. */
  logs: readonly WorkoutLogDto[]
  /** Pre-normalized UTC-midnight epoch — same pipeline as `WorkoutHero`'s `todayUtc`. */
  todayUtc: number
  units: PreferredUnits
  /**
   * `true` when the caller's workout-log fetch failed or is still in
   * flight — `logs` cannot be trusted to mean "nothing logged." Renders the
   * progress string's logged side as an explicit unknown marker and treats
   * every cell as un-logged, rather than fabricating a `0.0`/`done`-less
   * week that looks like confirmed zero progress. Defaults to `false` so
   * existing callers that already know their log state are unaffected.
   */
  logsUnavailable?: boolean
  className?: string
}

const CELL_FILL_CLASSES: Record<DayCellState, string> = {
  done: 'bg-positive',
  today: 'border-2 border-clay-marker bg-transparent',
  planned: 'border border-border bg-muted',
  rest: 'bg-card',
}

/**
 * State-dependent day-label styling. `today` goes semibold clay-text (the
 * only day singled out as "you are here"); `done`/`planned`/`rest` all
 * share the ordinary muted mono label.
 *
 * The design mock dims `rest` to the decorative-only faint tint, rejected
 * here — that tint fails AA contrast by design and must never carry
 * essential text, but a rest-day label ("SUN") is essential
 * calendar-legibility text, not decoration. `rest` instead renders in
 * `--muted-foreground` (already contrast-gated), the same class
 * `done`/`planned` already use — a deliberate, documented deviation from
 * the mock that collapses three of the four states to one shared
 * treatment. `today` remains the only visually distinguished label, which
 * is still the meaningful state distinction this grid needs to carry ("you
 * are here" vs. everything else).
 */
const CELL_LABEL_CLASSES: Record<DayCellState, string> = {
  done: 'text-muted-foreground',
  today: 'font-semibold text-clay-text',
  planned: 'text-muted-foreground',
  rest: 'text-muted-foreground',
}

/**
 * Today screen's week grid: `N.N/NN.N KM` progress + a 7-cell Sunday-first
 * grid of `done`/`today`/`planned`/`rest` day cells, joined from the current
 * meso week's day-slot template and this week's logged runs.
 */
export const TheWeek = ({
  currentWeek,
  currentWeekNumber,
  planStartDate,
  logs,
  todayUtc,
  units,
  logsUnavailable = false,
  className,
}: TheWeekProps): ReactElement => {
  if (currentWeek === undefined) {
    return (
      <section
        data-testid="the-week"
        data-state="unavailable"
        className={cn('flex flex-col gap-3', className)}
      >
        <p className="t-body text-muted-foreground">This week's plan isn't ready yet.</p>
      </section>
    )
  }

  // `logsUnavailable` forces an empty log list into both derivations below,
  // regardless of what the caller passed in `logs` — the same defensive
  // stance the home page takes when it resolves the hero's
  // `isTodayLogged`: never let a stale/partial `logs` array leak a
  // fabricated "done" or "0.0 km" fact through when the fetch itself is
  // known to be untrustworthy.
  const trustedLogs = logsUnavailable ? [] : logs
  const loggedKm = weekLoggedKm({ weekNumber: currentWeekNumber, planStartDate, logs: trustedLogs })
  const progress = logsUnavailable
    ? formatWeekProgressUnknown(currentWeek.weeklyTargetKm, units)
    : formatWeekProgress(loggedKm, currentWeek.weeklyTargetKm, units)
  const cells = resolveDayCells({
    week: currentWeek,
    weekNumber: currentWeekNumber,
    planStartDate,
    logs: trustedLogs,
    todayUtc,
  })

  return (
    <section data-testid="the-week" className={cn('flex flex-col gap-3', className)}>
      <SectionRule label="The week">
        <span className="t-data-label text-muted-foreground">{progress}</span>
      </SectionRule>
      <div className="grid grid-cols-7 gap-2">
        {cells.map((cell) => (
          <div
            key={cell.dayOfWeek}
            data-testid="the-week-day-cell"
            data-day-of-week={cell.dayOfWeek}
            data-state={cell.state}
            role="img"
            aria-label={`${DAY_OF_WEEK_LABELS[cell.dayOfWeek]}, ${cell.state}`}
            className="flex flex-col items-center gap-1"
          >
            <span
              aria-hidden="true"
              className={cn(
                'flex h-[26px] w-full items-center justify-center rounded-xs',
                CELL_FILL_CLASSES[cell.state],
              )}
            >
              {cell.state === 'done' ? (
                <CheckIcon className="size-[11px] text-background" strokeWidth={3.5} />
              ) : null}
            </span>
            <span aria-hidden="true" className={cn('t-data-label', CELL_LABEL_CLASSES[cell.state])}>
              {DAY_OF_WEEK_LABELS[cell.dayOfWeek].slice(0, 3)}
            </span>
          </div>
        ))}
      </div>
    </section>
  )
}
