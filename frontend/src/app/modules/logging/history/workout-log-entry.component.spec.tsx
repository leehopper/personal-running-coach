import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus, PreferredUnits } from '~/api/generated'
import { WorkoutLogEntry } from './workout-log-entry.component'

const baseLog = (overrides: Partial<WorkoutLogDto> = {}): WorkoutLogDto => ({
  workoutLogId: 'log-1',
  occurredOn: '2026-06-06',
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
  isOnPlan: false,
  ...overrides,
})

describe('WorkoutLogEntry', () => {
  it('renders the date, status, and derived core stats for a completed run', () => {
    render(<WorkoutLogEntry log={baseLog()} />)

    expect(screen.getByText('Sat, Jun 6')).toBeInTheDocument()
    expect(screen.getByText('Completed')).toBeInTheDocument()
    expect(screen.getByText('5.0 km')).toBeInTheDocument()
    expect(screen.getByText('30:00')).toBeInTheDocument()
    expect(screen.getByText('06:00/km')).toBeInTheDocument()
  })

  it('renders distance and pace in miles when units=Miles', () => {
    render(<WorkoutLogEntry log={baseLog()} units={PreferredUnits.Miles} />)

    // 5000 m / 1609.344 = 3.107... -> 3.1 mi
    expect(screen.getByText('3.1 mi')).toBeInTheDocument()
    // 360 s/km * 1.609344 = 579.36 -> 579 -> 09:39/mi.
    expect(screen.getByText('09:39/mi')).toBeInTheDocument()
  })

  it('renders the freeform note when present', () => {
    render(<WorkoutLogEntry log={baseLog({ notes: 'Legs felt heavy but pushed through.' })} />)
    expect(screen.getByTestId('workout-history-entry-notes')).toHaveTextContent(
      'Legs felt heavy but pushed through.',
    )
  })

  it('omits the note element when there is no note', () => {
    render(<WorkoutLogEntry log={baseLog()} />)
    expect(screen.queryByTestId('workout-history-entry-notes')).toBeNull()
  })

  it('renders present optional metrics via the sparse metric list', () => {
    render(<WorkoutLogEntry log={baseLog({ metrics: { hrAvg: 140 } })} />)
    expect(screen.getByText('Avg HR')).toBeInTheDocument()
    expect(screen.getByText('140 bpm')).toBeInTheDocument()
  })

  it('renders a splits summary when the log has splits', () => {
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
    render(<WorkoutLogEntry log={log} />)
    expect(screen.getByRole('button', { name: /2 splits/i })).toBeInTheDocument()
  })

  it('shows the skipped status and no pace for a skipped (zero-actuals) run', () => {
    render(
      <WorkoutLogEntry
        log={baseLog({
          distanceMeters: 0,
          durationSeconds: 0,
          completionStatus: CompletionStatus.Skipped,
        })}
      />,
    )
    expect(screen.getByText('Skipped')).toBeInTheDocument()
    expect(screen.queryByText('5.0 km')).toBeNull()
    expect(screen.queryByText(/\/km$/)).toBeNull()
  })
})
