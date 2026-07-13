import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { renderInBothThemes } from '~/modules/common/test-utils/render-in-both-themes'
import { fixtureWeekOneWorkouts } from './plan-display.fixture'
import { WorkoutHero, type WorkoutHeroProps } from './workout-hero.component'

const renderHero = (props: WorkoutHeroProps) =>
  render(
    <MemoryRouter>
      <WorkoutHero {...props} />
    </MemoryRouter>,
  )

// 2026-07-08 00:00:00 UTC is a Wednesday.
const TODAY_UTC = Date.UTC(2026, 6, 8)

const [easyMonday, intervalsWednesday] = fixtureWeekOneWorkouts()

const testidsIn = (container: HTMLElement): (string | null)[] =>
  [...container.querySelectorAll('[data-testid]')]
    .map((el) => el.getAttribute('data-testid'))
    .sort()

describe('WorkoutHero', () => {
  describe('run-day branch', () => {
    it('renders eyebrow/title/summary/3-cell stat band/LOG RUN/DETAILS, with LOG RUN linking to /log', () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })

      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('run')
      expect(screen.getByTestId('workout-hero-eyebrow')).toHaveTextContent(
        'Wednesday, JUL 8 — ON THE SCHEDULE',
      )
      expect(screen.getByRole('heading', { name: 'Threshold intervals' })).toBeInTheDocument()
      const cells = screen.getAllByTestId('stat-cell')
      expect(cells).toHaveLength(3)
      const logAction = screen.getByTestId('workout-hero-log-action')
      expect(logAction).toHaveAttribute('href', '/log')
      expect(screen.getByTestId('workout-hero-details-trigger')).toBeInTheDocument()
    })

    it("derives the eyebrow's weekday and date fragments from the same todayUtc, agreeing even under a non-UTC test timezone", () => {
      const originalTz = process.env.TZ
      process.env.TZ = 'Pacific/Kiritimati' // UTC+14 — exposes a local-getter mismatch if reintroduced.
      try {
        renderHero({
          kind: 'run',
          slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
          workout: intervalsWednesday,
          todayUtc: TODAY_UTC,
          units: PreferredUnits.Kilometers,
        })
        expect(screen.getByTestId('workout-hero-eyebrow')).toHaveTextContent(
          'Wednesday, JUL 8 — ON THE SCHEDULE',
        )
      } finally {
        process.env.TZ = originalTz
      }
    })

    it('DETAILS is collapsed by default, expands the full segment list on click, and collapses again on a second click', async () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const trigger = screen.getByTestId('workout-hero-details-trigger')
      expect(trigger).toHaveAttribute('aria-expanded', 'false')
      expect(screen.queryByTestId('micro-workout-segments')).not.toBeInTheDocument()

      await userEvent.click(trigger)
      expect(trigger).toHaveAttribute('aria-expanded', 'true')
      expect(trigger).toHaveAttribute('data-state', 'open')
      expect(trigger).toHaveAttribute('aria-controls')
      expect(screen.getByTestId('micro-workout-segments')).toBeInTheDocument()
      expect(screen.getAllByTestId('micro-workout-segment')).toHaveLength(
        intervalsWednesday.segments.length,
      )

      await userEvent.click(trigger)
      expect(trigger).toHaveAttribute('aria-expanded', 'false')
      expect(screen.queryByTestId('micro-workout-segments')).not.toBeInTheDocument()
    })

    it('nests only the DETAILS trigger + content inside the Collapsible root, with LOG RUN as a sibling outside it', () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const logAction = screen.getByTestId('workout-hero-log-action')
      const trigger = screen.getByTestId('workout-hero-details-trigger')
      expect(logAction.closest('[data-slot="collapsible"]')).toBeNull()
      expect(trigger.closest('[data-slot="collapsible"]')).not.toBeNull()
    })

    it('renders the DETAILS trigger as a text-only affordance with no icon (BD3 — no chevron)', () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const trigger = screen.getByTestId('workout-hero-details-trigger')
      expect(trigger).toHaveTextContent(/details/i)
      expect(trigger.querySelector('svg')).toBeNull()
    })

    it('threads the Miles preference into the stat band', () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Miles,
      })
      const cells = screen.getAllByTestId('stat-cell')
      // 9 km target distance -> 5.6 mi.
      expect(cells[0]).toHaveTextContent('5.6')
      expect(cells[0]).toHaveTextContent('MILES')
    })

    it('shows the reps branch (×N, REPS · M MIN) for an interval workout', () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const cells = screen.getAllByTestId('stat-cell')
      expect(cells[2]).toHaveTextContent('×5')
      expect(cells[2]).toHaveTextContent('REPS · 4 MIN')
    })

    it('shows the duration branch (bare number, MINUTES) for a continuous-effort workout', () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Easy', notes: '' },
        workout: easyMonday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const cells = screen.getAllByTestId('stat-cell')
      expect(cells[2]).toHaveTextContent('38')
      expect(cells[2]).toHaveTextContent('MINUTES')
    })

    it("renders the stat band via StatCell's hero typography, not the generic .t-numeral/.t-data-label role (§2.8)", () => {
      renderHero({
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const cells = screen.getAllByTestId('stat-cell')
      for (const cell of cells) {
        expect(cell.querySelector('.t-numeral')).toBeNull()
        expect(cell.querySelector('.t-data-label')).toBeNull()
        const value = cell.firstElementChild
        const label = cell.lastElementChild
        expect(value).toHaveClass('font-condensed', 'text-[30px]', 'font-bold')
        expect(label).toHaveClass(
          'font-mono',
          'text-[9.5px]',
          'uppercase',
          'text-[color:var(--alp-faint)]',
        )
      }
    })
  })

  describe('rest-day branch', () => {
    it('renders REST DAY / Recovery is training., no stat band, no LOG RUN, no DETAILS', () => {
      renderHero({
        kind: 'rest',
        slot: { slotType: 'Rest', workoutType: null, notes: '' },
        nextWorkout: undefined,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('rest')
      expect(screen.getByRole('heading', { name: 'Rest day' })).toBeInTheDocument()
      expect(screen.getByText('Recovery is training.')).toBeInTheDocument()
      expect(screen.queryByTestId('stat-band')).not.toBeInTheDocument()
      expect(screen.queryByTestId('workout-hero-log-action')).not.toBeInTheDocument()
      expect(screen.queryByTestId('workout-hero-details-trigger')).not.toBeInTheDocument()
    })

    it('drops the " — ON THE SCHEDULE" eyebrow suffix (date clause only)', () => {
      renderHero({
        kind: 'rest',
        slot: { slotType: 'Rest', workoutType: null, notes: '' },
        nextWorkout: undefined,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const eyebrow = screen.getByTestId('workout-hero-eyebrow')
      expect(eyebrow).toHaveTextContent('Wednesday, JUL 8')
      expect(eyebrow).not.toHaveTextContent('ON THE SCHEDULE')
    })

    it('renders a next-workout row styled like a row (day abbrev + title + distance) when a later workout exists', () => {
      renderHero({
        kind: 'rest',
        slot: { slotType: 'Rest', workoutType: null, notes: '' },
        nextWorkout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      const row = screen.getByTestId('workout-hero-next-workout')
      expect(row).toHaveTextContent(/wed/i)
      expect(row).toHaveTextContent('Threshold intervals')
      expect(row).toHaveTextContent('9.0 km')
    })

    it('renders no next-workout row when none exists', () => {
      renderHero({
        kind: 'rest',
        slot: { slotType: 'Rest', workoutType: null, notes: '' },
        nextWorkout: undefined,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      })
      expect(screen.queryByTestId('workout-hero-next-workout')).not.toBeInTheDocument()
    })
  })

  describe('unavailable branch', () => {
    it('renders the graceful unavailable message, not a crash', () => {
      renderHero({ kind: 'unavailable', todayUtc: TODAY_UTC, units: PreferredUnits.Kilometers })
      const hero = screen.getByTestId('workout-hero')
      expect(hero.dataset.variant).toBe('unavailable')
      expect(screen.getByText("This week's plan isn't ready yet.")).toBeInTheDocument()
    })
  })

  it('restates the workout-hero root testid on all three render branches, distinguished by data-variant', () => {
    const cases: WorkoutHeroProps[] = [
      { kind: 'unavailable', todayUtc: TODAY_UTC, units: PreferredUnits.Kilometers },
      {
        kind: 'run',
        slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
        workout: intervalsWednesday,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      },
      {
        kind: 'rest',
        slot: { slotType: 'Rest', workoutType: null, notes: '' },
        nextWorkout: undefined,
        todayUtc: TODAY_UTC,
        units: PreferredUnits.Kilometers,
      },
    ]
    for (const props of cases) {
      const { unmount, getByTestId } = renderHero(props)
      expect(getByTestId('workout-hero').dataset.variant).toBe(props.kind)
      unmount()
    }
  })

  describe('dual-theme parity', () => {
    it('renders the run-day variant identically in both themes with zero raw colour literals', () => {
      const { dark, light } = renderInBothThemes(
        <MemoryRouter>
          <WorkoutHero
            kind="run"
            slot={{ slotType: 'Run', workoutType: 'Interval', notes: '' }}
            workout={intervalsWednesday}
            todayUtc={TODAY_UTC}
            units={PreferredUnits.Kilometers}
          />
        </MemoryRouter>,
      )
      for (const result of [dark, light]) {
        expect(result.getByTestId('workout-hero')).toBeInTheDocument()
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })

    it('renders the rest-day variant identically in both themes with zero raw colour literals', () => {
      const { dark, light } = renderInBothThemes(
        <MemoryRouter>
          <WorkoutHero
            kind="rest"
            slot={{ slotType: 'Rest', workoutType: null, notes: '' }}
            nextWorkout={intervalsWednesday}
            todayUtc={TODAY_UTC}
            units={PreferredUnits.Kilometers}
          />
        </MemoryRouter>,
      )
      for (const result of [dark, light]) {
        expect(result.getByTestId('workout-hero')).toBeInTheDocument()
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const { container } = renderHero({
      kind: 'run',
      slot: { slotType: 'Run', workoutType: 'Interval', notes: '' },
      workout: intervalsWednesday,
      todayUtc: TODAY_UTC,
      units: PreferredUnits.Kilometers,
    })
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
