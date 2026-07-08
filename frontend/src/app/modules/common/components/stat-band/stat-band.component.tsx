import type { ReactElement, ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Props for {@link StatCell}. */
export interface StatCellProps {
  /** Condensed numeral, e.g. `"9.2 KM"` or a nowrap pace range like `"4:00–4:30/km"`. */
  value: ReactNode
  /** Mono label under the value, e.g. `"Distance"`. */
  label: string
  className?: string
}

/**
 * One cell of a {@link StatBand}: a condensed numeric value over a mono
 * label. Renders no divider itself — `StatBand` supplies the hairline
 * separators between sibling cells.
 */
export const StatCell = ({ value, label, className }: StatCellProps): ReactElement => (
  <div
    data-testid="stat-cell"
    className={cn('flex flex-1 flex-col gap-1 px-3 first:pl-0 last:pr-0', className)}
  >
    <span className="t-numeral text-foreground">{value}</span>
    <span className="t-data-label text-muted-foreground">{label}</span>
  </div>
)

/** Props for {@link StatBand}. */
export interface StatBandProps {
  /** {@link StatCell} elements to lay out in a hairline-divided row. */
  children: ReactNode
  className?: string
}

/**
 * Horizontal row of {@link StatCell}s separated by 1px hairline dividers.
 * Used for the hero stat band (distance / pace / reps or duration).
 */
export const StatBand = ({ children, className }: StatBandProps): ReactElement => (
  <div data-testid="stat-band" className={cn('flex w-full divide-x divide-border', className)}>
    {children}
  </div>
)
