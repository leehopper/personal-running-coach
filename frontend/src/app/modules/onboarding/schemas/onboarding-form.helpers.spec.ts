import { describe, expect, it } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import {
  convertDistanceInput,
  displayDistanceToKm,
  formatIsoDuration,
  isValidTimeInput,
  isoDurationToTimeInput,
  kmToDisplayDistance,
  parseIsoDuration,
  parseTimeInput,
  timeInputToIsoDuration,
} from './onboarding-form.helpers'

describe('distance conversion', () => {
  it('passes kilometres through unchanged when the unit is Kilometers', () => {
    expect(displayDistanceToKm(10, PreferredUnits.Kilometers)).toBe(10)
    expect(kmToDisplayDistance(10, PreferredUnits.Kilometers)).toBe(10)
  })

  it('converts a typed miles distance to canonical kilometres', () => {
    // 10 mi × 1.609344 = 16.09344 km
    expect(displayDistanceToKm(10, PreferredUnits.Miles)).toBeCloseTo(16.09344, 5)
  })

  it('round-trips km → miles → km without magnitude drift', () => {
    const displayed = kmToDisplayDistance(16.09344, PreferredUnits.Miles)
    expect(displayed).toBeCloseTo(10, 5)
    expect(displayDistanceToKm(displayed, PreferredUnits.Miles)).toBeCloseTo(16.09344, 5)
  })
})

describe('parseTimeInput', () => {
  it('parses MM:SS as minutes and seconds with zero hours', () => {
    expect(parseTimeInput('45:30')).toEqual({ hours: 0, minutes: 45, seconds: 30 })
  })

  it('parses H:MM:SS', () => {
    expect(parseTimeInput('1:45:30')).toEqual({ hours: 1, minutes: 45, seconds: 30 })
  })

  it('accepts a two-part entry with minutes above 59', () => {
    expect(parseTimeInput('90:00')).toEqual({ hours: 0, minutes: 90, seconds: 0 })
  })

  it('rejects seconds above 59', () => {
    expect(parseTimeInput('45:60')).toBeNull()
  })

  it('rejects three-part minutes above 59', () => {
    expect(parseTimeInput('1:75:00')).toBeNull()
  })

  it('rejects non-numeric, blank, and malformed shapes', () => {
    expect(parseTimeInput('abc')).toBeNull()
    expect(parseTimeInput('')).toBeNull()
    expect(parseTimeInput('1:2:3:4')).toBeNull()
    expect(parseTimeInput('12')).toBeNull()
    expect(parseTimeInput('1:-5')).toBeNull()
  })
})

describe('isValidTimeInput', () => {
  it('treats blank as valid (the field is optional)', () => {
    expect(isValidTimeInput('')).toBe(true)
    expect(isValidTimeInput('   ')).toBe(true)
  })

  it('accepts well-formed times and rejects garbage', () => {
    expect(isValidTimeInput('45:30')).toBe(true)
    expect(isValidTimeInput('1:45:30')).toBe(true)
    expect(isValidTimeInput('45:99')).toBe(false)
    expect(isValidTimeInput('nope')).toBe(false)
  })
})

describe('ISO duration formatting', () => {
  it('formats components as an xsd:duration', () => {
    expect(formatIsoDuration({ hours: 1, minutes: 45, seconds: 30 })).toBe('PT1H45M30S')
    expect(formatIsoDuration({ hours: 0, minutes: 45, seconds: 0 })).toBe('PT0H45M0S')
  })

  it('maps a runner time to ISO, blank to null', () => {
    expect(timeInputToIsoDuration('1:45:30')).toBe('PT1H45M30S')
    expect(timeInputToIsoDuration('45:30')).toBe('PT0H45M30S')
    expect(timeInputToIsoDuration('')).toBeNull()
    expect(timeInputToIsoDuration('garbage')).toBeNull()
  })
})

describe('ISO duration parsing (resume hydration)', () => {
  it('parses full and partial ISO durations', () => {
    expect(parseIsoDuration('PT1H45M30S')).toEqual({ hours: 1, minutes: 45, seconds: 30 })
    expect(parseIsoDuration('PT45M30S')).toEqual({ hours: 0, minutes: 45, seconds: 30 })
    expect(parseIsoDuration('PT90M')).toEqual({ hours: 1, minutes: 30, seconds: 0 })
  })

  it('returns null for absent or unparseable values', () => {
    expect(parseIsoDuration(null)).toBeNull()
    expect(parseIsoDuration(undefined)).toBeNull()
    expect(parseIsoDuration('PT')).toBeNull()
    expect(parseIsoDuration('nonsense')).toBeNull()
  })

  it('formats an ISO duration back to a clock string', () => {
    expect(isoDurationToTimeInput('PT1H45M30S')).toBe('1:45:30')
    expect(isoDurationToTimeInput('PT45M30S')).toBe('45:30')
    expect(isoDurationToTimeInput('PT0H5M0S')).toBe('5:00')
    expect(isoDurationToTimeInput(null)).toBe('')
  })
})

describe('convertDistanceInput (units change)', () => {
  it('keeps a blank entry blank', () => {
    expect(convertDistanceInput('', PreferredUnits.Kilometers, PreferredUnits.Miles)).toBe('')
  })

  it('returns the value unchanged when the unit is the same', () => {
    expect(convertDistanceInput('10', PreferredUnits.Miles, PreferredUnits.Miles)).toBe('10')
  })

  it('converts km → miles preserving the physical distance', () => {
    // 16.1 km ÷ 1.609344 ≈ 10.0 mi
    expect(convertDistanceInput('16.1', PreferredUnits.Kilometers, PreferredUnits.Miles)).toBe(
      '10.0',
    )
  })

  it('converts miles → km preserving the physical distance', () => {
    expect(convertDistanceInput('10', PreferredUnits.Miles, PreferredUnits.Kilometers)).toBe('16.1')
  })

  it('leaves an unparseable entry untouched', () => {
    expect(convertDistanceInput('abc', PreferredUnits.Kilometers, PreferredUnits.Miles)).toBe('abc')
  })
})
