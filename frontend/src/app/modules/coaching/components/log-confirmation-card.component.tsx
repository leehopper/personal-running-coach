import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import type { CompletionStatus, PreferredUnits, RunnerDistanceUnit } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import type { CoachCard } from '~/modules/coaching/hooks/use-coach-stream.hooks'
import { formatReceiptDate } from './transcript-time.helpers'

// The confirmation card renders a parsed workout-log draft for an explicit
// Confirm / Edit / Cancel. The plan-mutating commit fires only on Confirm — the
// LLM's parse is advisory until the runner approves it.

const DISTANCE_UNIT_LABEL: Record<RunnerDistanceUnit, string> = { 0: 'km', 1: 'mi', 2: 'm' }
const COMPLETION_LABEL: Record<CompletionStatus, string> = {
  0: 'Completed',
  1: 'Partial',
  2: 'Skipped',
}

const pad = (value: number): string => value.toString().padStart(2, '0')

const formatDuration = (hours: number, minutes: number, seconds: number): string =>
  hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${minutes}:${pad(seconds)}`

interface FieldProps {
  label: string
  value: string
  valueClassName?: string
}

const Field = ({ label, value, valueClassName }: FieldProps): ReactElement => (
  <div className="flex flex-col gap-0.5">
    <dt className="font-mono text-[9.5px] font-medium tracking-[0.1em] text-muted-foreground">
      {label}
    </dt>
    <dd className={cn('font-condensed text-[22px] font-bold text-foreground', valueClassName)}>
      {value}
    </dd>
  </div>
)

export interface LogConfirmationCardProps {
  card: CoachCard
  units: PreferredUnits
  onConfirm: () => void
  onEdit: () => void
  onCancel: () => void
  isConfirming: boolean
}

export const LogConfirmationCard = ({
  card,
  units,
  onConfirm,
  onEdit,
  onCancel,
  isConfirming,
}: LogConfirmationCardProps): ReactElement => {
  const { draft, prescription } = card
  const hasNote = draft.notes !== null && draft.notes.length > 0
  const dateLabel = formatReceiptDate(draft.occurredOn) ?? draft.occurredOn
  const targetLabel =
    prescription === null
      ? null
      : (formatDistanceKm(prescription.distanceMeters / 1000, units) ?? '—')

  return (
    <section
      data-testid="log-confirmation-card"
      aria-label="Confirm workout log"
      className={cn(
        'flex flex-col gap-3 rounded-lg border border-border bg-card p-[14px] text-sm text-card-foreground',
        isConfirming && 'opacity-75',
      )}
    >
      <h3 className="font-condensed text-[13px] font-semibold tracking-[0.16em] text-foreground uppercase">
        LOG THIS RUN?
      </h3>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-[10px]">
        <Field
          label="DISTANCE"
          value={`${draft.distanceValue} ${DISTANCE_UNIT_LABEL[draft.distanceUnit]}`}
        />
        <Field
          label="TIME"
          value={formatDuration(draft.durationHours, draft.durationMinutes, draft.durationSeconds)}
        />
        <Field label="DATE" value={dateLabel} />
        <Field
          label="STATUS"
          value={COMPLETION_LABEL[draft.completionStatus]}
          valueClassName="text-positive"
        />
      </dl>
      {hasNote && <p className="font-body text-[13px] text-muted-foreground">{draft.notes}</p>}
      {prescription !== null && (
        <p className="font-mono text-[10px] tracking-[0.08em] text-muted-foreground">
          ON-PLAN — {prescription.workoutType.toUpperCase()} · TARGET {targetLabel}
        </p>
      )}
      <div className="flex items-center gap-[10px]">
        <Button type="button" data-testid="log-confirm" onClick={onConfirm} disabled={isConfirming}>
          {isConfirming ? 'Saving…' : 'Confirm'}
        </Button>
        <Button
          type="button"
          variant="outline"
          data-testid="log-edit"
          onClick={onEdit}
          disabled={isConfirming}
        >
          Edit
        </Button>
        {!isConfirming && (
          <Button type="button" variant="ghost" data-testid="log-cancel" onClick={onCancel}>
            Cancel
          </Button>
        )}
      </div>
    </section>
  )
}
