import type { ReactElement } from 'react'
import type { MesoWeekTemplate } from '~/modules/plan/models/plan.model'
import { labelForPhase } from './plan-display.helpers'

export interface MesoWeekBlockProps {
  /** Pre-generated weekly templates from `PlanProjectionDto.mesoWeeks`. */
  weeks: readonly MesoWeekTemplate[]
  /**
   * 1-based current training week. The matching card highlights;
   * later weeks dim. Pass `null` to render every week in the neutral state
   * (used by tests and previews).
   */
  currentWeek: number | null
  className?: string
}

const cardStateFor = (
  weekNumber: number,
  currentWeek: number | null,
): 'past' | 'current' | 'future' | 'neutral' => {
  if (currentWeek === null) {
    return 'neutral'
  }
  if (weekNumber < currentWeek) {
    return 'past'
  }
  if (weekNumber === currentWeek) {
    return 'current'
  }
  return 'future'
}

const STATE_STYLES: Record<'past' | 'current' | 'future' | 'neutral', string> = {
  past: 'bg-slate-100 text-slate-500',
  current: 'bg-slate-900 text-slate-50 ring-2 ring-slate-900',
  future: 'bg-white text-slate-500 opacity-70',
  neutral: 'bg-white text-slate-700',
}

/**
 * Four-up grid of weekly template cards. Each card surfaces the high-level
 * coaching narrative for one meso week: target volume, deload flag, and the
 * one-line `weekSummary` from the structured-output schema. The card matching
 * `currentWeek` is highlighted; future weeks are dimmed per spec § Unit 4
 * R04.5.
 *
 * Trademark hygiene: this component renders no pace numbers and no zone
 * names. The `phaseType` label flows through `labelForPhase` for a single
 * point of canonical phrasing.
 */
export const MesoWeekBlock = ({
  weeks,
  currentWeek,
  className,
}: MesoWeekBlockProps): ReactElement => (
  <ol
    aria-label="Upcoming weeks"
    data-testid="meso-week-block"
    className={`grid w-full grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4 ${className ?? ''}`}
  >
    {weeks.map((week) => {
      const state = cardStateFor(week.weekNumber, currentWeek)
      return (
        <li
          // `weekNumber` is unique within the meso block (1..N) and stable
          // across renders, so it is a safe, non-positional key.
          key={week.weekNumber}
          data-testid="meso-week-card"
          data-week={week.weekNumber}
          data-state={state}
          aria-current={state === 'current' ? 'step' : undefined}
          className={`flex flex-col gap-2 rounded-lg border border-slate-200 p-4 text-sm shadow-sm transition-colors duration-200 ease-out ${STATE_STYLES[state]}`}
        >
          <header className="flex items-baseline justify-between">
            <span className="text-xs font-semibold uppercase tracking-wide">
              Week {week.weekNumber}
            </span>
            <span className="text-xs font-medium opacity-80">{labelForPhase(week.phaseType)}</span>
          </header>
          <p className="text-lg font-semibold leading-tight">{week.weeklyTargetKm.toFixed(1)} km</p>
          {week.isDeloadWeek ? (
            <span
              data-testid="meso-week-deload-flag"
              className="inline-flex w-fit items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-amber-900"
            >
              Deload week
            </span>
          ) : null}
          {week.weekSummary.trim().length > 0 ? (
            <p className="text-xs leading-snug opacity-90">{week.weekSummary}</p>
          ) : null}
        </li>
      )
    })}
  </ol>
)
