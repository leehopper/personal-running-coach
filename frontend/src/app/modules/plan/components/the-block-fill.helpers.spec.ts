import { describe, expect, it } from 'vitest'
import type { PhaseType, PlanPhaseDto } from '~/modules/plan/models/plan.model'
import { computePhaseRanges } from './plan-display.helpers'
import { formatGoalChip, resolveBlockFillTiers, shortEventLabel } from './the-block-fill.helpers'

const buildPhase = (phaseType: PhaseType, weeks: number): PlanPhaseDto => ({
  phaseType,
  weeks,
  weeklyDistanceStartKm: 20,
  weeklyDistanceEndKm: 30,
  intensityDistribution: '80/20',
  allowedWorkoutTypes: ['Easy'],
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 300,
  notes: '',
  includesDeload: false,
})

describe('resolveBlockFillTiers', () => {
  it("mirrors the mock's own example: currentWeek 1 of a 12-week/4-phase plan", () => {
    const ranges = computePhaseRanges([
      buildPhase('Base', 4),
      buildPhase('Build', 5),
      buildPhase('Peak', 2),
      buildPhase('Taper', 1),
    ])
    const tiers = resolveBlockFillTiers({ ranges, currentWeek: 1, totalWeeks: 12 })
    expect(tiers).toEqual([
      'current',
      'currentPhase',
      'currentPhase',
      'currentPhase',
      'nextPhase',
      'nextPhase',
      'nextPhase',
      'nextPhase',
      'nextPhase',
      'distant',
      'distant',
      'distant',
    ])
  })

  it('renders the WHOLE current phase — including weeks before currentWeek — as currentPhase, not just the tail', () => {
    const ranges = computePhaseRanges([
      buildPhase('Base', 4),
      buildPhase('Build', 5),
      buildPhase('Peak', 2),
      buildPhase('Taper', 1),
    ])
    const tiers = resolveBlockFillTiers({ ranges, currentWeek: 7, totalWeeks: 12 })
    // Build spans weeks 5-9; currentWeek=7 sits mid-phase.
    expect(tiers[4]).toBe('currentPhase') // week 5
    expect(tiers[5]).toBe('currentPhase') // week 6
    expect(tiers[6]).toBe('current') // week 7
    expect(tiers[7]).toBe('currentPhase') // week 8
    expect(tiers[8]).toBe('currentPhase') // week 9
  })

  it('renders weeks in an EARLIER phase than the current one as currentPhase (BD2), not distant', () => {
    const ranges = computePhaseRanges([
      buildPhase('Base', 4),
      buildPhase('Build', 5),
      buildPhase('Peak', 2),
      buildPhase('Taper', 1),
    ])
    const tiers = resolveBlockFillTiers({ ranges, currentWeek: 7, totalWeeks: 12 })
    // Base spans weeks 1-4, entirely before the current (Build) phase.
    expect(tiers[0]).toBe('currentPhase')
    expect(tiers[1]).toBe('currentPhase')
    expect(tiers[2]).toBe('currentPhase')
    expect(tiers[3]).toBe('currentPhase')
  })

  it('scales to an arbitrary TotalWeeks, not hardcoded to 12', () => {
    const ranges = computePhaseRanges([buildPhase('Base', 16)])
    const tiers = resolveBlockFillTiers({ ranges, currentWeek: 1, totalWeeks: 16 })
    expect(tiers).toHaveLength(16)
  })

  it('skips a zero-week phase BETWEEN two real phases when resolving nextRange, rather than starving the true next phase', () => {
    // phases = [{Build,5},{Peak,0},{Taper,4}], totalWeeks=9, currentWeek=3 (mid-Build)
    // -> Taper's weeks 6-9 must read nextPhase, not distant.
    const ranges = computePhaseRanges([
      buildPhase('Build', 5),
      buildPhase('Peak', 0),
      buildPhase('Taper', 4),
    ])
    const tiers = resolveBlockFillTiers({ ranges, currentWeek: 3, totalWeeks: 9 })
    expect(tiers[5]).toBe('nextPhase') // week 6
    expect(tiers[6]).toBe('nextPhase') // week 7
    expect(tiers[7]).toBe('nextPhase') // week 8
    expect(tiers[8]).toBe('nextPhase') // week 9
  })

  it('does not let a zero-week phase BEFORE the current phase shift which weeks count as "before" it (BD2 + §2.3 composition)', () => {
    // phases = [{Base,4},{Recovery,0},{Build,5}], totalWeeks=9, currentWeek=7 (mid-Build)
    // -> weeks 1-4 (Base) still read currentPhase.
    const ranges = computePhaseRanges([
      buildPhase('Base', 4),
      buildPhase('Recovery', 0),
      buildPhase('Build', 5),
    ])
    const tiers = resolveBlockFillTiers({ ranges, currentWeek: 7, totalWeeks: 9 })
    expect(tiers[0]).toBe('currentPhase') // week 1
    expect(tiers[3]).toBe('currentPhase') // week 4
    expect(tiers[4]).toBe('currentPhase') // week 5 (Build starts here)
    expect(tiers[6]).toBe('current') // week 7
  })
})

describe('shortEventLabel', () => {
  it.each([
    [5, '5K'],
    [5.4, '5K'],
    [10, '10K'],
    [10.5, '10K'],
    [21.1, 'HALF'],
    [22, 'HALF'],
    [42.2, 'MARATHON'],
    [43, 'MARATHON'],
    [15, '15K'],
    [7.6, '8K'],
  ])('maps %s km to %s', (distanceKm, expected) => {
    expect(shortEventLabel(distanceKm)).toBe(expected)
  })
})

describe('formatGoalChip', () => {
  it('formats a race-training goal chip exactly as "10K — OCT 3"', () => {
    expect(formatGoalChip({ targetEventDistanceKm: 10, targetEventDate: '2026-10-03' })).toBe(
      '10K — OCT 3',
    )
  })

  it('returns null when both target-event fields are null (general fitness)', () => {
    expect(formatGoalChip({ targetEventDistanceKm: null, targetEventDate: null })).toBeNull()
  })

  it('returns null when only the distance is present', () => {
    expect(formatGoalChip({ targetEventDistanceKm: 10, targetEventDate: null })).toBeNull()
  })

  it('returns null when only the date is present', () => {
    expect(
      formatGoalChip({ targetEventDistanceKm: null, targetEventDate: '2026-10-03' }),
    ).toBeNull()
  })

  it('returns null (not a Unix-epoch-derived chip) for a non-null but unparseable targetEventDate', () => {
    const result = formatGoalChip({ targetEventDistanceKm: 10, targetEventDate: 'not-a-date' })
    expect(result).toBeNull()
  })
})
