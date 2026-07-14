import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { renderInBothThemes } from '~/modules/common/test-utils/render-in-both-themes'
import { fixtureWeekOneWorkouts } from './plan-display.fixture'
import { UpNext } from './up-next.component'

const [, intervalsWednesday, longRunSaturday] = fixtureWeekOneWorkouts()
const workouts = fixtureWeekOneWorkouts()

// 2026-04-20 is a Monday; 2026-04-25 is a Saturday.
const MONDAY = new Date(2026, 3, 20)
const SATURDAY = new Date(2026, 3, 25)

const testidsIn = (container: HTMLElement): (string | null)[] =>
  [...container.querySelectorAll('[data-testid]')]
    .map((el) => el.getAttribute('data-testid'))
    .sort()

describe('UpNext', () => {
  it('shows the remaining workouts this week, in day order, each {DAY}/{title}/{distance}', () => {
    render(
      <UpNext currentWeekWorkouts={workouts} today={MONDAY} units={PreferredUnits.Kilometers} />,
    )

    const rows = screen.getAllByTestId('up-next-row')
    expect(rows).toHaveLength(2)
    expect(rows[0]).toHaveTextContent(/wed/i)
    expect(rows[0]).toHaveTextContent(intervalsWednesday.title)
    expect(rows[0]).toHaveTextContent('9.0 km')
    expect(rows[1]).toHaveTextContent(/sat/i)
    expect(rows[1]).toHaveTextContent(longRunSaturday.title)
    expect(rows[1]).toHaveTextContent('14.0 km')
  })

  it('puts a bottom border on every row except the last', () => {
    render(
      <UpNext currentWeekWorkouts={workouts} today={MONDAY} units={PreferredUnits.Kilometers} />,
    )

    const rows = screen.getAllByTestId('up-next-row')
    expect(rows[0]).toHaveClass('border-b', 'border-border')
    expect(rows[1]).not.toHaveClass('border-b')
  })

  it('renders the section rule with an empty body when nothing remains this week', () => {
    render(
      <UpNext currentWeekWorkouts={workouts} today={SATURDAY} units={PreferredUnits.Kilometers} />,
    )

    expect(screen.getByTestId('up-next')).toBeInTheDocument()
    // Source copy stays sentence case — `uppercase` is applied via CSS only.
    expect(screen.getByText('Up next')).toBeInTheDocument()
    expect(screen.queryByTestId('up-next-row')).not.toBeInTheDocument()
  })

  it('threads the Miles preference into the row distances', () => {
    render(<UpNext currentWeekWorkouts={workouts} today={MONDAY} units={PreferredUnits.Miles} />)

    // 9 km -> 5.6 mi ; 14 km -> 8.7 mi.
    const rows = screen.getAllByTestId('up-next-row')
    expect(rows[0]).toHaveTextContent('5.6 mi')
    expect(rows[1]).toHaveTextContent('8.7 mi')
    expect(screen.queryByText(/\d\.\d km/u)).not.toBeInTheDocument()
  })

  describe('dual-theme parity', () => {
    it('renders identically in both themes with zero raw colour literals', () => {
      const { dark, light } = renderInBothThemes(
        <UpNext currentWeekWorkouts={workouts} today={MONDAY} units={PreferredUnits.Kilometers} />,
      )
      for (const result of [dark, light]) {
        expect(result.getByTestId('up-next')).toBeInTheDocument()
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const { container } = render(
      <UpNext currentWeekWorkouts={workouts} today={MONDAY} units={PreferredUnits.Kilometers} />,
    )
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
