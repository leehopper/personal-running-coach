import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'
import type { PreferredUnits } from '~/api/generated'
import type { LoggedRunSummaryDto } from '~/modules/coaching/models/conversation.model'

export interface CoachTextTurnProps {
  content: string
  time: string
  /** Appends the clay block-cursor inline at the end of the body when a live stream is still in flight. */
  streaming?: boolean
  /** Wires into the durable `LoggedRunReceipt` (spec §3 PR-D); undefined when the receipt isn't rendered. */
  loggedRun?: LoggedRunSummaryDto | null
  /** Unit-aware distance for the receipt (spec §3 PR-D). Defaults to Kilometers. */
  units?: PreferredUnits
  className?: string
}

/**
 * A coach reply in the transcript (D3) — no bubble, just a mono
 * `COACH · HH:MM` clay-text label above plain bone body text. Never
 * markdown: `whitespace-pre-wrap` renders literal `**asterisks**` verbatim.
 * `streaming` appends an `aria-hidden` clay block-cursor inline at the end
 * of the body while a live reply is still arriving.
 *
 * `loggedRun`/`units` are declared here (not destructured) as forward
 * compatibility for the durable receipt (spec §3 PR-D), which renders
 * `<LoggedRunReceipt>` beneath the body when `loggedRun` is non-null.
 */
export const CoachTextTurn = ({
  content,
  time,
  streaming,
  className,
}: CoachTextTurnProps): ReactElement => (
  <div data-testid="coach-text-turn" className={cn('flex flex-col gap-1', className)}>
    <span className="font-mono text-[10px] font-semibold tracking-[0.1em] text-clay-text">
      COACH · {time}
    </span>
    <p className="font-body text-[14.5px] leading-[1.55] whitespace-pre-wrap text-foreground">
      {content}
      {streaming === true && (
        <span
          aria-hidden
          data-testid="coach-stream-cursor"
          className="ml-0.5 inline-block h-[15px] w-2 bg-primary [vertical-align:text-bottom]"
        />
      )}
    </p>
  </div>
)
