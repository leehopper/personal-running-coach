import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'

export interface DateDividerProps {
  label: string
  className?: string
}

/**
 * A transcript date divider (D7) — a mono faint label flanked by two
 * hairlines, emitted before each new local-calendar-day group of persisted
 * timeline turns (see `groupTurnsByLocalDay` in `transcript-time.
 * helpers.ts`, which supplies `label`).
 */
export const DateDivider = ({ label, className }: DateDividerProps): ReactElement => (
  <div data-testid="date-divider" className={cn('flex items-center gap-[10px]', className)}>
    <span className="h-px flex-1 bg-border" />
    <span className="font-mono text-[9.5px] font-medium tracking-[0.1em] text-[var(--alp-faint)]">
      {label}
    </span>
    <span className="h-px flex-1 bg-border" />
  </div>
)
