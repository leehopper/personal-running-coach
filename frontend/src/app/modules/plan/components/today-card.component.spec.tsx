import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { TodayCard, type TodayCardProps } from './today-card.component'
import { buildPlanFixture, fixtureWeekOneWorkouts } from './plan-display.fixture'

// TodayCard renders a <Link> in its workout variant, so every render needs a
// Router context.
const renderCard = (props: TodayCardProps) =>
  render(
    <MemoryRouter>
      <TodayCard {...props} />
    </MemoryRouter>,
  )

describe('TodayCard', () => {
  // Local Sunday April 2026-04-26 = day-of-week 0; test seed dates here
  // are constructed via the local-time `new Date(year, monthIndex, day)`
  // overload so `getDay()` resolves identically across CI timezones.
  const sunday = new Date(2026, 3, 26)
  const monday = new Date(2026, 3, 27)
  const wednesday = new Date(2026, 3, 29)
  const thursday = new Date(2026, 3, 30)

  it('renders the workout variant when today is a run day', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: monday,
    })
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('workout')
    expect(screen.getByRole('heading', { name: 'Easy aerobic shakeout' })).toBeInTheDocument()
    const inner = screen.getByTestId('micro-workout-card')
    expect(inner.dataset.emphasized).toBe('true')
  })

  it('threads the unit preference into the embedded workout card', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: monday,
      units: PreferredUnits.Miles,
    })
    // Monday's easy run is 6 km -> 3.7 mi
    expect(screen.getByTestId('micro-workout-distance').textContent).toBe('3.7 mi')
  })

  it('renders a "Log run" action linking to /log in the workout variant', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: monday,
    })
    const logAction = screen.getByTestId('today-card-log-action')
    expect(logAction).toHaveAttribute('href', '/log')
    expect(logAction).toHaveTextContent(/log run/i)
  })

  it('renders the rest-day variant on a rest slot, calling out the next workout', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: sunday,
    })
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('rest')
    const next = screen.getByTestId('today-card-next-workout')
    expect(next.textContent).toMatch(/monday/iu)
    expect(next.textContent).toMatch(/easy aerobic shakeout/iu)
  })

  it('does not render a Log action in the rest-day variant', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: sunday,
    })
    expect(screen.queryByTestId('today-card-log-action')).toBeNull()
  })

  it('renders the rest-day variant when slot is Rest even on a midweek day', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: thursday,
    })
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('rest')
    const next = screen.getByTestId('today-card-next-workout')
    expect(next.textContent).toMatch(/saturday/iu)
  })

  it('shows interval session details when today is the threshold day', () => {
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: wednesday,
    })
    expect(screen.getByRole('heading', { name: 'Threshold intervals' })).toBeInTheDocument()
  })

  it('renders rest-day variant when slot is Run but no micro workout exists for that day', () => {
    // Friday (day-of-week 5) has slotType: 'Run' in baseWeek but fixtureWeekOneWorkouts
    // contains no dayOfWeek=5 entry, exercising the graceful-degradation branch.
    const friday = new Date(2026, 3, 24) // 2026-04-24 is a Friday
    const plan = buildPlanFixture()
    renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: friday,
    })
    const card = screen.getByTestId('today-card')
    expect(card.dataset.variant).toBe('rest')
    expect(screen.getByText('Rest day — recover well.')).toBeInTheDocument()
    // Next workout is Saturday's long run (dayOfWeek=6)
    const next = screen.getByTestId('today-card-next-workout')
    expect(next.textContent).toMatch(/saturday/iu)
    expect(next.textContent).toMatch(/long aerobic run/iu)
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const plan = buildPlanFixture()
    const { container } = renderCard({
      currentWeek: plan.mesoWeeks[0],
      workouts: fixtureWeekOneWorkouts(),
      today: wednesday,
    })
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
