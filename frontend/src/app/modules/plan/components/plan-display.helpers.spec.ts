import { afterEach, describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import type {
  MicroWorkoutCardDto,
  PlanPhaseDto,
  WorkoutSegmentDto,
} from '~/modules/plan/models/plan.model'
import {
  composeWorkoutSummary,
  computePhaseRanges,
  describeSegment,
  formatHeroEyebrowDate,
  formatShortDateUtc,
  parseIsoDateUtc,
  phaseForWeek,
  resolveCalendarDateUtc,
  resolveHeroDistanceStat,
  resolveHeroThirdStat,
  toUtcMidnight,
} from './plan-display.helpers'
import { fixtureWeekOneWorkouts } from './plan-display.fixture'

/** Minimal `PlanPhaseDto` builder — only `phaseType`/`weeks` vary per test case. */
const buildPhase = (phaseType: PlanPhaseDto['phaseType'], weeks: number): PlanPhaseDto => ({
  phaseType,
  weeks,
  weeklyDistanceStartKm: 20,
  weeklyDistanceEndKm: 25,
  intensityDistribution: '80/20',
  allowedWorkoutTypes: ['Easy'],
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 300,
  notes: '',
  includesDeload: false,
})

const buildSegment = (overrides: Partial<WorkoutSegmentDto>): WorkoutSegmentDto => ({
  segmentType: 'Work',
  durationMinutes: 4,
  targetPaceSecPerKm: 240,
  intensity: 'Threshold',
  repetitions: 1,
  notes: '',
  ...overrides,
})

const buildWorkout = (overrides: Partial<MicroWorkoutCardDto>): MicroWorkoutCardDto => ({
  dayOfWeek: 3,
  workoutType: 'Easy',
  title: 'Test workout',
  targetDistanceKm: 6,
  targetDurationMinutes: 38,
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 330,
  segments: [],
  warmupNotes: '',
  cooldownNotes: '',
  coachingNotes: '',
  perceivedEffort: 3,
  ...overrides,
})

describe('toUtcMidnight', () => {
  it('normalizes a local-Date instant to the UTC epoch of its own local Y/M/D', () => {
    const local = new Date(2026, 6, 8, 23, 59) // 2026-07-08, late local time
    expect(toUtcMidnight(local)).toBe(Date.UTC(2026, 6, 8))
  })

  it('is stable regardless of the time-of-day component', () => {
    const morning = new Date(2026, 6, 8, 0, 1)
    const night = new Date(2026, 6, 8, 23, 58)
    expect(toUtcMidnight(morning)).toBe(toUtcMidnight(night))
  })
})

describe('parseIsoDateUtc', () => {
  it('parses a valid YYYY-MM-DD string to its UTC-midnight epoch', () => {
    expect(parseIsoDateUtc('2026-04-19')).toBe(Date.UTC(2026, 3, 19))
  })

  it('returns null for an empty string', () => {
    expect(parseIsoDateUtc('')).toBeNull()
  })

  it('returns null for a non-ISO-shaped string', () => {
    expect(parseIsoDateUtc('not-a-date')).toBeNull()
  })

  it('returns null for a string with the wrong separator/shape', () => {
    expect(parseIsoDateUtc('2026/04/19')).toBeNull()
  })

  it('returns null for a shape-valid but non-existent calendar date (Feb 30)', () => {
    // Date.UTC silently rolls this forward to Mar 2, 2026 — the round-trip
    // check must catch that rather than returning the epoch for Mar 2.
    expect(parseIsoDateUtc('2026-02-30')).toBeNull()
  })

  it('returns null for a shape-valid but out-of-range month (13)', () => {
    // Date.UTC silently rolls this forward to Jan 1, 2027.
    expect(parseIsoDateUtc('2026-13-01')).toBeNull()
  })
})

describe('resolveCalendarDateUtc', () => {
  const planStartDate = '2026-04-19' // Sunday

  it('resolves week 1, day 0 to the plan start date itself', () => {
    const date = resolveCalendarDateUtc(planStartDate, 1, 0)
    expect(date?.getTime()).toBe(Date.UTC(2026, 3, 19))
  })

  it('resolves a mid-week day within week 1', () => {
    // Week 1, day 3 (Wednesday) = 2026-04-22.
    const date = resolveCalendarDateUtc(planStartDate, 1, 3)
    expect(date?.getTime()).toBe(Date.UTC(2026, 3, 22))
  })

  it('resolves a later week correctly', () => {
    // Week 3, day 0 = 14 days after the anchor = 2026-05-03.
    const date = resolveCalendarDateUtc(planStartDate, 3, 0)
    expect(date?.getTime()).toBe(Date.UTC(2026, 4, 3))
  })

  it('returns null when planStartDate is malformed', () => {
    expect(resolveCalendarDateUtc('not-a-date', 1, 0)).toBeNull()
  })
})

describe('formatShortDateUtc', () => {
  it('formats a single-digit day with no leading zero', () => {
    expect(formatShortDateUtc(new Date(Date.UTC(2026, 6, 8)))).toBe('JUL 8')
  })

  it('formats a double-digit day', () => {
    expect(formatShortDateUtc(new Date(Date.UTC(2026, 9, 23)))).toBe('OCT 23')
  })

  it('formats the first month of the year (January boundary)', () => {
    expect(formatShortDateUtc(new Date(Date.UTC(2026, 0, 1)))).toBe('JAN 1')
  })

  it('formats the last month of the year (December boundary)', () => {
    expect(formatShortDateUtc(new Date(Date.UTC(2026, 11, 31)))).toBe('DEC 31')
  })
})

describe('formatHeroEyebrowDate', () => {
  const originalTz = process.env.TZ

  afterEach(() => {
    process.env.TZ = originalTz
  })

  it('composes "{Weekday}, {MONTH_ABBR} {DAY}" from a single UTC-normalized epoch', () => {
    // 2026-07-08 00:00:00 UTC is a Wednesday.
    const todayUtc = Date.UTC(2026, 6, 8)
    expect(formatHeroEyebrowDate(todayUtc)).toBe('Wednesday, JUL 8')
  })

  it('agrees on the same date even under a process timezone far AHEAD of UTC', () => {
    // A local (non-UTC) getDay()/getDate() read on `new Date(todayUtc)` would
    // roll forward into Thursday July 9 under this offset (UTC+14) — this
    // function must not do that; it reads getUTC* exclusively.
    process.env.TZ = 'Pacific/Kiritimati'
    const todayUtc = Date.UTC(2026, 6, 8)
    expect(formatHeroEyebrowDate(todayUtc)).toBe('Wednesday, JUL 8')
  })

  it('agrees on the same date even under a process timezone far BEHIND UTC', () => {
    // A local read would roll backward into Tuesday July 7 under this
    // offset (UTC-11).
    process.env.TZ = 'Pacific/Niue'
    const todayUtc = Date.UTC(2026, 6, 8)
    expect(formatHeroEyebrowDate(todayUtc)).toBe('Wednesday, JUL 8')
  })
})

describe('computePhaseRanges', () => {
  it('assigns sequential 1-based spans for a normal (all-positive-week) phase list', () => {
    const ranges = computePhaseRanges([buildPhase('Base', 4), buildPhase('Build', 5)])
    expect(ranges).toEqual([
      { phase: ranges[0].phase, startWeek: 1, endWeek: 4 },
      { phase: ranges[1].phase, startWeek: 5, endWeek: 9 },
    ])
  })

  it('gives a zero-week phase an empty (inverted) span and does not advance the cursor', () => {
    const phases = [buildPhase('Base', 4), buildPhase('Recovery', 0), buildPhase('Build', 5)]
    const ranges = computePhaseRanges(phases)
    expect(ranges[0]).toEqual({ phase: phases[0], startWeek: 1, endWeek: 4 })
    // The zero-week phase's span is empty (startWeek > endWeek) — not a
    // spurious 1-week claim on week 5.
    expect(ranges[1]).toEqual({ phase: phases[1], startWeek: 5, endWeek: 4 })
    // The shift-proof regression: the real next phase still starts at 5,
    // not 6 — the zero-week entry must not have consumed a week.
    expect(ranges[2]).toEqual({ phase: phases[2], startWeek: 5, endWeek: 9 })
  })

  it('keeps a zero-week LAST phase in the array so the last element still resolves to it', () => {
    const phases = [buildPhase('Base', 4), buildPhase('Taper', 0)]
    const ranges = computePhaseRanges(phases)
    expect(ranges).toHaveLength(2)
    expect(ranges[ranges.length - 1]?.phase).toBe(phases[1])
  })
})

describe('phaseForWeek', () => {
  it('resolves the phase whose span contains weekNumber', () => {
    const ranges = computePhaseRanges([buildPhase('Base', 4), buildPhase('Build', 5)])
    expect(phaseForWeek(ranges, 2)?.phaseType).toBe('Base')
    expect(phaseForWeek(ranges, 7)?.phaseType).toBe('Build')
  })

  it('falls back to the last phase when weekNumber matches no span', () => {
    const ranges = computePhaseRanges([buildPhase('Base', 4)])
    expect(phaseForWeek(ranges, 99)?.phaseType).toBe('Base')
  })

  it('correctly matches the FOLLOWING real phase for a week the old buggy zero-week span would have claimed', () => {
    // Under the pre-fix computePhaseRanges, the zero-week middle phase would
    // have claimed a phantom 1-week span at week 5, shifting the real next
    // phase to start at week 6. The fix must resolve week 5 to the real
    // (Build) phase, not the empty (Recovery) one.
    const ranges = computePhaseRanges([
      buildPhase('Base', 4),
      buildPhase('Recovery', 0),
      buildPhase('Build', 5),
    ])
    expect(phaseForWeek(ranges, 5)?.phaseType).toBe('Build')
  })
})

describe('describeSegment', () => {
  it('describes a Warmup segment', () => {
    expect(describeSegment(buildSegment({ segmentType: 'Warmup', durationMinutes: 12 }))).toBe(
      "12' easy",
    )
  })

  it('describes a Cooldown segment', () => {
    expect(describeSegment(buildSegment({ segmentType: 'Cooldown', durationMinutes: 8 }))).toBe(
      "8' down",
    )
  })

  it('describes a single (non-repeated) Work segment with its intensity phrase', () => {
    expect(
      describeSegment(
        buildSegment({
          segmentType: 'Work',
          durationMinutes: 20,
          repetitions: 1,
          intensity: 'Threshold',
        }),
      ),
    ).toBe("20' at threshold")
  })

  it('describes a repeated Work segment as "N × M\' at {intensity}"', () => {
    expect(
      describeSegment(
        buildSegment({
          segmentType: 'Work',
          durationMinutes: 4,
          repetitions: 5,
          intensity: 'Threshold',
        }),
      ),
    ).toBe("5 × 4' at threshold")
  })

  it('describes a single (non-repeated) Recovery segment', () => {
    expect(
      describeSegment(
        buildSegment({ segmentType: 'Recovery', durationMinutes: 3, repetitions: 1 }),
      ),
    ).toBe("3' easy recovery")
  })

  it('describes a repeated Recovery segment as "N × M\' easy recovery"', () => {
    expect(
      describeSegment(
        buildSegment({ segmentType: 'Recovery', durationMinutes: 3, repetitions: 4 }),
      ),
    ).toBe("4 × 3' easy recovery")
  })
})

describe('composeWorkoutSummary', () => {
  it('uses the coaching note alone (no leading ". ") for a continuous-effort workout with no segments', () => {
    // easyMonday-shaped: segments: [], populated coachingNotes.
    const [easyMonday] = fixtureWeekOneWorkouts()
    expect(composeWorkoutSummary(easyMonday)).toBe(
      'Keep the pace conversational — Daniels-Gilbert easy zone.',
    )
  })

  it('joins multiple segment phrases with ", then " and appends the coaching note, including a Recovery segment between Work blocks', () => {
    const workout = buildWorkout({
      coachingNotes: 'Hold form through the recoveries.',
      segments: [
        buildSegment({ segmentType: 'Warmup', durationMinutes: 12, repetitions: 1 }),
        buildSegment({
          segmentType: 'Work',
          durationMinutes: 4,
          repetitions: 5,
          intensity: 'Threshold',
        }),
        buildSegment({ segmentType: 'Recovery', durationMinutes: 2, repetitions: 4 }),
        buildSegment({ segmentType: 'Cooldown', durationMinutes: 8, repetitions: 1 }),
      ],
    })
    expect(composeWorkoutSummary(workout)).toBe(
      "12' easy, then 5 × 4' at threshold, then 4 × 2' easy recovery, then 8' down. Hold form through the recoveries.",
    )
  })

  it('falls back to a bare continuous-effort sentence when segments AND coachingNotes are both empty', () => {
    const workout = buildWorkout({ segments: [], coachingNotes: '', targetDurationMinutes: 45 })
    expect(composeWorkoutSummary(workout)).toBe("45' continuous effort.")
  })
})

describe('resolveHeroThirdStat', () => {
  it('returns the reps branch for a workout with an interval segment', () => {
    // intervalsWednesday: Work segment, durationMinutes: 4, repetitions: 5.
    // Sentence-case source copy — StatCell's hero label applies `uppercase`
    // via CSS, so the rendered text is still "REPS · 4 MIN".
    const [, intervalsWednesday] = fixtureWeekOneWorkouts()
    expect(resolveHeroThirdStat(intervalsWednesday)).toEqual({ value: '×5', label: 'Reps · 4 min' })
  })

  it('returns the duration branch for a continuous-effort workout with no interval segments', () => {
    const [easyMonday] = fixtureWeekOneWorkouts()
    expect(resolveHeroThirdStat(easyMonday)).toEqual({ value: '38', label: 'Minutes' })
  })

  it('returns the defensive dash branch when neither an interval segment nor a positive duration exists', () => {
    const workout = buildWorkout({ segments: [], targetDurationMinutes: 0 })
    expect(resolveHeroThirdStat(workout)).toEqual({ value: '—', label: 'Minutes' })
  })
})

describe('resolveHeroDistanceStat', () => {
  it('returns the bare kilometre number (no suffix) for a positive distance', () => {
    expect(resolveHeroDistanceStat(9, PreferredUnits.Kilometers)).toBe('9.0')
  })

  it('returns the bare mile-converted number under the Miles preference', () => {
    // 6 km -> 3.7 mi
    expect(resolveHeroDistanceStat(6, PreferredUnits.Miles)).toBe('3.7')
  })

  it('returns the em-dash placeholder for a non-positive distance', () => {
    expect(resolveHeroDistanceStat(0, PreferredUnits.Kilometers)).toBe('—')
  })
})
