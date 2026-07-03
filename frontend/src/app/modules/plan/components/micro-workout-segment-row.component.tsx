import type { ReactElement } from 'react'
import { PreferredUnits } from '~/api/generated'
import { formatPaceSecPerKm } from '~/modules/common/utils/unit-format.helpers'
import type { WorkoutSegmentDto } from '~/modules/plan/models/plan.model'
import { INTENSITY_LABELS } from './plan-display.helpers'

export interface MicroWorkoutSegmentRowProps {
  segment: WorkoutSegmentDto
  index: number
  /**
   * Display unit for the segment pace. Defaults to Kilometers so callers that
   * predate the unit preference (and isolated tests) render the km form
   * unchanged.
   */
  units?: PreferredUnits
}

export const MicroWorkoutSegmentRow = ({
  segment,
  index,
  units = PreferredUnits.Kilometers,
}: MicroWorkoutSegmentRowProps): ReactElement => {
  const pace = formatPaceSecPerKm(segment.targetPaceSecPerKm, units)
  return (
    <li
      data-testid="micro-workout-segment"
      data-segment-type={segment.segmentType}
      data-segment-index={index}
      className="flex items-baseline justify-between gap-2 rounded-md bg-muted px-3 py-2 text-xs"
    >
      <span className="font-semibold text-foreground">
        {segment.segmentType}
        {segment.repetitions > 1 ? ` × ${segment.repetitions}` : ''}
      </span>
      <span className="text-muted-foreground">
        {segment.durationMinutes} min{pace === null ? '' : ` · ${pace}`}
      </span>
      <span className="text-[11px] uppercase tracking-wide text-muted-foreground">
        {INTENSITY_LABELS[segment.intensity] ?? segment.intensity}
      </span>
    </li>
  )
}
