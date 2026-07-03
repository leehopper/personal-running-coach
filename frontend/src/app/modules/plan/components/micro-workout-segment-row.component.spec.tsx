import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import type { WorkoutSegmentDto } from '~/modules/plan/models/plan.model'
import { MicroWorkoutSegmentRow } from './micro-workout-segment-row.component'

const segment = (overrides: Partial<WorkoutSegmentDto> = {}): WorkoutSegmentDto => ({
  segmentType: 'Work',
  durationMinutes: 4,
  targetPaceSecPerKm: 240,
  intensity: 'Threshold',
  repetitions: 5,
  notes: '',
  ...overrides,
})

// The row renders an <li>, which must live inside a list to stay valid DOM
// (and to keep React from warning), so every render wraps it in a <ul>.
const renderRow = (seg: WorkoutSegmentDto, units?: PreferredUnits) =>
  render(
    <ul>
      <MicroWorkoutSegmentRow segment={seg} index={0} units={units} />
    </ul>,
  )

describe('MicroWorkoutSegmentRow', () => {
  it('defaults to kilometres, rendering the pace as MM:SS/km', () => {
    renderRow(segment())
    const row = screen.getByTestId('micro-workout-segment')
    // 240 sec/km -> 04:00/km
    expect(row.textContent).toMatch(/04:00\/km/u)
    expect(row.textContent).toMatch(/4 min/u)
  })

  it('renders the pace in miles when units=Miles', () => {
    renderRow(segment(), PreferredUnits.Miles)
    const row = screen.getByTestId('micro-workout-segment')
    // 240 sec/km * 1.609344 = 386.24 -> 386 -> 06:26/mi
    expect(row.textContent).toMatch(/06:26\/mi/u)
    expect(row.textContent).not.toMatch(/\/km/u)
  })

  it('omits the pace fragment entirely when the pace is out of range', () => {
    renderRow(segment({ repetitions: 1, targetPaceSecPerKm: 6000 }), PreferredUnits.Miles)
    const row = screen.getByTestId('micro-workout-segment')
    // 6000 sec/km is above the ceiling in both units -> null -> just the duration.
    expect(row.textContent).toMatch(/4 min/u)
    expect(row.textContent).not.toMatch(/·/u)
    expect(row.textContent).not.toMatch(/\/mi|\/km/u)
  })
})
