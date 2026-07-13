import type { ReactElement, ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Props for {@link StatCell}. */
export interface StatCellProps {
  /** Condensed numeral, e.g. `"9.2 KM"` or a nowrap pace range like `"4:00–4:30/km"`. */
  value: ReactNode
  /** Mono label under the value, e.g. `"Distance"`. */
  label: string
  className?: string
  /**
   * `'default'` (today's `.t-numeral`/`.t-data-label` styling, unchanged) —
   * `'hero'` — the workout hero's stat band typography: condensed 700/30px
   * value, mono 500/9.5px/+0.1em/uppercase label in `--alp-faint` (Slice 2
   * §2.8). Mirrors {@link StatBand}'s own `variant` prop so the two stay
   * consistent — same default-preserving pattern, same naming.
   */
  variant?: 'default' | 'hero'
}

const STAT_CELL_VALUE_CLASSES: Record<NonNullable<StatCellProps['variant']>, string> = {
  default: 't-numeral text-foreground',
  hero: 'font-condensed text-[30px] font-bold leading-none whitespace-nowrap text-foreground',
}

const STAT_CELL_LABEL_CLASSES: Record<NonNullable<StatCellProps['variant']>, string> = {
  default: 't-data-label text-muted-foreground',
  // `--alp-faint` has no semantic slot by design (decorative-only, fails AA
  // — CLAUDE.md) — consumed via a Tailwind arbitrary value referencing the
  // primitive directly, one of this slice's three named --alp-faint sites
  // (stat-band unit labels, § Non-negotiables).
  hero: 'font-mono text-[9.5px] font-medium uppercase tracking-[0.1em] text-[color:var(--alp-faint)]',
}

/**
 * One cell of a {@link StatBand}: a condensed numeric value over a mono
 * label. Renders no divider itself — `StatBand` supplies the hairline
 * separators between sibling cells.
 */
export const StatCell = ({
  value,
  label,
  className,
  variant = 'default',
}: StatCellProps): ReactElement => (
  <div
    data-testid="stat-cell"
    className={cn('flex flex-1 flex-col gap-1 px-3 first:pl-0 last:pr-0', className)}
  >
    <span className={STAT_CELL_VALUE_CLASSES[variant]}>{value}</span>
    <span className={STAT_CELL_LABEL_CLASSES[variant]}>{label}</span>
  </div>
)

/** Props for {@link StatBand}. */
export interface StatBandProps {
  /** {@link StatCell} elements to lay out in a hairline-divided row. */
  children: ReactNode
  className?: string
  /**
   * `'divided'` (default) — today's equal-width flex row with `divide-x`
   * hairline separators, unchanged. `'hero'` — the workout hero's asymmetric
   * `1fr/1.7fr/1fr` CSS grid with a full top+bottom hairline border,
   * introduced by Slice 2.
   */
  variant?: 'divided' | 'hero'
}

/**
 * Horizontal row of {@link StatCell}s separated by 1px hairline dividers.
 * Used for the hero stat band (distance / pace / reps or duration).
 *
 * `divide-x divide-border` is reused unchanged for both variants — Tailwind's
 * `divide-x` applies a `border-left` to every child but the first via a
 * `:not(:first-child)` sibling selector, which produces the same "each cell
 * `border-left` except the first" contract regardless of whether the parent
 * is `flex` or `grid`; only the track-sizing mechanism (`grid-template-
 * columns` vs. equal-`flex-1`) and the `'hero'` variant's added `border-y`
 * (top+bottom hairline, absent from `'divided'`) differ between the two.
 */
export const StatBand = ({
  children,
  className,
  variant = 'divided',
}: StatBandProps): ReactElement => (
  <div
    data-testid="stat-band"
    className={cn(
      variant === 'divided'
        ? 'flex w-full divide-x divide-border'
        : 'grid w-full grid-cols-[1fr_1.7fr_1fr] divide-x divide-border border-y border-border',
      className,
    )}
  >
    {children}
  </div>
)
