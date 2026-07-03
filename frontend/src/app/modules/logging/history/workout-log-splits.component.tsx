import { useState } from 'react'
import type { ReactElement } from 'react'
import { ChevronDownIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { PreferredUnits, type WorkoutLogSplitDto } from '~/api/generated'
import { formatPaceSecPerKm } from '~/modules/common/utils/unit-format.helpers'
import { formatHistoryDistanceKm, formatDuration } from './history-format.helpers'

export interface WorkoutLogSplitsProps {
  /** The per-lap splits of a logged workout (display-only at MVP-0). */
  splits: readonly WorkoutLogSplitDto[]
  /**
   * Display unit for the split distance + pace. Defaults to Kilometers so
   * callers that predate the unit preference (and isolated tests) render the
   * km form unchanged.
   */
  units?: PreferredUnits
}

const headerCellClass =
  'px-3 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground'
const dataCellClass = 'px-3 py-2 text-foreground'

/**
 * Display-only splits for a logged workout: a one-line `"N splits"` summary that
 * expands a lazy nested `<table>` (DEC-075 / spec § Unit 7). Radix unmounts the
 * collapsible content while closed, so the table is only rendered once opened.
 * The HR column appears only when at least one split carries a heart rate, so a
 * GPS-only import doesn't show an empty column.
 */
export const WorkoutLogSplits = ({
  splits,
  units = PreferredUnits.Kilometers,
}: WorkoutLogSplitsProps): ReactElement | null => {
  const [open, setOpen] = useState(false)

  if (splits.length === 0) {
    return null
  }

  const summary = `${splits.length} split${splits.length === 1 ? '' : 's'}`
  const pacePlaceholder = units === PreferredUnits.Miles ? '—/mi' : '—/km'
  const showHeartRate = splits.some(
    (split) => split.averageHeartRate !== null && split.averageHeartRate !== undefined,
  )

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="flex flex-col gap-2">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-full justify-between px-3 text-muted-foreground"
          data-testid="workout-history-splits-trigger"
        >
          {summary}
          <ChevronDownIcon
            aria-hidden="true"
            className={`size-4 transition-transform duration-200 ease-out motion-reduce:transition-none ${
              open ? 'rotate-180' : ''
            }`}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="overflow-x-auto data-[state=open]:animate-in data-[state=closed]:animate-out motion-reduce:animate-none">
        <table
          className="w-full border-collapse text-sm"
          data-testid="workout-history-splits-table"
        >
          <thead>
            <tr className="border-b border-border">
              <th scope="col" className={headerCellClass}>
                #
              </th>
              <th scope="col" className={headerCellClass}>
                Distance
              </th>
              <th scope="col" className={headerCellClass}>
                Time
              </th>
              <th scope="col" className={headerCellClass}>
                Pace
              </th>
              {showHeartRate ? (
                <th scope="col" className={headerCellClass}>
                  HR
                </th>
              ) : null}
            </tr>
          </thead>
          <tbody>
            {splits.map((split) => (
              <tr key={split.index} className="border-b border-border last:border-0">
                <td className={dataCellClass}>{split.index + 1}</td>
                <td className={dataCellClass}>
                  {formatHistoryDistanceKm(split.distanceMeters, units) ?? '—'}
                </td>
                <td className={dataCellClass}>{formatDuration(split.durationSeconds) ?? '—'}</td>
                <td className={dataCellClass}>
                  {formatPaceSecPerKm(split.paceSecPerKm, units) ?? pacePlaceholder}
                </td>
                {showHeartRate ? (
                  <td className={dataCellClass}>{split.averageHeartRate ?? '—'}</td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </CollapsibleContent>
    </Collapsible>
  )
}
