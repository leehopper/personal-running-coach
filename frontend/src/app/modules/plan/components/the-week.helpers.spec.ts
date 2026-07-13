import { describe, expect, it } from 'vitest'
import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus, PreferredUnits } from '~/api/generated'
import { toUtcMidnight } from './plan-display.helpers'
import { buildPlanFixture } from './plan-display.fixture'
import { formatWeekProgress, resolveDayCells, weekLoggedKm } from './the-week.helpers'

// planStartDate anchors to Sunday 2026-04-19 (matching the shared fixture),
// so week 1 spans 2026-04-19 (Sun) .. 2026-04-25 (Sat). Week 1's template
// (baseWeek(1, false)) is Sunday=Rest, Monday=Run, Tuesday=Rest,
// Wednesday=Run, Thursday=Rest, Friday=Run, Saturday=Run.
const PLAN_START_DATE = '2026-04-19'

const log = (occurredOn: string, distanceMeters = 6000): WorkoutLogDto => ({
  workoutLogId: `log-${occurredOn}`,
  occurredOn,
  distanceMeters,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
})

const weekOneTemplate = () => buildPlanFixture().mesoWeeks[0]

describe('resolveDayCells', () => {
  it('resolves all 4 states across the week from the slot template + a single log', () => {
    // Wednesday 2026-04-22 is "today"; Monday 2026-04-20 has a log.
    const todayUtc = toUtcMidnight(new Date(2026, 3, 22))
    const cells = resolveDayCells({
      week: weekOneTemplate(),
      weekNumber: 1,
      planStartDate: PLAN_START_DATE,
      logs: [log('2026-04-20')],
      todayUtc,
    })

    expect(cells).toHaveLength(7)
    const stateByDay = Object.fromEntries(cells.map((cell) => [cell.dayOfWeek, cell.state]))
    expect(stateByDay).toEqual({
      0: 'rest', // Sunday — Rest slot
      1: 'done', // Monday — logged
      2: 'rest', // Tuesday — Rest slot
      3: 'today', // Wednesday — Run slot, no log, is today
      4: 'rest', // Thursday — Rest slot
      5: 'planned', // Friday — Run slot, no log
      6: 'planned', // Saturday — Run slot, no log
    })
  })

  it('renders a cell "done", not "today", when a log exists for today\'s own date', () => {
    const todayUtc = toUtcMidnight(new Date(2026, 3, 22)) // Wednesday
    const cells = resolveDayCells({
      week: weekOneTemplate(),
      weekNumber: 1,
      planStartDate: PLAN_START_DATE,
      logs: [log('2026-04-22')], // a run logged today
      todayUtc,
    })
    const wednesday = cells.find((cell) => cell.dayOfWeek === 3)
    expect(wednesday?.state).toBe('done')
  })

  it('degrades to planned/rest (no done/today) when planStartDate is unparseable', () => {
    const todayUtc = toUtcMidnight(new Date(2026, 3, 22))
    const cells = resolveDayCells({
      week: weekOneTemplate(),
      weekNumber: 1,
      planStartDate: 'not-a-date',
      // Even a log dated exactly on Monday's real calendar date cannot match
      // a cell whose date resolution failed — there is no date to compare.
      logs: [log('2026-04-20')],
      todayUtc,
    })
    expect(cells.every((cell) => cell.date === null)).toBe(true)
    const stateByDay = Object.fromEntries(cells.map((cell) => [cell.dayOfWeek, cell.state]))
    expect(stateByDay).toEqual({
      0: 'rest',
      1: 'planned', // Run slot, but no date match possible
      2: 'rest',
      3: 'planned',
      4: 'rest',
      5: 'planned',
      6: 'planned',
    })
  })

  it('renders every non-logged, non-today cell as "rest" when no meso week template is available', () => {
    const todayUtc = toUtcMidnight(new Date(2026, 3, 22))
    const cells = resolveDayCells({
      week: undefined,
      weekNumber: 1,
      planStartDate: PLAN_START_DATE,
      logs: [log('2026-04-20')],
      todayUtc,
    })
    const stateByDay = Object.fromEntries(cells.map((cell) => [cell.dayOfWeek, cell.state]))
    expect(stateByDay).toEqual({
      0: 'rest',
      1: 'done',
      2: 'rest',
      3: 'today',
      4: 'rest',
      5: 'rest',
      6: 'rest',
    })
  })
})

describe('weekLoggedKm', () => {
  it("sums the distance of logs falling within the week's Sunday–Saturday span", () => {
    const totalKm = weekLoggedKm({
      weekNumber: 1,
      planStartDate: PLAN_START_DATE,
      logs: [log('2026-04-20', 6000), log('2026-04-24', 5000)],
    })
    expect(totalKm).toBeCloseTo(11, 5)
  })

  it('excludes logs outside the week span', () => {
    const totalKm = weekLoggedKm({
      weekNumber: 1,
      planStartDate: PLAN_START_DATE,
      logs: [log('2026-04-20', 6000), log('2026-04-26', 8000)], // 04-26 is week 2's Sunday
    })
    expect(totalKm).toBeCloseTo(6, 5)
  })

  it('counts a Skipped-status log toward the sum (a log exists ⇒ it counts, regardless of status)', () => {
    const skipped: WorkoutLogDto = {
      ...log('2026-04-20', 6000),
      completionStatus: CompletionStatus.Skipped,
    }
    const totalKm = weekLoggedKm({
      weekNumber: 1,
      planStartDate: PLAN_START_DATE,
      logs: [skipped],
    })
    expect(totalKm).toBeCloseTo(6, 5)
  })

  it('returns 0 when planStartDate is unparseable', () => {
    const totalKm = weekLoggedKm({
      weekNumber: 1,
      planStartDate: 'not-a-date',
      logs: [log('2026-04-20', 6000)],
    })
    expect(totalKm).toBe(0)
  })
})

describe('formatWeekProgress', () => {
  it('formats "N.N/NN.N KM" for the kilometres preference', () => {
    expect(formatWeekProgress(6, 30, PreferredUnits.Kilometers)).toBe('6.0/30.0 KM')
  })

  it('formats "N.N/NN.N MI" for the miles preference', () => {
    // 6 km -> 3.7 mi, 30 km -> 18.6 mi
    expect(formatWeekProgress(6, 30, PreferredUnits.Miles)).toBe('3.7/18.6 MI')
  })

  it('renders "0.0" rather than omitting the logged side when nothing has been run yet', () => {
    expect(formatWeekProgress(0, 30, PreferredUnits.Kilometers)).toBe('0.0/30.0 KM')
  })
})
