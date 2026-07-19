import { describe, expect, it } from 'vitest'

import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus, PreferredUnits } from '~/api/generated'
import { computeWeekAggregate, formatWeekAggregate } from './week-aggregate.helpers'

const log = (overrides: Partial<WorkoutLogDto> = {}): WorkoutLogDto => ({
  workoutLogId: 'log-1',
  occurredOn: '2026-06-01',
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
  isOnPlan: false,
  ...overrides,
})

describe('computeWeekAggregate', () => {
  it('sums distance and counts runs for an all-completed week', () => {
    const agg = computeWeekAggregate([log({ distanceMeters: 5000 }), log({ distanceMeters: 8000 })])
    expect(agg).toEqual({ distanceMeters: 13000, runCount: 2, skipCount: 0 })
  })

  it('counts a Partial log as a run and includes its actual distance', () => {
    const agg = computeWeekAggregate([
      log({ distanceMeters: 5000, completionStatus: CompletionStatus.Complete }),
      log({ distanceMeters: 3000, completionStatus: CompletionStatus.Partial }),
    ])
    expect(agg).toEqual({ distanceMeters: 8000, runCount: 2, skipCount: 0 })
  })

  it('counts a Skipped log toward skipCount only, contributing zero distance', () => {
    const agg = computeWeekAggregate([
      log({ distanceMeters: 5000, completionStatus: CompletionStatus.Complete }),
      log({ distanceMeters: 0, completionStatus: CompletionStatus.Skipped }),
    ])
    expect(agg).toEqual({ distanceMeters: 5000, runCount: 1, skipCount: 1 })
  })

  it('returns all-zero for an all-skipped week', () => {
    const agg = computeWeekAggregate([
      log({ distanceMeters: 0, completionStatus: CompletionStatus.Skipped }),
      log({ distanceMeters: 0, completionStatus: CompletionStatus.Skipped }),
    ])
    expect(agg).toEqual({ distanceMeters: 0, runCount: 0, skipCount: 2 })
  })

  it('returns all-zero for an empty log list', () => {
    expect(computeWeekAggregate([])).toEqual({ distanceMeters: 0, runCount: 0, skipCount: 0 })
  })
})

describe('formatWeekAggregate', () => {
  it('formats km with a plural RUNS and no skip clause when there are no skips', () => {
    const formatted = formatWeekAggregate(
      { distanceMeters: 15200, runCount: 3, skipCount: 0 },
      PreferredUnits.Kilometers,
    )
    expect(formatted).toBe('15.2 km · 3 RUNS')
  })

  it('uses singular RUN for a single-run week', () => {
    const formatted = formatWeekAggregate(
      { distanceMeters: 5000, runCount: 1, skipCount: 0 },
      PreferredUnits.Kilometers,
    )
    expect(formatted).toBe('5.0 km · 1 RUN')
  })

  it('appends the SKIP clause AFTER the run clause when skips occurred', () => {
    const formatted = formatWeekAggregate(
      { distanceMeters: 15200, runCount: 3, skipCount: 1 },
      PreferredUnits.Kilometers,
    )
    expect(formatted).toBe('15.2 km · 3 RUNS · 1 SKIP')
  })

  it('falls back to the "0.0 km" placeholder for a zero-distance aggregate', () => {
    const formatted = formatWeekAggregate(
      { distanceMeters: 0, runCount: 0, skipCount: 2 },
      PreferredUnits.Kilometers,
    )
    expect(formatted).toBe('0.0 km · 0 RUNS · 2 SKIP')
  })

  it('formats distance in miles under a Miles preference', () => {
    // 5000 m / 1609.344 = 3.107... -> 3.1 mi
    const formatted = formatWeekAggregate(
      { distanceMeters: 5000, runCount: 1, skipCount: 0 },
      PreferredUnits.Miles,
    )
    expect(formatted).toBe('3.1 mi · 1 RUN')
  })

  it('falls back to the "0.0 mi" placeholder for a zero-distance aggregate under a Miles preference', () => {
    const formatted = formatWeekAggregate(
      { distanceMeters: 0, runCount: 0, skipCount: 2 },
      PreferredUnits.Miles,
    )
    expect(formatted).toBe('0.0 mi · 0 RUNS · 2 SKIP')
  })
})
