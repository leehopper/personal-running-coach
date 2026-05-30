import type { ReactElement } from 'react'
import type { MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'
import { formatPaceRangePerKm } from '~/modules/plan/utils/pace-format.helpers'
import { MicroWorkoutSegmentRow } from './micro-workout-segment-row.component'
import { DAY_OF_WEEK_LABELS, WORKOUT_TYPE_LABELS } from './plan-display.helpers'

/** Props for {@link MicroWorkoutCard}. */
export interface MicroWorkoutCardProps {
  /** Detailed workout from `PlanProjectionDto.microWorkoutsByWeek[N]`. */
  workout: MicroWorkoutCardDto
  /** Optional emphasis flag used by `TodayCard` to render the prominent variant. */
  emphasized?: boolean
  className?: string
}

/**
 * Detail card for a single workout. Renders title, workout-type label,
 * target distance, target pace range (`MM:SS/km`), structured segments,
 * and the LLM-emitted coaching note. Segment list collapses cleanly when
 * the structured-output schema emits zero segments (typical for an easy run).
 *
 * The pace string flows through `formatPaceRangePerKm` from `pace-format.helpers`
 * — the single home for `MM:SS/km` formatting in this module. Workout-type
 * labels and intensity labels live in `plan-display.helpers.ts` to keep
 * trademark-clean phrasing in one auditable place.
 */
export const MicroWorkoutCard = ({
  workout,
  emphasized = false,
  className,
}: MicroWorkoutCardProps): ReactElement => {
  const paceRange = formatPaceRangePerKm(
    workout.targetPaceFastSecPerKm,
    workout.targetPaceEasySecPerKm,
  )
  const dayLabel = DAY_OF_WEEK_LABELS[workout.dayOfWeek] ?? ''

  return (
    <article
      data-testid="micro-workout-card"
      data-workout-type={workout.workoutType}
      data-emphasized={emphasized ? 'true' : 'false'}
      className={`flex flex-col gap-3 rounded-lg border bg-card p-4 text-sm text-card-foreground shadow-sm transition-colors duration-200 ease-out motion-reduce:transition-none ${
        emphasized ? 'border-primary ring-2 ring-primary' : ''
      } ${className ?? ''}`}
    >
      <header className="flex flex-col gap-1">
        {dayLabel.length > 0 ? (
          <span className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
            {dayLabel}
          </span>
        ) : null}
        <h3 className="text-base font-semibold leading-tight text-foreground">{workout.title}</h3>
        <span
          data-testid="micro-workout-type-label"
          className="text-xs font-medium uppercase tracking-wide text-muted-foreground"
        >
          {WORKOUT_TYPE_LABELS[workout.workoutType]}
        </span>
      </header>

      <dl className="grid grid-cols-2 gap-3 text-sm">
        <div>
          <dt className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
            Distance
          </dt>
          <dd className="text-base font-semibold text-foreground">
            {workout.targetDistanceKm.toFixed(1)} km
          </dd>
        </div>
        <div>
          <dt className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
            Target pace
          </dt>
          <dd data-testid="micro-workout-pace" className="text-base font-semibold text-foreground">
            {paceRange ?? '—/km'}
          </dd>
        </div>
      </dl>

      {workout.segments.length > 0 ? (
        <ul
          aria-label="Workout segments"
          data-testid="micro-workout-segments"
          className="flex flex-col gap-1"
        >
          {workout.segments.map((segment, index) => (
            // Segments arrive in execution order from a structured-output
            // schema and rerender as a unit when the workout changes;
            // combining `segmentType` + index keeps the key stable without
            // claiming false uniqueness across reorderings.
            <MicroWorkoutSegmentRow
              key={`${segment.segmentType}-${index}`}
              segment={segment}
              index={index}
            />
          ))}
        </ul>
      ) : null}

      {workout.coachingNotes.trim().length > 0 ? (
        <p
          data-testid="micro-workout-coaching-notes"
          className="rounded-md bg-muted px-3 py-2 text-xs leading-snug text-muted-foreground"
        >
          {workout.coachingNotes}
        </p>
      ) : null}
    </article>
  )
}
