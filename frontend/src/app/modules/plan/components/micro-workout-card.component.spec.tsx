import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MicroWorkoutCard } from './micro-workout-card.component'
import { fixtureWeekOneWorkouts } from './plan-display.fixture'

describe('MicroWorkoutCard', () => {
  it('renders the workout title, type label, and distance', () => {
    const workout = fixtureWeekOneWorkouts()[0] // Easy Monday
    render(<MicroWorkoutCard workout={workout} />)
    expect(screen.getByRole('heading', { name: 'Easy aerobic shakeout' })).toBeInTheDocument()
    const typeLabel = screen.getByTestId('micro-workout-type-label')
    expect(typeLabel.textContent).toBe('Easy run')
    expect(screen.getByText(/6\.0 km/u)).toBeInTheDocument()
  })

  it('formats the target pace range as MM:SS/km', () => {
    const workout = fixtureWeekOneWorkouts()[0]
    render(<MicroWorkoutCard workout={workout} />)
    const pace = screen.getByTestId('micro-workout-pace')
    // fast 330s = 05:30, slow 360s = 06:00 → "05:30-06:00/km"
    expect(pace.textContent).toBe('05:30-06:00/km')
  })

  it('renders structured segments with intensity labels and trademark-clean phrasing', () => {
    const workout = fixtureWeekOneWorkouts()[1] // Threshold intervals
    render(<MicroWorkoutCard workout={workout} />)
    const segmentsList = screen.getByTestId('micro-workout-segments')
    const segments = within(segmentsList).getAllByTestId('micro-workout-segment')
    expect(segments).toHaveLength(3)
    expect(segments[1].dataset.segmentType).toBe('Work')
    expect(segments[1].textContent).toMatch(/× 5/u)
    expect(segments[1].textContent).toMatch(/Threshold \(pace-zone index\)/u)
  })

  it('omits the segments list when no segments are present', () => {
    const workout = fixtureWeekOneWorkouts()[0] // Easy run, zero segments
    render(<MicroWorkoutCard workout={workout} />)
    expect(screen.queryByTestId('micro-workout-segments')).not.toBeInTheDocument()
  })

  it('renders coaching notes when provided', () => {
    const workout = fixtureWeekOneWorkouts()[1]
    render(<MicroWorkoutCard workout={workout} />)
    const notes = screen.getByTestId('micro-workout-coaching-notes')
    expect(notes.textContent).toMatch(/threshold session/iu)
  })

  it('applies emphasized styling when emphasized=true', () => {
    const workout = fixtureWeekOneWorkouts()[1]
    render(<MicroWorkoutCard workout={workout} emphasized={true} />)
    const card = screen.getByTestId('micro-workout-card')
    expect(card.dataset.emphasized).toBe('true')
    expect(card.className).toMatch(/ring-2/)
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const workout = fixtureWeekOneWorkouts()[1]
    const { container } = render(<MicroWorkoutCard workout={workout} />)
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
