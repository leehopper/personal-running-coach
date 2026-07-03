import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { buildDiff, buildDiffWorkout } from './conversation.fixture'
import { describeWeeklyTargetChange, describeWorkoutChange } from './before-after-diff.helpers'
import { BeforeAfterDiff } from './before-after-diff.component'

describe('BeforeAfterDiff', () => {
  it('renders collapsed by default with only the toggle visible', () => {
    render(<BeforeAfterDiff diff={buildDiff()} />)

    expect(screen.getByTestId('diff-toggle')).toHaveTextContent('Show what changed')
    // Radix keeps the closed content mounted but `hidden` — assert
    // visibility, not presence.
    expect(screen.getByTestId('before-after-diff')).not.toBeVisible()
  })

  it('expands to the structured before/after lines and collapses again', async () => {
    const user = userEvent.setup()
    render(<BeforeAfterDiff diff={buildDiff()} />)

    await user.click(screen.getByTestId('diff-toggle'))
    expect(screen.getByTestId('before-after-diff')).toBeVisible()
    expect(screen.getAllByTestId('diff-workout-change')).toHaveLength(1)
    expect(screen.getAllByTestId('diff-weekly-target-change')).toHaveLength(2)
    expect(screen.getByText('Week 2 volume')).toBeInTheDocument()
    expect(screen.getByText('38.0 km → 36.0 km')).toBeInTheDocument()

    await user.click(screen.getByTestId('diff-toggle'))
    expect(screen.getByTestId('before-after-diff')).not.toBeVisible()
  })

  it('renders the before/after distances in miles when units=Miles', async () => {
    const user = userEvent.setup()
    render(<BeforeAfterDiff diff={buildDiff()} units={PreferredUnits.Miles} />)

    await user.click(screen.getByTestId('diff-toggle'))
    // 38 km / 1.609344 = 23.61... -> 23.6 mi ; 36 km -> 22.4 mi
    expect(screen.getByText('23.6 mi → 22.4 mi')).toBeInTheDocument()
    // 10 km (before) -> 6.2 mi ; 8 km (after) -> 5.0 mi
    expect(
      screen.getByText('Threshold Intervals (6.2 mi) → Easy Aerobic Run (5.0 mi)'),
    ).toBeInTheDocument()
  })

  it('renders an added workout (null before) as an Added line', async () => {
    const user = userEvent.setup()
    const diff = buildDiff({
      workoutChanges: [
        { weekNumber: 1, dayOfWeek: 5, before: null, after: buildDiffWorkout({ dayOfWeek: 5 }) },
      ],
      weeklyTargetChanges: [],
    })
    render(<BeforeAfterDiff diff={diff} />)

    await user.click(screen.getByTestId('diff-toggle'))
    expect(screen.getByText('Week 1 · Friday')).toBeInTheDocument()
    expect(screen.getByText('Added Easy Aerobic Run (8.0 km)')).toBeInTheDocument()
  })

  it('renders a removed workout (null after) as a Removed line', async () => {
    const user = userEvent.setup()
    const diff = buildDiff({
      workoutChanges: [
        {
          weekNumber: 1,
          dayOfWeek: 3,
          before: buildDiffWorkout({ dayOfWeek: 3, title: 'Tempo Run', targetDistanceKm: 9 }),
          after: null,
        },
      ],
      weeklyTargetChanges: [],
    })
    render(<BeforeAfterDiff diff={diff} />)

    await user.click(screen.getByTestId('diff-toggle'))
    expect(screen.getByText('Removed Tempo Run (9.0 km)')).toBeInTheDocument()
  })

  it('renders nothing for an empty diff (no dangling toggle)', () => {
    const { container } = render(
      <BeforeAfterDiff diff={{ workoutChanges: [], weeklyTargetChanges: [] }} />,
    )

    expect(container.firstChild).toBeNull()
  })

  it('skips a meaningless both-null workout change', () => {
    expect(
      describeWorkoutChange({ weekNumber: 1, dayOfWeek: 1, before: null, after: null }),
    ).toBeNull()
  })

  it('renders an em-dash placeholder for a non-positive workout distance', () => {
    // A zero/non-positive `targetDistanceKm` yields `null` from the shared
    // formatter; the helper substitutes an em-dash rather than a misleading
    // `0.0 km`/`0.0 mi`.
    expect(
      describeWorkoutChange({
        weekNumber: 1,
        dayOfWeek: 1,
        before: null,
        after: buildDiffWorkout({ title: 'Cross-Train', targetDistanceKm: 0 }),
      }),
    ).toBe('Added Cross-Train (—)')
  })

  it('renders an em-dash placeholder for a non-positive weekly volume target', () => {
    expect(
      describeWeeklyTargetChange({
        weekNumber: 3,
        beforeWeeklyTargetKm: 0,
        afterWeeklyTargetKm: 28,
      }),
    ).toBe('— → 28.0 km')
  })
})
