import { describe, expect, it } from 'vitest'
import { buildPlanFixture } from '~/modules/plan/components/plan-display.fixture'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'
import { resolveCurrentWeek } from './use-plan.hooks'

// The shared fixture anchors `planStartDate` to Sunday 2026-04-19 with meso
// templates for weeks 1–4 and micro detail for week 1 only. These specs pin
// `resolveCurrentWeek` to a date-derived week (DEC-076 / slice-2b Unit 1),
// passing an explicit reference date so they never depend on the wall clock.

describe('resolveCurrentWeek', () => {
  it('derives the current week from planStartDate relative to the reference date', () => {
    // 2026-05-03 is 14 days after the fixture's 2026-04-19 anchor → week 3.
    // The lowest-populated heuristic would return week 1 (only micro week 1
    // is populated), so a result of 3 proves the date-derived path is used.
    expect(resolveCurrentWeek(buildPlanFixture(), new Date(2026, 4, 3))).toBe(3)
  })

  it('does not fall back to the lowest-populated-week heuristic for an in-range week', () => {
    const plan = buildPlanFixture()
    const lowestPopulated = Number.parseInt(Object.keys(plan.microWorkoutsByWeek)[0] ?? '1', 10)
    const derived = resolveCurrentWeek(plan, new Date(2026, 4, 3))
    expect(derived).not.toBe(lowestPopulated)
    expect(derived).toBe(3)
  })

  it('maps the plan’s first day to week 1', () => {
    expect(resolveCurrentWeek(buildPlanFixture(), new Date(2026, 3, 19))).toBe(1)
  })

  it('resolves a mid-week date into the same week via floor division', () => {
    // 2026-04-29 is 10 days after the anchor → floor(10 / 7) + 1 = 2.
    expect(resolveCurrentWeek(buildPlanFixture(), new Date(2026, 3, 29))).toBe(2)
  })

  it('returns a defined in-range week when that week has no micro detail loaded', () => {
    // Week 3 has a meso template but no micro-cycle detail in the fixture.
    const derived = resolveCurrentWeek(buildPlanFixture(), new Date(2026, 4, 3))
    expect(Number.isInteger(derived)).toBe(true)
    expect(derived).toBe(3)
    expect(buildPlanFixture().microWorkoutsByWeek[3]).toBeUndefined()
  })

  it('clamps to the first template week when the reference date precedes the plan start', () => {
    expect(resolveCurrentWeek(buildPlanFixture(), new Date(2026, 3, 12))).toBe(1)
  })

  it('clamps to the last template week when the reference date is beyond the generated weeks', () => {
    // Fixture has meso templates for weeks 1–4; a far-future date clamps to 4.
    expect(resolveCurrentWeek(buildPlanFixture(), new Date(2026, 7, 1))).toBe(4)
  })

  it('falls back to the lowest-populated week when planStartDate is malformed', () => {
    const plan: PlanProjectionDto = {
      ...buildPlanFixture(),
      planStartDate: '',
      microWorkoutsByWeek: {
        2: { workouts: [] },
        3: { workouts: [] },
      },
    }
    // No usable anchor → the defensive heuristic returns the lowest populated
    // micro week (2), not a clamped date-derived value.
    expect(resolveCurrentWeek(plan, new Date(2026, 4, 3))).toBe(2)
  })
})
