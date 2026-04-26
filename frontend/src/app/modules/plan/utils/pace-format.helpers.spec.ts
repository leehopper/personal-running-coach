import { describe, expect, it } from 'vitest'
import { formatPacePerKm, formatPaceRangePerKm } from './pace-format.helpers'

describe('formatPacePerKm', () => {
  it.each([
    { input: 0, expected: '00:00/km' },
    { input: 59, expected: '00:59/km' },
    { input: 60, expected: '01:00/km' },
    { input: 330, expected: '05:30/km' }, // 5:30/km easy pace
    { input: 240, expected: '04:00/km' }, // 4:00/km threshold
    { input: 600, expected: '10:00/km' },
    { input: 99 * 60 + 59, expected: '99:59/km' },
  ])('formats $input seconds as $expected', ({ input, expected }) => {
    const actual = formatPacePerKm(input)
    expect(actual).toBe(expected)
  })

  it('rounds fractional inputs to the nearest whole second', () => {
    const actual = formatPacePerKm(330.4)
    expect(actual).toBe('05:30/km')
  })

  it('rounds half-second fractional inputs upward', () => {
    const actual = formatPacePerKm(330.5)
    expect(actual).toBe('05:31/km')
  })

  it('round-trips through parsing without drift', () => {
    const original = 5 * 60 + 30
    const formatted = formatPacePerKm(original)
    expect(formatted).toBe('05:30/km')
    if (formatted === null) {
      throw new Error('formatter returned null on a valid input')
    }
    const [mm, ss] = formatted.replace('/km', '').split(':').map(Number)
    expect(mm * 60 + ss).toBe(original)
  })

  it.each([
    { input: -1, label: 'negative' },
    { input: Number.NaN, label: 'NaN' },
    { input: Number.POSITIVE_INFINITY, label: 'infinite' },
    { input: Number.NEGATIVE_INFINITY, label: 'negative infinite' },
    { input: 99 * 60 + 60, label: 'above ceiling' },
  ])('returns null for $label inputs', ({ input }) => {
    const actual = formatPacePerKm(input)
    expect(actual).toBeNull()
  })
})

describe('formatPaceRangePerKm', () => {
  it('renders fast pace before slow with single /km suffix', () => {
    // Threshold (faster) and easy (slower) — faster value comes first.
    const actual = formatPaceRangePerKm(240, 330)
    expect(actual).toBe('04:00-05:30/km')
  })

  it('reorders to put the faster pace first when called with slow then fast', () => {
    // Reversed argument order — caller still gets fast-first output.
    const actual = formatPaceRangePerKm(330, 240)
    expect(actual).toBe('04:00-05:30/km')
  })

  it('collapses to a single pace when the bounds round to the same MM:SS', () => {
    const actual = formatPaceRangePerKm(300, 300)
    expect(actual).toBe('05:00/km')
  })

  it('collapses to a single pace when fractional inputs round to the same MM:SS', () => {
    const actual = formatPaceRangePerKm(300.1, 299.9)
    expect(actual).toBe('05:00/km')
  })

  it('returns the valid side when one bound is invalid', () => {
    const actual = formatPaceRangePerKm(Number.NaN, 330)
    expect(actual).toBe('05:30/km')
  })

  it('returns the valid side when the other bound is invalid', () => {
    const actual = formatPaceRangePerKm(240, -1)
    expect(actual).toBe('04:00/km')
  })

  it('returns null when both bounds are invalid', () => {
    const actual = formatPaceRangePerKm(Number.NaN, Number.POSITIVE_INFINITY)
    expect(actual).toBeNull()
  })
})
