import type { ReactElement } from 'react'

import { WORKOUT_METRIC_META } from '~/modules/logging/metric-meta'

export interface WorkoutLogMetricsProps {
  /** The open `metrics` bag from a `WorkoutLogDto` (may be null/undefined). */
  metrics?: Record<string, unknown> | null
}

interface PresentMetric {
  key: string
  label: string
  display: string
}

const isPresent = (value: unknown): boolean => value !== undefined && value !== null && value !== ''

/**
 * Collects the present metrics in canonical (`WORKOUT_METRIC_META`) order so
 * the sparse list reads consistently and unknown keys are dropped. Iterating
 * the metadata map — not the bag — is what guarantees identical labels/units to
 * the log form (DEC-075) and skips any non-canonical key the wire might carry.
 */
const collectPresentMetrics = (
  metrics: Record<string, unknown> | null | undefined,
): PresentMetric[] => {
  if (metrics === null || metrics === undefined) {
    return []
  }
  const present: PresentMetric[] = []
  for (const [key, { label, unit }] of Object.entries(WORKOUT_METRIC_META)) {
    const value = metrics[key]
    if (!isPresent(value)) {
      continue
    }
    const display = unit.length > 0 ? `${String(value)} ${unit}` : String(value)
    present.push({ key, label, display })
  }
  return present
}

/**
 * Renders the present optional metrics of a logged workout as a raw, sparse
 * `<dl>` — only metrics that were actually recorded appear, each labelled from
 * the shared metric-meta map (DEC-075 / spec § Unit 7). Renders nothing when no
 * canonical metric is present.
 */
export const WorkoutLogMetrics = ({ metrics }: WorkoutLogMetricsProps): ReactElement | null => {
  const present = collectPresentMetrics(metrics)
  if (present.length === 0) {
    return null
  }

  return (
    <dl
      data-testid="workout-history-metrics"
      className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3"
    >
      {present.map(({ key, label, display }) => (
        <div key={key} className="flex flex-col">
          <dt className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
            {label}
          </dt>
          <dd className="font-semibold text-foreground">{display}</dd>
        </div>
      ))}
    </dl>
  )
}
