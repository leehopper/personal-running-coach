import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus } from '~/api/generated'
import { WorkoutHistoryList } from './workout-history-list.component'

const log = (occurredOn: string): WorkoutLogDto => ({
  workoutLogId: occurredOn,
  occurredOn,
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
  isOnPlan: false,
})

describe('WorkoutHistoryList', () => {
  it('renders one week header per ISO week, newest week first', () => {
    render(<WorkoutHistoryList logs={[log('2026-06-08'), log('2026-06-07'), log('2026-05-31')]} />)

    const headers = screen.getAllByTestId('workout-history-week-header').map((h) => h.textContent)
    expect(headers).toEqual(['Week of Jun 8, 2026', 'Week of Jun 1, 2026', 'Week of May 25, 2026'])
    expect(screen.getAllByTestId('workout-history-entry')).toHaveLength(3)
  })

  it('groups logs of one ISO week split across two pages under a single header', () => {
    // Simulates a merged infinite-query result where the page boundary splits
    // one ISO week — the bucketing must not duplicate the header.
    const page1 = [log('2026-06-07'), log('2026-06-03')]
    const page2 = [log('2026-06-01'), log('2026-05-31')]
    render(<WorkoutHistoryList logs={[...page1, ...page2]} />)

    const headers = screen.getAllByTestId('workout-history-week-header').map((h) => h.textContent)
    expect(headers).toEqual(['Week of Jun 1, 2026', 'Week of May 25, 2026'])

    const firstWeek = screen.getByRole('region', { name: 'Week of Jun 1, 2026' })
    expect(within(firstWeek).getAllByTestId('workout-history-entry')).toHaveLength(3)
  })

  it('renders nothing visible for an empty log list', () => {
    render(<WorkoutHistoryList logs={[]} />)
    expect(screen.queryByTestId('workout-history-week-header')).toBeNull()
  })
})
