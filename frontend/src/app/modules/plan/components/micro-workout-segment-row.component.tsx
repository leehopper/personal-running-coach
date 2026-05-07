import type { ReactElement } from 'react'
import type { WorkoutSegmentDto } from '~/modules/plan/models/plan.model'
import { formatPacePerKm } from '~/modules/plan/utils/pace-format.helpers'
import { INTENSITY_LABELS } from './plan-display.helpers'

export interface MicroWorkoutSegmentRowProps {
  segment: WorkoutSegmentDto
  index: number
}

export const MicroWorkoutSegmentRow = ({
  segment,
  index,
}: MicroWorkoutSegmentRowProps): ReactElement => {
  const pace = formatPacePerKm(segment.targetPaceSecPerKm)
  return (
    <li
      data-testid="micro-workout-segment"
      data-segment-type={segment.segmentType}
      data-segment-index={index}
      className="flex items-baseline justify-between gap-2 rounded-md bg-slate-50 px-3 py-2 text-xs"
    >
      <span className="font-semibold text-slate-700">
        {segment.segmentType}
        {segment.repetitions > 1 ? ` × ${segment.repetitions}` : ''}
      </span>
      <span className="text-slate-600">
        {segment.durationMinutes} min{pace === null ? '' : ` · ${pace}`}
      </span>
      <span className="text-[11px] uppercase tracking-wide text-slate-500">
        {INTENSITY_LABELS[segment.intensity]}
      </span>
    </li>
  )
}
