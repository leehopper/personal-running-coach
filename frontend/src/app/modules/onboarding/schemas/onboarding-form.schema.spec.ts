import { describe, expect, it } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import {
  OnboardingStatus,
  PrimaryGoal,
  type OnboardingStateDto,
} from '~/modules/onboarding/models/onboarding.model'
import {
  hydrateOnboardingFormFields,
  makeDefaultOnboardingFormFields,
  makeOnboardingFormSchema,
  reseedDistancesForUnitChange,
  toSubmitStructuredAnswersRequest,
  type OnboardingFormFields,
} from './onboarding-form.schema'

const validRaceFields = (): OnboardingFormFields => ({
  ...makeDefaultOnboardingFormFields(),
  goal: String(PrimaryGoal.RaceTraining),
  goalDescription: 'Sub-4 marathon',
  eventName: 'Berlin Marathon',
  eventDistance: '42.2',
  eventDate: '2026-09-27',
  targetFinishTime: '3:45:00',
  typicalWeekly: '40',
  longestRecentRun: '18',
  recentRaceDistance: '10',
  recentRaceTime: '45:30',
  fitnessDescription: 'Consistent base',
  maxRunDays: '5',
  sessionMinutes: '60',
  days: ['monday', 'wednesday', 'saturday'],
  scheduleDescription: 'No early mornings',
  hasActiveInjury: false,
  pastInjurySummary: 'Old ITB niggle',
  preferTrail: true,
  comfortableWithIntensity: true,
  preferencesDescription: 'Likes structure',
})

const parse = (fields: OnboardingFormFields, units: PreferredUnits = PreferredUnits.Kilometers) =>
  makeOnboardingFormSchema(units).safeParse(fields)

describe('makeOnboardingFormSchema — validation', () => {
  it('accepts a complete race-training submission', () => {
    expect(parse(validRaceFields()).success).toBe(true)
  })

  it('requires a goal, weekly volume, long run, run days, and session length', () => {
    const result = parse({
      ...makeDefaultOnboardingFormFields(),
      goal: '',
    })
    expect(result.success).toBe(false)
    if (result.success) return
    const paths = result.error.issues.map((issue) => issue.path.join('.'))
    expect(paths).toContain('goal')
    expect(paths).toContain('typicalWeekly')
    expect(paths).toContain('longestRecentRun')
    expect(paths).toContain('maxRunDays')
    expect(paths).toContain('sessionMinutes')
  })

  it('requires the event name/distance/date only for a race-training goal', () => {
    const raceMissingEvent = parse({
      ...validRaceFields(),
      eventName: '',
      eventDistance: '',
      eventDate: '',
    })
    expect(raceMissingEvent.success).toBe(false)
    if (!raceMissingEvent.success) {
      const paths = raceMissingEvent.error.issues.map((issue) => issue.path.join('.'))
      expect(paths).toEqual(expect.arrayContaining(['eventName', 'eventDistance', 'eventDate']))
    }

    // A non-race goal with the event fields left blank is valid.
    const generalFitness = parse({
      ...validRaceFields(),
      goal: String(PrimaryGoal.GeneralFitness),
      eventName: '',
      eventDistance: '',
      eventDate: '',
      targetFinishTime: '',
    })
    expect(generalFitness.success).toBe(true)
  })

  it('requires an active-injury description only when an active injury is flagged', () => {
    const withInjuryNoDesc = parse({ ...validRaceFields(), hasActiveInjury: true })
    expect(withInjuryNoDesc.success).toBe(false)
    if (!withInjuryNoDesc.success) {
      const paths = withInjuryNoDesc.error.issues.map((issue) => issue.path.join('.'))
      expect(paths).toContain('activeInjuryDescription')
    }

    const withInjuryAndDesc = parse({
      ...validRaceFields(),
      hasActiveInjury: true,
      activeInjuryDescription: 'Left calf strain',
    })
    expect(withInjuryAndDesc.success).toBe(true)
  })

  it('enforces the 1-7 whole-number run-days range', () => {
    expect(parse({ ...validRaceFields(), maxRunDays: '8' }).success).toBe(false)
    expect(parse({ ...validRaceFields(), maxRunDays: '0' }).success).toBe(false)
    expect(parse({ ...validRaceFields(), maxRunDays: '3.5' }).success).toBe(false)
    expect(parse({ ...validRaceFields(), maxRunDays: '3' }).success).toBe(true)
  })

  it('rejects a malformed finish time', () => {
    expect(parse({ ...validRaceFields(), targetFinishTime: '3h45' }).success).toBe(false)
  })

  it('caps miles-mode distances so the client never accepts a value the backend km-cap rejects', () => {
    // The backend rejects distances whose km value exceeds 100 000 km. 62138 mi
    // converts to ~100 001.4 km (backend-invalid), so the client must reject it;
    // 62137 mi ≈ 99 999.8 km stays under the cap.
    expect(
      parse({ ...validRaceFields(), typicalWeekly: '62138' }, PreferredUnits.Miles).success,
    ).toBe(false)
    expect(
      parse({ ...validRaceFields(), typicalWeekly: '62137' }, PreferredUnits.Miles).success,
    ).toBe(true)
  })
})

describe('toSubmitStructuredAnswersRequest — mapping to the wire', () => {
  const mapOf = (
    fields: OnboardingFormFields,
    units: PreferredUnits = PreferredUnits.Kilometers,
  ) => {
    const result = parse(fields, units)
    if (!result.success) throw new Error('fixture should be valid')
    return toSubmitStructuredAnswersRequest(result.data, 'idem-key', units)
  }

  it('maps a race submission with km distances passed through unchanged', () => {
    const request = mapOf(validRaceFields())
    expect(request.idempotencyKey).toBe('idem-key')
    expect(request.primaryGoal).toEqual({
      goal: PrimaryGoal.RaceTraining,
      description: 'Sub-4 marathon',
    })
    expect(request.targetEvent).toEqual({
      eventName: 'Berlin Marathon',
      distanceKm: 42.2,
      eventDateIso: '2026-09-27',
      targetFinishTimeIso: 'PT3H45M0S',
    })
    expect(request.currentFitness).toEqual({
      typicalWeeklyKm: 40,
      longestRecentRunKm: 18,
      recentRaceDistanceKm: 10,
      recentRaceTimeIso: 'PT0H45M30S',
      description: 'Consistent base',
    })
    expect(request.weeklySchedule).toMatchObject({
      maxRunDaysPerWeek: 5,
      typicalSessionMinutes: 60,
      monday: true,
      tuesday: false,
      wednesday: true,
      thursday: false,
      friday: false,
      saturday: true,
      sunday: false,
      description: 'No early mornings',
    })
    expect(request.preferences).toEqual({
      preferredUnits: PreferredUnits.Kilometers,
      preferTrail: true,
      comfortableWithIntensity: true,
      description: 'Likes structure',
    })
    expect(request.injuryHistory).toEqual({
      hasActiveInjury: false,
      activeInjuryDescription: '',
      pastInjurySummary: 'Old ITB niggle',
    })
  })

  it('sends targetEvent as null for a non-race goal', () => {
    const request = mapOf({
      ...validRaceFields(),
      goal: String(PrimaryGoal.GeneralFitness),
      eventName: '',
      eventDistance: '',
      eventDate: '',
      targetFinishTime: '',
    })
    expect(request.targetEvent).toBeNull()
  })

  it('converts miles entries to canonical kilometres before the wire', () => {
    const request = mapOf(
      { ...validRaceFields(), typicalWeekly: '10', eventDistance: '26.2' },
      PreferredUnits.Miles,
    )
    // 10 mi × 1.609344 = 16.09344 km; 26.2 mi × 1.609344 ≈ 42.16 km
    expect(request.currentFitness.typicalWeeklyKm).toBeCloseTo(16.09344, 4)
    expect(request.targetEvent?.distanceKm).toBeCloseTo(42.164, 2)
    expect(request.preferences.preferredUnits).toBe(PreferredUnits.Miles)
  })

  it('omits an optional recent-race distance/time as null', () => {
    const request = mapOf({ ...validRaceFields(), recentRaceDistance: '', recentRaceTime: '' })
    expect(request.currentFitness.recentRaceDistanceKm).toBeNull()
    expect(request.currentFitness.recentRaceTimeIso).toBeNull()
  })

  it('drops the active-injury description when no active injury is flagged', () => {
    const request = mapOf({
      ...validRaceFields(),
      hasActiveInjury: false,
      activeInjuryDescription: 'stale text',
    })
    expect(request.injuryHistory.activeInjuryDescription).toBe('')
  })
})

describe('reseedDistancesForUnitChange — units change mid-form', () => {
  it('converts the four distance fields km → miles, preserving physical distance', () => {
    const fields: OnboardingFormFields = {
      ...makeDefaultOnboardingFormFields(),
      eventDistance: '42.2',
      typicalWeekly: '40',
      longestRecentRun: '16.1',
      recentRaceDistance: '10',
      sessionMinutes: '60',
      maxRunDays: '5',
      goalDescription: 'keep me',
    }
    const next = reseedDistancesForUnitChange(
      fields,
      PreferredUnits.Kilometers,
      PreferredUnits.Miles,
    )

    // 40 km ÷ 1.609344 ≈ 24.9 mi; 16.1 km ≈ 10.0 mi; 10 km ≈ 6.2 mi; 42.2 km ≈ 26.2 mi
    expect(next.typicalWeekly).toBe('24.9')
    expect(next.longestRecentRun).toBe('10.0')
    expect(next.recentRaceDistance).toBe('6.2')
    expect(next.eventDistance).toBe('26.2')
  })

  it('leaves non-distance fields and blank distances untouched', () => {
    const fields: OnboardingFormFields = {
      ...makeDefaultOnboardingFormFields(),
      typicalWeekly: '40',
      recentRaceDistance: '',
      sessionMinutes: '60',
      maxRunDays: '5',
      goalDescription: 'a note',
      days: ['monday'],
    }
    const next = reseedDistancesForUnitChange(
      fields,
      PreferredUnits.Kilometers,
      PreferredUnits.Miles,
    )

    expect(next.recentRaceDistance).toBe('')
    expect(next.sessionMinutes).toBe('60')
    expect(next.maxRunDays).toBe('5')
    expect(next.goalDescription).toBe('a note')
    expect(next.days).toEqual(['monday'])
  })
})

describe('hydrateOnboardingFormFields — resume', () => {
  const seededState = (): OnboardingStateDto => ({
    userId: 'u1',
    status: OnboardingStatus.InProgress,
    currentTopic: null,
    completedTopics: 6,
    totalTopics: 6,
    isComplete: false,
    outstandingClarifications: [],
    primaryGoal: { goal: PrimaryGoal.RaceTraining, description: 'Marathon' },
    targetEvent: {
      eventName: 'City Marathon',
      distanceKm: 42.2,
      eventDateIso: '2026-10-01',
      targetFinishTimeIso: 'PT3H30M0S',
    },
    currentFitness: {
      typicalWeeklyKm: 50,
      longestRecentRunKm: 20,
      recentRaceDistanceKm: 21.1,
      recentRaceTimeIso: 'PT1H35M0S',
      description: 'Strong base',
    },
    weeklySchedule: {
      maxRunDaysPerWeek: 6,
      typicalSessionMinutes: 75,
      monday: true,
      tuesday: false,
      wednesday: true,
      thursday: false,
      friday: false,
      saturday: true,
      sunday: false,
      description: 'Flexible',
    },
    injuryHistory: {
      hasActiveInjury: true,
      activeInjuryDescription: 'Calf',
      pastInjurySummary: 'None',
    },
    preferences: {
      preferredUnits: PreferredUnits.Kilometers,
      preferTrail: false,
      comfortableWithIntensity: true,
      description: '',
    },
    currentPlanId: null,
  })

  it('hydrates populated km slots into form fields', () => {
    const fields = hydrateOnboardingFormFields(seededState(), PreferredUnits.Kilometers)
    expect(fields.goal).toBe(String(PrimaryGoal.RaceTraining))
    expect(fields.eventName).toBe('City Marathon')
    expect(fields.eventDistance).toBe('42.2')
    expect(fields.targetFinishTime).toBe('3:30:00')
    expect(fields.typicalWeekly).toBe('50.0')
    expect(fields.recentRaceTime).toBe('1:35:00')
    expect(fields.days).toEqual(['monday', 'wednesday', 'saturday'])
    expect(fields.hasActiveInjury).toBe(true)
    expect(fields.comfortableWithIntensity).toBe(true)
  })

  it('converts stored km into the runner display unit on resume', () => {
    const fields = hydrateOnboardingFormFields(seededState(), PreferredUnits.Miles)
    // 50 km ÷ 1.609344 ≈ 31.1 mi
    expect(fields.typicalWeekly).toBe('31.1')
  })

  it('leaves slots blank when the state has no captured answer', () => {
    const blank = hydrateOnboardingFormFields(
      { ...seededState(), primaryGoal: null, targetEvent: null, weeklySchedule: null },
      PreferredUnits.Kilometers,
    )
    expect(blank.goal).toBe('')
    expect(blank.eventName).toBe('')
    expect(blank.days).toEqual([])
    expect(blank.maxRunDays).toBe('')
  })
})
