import type { ReactElement, ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Visual tone for {@link MonoLabel}. */
export type MonoLabelTone = 'muted' | 'clay' | 'positive'

const toneClassName: Record<MonoLabelTone, string> = {
  muted: 'text-muted-foreground',
  clay: 'text-clay-text',
  positive: 'text-positive',
}

/** Props for {@link MonoLabel}. */
export interface MonoLabelProps {
  children: ReactNode
  /** Foreground tone. Defaults to `muted`. */
  tone?: MonoLabelTone
  className?: string
}

/**
 * IBM Plex Mono eyebrow/label — uppercase, tracked, tabular-numeric via
 * `.t-data-label`. Used for row eyebrows, meta lines (e.g. "YOU · 14:32"),
 * and short data tags. Source copy stays sentence case; the caps transform
 * is presentation only.
 */
export const MonoLabel = ({
  children,
  tone = 'muted',
  className,
}: MonoLabelProps): ReactElement => (
  <span data-testid="mono-label" className={cn('t-data-label', toneClassName[tone], className)}>
    {children}
  </span>
)
