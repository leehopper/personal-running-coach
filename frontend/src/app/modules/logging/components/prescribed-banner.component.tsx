// The /log form's "prescribed" banner: a single-line supplementary readout
// of the active plan's prescription for the log form's target date, sourced
// from the server-authoritative `getPrescribedWorkout` endpoint. This is
// display-only — the log form itself never reads or writes the
// prescription, it stays a free-typed log. The rendered label comes from
// `WORKOUT_TYPE_LABELS`, which carries the project's trademark-clean
// Daniels-Gilbert / pace-zone-index phrasing.

import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'
import type { PreferredUnits } from '~/api/generated'
import { useGetPrescribedWorkoutQuery } from '~/api/workout-log.api'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import { formatPaceRange } from '~/modules/logging/log-derivations.helpers'
import { WORKOUT_TYPE_LABELS } from '~/modules/plan/components/plan-display.helpers'
import type { WorkoutType } from '~/modules/plan/models/plan.model'

export interface PrescribedBannerProps {
  /** ISO `YYYY-MM-DD` date-only string — the log form's target date. */
  date: string
  units: PreferredUnits
  className?: string
}

/**
 * `workoutType` is a bare `string` on the wire (no validated union), so an
 * unmapped value (a future enum member, or a stale/malformed snapshot) must
 * be guarded before it can safely index `WORKOUT_TYPE_LABELS` — narrowing
 * this way avoids an unchecked `as WorkoutType` assertion at the call site.
 */
const isKnownWorkoutType = (value: string): value is WorkoutType =>
  Object.prototype.hasOwnProperty.call(WORKOUT_TYPE_LABELS, value)

/**
 * Renders nothing while loading (this banner is supplementary — no skeleton
 * is worth the layout churn), on a fetch error, and when the resolved date
 * has no prescription (`data === null`: off-plan, rest day, no active plan,
 * or a malformed stored prescription — the endpoint folds all of these into
 * one `null` body per PR-A). Only a present prescription renders the single
 * `Prescribed — {Type} · {Distance} · {Pace range}` line — source copy stays
 * sentence case; the span's CSS `uppercase` class is solely responsible for
 * the all-caps presentation, so nothing here calls `.toUpperCase()`.
 *
 * Reads `currentData`, not `data`: RTK Query retains the last successful
 * result across arg changes, so as `date` changes (e.g. the log form's date
 * picker moves), `data` would keep showing the previous date's prescription
 * until the new request resolves. `currentData` is `undefined` during that
 * in-flight window, which the loading/error/null guard below already treats
 * as "render nothing" — so the stale prescription never flashes.
 */
export const PrescribedBanner = ({
  date,
  units,
  className,
}: PrescribedBannerProps): ReactElement | null => {
  const { currentData, isLoading, isError } = useGetPrescribedWorkoutQuery(date)

  if (isLoading || isError || currentData === null || currentData === undefined) {
    return null
  }

  const workoutTypeLabel = isKnownWorkoutType(currentData.workoutType)
    ? WORKOUT_TYPE_LABELS[currentData.workoutType]
    : currentData.workoutType

  return (
    <div data-testid="prescribed-banner" className={cn('flex items-center gap-[10px]', className)}>
      <span aria-hidden className="size-2.5 rounded-xs bg-clay-marker" />
      <span className="font-mono text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
        Prescribed — {workoutTypeLabel} ·{' '}
        {formatDistanceKm(currentData.distanceMeters / 1000, units) ?? '—'} ·{' '}
        {formatPaceRange(currentData.paceFastSecPerKm, currentData.paceEasySecPerKm, units)}
      </span>
    </div>
  )
}
