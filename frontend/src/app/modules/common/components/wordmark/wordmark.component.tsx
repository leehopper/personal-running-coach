import { cn } from '@/lib/utils'

export type WordmarkSize = 'header' | 'poster'

export interface WordmarkProps {
  /** 'header' (~21px, nav/page headers) or 'poster' (~58px, auth screen). */
  size?: WordmarkSize
  className?: string
}

// Font-size per size prop. Picked the midpoint of the header range (20-22px)
// the same way index.css's `.t-*` role classes pick range midpoints.
const SIZE_CLASSES: Record<WordmarkSize, string> = {
  header: 'text-[21px]',
  poster: 'text-[58px]',
}

/**
 * Brand mark: "SPLIT" immediately followed by a clay "/" with no visual gap
 * between them. Letter-spacing is applied only to the "SPLIT" text run, and
 * the slash is pulled back by the same amount, because CSS letter-spacing
 * inserts space after every character (including the last one) whenever
 * another inline element follows it on the same line - left alone, that
 * would open a gap between the "T" and the slash.
 *
 * The two-part text is presentational: the wrapper carries a single
 * `aria-label` and both inner spans are `aria-hidden` so assistive tech
 * announces "Split" once instead of reading the glyphs individually.
 */
export const Wordmark = ({ size = 'header', className }: WordmarkProps) => {
  return (
    <span
      data-slot="wordmark"
      data-size={size}
      aria-label="Split"
      role="img"
      className={cn(
        'inline-flex items-baseline font-condensed leading-none font-extrabold',
        SIZE_CLASSES[size],
        className,
      )}
    >
      <span aria-hidden="true" className="tracking-[0.035em] text-foreground">
        SPLIT
      </span>
      <span aria-hidden="true" className="-ml-[0.035em] text-clay-text">
        /
      </span>
    </span>
  )
}
