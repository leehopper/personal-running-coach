import type { ReactElement } from 'react'

import type { WorkoutLogDto } from '~/api/generated'
import {
  COMPLETION_STATUS_LABELS,
  formatDistanceKm,
  formatDuration,
  formatLogDate,
  formatLogPace,
} from './history-format.helpers'
import { WorkoutLogMetrics } from './workout-log-metrics.component'
import { WorkoutLogSplits } from './workout-log-splits.component'

export interface WorkoutLogEntryProps {
  /** A single logged workout from the history query. */
  log: WorkoutLogDto
}

interface CoreStat {
  label: string
  value: string
}

const buildCoreStats = (log: WorkoutLogDto): CoreStat[] => {
  const stats: CoreStat[] = []
  const distance = formatDistanceKm(log.distanceMeters)
  if (distance !== null) {
    stats.push({ label: 'Distance', value: distance })
  }
  const duration = formatDuration(log.durationSeconds)
  if (duration !== null) {
    stats.push({ label: 'Duration', value: duration })
  }
  const pace = formatLogPace(log.distanceMeters, log.durationSeconds)
  if (pace !== null) {
    stats.push({ label: 'Pace', value: pace })
  }
  return stats
}

const hasText = (value: string | null | undefined): value is string =>
  value !== null && value !== undefined && value.trim().length > 0

/**
 * One logged workout as a card: date + completion status header, the derived
 * core stats (distance/duration/pace, each shown only when meaningful — a
 * skipped run has none), the sparse optional-metrics `<dl>`, the freeform note,
 * and the display-only splits collapsible (spec § Unit 7). All colour is
 * semantic-token-based and animations pair a `motion-reduce:` variant.
 */
export const WorkoutLogEntry = ({ log }: WorkoutLogEntryProps): ReactElement => {
  const coreStats = buildCoreStats(log)

  return (
    <article
      data-testid="workout-history-entry"
      data-completion-status={log.completionStatus}
      className="flex flex-col gap-3 rounded-lg border bg-card p-4 text-sm text-card-foreground shadow-sm"
    >
      <header className="flex items-baseline justify-between gap-2">
        <h3 className="text-base font-semibold text-foreground">{formatLogDate(log.occurredOn)}</h3>
        <span
          data-testid="workout-history-entry-status"
          className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground"
        >
          {COMPLETION_STATUS_LABELS[log.completionStatus]}
        </span>
      </header>

      {coreStats.length > 0 ? (
        <dl className="grid grid-cols-3 gap-3">
          {coreStats.map((stat) => (
            <div key={stat.label} className="flex flex-col">
              <dt className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                {stat.label}
              </dt>
              <dd className="text-base font-semibold text-foreground">{stat.value}</dd>
            </div>
          ))}
        </dl>
      ) : null}

      <WorkoutLogMetrics metrics={log.metrics} />

      {hasText(log.notes) ? (
        <p
          data-testid="workout-history-entry-notes"
          className="rounded-md bg-muted px-3 py-2 text-xs leading-snug text-muted-foreground"
        >
          {log.notes}
        </p>
      ) : null}

      {log.splits !== null && log.splits !== undefined && log.splits.length > 0 ? (
        <WorkoutLogSplits splits={log.splits} />
      ) : null}
    </article>
  )
}
