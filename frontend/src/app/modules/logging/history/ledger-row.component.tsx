import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'
import { CompletionStatus, PreferredUnits, type WorkoutLogDto } from '~/api/generated'
import { WORKOUT_TYPE_LABELS } from '~/modules/plan/components/plan-display.helpers'
import type { WorkoutType } from '~/modules/plan/models/plan.model'
import {
  COMPLETION_STATUS_LABELS,
  formatDuration,
  formatHistoryDistanceKm,
  formatLedgerDayParts,
  formatLogPace,
} from './history-format.helpers'
import { WorkoutLogMetrics } from './workout-log-metrics.component'
import { WorkoutLogSplits } from './workout-log-splits.component'

export interface LedgerRowProps {
  /** A single logged workout from the history query. */
  log: WorkoutLogDto
  /**
   * Display unit for the distance + pace. Defaults to Kilometers so callers
   * that predate the unit preference (and isolated tests) render the km form
   * unchanged.
   */
  units?: PreferredUnits
}

/**
 * `prescribedWorkoutType` is a bare `string` on the wire (no validated
 * union), so an unmapped value must be guarded before it can safely index
 * `WORKOUT_TYPE_LABELS` — same narrowing as the `/log` prescribed banner's
 * `isKnownWorkoutType`, duplicated locally rather than shared because the
 * two call sites resolve to different fallbacks (the banner falls back to
 * the raw wire string; the ledger row falls back to the generic "Run").
 */
const isKnownWorkoutType = (value: string): value is WorkoutType =>
  Object.prototype.hasOwnProperty.call(WORKOUT_TYPE_LABELS, value)

/**
 * Resolves the row's title: the prescribed workout's display label when the
 * log carries a recognised `prescribedWorkoutType`, else the generic "Run" —
 * covering both an off-plan log (no prescription) and a legacy/malformed one
 * (`null`/unrecognised type).
 */
const resolveTitle = (prescribedWorkoutType: string | null | undefined): string => {
  if (
    prescribedWorkoutType !== null &&
    prescribedWorkoutType !== undefined &&
    isKnownWorkoutType(prescribedWorkoutType)
  ) {
    return WORKOUT_TYPE_LABELS[prescribedWorkoutType]
  }
  return 'Run'
}

/** Status-tag text colour per completion status (spec § 3 PR-C, ratified). */
const STATUS_COLOR_CLASS: Record<CompletionStatus, string> = {
  [CompletionStatus.Complete]: 'text-positive',
  [CompletionStatus.Partial]: 'text-warning-text',
  [CompletionStatus.Skipped]: 'text-danger-text',
}

const hasText = (value: string | null | undefined): value is string =>
  value !== null && value !== undefined && value.trim().length > 0

interface LedgerStat {
  key: string
  value: string
  className: string
}

/**
 * Builds the row's right-column stats: distance, duration, and pace — each
 * included only when its formatter returns non-null (a skipped/zero-actuals
 * log yields none). Pace gets a space injected before its unit suffix
 * (`"06:00 /km"`, spec § 4.4) — the ledger row's pace rendering intentionally
 * diverges from the `/log` prescribed banner's unspaced `"06:00/km"`; do not
 * unify the two.
 */
const buildLedgerStats = (log: WorkoutLogDto, units: PreferredUnits): LedgerStat[] => {
  const stats: LedgerStat[] = []
  const distance = formatHistoryDistanceKm(log.distanceMeters, units)
  if (distance !== null) {
    stats.push({
      key: 'distance',
      value: distance,
      className: 'font-condensed text-[17px] font-bold text-foreground',
    })
  }
  const duration = formatDuration(log.durationSeconds)
  if (duration !== null) {
    stats.push({
      key: 'duration',
      value: duration,
      className: 't-data-value text-muted-foreground',
    })
  }
  const pace = formatLogPace(log.distanceMeters, log.durationSeconds, units)
  if (pace !== null) {
    stats.push({
      key: 'pace',
      value: pace.replace('/', ' /'),
      className: 't-data-value text-muted-foreground',
    })
  }
  return stats
}

/**
 * One logged workout as a hairline-separated ledger row (spec § 3 PR-C,
 * DEC-089 D5): a day numeral + faint weekday in column 1, the resolved
 * workout title + status tag + optional note snippet in column 2, and up to
 * three right-aligned derived stats (distance/duration/pace) in column 3 —
 * collapsing to a single "—" placeholder when a skipped/zero-actuals log
 * leaves all three null. The optional-metrics `<dl>` and the display-only
 * splits collapsible mount below the grid exactly as they did on the
 * card-shaped `WorkoutLogEntry` this component replaces — unchanged props,
 * unchanged components, so their own coverage and (PR-D's) splits mount
 * point both carry over untouched.
 */
export const LedgerRow = ({
  log,
  units = PreferredUnits.Kilometers,
}: LedgerRowProps): ReactElement => {
  const isSkipped = log.completionStatus === CompletionStatus.Skipped
  const { dayNum, weekday } = formatLedgerDayParts(log.occurredOn)
  const title = resolveTitle(log.prescribedWorkoutType)
  const statusLabel = `${COMPLETION_STATUS_LABELS[log.completionStatus]}${log.isOnPlan ? ' · ON-PLAN' : ''}`
  const stats = buildLedgerStats(log, units)

  return (
    <article
      data-testid="workout-history-entry"
      data-completion-status={log.completionStatus}
      className={cn(
        'grid grid-cols-[52px_1fr_auto] items-start gap-3 border-b border-border py-3',
        isSkipped ? 'opacity-75' : null,
      )}
    >
      <div className="flex flex-col gap-0.5">
        {/*
         * The mock renders the skipped day-numeral in `--alp-faint`, but that
         * token is decorative-only / AA-failing and must never carry
         * essential text (spec §8) — the day numeral is the row's primary
         * date identifier. `text-muted-foreground` is the AA-safe dimming
         * token instead (same faint→muted migration Slice 2 made for the
         * skipped title below); the row-level `opacity-75` remains the
         * primary visual dimming mechanism for the skipped state.
         */}
        <span
          data-testid="workout-history-entry-day"
          className={cn('t-numeral', isSkipped ? 'text-muted-foreground' : 'text-foreground')}
        >
          {dayNum}
        </span>
        <span className="t-data-label uppercase text-[var(--alp-faint)]">{weekday}</span>
      </div>

      <div className="flex flex-col gap-[3px]">
        <span
          className={cn('t-row-title', isSkipped ? 'text-muted-foreground' : 'text-foreground')}
        >
          {title}
        </span>
        <span
          data-testid="workout-history-entry-status"
          className={cn('t-data-label uppercase', STATUS_COLOR_CLASS[log.completionStatus])}
        >
          {statusLabel}
        </span>
        {hasText(log.notes) ? (
          <p
            data-testid="workout-history-entry-notes"
            className="line-clamp-1 text-[13px] leading-snug text-muted-foreground"
          >
            {log.notes}
          </p>
        ) : null}
      </div>

      <div className="flex flex-col items-end gap-0.5">
        {stats.length > 0 ? (
          stats.map((stat) => (
            <span key={stat.key} className={cn(stat.className, 'uppercase')}>
              {stat.value}
            </span>
          ))
        ) : (
          <span className="t-data-value text-muted-foreground">—</span>
        )}
      </div>

      <div className="col-span-3">
        <WorkoutLogMetrics metrics={log.metrics} />
        {log.splits !== null && log.splits !== undefined && log.splits.length > 0 ? (
          <WorkoutLogSplits splits={log.splits} units={units} />
        ) : null}
      </div>
    </article>
  )
}
