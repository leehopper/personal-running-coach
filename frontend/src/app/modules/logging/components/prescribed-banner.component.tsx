// The /log form's "prescribed" banner (Slice 4 PR-B): a single-line
// supplementary readout of the active plan's prescription for the log
// form's target date, sourced from the server-authoritative
// `getPrescribedWorkout` endpoint (PR-A). This is display-only — the log
// form itself never reads or writes the prescription, it stays a free-typed
// log. Per the root `CLAUDE.md` trademark rule, the rendered label comes from
// `WORKOUT_TYPE_LABELS` (Daniels-Gilbert / pace-zone-index phrasing) — never
// "VDOT".

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
 * Renders nothing while loading (this banner is supplementary — no skeleton
 * is worth the layout churn), on a fetch error, and when the resolved date
 * has no prescription (`data === null`: off-plan, rest day, no active plan,
 * or a malformed stored prescription — the endpoint folds all of these into
 * one `null` body per PR-A). Only a present prescription renders the single
 * `PRESCRIBED — {TYPE} · {DISTANCE} · {PACE RANGE}` line, with the dynamic
 * fragments uppercased via `.toUpperCase()` and the static copy/separators
 * uppercased via CSS — both derived-value paths agree on casing, so the
 * static "PRESCRIBED" reads consistently alongside them.
 */
export const PrescribedBanner = ({
  date,
  units,
  className,
}: PrescribedBannerProps): ReactElement | null => {
  const { data, isLoading, isError } = useGetPrescribedWorkoutQuery(date)

  if (isLoading || isError || data === null || data === undefined) {
    return null
  }

  // `workoutType` is a bare `string` on the wire (no validated union), so an
  // unmapped value (a future enum member, or a stale/malformed snapshot)
  // falls back to the raw type string rather than crashing the lookup.
  const workoutTypeLabel = WORKOUT_TYPE_LABELS[data.workoutType as WorkoutType] ?? data.workoutType

  return (
    <div data-testid="prescribed-banner" className={cn('flex items-center gap-[10px]', className)}>
      <span aria-hidden className="size-2.5 rounded-xs bg-clay-marker" />
      <span className="font-mono text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
        PRESCRIBED — {workoutTypeLabel.toUpperCase()} ·{' '}
        {formatDistanceKm(data.distanceMeters / 1000, units) ?? '—'} ·{' '}
        {formatPaceRange(data.paceFastSecPerKm, data.paceEasySecPerKm, units).toUpperCase()}
      </span>
    </div>
  )
}
