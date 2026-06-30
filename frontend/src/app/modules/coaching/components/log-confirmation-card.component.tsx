import type { ReactElement } from 'react'

import { Button } from '@/components/ui/button'
import type { CompletionStatus, RunnerDistanceUnit } from '~/api/generated'
import type { CoachCard } from '~/modules/coaching/hooks/use-coach-stream.hooks'

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
}

const Field = ({ label, value }: FieldProps): ReactElement => (
  <div className="flex flex-col">
    <dt className="text-xs text-muted-foreground">{label}</dt>
    <dd className="text-foreground">{value}</dd>
  </div>
)

export interface LogConfirmationCardProps {
  card: CoachCard
  onConfirm: () => void
  onEdit: () => void
  onCancel: () => void
  isConfirming: boolean
}

export const LogConfirmationCard = ({
  card,
  onConfirm,
  onEdit,
  onCancel,
  isConfirming,
}: LogConfirmationCardProps): ReactElement => {
  const { draft, prescription } = card
  const hasNote = draft.notes !== null && draft.notes.length > 0

  return (
    <section
      data-testid="log-confirmation-card"
      aria-label="Confirm workout log"
      className="flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 text-sm text-card-foreground"
    >
      <h3 className="font-semibold text-foreground">Log this run?</h3>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-2">
        <Field
          label="Distance"
          value={`${draft.distanceValue} ${DISTANCE_UNIT_LABEL[draft.distanceUnit]}`}
        />
        <Field
          label="Time"
          value={formatDuration(draft.durationHours, draft.durationMinutes, draft.durationSeconds)}
        />
        <Field label="Date" value={draft.occurredOn} />
        <Field label="Status" value={COMPLETION_LABEL[draft.completionStatus]} />
      </dl>
      {hasNote && <p className="text-muted-foreground">{draft.notes}</p>}
      {prescription !== null && (
        <p className="text-muted-foreground">On-plan: {prescription.workoutType} run</p>
      )}
      <div className="flex flex-wrap gap-2">
        <Button type="button" onClick={onConfirm} disabled={isConfirming}>
          Confirm
        </Button>
        <Button type="button" variant="secondary" onClick={onEdit} disabled={isConfirming}>
          Edit
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isConfirming}>
          Cancel
        </Button>
      </div>
    </section>
  )
}
