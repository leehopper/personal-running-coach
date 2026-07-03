import type { ReactElement } from 'react'
import { Badge } from '@/components/ui/badge'
import { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import type { MesoWeekTemplateDto } from '~/modules/plan/models/plan.model'
import { labelForPhase } from './plan-display.helpers'

/** Props for {@link MesoWeekBlock}. */
export interface MesoWeekBlockProps {
  /** Pre-generated weekly templates from `PlanProjectionDto.mesoWeeks`. */
  weeks: readonly MesoWeekTemplateDto[]
  /**
   * 1-based current training week. The matching card highlights;
   * later weeks dim. Pass `null` to render every week in the neutral state
   * (used by tests and previews).
   */
  currentWeek: number | null
  /**
   * Display unit for the weekly target volume. Defaults to Kilometers so
   * callers that predate the unit preference (and isolated tests) render the
   * km form unchanged.
   */
  units?: PreferredUnits
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
  past: 'bg-muted text-muted-foreground',
  current: 'bg-primary text-primary-foreground ring-2 ring-primary',
  future: 'bg-card text-muted-foreground opacity-70',
  neutral: 'bg-card text-card-foreground',
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
  units = PreferredUnits.Kilometers,
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
          className={`flex flex-col gap-2 rounded-lg border p-4 text-sm shadow-sm transition-colors duration-200 ease-out motion-reduce:transition-none ${STATE_STYLES[state]}`}
        >
          <header className="flex items-baseline justify-between">
            <span className="text-xs font-semibold uppercase tracking-wide">
              Week {week.weekNumber}
            </span>
            <span className="text-xs font-medium opacity-80">{labelForPhase(week.phaseType)}</span>
          </header>
          <p className="text-lg font-semibold leading-tight">
            {formatDistanceKm(week.weeklyTargetKm, units) ?? '—'}
          </p>
          {week.isDeloadWeek ? (
            <Badge
              variant="secondary"
              data-testid="meso-week-deload-flag"
              className="text-[11px] font-semibold uppercase tracking-wide"
            >
              Deload week
            </Badge>
          ) : null}
          {week.weekSummary.trim().length > 0 ? (
            <p className="text-xs leading-snug opacity-90">{week.weekSummary}</p>
          ) : null}
        </li>
      )
    })}
  </ol>
)
