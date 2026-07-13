import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus, PreferredUnits } from '~/api/generated'
import { renderInBothThemes } from '~/modules/common/test-utils/render-in-both-themes'
import { toUtcMidnight } from './plan-display.helpers'
import { buildPlanFixture } from './plan-display.fixture'
import { TheWeek, type TheWeekProps } from './the-week.component'

const PLAN_START_DATE = '2026-04-19' // Sunday — week 1 spans 04-19..04-25.

const log = (occurredOn: string, distanceMeters = 6000): WorkoutLogDto => ({
  workoutLogId: `log-${occurredOn}`,
  occurredOn,
  distanceMeters,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
})

const weekOneTemplate = () => buildPlanFixture().mesoWeeks[0] // weeklyTargetKm: 30

const baseProps = (overrides: Partial<TheWeekProps> = {}): TheWeekProps => ({
  currentWeek: weekOneTemplate(),
  currentWeekNumber: 1,
  planStartDate: PLAN_START_DATE,
  logs: [],
  todayUtc: toUtcMidnight(new Date(2026, 3, 22)), // Wednesday 2026-04-22
  units: PreferredUnits.Kilometers,
  ...overrides,
})

describe('TheWeek', () => {
  it('renders 7 day cells with the correct states, testids, and per-cell aria-labels', () => {
    render(<TheWeek {...baseProps({ logs: [log('2026-04-20')] })} />)

    const weekSection = screen.getByTestId('the-week')
    expect(weekSection.dataset.state).toBeUndefined()

    const cells = screen.getAllByTestId('the-week-day-cell')
    expect(cells).toHaveLength(7)

    const byDay = Object.fromEntries(cells.map((cell) => [cell.dataset.dayOfWeek, cell]))
    expect(byDay['0'].dataset.state).toBe('rest') // Sunday — Rest slot
    expect(byDay['1'].dataset.state).toBe('done') // Monday — logged
    expect(byDay['1']).toHaveAttribute('aria-label', 'Monday, done')
    expect(byDay['2'].dataset.state).toBe('rest') // Tuesday — Rest slot
    expect(byDay['3'].dataset.state).toBe('today') // Wednesday — Run slot, is today
    expect(byDay['3']).toHaveAttribute('aria-label', 'Wednesday, today')
    expect(byDay['4'].dataset.state).toBe('rest') // Thursday — Rest slot
    expect(byDay['5'].dataset.state).toBe('planned') // Friday — Run slot
    expect(byDay['6'].dataset.state).toBe('planned') // Saturday — Run slot
  })

  it('shows the N.N/NN.N KM progress string from logged vs. weekly target km', () => {
    render(<TheWeek {...baseProps({ logs: [log('2026-04-20', 6000)] })} />)
    expect(screen.getByTestId('the-week').textContent).toContain('6.0/30.0 KM')
  })

  it('shows the progress string in miles under the Miles preference', () => {
    render(
      <TheWeek {...baseProps({ logs: [log('2026-04-20', 6000)], units: PreferredUnits.Miles })} />,
    )
    // 6 km -> 3.7 mi, 30 km -> 18.6 mi
    expect(screen.getByTestId('the-week').textContent).toContain('3.7/18.6 MI')
  })

  it('renders a cell "done" (not "today") when a log exists for today\'s own date', () => {
    render(<TheWeek {...baseProps({ logs: [log('2026-04-22')] })} />)
    const cells = screen.getAllByTestId('the-week-day-cell')
    const wednesday = cells.find((cell) => cell.dataset.dayOfWeek === '3')
    expect(wednesday?.dataset.state).toBe('done')
  })

  it('styles each day label state-dependently — today semibold clay-text, rest faint, done/planned muted', () => {
    render(<TheWeek {...baseProps({ logs: [log('2026-04-20')] })} />)
    const cells = screen.getAllByTestId('the-week-day-cell')
    const byDay = Object.fromEntries(cells.map((cell) => [cell.dataset.dayOfWeek, cell]))

    const labelOf = (cell: HTMLElement) => cell.querySelector('.t-data-label')

    const restLabel = labelOf(byDay['0']) // Sunday — rest
    expect(restLabel).toHaveClass('text-[color:var(--alp-faint)]')
    expect(restLabel).not.toHaveClass('text-muted-foreground')
    expect(restLabel).not.toHaveClass('font-semibold')

    const doneLabel = labelOf(byDay['1']) // Monday — done (logged)
    expect(doneLabel).toHaveClass('text-muted-foreground')
    expect(doneLabel).not.toHaveClass('text-[color:var(--alp-faint)]')
    expect(doneLabel).not.toHaveClass('font-semibold')

    const todayLabel = labelOf(byDay['3']) // Wednesday — today
    expect(todayLabel).toHaveClass('font-semibold', 'text-clay-text')
    expect(todayLabel).not.toHaveClass('text-muted-foreground')
    expect(todayLabel).not.toHaveClass('text-[color:var(--alp-faint)]')

    const plannedLabel = labelOf(byDay['5']) // Friday — planned
    expect(plannedLabel).toHaveClass('text-muted-foreground')
    expect(plannedLabel).not.toHaveClass('text-[color:var(--alp-faint)]')
    expect(plannedLabel).not.toHaveClass('font-semibold')

    // Labels genuinely differ by state — not just present for every cell.
    expect(restLabel?.className).not.toBe(todayLabel?.className)
    expect(restLabel?.className).not.toBe(doneLabel?.className)
    expect(doneLabel?.className).not.toBe(todayLabel?.className)
  })

  it('renders the moss checkmark via a semantic token class, not a hardcoded colour, for done cells', () => {
    render(<TheWeek {...baseProps({ logs: [log('2026-04-20')] })} />)
    const cells = screen.getAllByTestId('the-week-day-cell')
    const monday = cells.find((cell) => cell.dataset.dayOfWeek === '1')
    const check = monday?.querySelector('svg')
    expect(check).not.toBeNull()
    expect(check).toHaveClass('text-background')
  })

  it('renders the unavailable state gracefully with the exact copy, when no meso week is available', () => {
    render(<TheWeek {...baseProps({ currentWeek: undefined })} />)
    const weekSection = screen.getByTestId('the-week')
    expect(weekSection.dataset.state).toBe('unavailable')
    expect(screen.getByText("This week's plan isn't ready yet.")).toBeInTheDocument()
    expect(screen.queryByTestId('the-week-day-cell')).not.toBeInTheDocument()
  })

  it('documents the page-1 log-coverage limitation: a run absent from a 20-entry logs page renders planned/rest and is excluded from the progress sum', () => {
    // Simulates a runner who has logged 20+ MORE RECENT runs than this
    // week's real Monday run, so `/history`'s page-1 fetch (20 entries,
    // newest-first) never includes it — Home passes TheWeek only what it
    // fetched. TheWeek has no way to compensate; this pins that as accepted,
    // tested behaviour (§2.5/§9 #9), not a silent bug.
    const laterLogs: WorkoutLogDto[] = Array.from({ length: 20 }, (_, i) =>
      log(`2026-06-${String(i + 1).padStart(2, '0')}`, 5000),
    )
    render(<TheWeek {...baseProps({ logs: laterLogs })} />)

    const cells = screen.getAllByTestId('the-week-day-cell')
    const monday = cells.find((cell) => cell.dataset.dayOfWeek === '1')
    expect(monday?.dataset.state).toBe('planned') // NOT 'done' — the log isn't in `logs`.
    expect(screen.getByTestId('the-week').textContent).toContain('0.0/30.0 KM')
  })

  describe('dual-theme parity', () => {
    it('renders all 4 cell states identically in both themes with zero raw colour literals', () => {
      const { dark, light } = renderInBothThemes(
        <TheWeek {...baseProps({ logs: [log('2026-04-20')] })} />,
      )
      for (const result of [dark, light]) {
        expect(result.getByTestId('the-week')).toBeInTheDocument()
        expect(result.getAllByTestId('the-week-day-cell')).toHaveLength(7)
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      const testidsIn = (container: HTMLElement) =>
        [...container.querySelectorAll('[data-testid]')]
          .map((el) => el.getAttribute('data-testid'))
          .sort()
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const { container } = render(<TheWeek {...baseProps({ logs: [log('2026-04-20')] })} />)
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
