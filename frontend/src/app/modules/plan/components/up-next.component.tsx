import type { ReactElement } from 'react'
import { cn } from '@/lib/utils'
import type { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import { SectionRule } from '~/modules/common/components/section-rule/section-rule.component'
import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'
import { DAY_OF_WEEK_LABELS } from './plan-display.helpers'

/** Props for {@link UpNext}. */
export interface UpNextProps {
  /** Detailed workouts for the *current* week. */
  currentWeekWorkouts: readonly MicroWorkoutCardDto[]
  /** Raw local `Date` — a LOCAL-getter consumer (`today.getDay()`), not a `todayUtc` one (Slice 2 §2.1). */
  today: Date
  units: PreferredUnits
  className?: string
}

/**
 * Today screen's "UP NEXT" section: the remainder of the current week's
 * workouts strictly after today's day-of-week, one row per workout (day
 * abbreviation, title, right-aligned distance — the same row shape
 * `WorkoutHero`'s rest-day next-workout line reuses verbatim). Renders no
 * placeholder when nothing remains this week.
 */
export const UpNext = ({
  currentWeekWorkouts,
  today,
  units,
  className,
}: UpNextProps): ReactElement => {
  const todayIndex = today.getDay()
  const remaining = [...currentWeekWorkouts]
    .filter((workout) => workout.dayOfWeek > todayIndex)
    .sort((left, right) => left.dayOfWeek - right.dayOfWeek)

  return (
    <section data-testid="up-next" className={cn('flex flex-col gap-3', className)}>
      <SectionRule label="Up next" />
      {remaining.map((workout, index) => (
        <div
          key={workout.dayOfWeek}
          data-testid="up-next-row"
          className={cn(
            'flex items-baseline justify-between gap-3 py-2',
            index < remaining.length - 1 ? 'border-b border-border' : null,
          )}
        >
          <span className="flex items-baseline gap-3">
            <span className="t-data-label shrink-0 text-muted-foreground">
              {DAY_OF_WEEK_LABELS[workout.dayOfWeek].slice(0, 3)}
            </span>
            <span className="t-row-title text-foreground">{workout.title}</span>
          </span>
          <span className="t-numeral text-muted-foreground">
            {formatDistanceKm(workout.targetDistanceKm, units) ?? '—'}
          </span>
        </div>
      ))}
    </section>
  )
}
