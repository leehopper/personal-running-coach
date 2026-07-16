import type { ReactElement } from 'react'
import { CheckIcon } from 'lucide-react'
import { Link } from 'react-router-dom'

import { cn } from '@/lib/utils'
import type { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import type { LoggedRunSummaryDto } from '~/modules/coaching/models/conversation.model'
import { formatDurationSeconds, formatReceiptDate } from './transcript-time.helpers'

export interface LoggedRunReceiptProps {
  summary: LoggedRunSummaryDto
  units: PreferredUnits
  className?: string
}

/**
 * The durable one-line receipt for a confirmed conversational log (DEC-091,
 * D6) — rendered solely by `CoachTextTurn` beneath the confirm-ack coach
 * turn's body (sourced from `interactive.loggedRun`), which is what survives
 * reload since it reads off the persisted timeline turn rather than any
 * in-session card state. `LogConfirmationCard` never renders this component;
 * an optimistic in-session bridge that would show it in the card slot while
 * awaiting the post-confirm refetch is explicitly deferred (spec §9 #2).
 * Distance is unit-aware; the date fragment is omitted (not crashed into the
 * Unix epoch) when `occurredOn` is unparseable.
 */
export const LoggedRunReceipt = ({
  summary,
  units,
  className,
}: LoggedRunReceiptProps): ReactElement => {
  const date = formatReceiptDate(summary.occurredOn)
  const distance = formatDistanceKm(summary.distanceKm, units) ?? '—'
  const duration = formatDurationSeconds(summary.durationSeconds)

  return (
    <div
      data-testid="logged-run-receipt"
      className={cn(
        'flex items-center justify-between rounded-md border border-border bg-input-fill px-[14px] py-[11px]',
        className,
      )}
    >
      <span className="flex items-center gap-[10px]">
        <span className="flex size-[18px] items-center justify-center rounded-xs bg-positive">
          <CheckIcon aria-hidden className="size-3 text-background" strokeWidth={3.5} />
        </span>
        <span className="font-mono text-[12px] font-semibold tracking-[0.06em] text-muted-foreground">
          LOGGED — {distance} · {duration}
          {date !== null ? ` · ${date}` : ''}
        </span>
      </span>
      <Link
        to="/history"
        data-testid="logged-run-receipt-logbook"
        className="font-condensed text-[11px] font-semibold tracking-[0.1em] text-clay-text uppercase"
      >
        LOG BOOK →
      </Link>
    </div>
  )
}
