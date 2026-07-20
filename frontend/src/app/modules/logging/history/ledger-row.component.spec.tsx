import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus, PreferredUnits } from '~/api/generated'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { LedgerRow } from './ledger-row.component'

const baseLog = (overrides: Partial<WorkoutLogDto> = {}): WorkoutLogDto => ({
  workoutLogId: 'log-1',
  occurredOn: '2026-06-06',
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
  isOnPlan: false,
  ...overrides,
})

describe('LedgerRow', () => {
  it("renders the day-numeral + weekday from the log's occurredOn", () => {
    render(<LedgerRow log={baseLog()} />)
    // 2026-06-06 is a Saturday.
    expect(screen.getByText('06')).toBeInTheDocument()
    expect(screen.getByText('Sat')).toBeInTheDocument()
  })

  it('renders the prescribed workout title + ON-PLAN suffix for an on-plan completed log', () => {
    render(<LedgerRow log={baseLog({ isOnPlan: true, prescribedWorkoutType: 'Tempo' })} />)
    expect(screen.getByText('Threshold run')).toBeInTheDocument()
    const status = screen.getByTestId('workout-history-entry-status')
    expect(status).toHaveTextContent('Completed · ON-PLAN')
    expect(status).toHaveClass('text-positive')
  })

  it('falls back to "Run" and omits ON-PLAN for an off-plan/legacy log', () => {
    render(<LedgerRow log={baseLog({ isOnPlan: false, prescribedWorkoutType: null })} />)
    expect(screen.getByText('Run')).toBeInTheDocument()
    expect(screen.getByTestId('workout-history-entry-status')).toHaveTextContent('Completed')
    expect(screen.getByTestId('workout-history-entry-status')).not.toHaveTextContent('ON-PLAN')
  })

  it('falls back to "Run" for an on-plan log carrying an unrecognised wire workout type', () => {
    // A future backend enum value the frontend doesn't know about yet must
    // degrade gracefully to the generic title, never render "undefined".
    render(<LedgerRow log={baseLog({ isOnPlan: true, prescribedWorkoutType: 'SomeFutureEnum' })} />)
    expect(screen.getByText('Run')).toBeInTheDocument()
    expect(screen.queryByText('undefined')).toBeNull()
  })

  it('renders distance, duration, and a space-separated pace unit in km', () => {
    render(<LedgerRow log={baseLog()} />)
    expect(screen.getByText('5.0 km')).toBeInTheDocument()
    expect(screen.getByText('30:00')).toBeInTheDocument()
    expect(screen.getByText('06:00 /km')).toBeInTheDocument()
  })

  it('renders distance, duration, and pace in miles when units=Miles', () => {
    render(<LedgerRow log={baseLog()} units={PreferredUnits.Miles} />)
    // 5000 m / 1609.344 = 3.107... -> 3.1 mi
    expect(screen.getByText('3.1 mi')).toBeInTheDocument()
    // 360 s/km * 1.609344 = 579.36 -> 579 -> 09:39/mi -> "09:39 /mi" (spaced).
    expect(screen.getByText('09:39 /mi')).toBeInTheDocument()
  })

  it('collapses to a single "—" placeholder and dims the row for a skipped (zero-actuals) log', () => {
    render(
      <LedgerRow
        log={baseLog({
          completionStatus: CompletionStatus.Skipped,
          distanceMeters: 0,
          durationSeconds: 0,
        })}
      />,
    )

    const status = screen.getByTestId('workout-history-entry-status')
    expect(status).toHaveTextContent('Skipped')
    expect(status).toHaveClass('text-danger-text')
    expect(screen.queryByText('5.0 km')).toBeNull()
    expect(screen.queryByText(/\/km/)).toBeNull()
    expect(screen.getAllByText('—')).toHaveLength(1)

    const article = screen.getByTestId('workout-history-entry')
    expect(article).toHaveClass('opacity-75')
    expect(article).toHaveAttribute('data-completion-status', String(CompletionStatus.Skipped))

    // The day numeral is the row's primary date identifier (essential text)
    // and must never carry the AA-failing `--alp-faint` token, even in the
    // dimmed/skipped state — `text-muted-foreground` is the AA-safe dimming
    // token; `opacity-75` on the row is the primary visual dimming mechanism.
    expect(screen.getByTestId('workout-history-entry-day')).toHaveClass('text-muted-foreground')
    expect(screen.getByTestId('workout-history-entry-day')).not.toHaveClass(
      'text-[var(--alp-faint)]',
    )
  })

  it('renders a Partial-status log normally (stats do NOT collapse to "—")', () => {
    render(
      <LedgerRow
        log={baseLog({
          completionStatus: CompletionStatus.Partial,
          isOnPlan: true,
          distanceMeters: 5000,
          durationSeconds: 1800,
        })}
      />,
    )

    const status = screen.getByTestId('workout-history-entry-status')
    expect(status).toHaveTextContent('Partial')
    expect(status).toHaveTextContent('· ON-PLAN')
    expect(status).toHaveClass('text-warning-text')

    // A non-Complete, non-Skipped status must still render its real stats —
    // guards against the skipped-collapse logic ever widening to "any
    // non-Complete status".
    expect(screen.getByText('5.0 km')).toBeInTheDocument()
    expect(screen.getByText('30:00')).toBeInTheDocument()
    expect(screen.getByText('06:00 /km')).toBeInTheDocument()
    expect(screen.queryByText('—')).toBeNull()
  })

  it('renders the freeform note when present', () => {
    render(<LedgerRow log={baseLog({ notes: 'Legs felt heavy but pushed through.' })} />)
    expect(screen.getByTestId('workout-history-entry-notes')).toHaveTextContent(
      'Legs felt heavy but pushed through.',
    )
  })

  it('omits the note element when there is no note', () => {
    render(<LedgerRow log={baseLog()} />)
    expect(screen.queryByTestId('workout-history-entry-notes')).toBeNull()
  })

  it('keeps the optional-metrics `<dl>` mounted below the grid', () => {
    render(<LedgerRow log={baseLog({ metrics: { hrAvg: 140 } })} />)
    expect(screen.getByText('Avg HR')).toBeInTheDocument()
    expect(screen.getByText('140 bpm')).toBeInTheDocument()
  })

  it('keeps the splits collapsible mounted below the grid', () => {
    const log = baseLog({
      splits: [
        {
          index: 0,
          distanceMeters: 1000,
          durationSeconds: 300,
          paceSecPerKm: 300,
          averageHeartRate: null,
        },
        {
          index: 1,
          distanceMeters: 1000,
          durationSeconds: 300,
          paceSecPerKm: 300,
          averageHeartRate: null,
        },
      ],
    })
    render(<LedgerRow log={log} />)
    expect(screen.getByRole('button', { name: /2 splits/i })).toBeInTheDocument()
  })

  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('renders identically in both themes with no raw colour literals', () => {
    const result = renderInBothThemes(<LedgerRow log={baseLog()} />)
    expectDualThemeParity(result, 'workout-history-entry')
  })
})
