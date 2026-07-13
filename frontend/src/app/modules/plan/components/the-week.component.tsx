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
  className?: string
}

const CELL_FILL_CLASSES: Record<DayCellState, string> = {
  done: 'bg-positive',
  today: 'border-2 border-clay-marker bg-transparent',
  planned: 'border border-border bg-muted',
  rest: 'bg-card',
}

/**
 * State-dependent day-label styling (design source: `split-alpine.dc.html`
 * sheets 2a/4a's THE WEEK grid). `done`/`planned` share the ordinary muted
 * mono label; `today` goes semibold clay-text (the only day singled out as
 * "you are here"); `rest` dims to `--alp-faint` — one of this slice's three
 * named `--alp-faint` consumption sites (§8 Non-negotiables: "rest-cell mono
 * labels"), consumed via the arbitrary-value pattern since the token has no
 * semantic slot by design (decorative-only, fails AA).
 */
const CELL_LABEL_CLASSES: Record<DayCellState, string> = {
  done: 'text-muted-foreground',
  today: 'font-semibold text-clay-text',
  planned: 'text-muted-foreground',
  rest: 'text-[color:var(--alp-faint)]',
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

  const loggedKm = weekLoggedKm({ weekNumber: currentWeekNumber, planStartDate, logs })
  const progress = formatWeekProgress(loggedKm, currentWeek.weeklyTargetKm, units)
  const cells = resolveDayCells({
    week: currentWeek,
    weekNumber: currentWeekNumber,
    planStartDate,
    logs,
    todayUtc,
  })

  return (
    <section data-testid="the-week" className={cn('flex flex-col gap-3', className)}>
      <SectionRule label="THE WEEK">
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
