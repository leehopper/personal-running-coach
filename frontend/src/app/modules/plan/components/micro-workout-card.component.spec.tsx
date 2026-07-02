import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
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

  it('renders distance and pace range in miles when units=Miles', () => {
    const workout = fixtureWeekOneWorkouts()[0] // 6 km, fast 330, slow 360 sec/km
    render(<MicroWorkoutCard workout={workout} units={PreferredUnits.Miles} />)
    // 6 km / 1.609344 = 3.728 -> 3.7 mi
    expect(screen.getByTestId('micro-workout-distance').textContent).toBe('3.7 mi')
    // fast 330*1.609344=531.08->531->08:51 ; slow 360*1.609344=579.36->579->09:39
    expect(screen.getByTestId('micro-workout-pace').textContent).toBe('08:51-09:39/mi')
  })

  it('threads the unit preference down to the segment paces', () => {
    const workout = fixtureWeekOneWorkouts()[1] // Threshold intervals with segments
    render(<MicroWorkoutCard workout={workout} units={PreferredUnits.Miles} />)
    const segments = screen.getAllByTestId('micro-workout-segment')
    // Work segment pace 240 sec/km -> 386 sec/mi -> 06:26/mi
    expect(segments[1].textContent).toMatch(/06:26\/mi/u)
  })

  it('renders unit-aware placeholders in miles when distance and pace are unavailable', () => {
    // A skipped/zero-distance workout with unusable paces exercises the null
    // fallbacks: distance -> "—" and the miles pace placeholder -> "—/mi".
    const workout = {
      ...fixtureWeekOneWorkouts()[0],
      targetDistanceKm: 0,
      targetPaceFastSecPerKm: Number.NaN,
      targetPaceEasySecPerKm: Number.NaN,
    }
    render(<MicroWorkoutCard workout={workout} units={PreferredUnits.Miles} />)
    expect(screen.getByTestId('micro-workout-distance').textContent).toBe('—')
    expect(screen.getByTestId('micro-workout-pace').textContent).toBe('—/mi')
  })

  it('falls back to the km pace placeholder when the pace is unavailable in kilometres', () => {
    const workout = {
      ...fixtureWeekOneWorkouts()[0],
      targetPaceFastSecPerKm: Number.NaN,
      targetPaceEasySecPerKm: Number.NaN,
    }
    render(<MicroWorkoutCard workout={workout} />)
    expect(screen.getByTestId('micro-workout-pace').textContent).toBe('—/km')
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
