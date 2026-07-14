import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import type { PlanAdaptationDiffDto } from '~/modules/coaching/models/conversation.model'
import { buildDiffWorkout } from './conversation.fixture'
import { composeAdaptationHeadline } from './adaptation-digest.helpers'

const emptyDiff: PlanAdaptationDiffDto = { workoutChanges: [], weeklyTargetChanges: [] }

describe('composeAdaptationHeadline', () => {
  it('composes the weekly-target sentence with "This week" when the change is on the current week', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      weeklyTargetChanges: [{ weekNumber: 3, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 26 }],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('This week 30.0 km → 26.0 km.')
  })

  it('composes the weekly-target sentence with "Week N" when the change is on a different week', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      weeklyTargetChanges: [{ weekNumber: 5, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 26 }],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Week 5 30.0 km → 26.0 km.')
  })

  it('composes a "trims to" sentence when the workout distance decreases', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      workoutChanges: [
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: buildDiffWorkout({ targetDistanceKm: 14 }),
          after: buildDiffWorkout({ targetDistanceKm: 8 }),
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Saturday trims to 8.0 km.')
  })

  it('composes an "extends to" sentence when the workout distance increases', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      workoutChanges: [
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: buildDiffWorkout({ targetDistanceKm: 8 }),
          after: buildDiffWorkout({ targetDistanceKm: 14 }),
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Saturday extends to 14.0 km.')
  })

  it('composes an "adds" sentence when a workout is newly added (before === null)', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      workoutChanges: [
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: null,
          after: buildDiffWorkout({ targetDistanceKm: 8 }),
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Saturday adds 8.0 km.')
  })

  it('composes an "is removed" sentence when a workout is removed (after === null)', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      workoutChanges: [
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: buildDiffWorkout({ targetDistanceKm: 8 }),
          after: null,
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Saturday is removed.')
  })

  it('composes an "is adjusted" sentence when the workout distance is unchanged', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      workoutChanges: [
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: buildDiffWorkout({ targetDistanceKm: 8 }),
          after: buildDiffWorkout({ targetDistanceKm: 8 }),
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Saturday is adjusted.')
  })

  it('picks the first workoutChanges entry with a non-null after, skipping a leading removal', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      workoutChanges: [
        { weekNumber: 3, dayOfWeek: 2, before: buildDiffWorkout(), after: null },
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: null,
          after: buildDiffWorkout({ targetDistanceKm: 8 }),
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('Saturday adds 8.0 km.')
  })

  it('caps at 2 sentences when both a weekly-target and a workout change are present', () => {
    const diff: PlanAdaptationDiffDto = {
      weeklyTargetChanges: [{ weekNumber: 3, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 26 }],
      workoutChanges: [
        {
          weekNumber: 3,
          dayOfWeek: 6,
          before: buildDiffWorkout({ targetDistanceKm: 14 }),
          after: buildDiffWorkout({ targetDistanceKm: 8 }),
        },
      ],
    }
    expect(
      composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Kilometers }),
    ).toBe('This week 30.0 km → 26.0 km. Saturday trims to 8.0 km.')
  })

  it('falls back to a generic sentence for a defensively-empty diff', () => {
    expect(
      composeAdaptationHeadline({
        diff: emptyDiff,
        currentWeek: 3,
        units: PreferredUnits.Kilometers,
      }),
    ).toBe('Your plan was adjusted.')
  })

  it('threads the Miles preference into the weekly-target sentence', () => {
    const diff: PlanAdaptationDiffDto = {
      ...emptyDiff,
      weeklyTargetChanges: [{ weekNumber: 3, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 26 }],
    }
    // 30 km / 1.609344 = 18.64... -> 18.6 mi ; 26 km -> 16.2 mi.
    expect(composeAdaptationHeadline({ diff, currentWeek: 3, units: PreferredUnits.Miles })).toBe(
      'This week 18.6 mi → 16.2 mi.',
    )
  })
})
