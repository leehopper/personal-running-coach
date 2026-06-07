import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import type { WorkoutLogSplitDto } from '~/api/generated'
import { WorkoutLogSplits } from './workout-log-splits.component'

const split = (index: number, overrides: Partial<WorkoutLogSplitDto> = {}): WorkoutLogSplitDto => ({
  index,
  distanceMeters: 1000,
  durationSeconds: 300,
  paceSecPerKm: 300,
  averageHeartRate: null,
  ...overrides,
})

const threeSplits = [split(0), split(1), split(2)]

describe('WorkoutLogSplits', () => {
  it('shows a one-line splits summary and hides the table until expanded', () => {
    render(<WorkoutLogSplits splits={threeSplits} />)

    expect(screen.getByRole('button', { name: /3 splits/i })).toHaveAttribute(
      'aria-expanded',
      'false',
    )
    // Lazy: the table is not in the DOM until the collapsible is opened.
    expect(screen.queryByRole('table')).toBeNull()
  })

  it('reveals a table with one row per split when expanded', async () => {
    const user = userEvent.setup()
    render(<WorkoutLogSplits splits={threeSplits} />)

    await user.click(screen.getByRole('button', { name: /3 splits/i }))

    expect(screen.getByRole('table')).toBeInTheDocument()
    // 1 header row + 3 data rows.
    expect(screen.getAllByRole('row')).toHaveLength(4)
  })

  it('singularises the summary for a single split', () => {
    render(<WorkoutLogSplits splits={[split(0)]} />)
    expect(screen.getByRole('button', { name: /^1 split$/i })).toBeInTheDocument()
  })

  it('renders an HR column only when at least one split has a heart rate', async () => {
    const user = userEvent.setup()
    render(<WorkoutLogSplits splits={[split(0, { averageHeartRate: 150 }), split(1)]} />)

    await user.click(screen.getByRole('button', { name: /2 splits/i }))
    expect(screen.getByRole('columnheader', { name: /hr/i })).toBeInTheDocument()
    expect(screen.getByText('150')).toBeInTheDocument()
  })

  it('renders nothing when there are no splits', () => {
    const { container } = render(<WorkoutLogSplits splits={[]} />)
    expect(container).toBeEmptyDOMElement()
  })
})
