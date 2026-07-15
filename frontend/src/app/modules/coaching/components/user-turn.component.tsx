import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'

export interface UserTurnProps {
  content: string
  time: string
  className?: string
}

/**
 * A runner's message in the transcript (D2) — a right-aligned bubble with a
 * mono `YOU · HH:MM` meta line beneath it. Renders at any content length
 * with no clamp; `whitespace-pre-wrap break-words` preserves the runner's
 * own line breaks without ever overflowing the bubble.
 */
export const UserTurn = ({ content, time, className }: UserTurnProps): ReactElement => (
  <div data-testid="user-turn" className={cn('flex flex-col items-end gap-[6px]', className)}>
    <div className="max-w-[85%] rounded-[10px_10px_4px_10px] bg-muted px-[14px] py-[10px]">
      <p className="font-body text-[14px] leading-[1.5] whitespace-pre-wrap break-words text-foreground">
        {content}
      </p>
    </div>
    <span
      data-testid="turn-meta"
      className="font-mono text-[9px] font-medium tracking-[0.08em] text-[var(--alp-faint)]"
    >
      YOU · {time}
    </span>
  </div>
)
