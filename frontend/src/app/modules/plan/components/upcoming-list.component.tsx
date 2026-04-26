import type { ReactElement } from 'react'
import type {
  MesoWeekTemplate,
  MicroWorkoutCard as MicroWorkoutDto,
} from '~/modules/plan/models/plan.model'
import { MesoWeekBlock } from './meso-week-block.component'
import { MicroWorkoutCard } from './micro-workout-card.component'

export interface UpcomingListProps {
  /** Detailed workouts for the *current* week. */
  currentWeekWorkouts: readonly MicroWorkoutDto[]
  /** All meso-week templates (Slice 1 emits exactly four). */
  weeks: readonly MesoWeekTemplate[]
  /**
   * 1-based current training week. Workouts strictly *after* `today` within
   * this week are surfaced; the matching meso card highlights in the
   * subordinate `MesoWeekBlock`.
   */
  currentWeek: number
  /** Local date used to compute "today's" day-of-week index. */
  today?: Date
  className?: string
}

/**
 * "Upcoming" stack composed beneath `TodayCard`. Surfaces:
 *   1. The remainder of the current week's micro workouts (strictly after
 *      today's day-of-week).
 *   2. A `MesoWeekBlock` summarising every pre-generated week in the meso
 *      block (Slice 1 always emits four).
 *
 * Keeping the composition in this single component lets `HomePage` render
 * the upcoming surface as one slot without orchestrating the stacking
 * itself.
 */
export const UpcomingList = ({
  currentWeekWorkouts,
  weeks,
  currentWeek,
  today,
  className,
}: UpcomingListProps): ReactElement => {
  const date = today ?? new Date()
  const todayIndex = date.getDay()
  const remainder = [...currentWeekWorkouts]
    .filter((workout) => workout.dayOfWeek > todayIndex)
    .sort((left, right) => left.dayOfWeek - right.dayOfWeek)

  return (
    <section
      aria-label="Upcoming"
      data-testid="upcoming-list"
      className={`flex flex-col gap-6 ${className ?? ''}`}
    >
      {remainder.length > 0 ? (
        <div className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold text-slate-900">Rest of this week</h2>
          <ol
            aria-label="Workouts later this week"
            data-testid="upcoming-week-remainder"
            className="flex flex-col gap-3"
          >
            {remainder.map((workout) => (
              // dayOfWeek (0..6) is unique within a single week's workouts
              // because the structured-output schema emits one workout per
              // day at most.
              <li key={`day-${workout.dayOfWeek}`} data-testid="upcoming-workout-item">
                <MicroWorkoutCard workout={workout} />
              </li>
            ))}
          </ol>
        </div>
      ) : null}

      {weeks.length > 0 ? (
        <div className="flex flex-col gap-3">
          <h2 className="text-lg font-semibold text-slate-900">Upcoming weeks</h2>
          <MesoWeekBlock weeks={weeks} currentWeek={currentWeek} />
        </div>
      ) : null}
    </section>
  )
}
