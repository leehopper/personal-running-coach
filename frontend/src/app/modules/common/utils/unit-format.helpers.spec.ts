import { describe, expect, it } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import { formatHistoryDistanceKm as historyFormatDistanceKm } from '~/modules/logging/history/history-format.helpers'

import {
  distanceUnitLabel,
  formatDistanceKm,
  formatDistanceMeters,
  formatPaceRangeSecPerKm,
  formatPaceSecPerKm,
  METERS_PER_MILE,
  metresToPreferredDistance,
  preferredDistanceToMeters,
} from './unit-format.helpers'

const KM = PreferredUnits.Kilometers
const MI = PreferredUnits.Miles

describe('METERS_PER_MILE', () => {
  it('is the canonical statute-mile constant', () => {
    // Asserted via the string form to pin the exact value without a
    // floating-point equality comparison (sonarjs/no-floating-point-equality).
    expect(METERS_PER_MILE.toString()).toBe('1609.344')
  })
})

describe('distanceUnitLabel', () => {
  it('is the literal km/mi suffix for the preferred unit', () => {
    expect(distanceUnitLabel(KM)).toBe('km')
    expect(distanceUnitLabel(MI)).toBe('mi')
  })
})

describe('preferredDistanceToMeters — the single write conversion', () => {
  it('leaves the km path byte-identical to the prior `distanceKm * 1000`', () => {
    // Exact integers on the km path so a persisted 5 km stays 5000 m (no drift).
    expect(preferredDistanceToMeters(5, KM)).toBe(5000)
    expect(preferredDistanceToMeters(0, KM)).toBe(0)
    expect(preferredDistanceToMeters(42.195, KM)).toBe(42195)
  })

  it('interprets the value as miles and converts to metres on the miles path', () => {
    // 5 mi * 1609.344 = 8046.72 m. Asserted via the exact expression (string form
    // pins byte-exact equality without a floating-point `===`).
    expect(String(preferredDistanceToMeters(5, MI))).toBe(String(5 * METERS_PER_MILE))
    expect(String(preferredDistanceToMeters(3.1, MI))).toBe(String(3.1 * METERS_PER_MILE))
  })

  it('maps a zero distance to zero metres in either unit (skipped-run fallback)', () => {
    expect(preferredDistanceToMeters(0, KM)).toBe(0)
    expect(preferredDistanceToMeters(0, MI)).toBe(0)
  })
})

describe('metresToPreferredDistance — inverse for draft pre-fill', () => {
  it('divides metres into the preferred unit', () => {
    expect(metresToPreferredDistance(5000, KM)).toBe(5)
    expect(String(metresToPreferredDistance(8046.72, MI))).toBe(String(8046.72 / METERS_PER_MILE))
  })

  it('round-trips a preferred-unit distance through metres and back', () => {
    for (const units of [KM, MI]) {
      const metres = preferredDistanceToMeters(7.3, units)
      expect(metresToPreferredDistance(metres, units)).toBeCloseTo(7.3, 10)
    }
  })
})

describe('formatDistanceKm', () => {
  it.each([
    { km: 8, units: KM, expected: '8.0 km' },
    { km: 8, units: MI, expected: '5.0 mi' }, // 8 / 1.609344 = 4.97096 -> 5.0
    { km: 5, units: MI, expected: '3.1 mi' }, // 5 / 1.609344 = 3.10685 -> 3.1
    { km: 10, units: KM, expected: '10.0 km' },
    { km: 42.195, units: KM, expected: '42.2 km' },
    { km: 1, units: MI, expected: '0.6 mi' }, // 1 / 1.609344 = 0.6214 -> 0.6
  ])('formats $km km in $units as $expected', ({ km, units, expected }) => {
    expect(formatDistanceKm(km, units)).toBe(expected)
  })

  it('rounds miles to one decimal matching the km convention', () => {
    // 5 km -> 3.10685... mi, one-decimal like the existing `toFixed(1) km`.
    const actual = formatDistanceKm(5, MI)
    expect(actual).toBe('3.1 mi')
    expect(actual?.split(' ')[0].split('.')[1]).toHaveLength(1)
  })

  it.each([
    { km: 0, label: 'zero (skipped run)' },
    { km: -1, label: 'negative' },
    { km: Number.NaN, label: 'NaN' },
    { km: Number.POSITIVE_INFINITY, label: 'infinite' },
  ])('returns null for a $label distance in either unit', ({ km }) => {
    expect(formatDistanceKm(km, KM)).toBeNull()
    expect(formatDistanceKm(km, MI)).toBeNull()
  })
})

describe('formatDistanceMeters', () => {
  it.each([
    { meters: 8000, units: KM, expected: '8.0 km' },
    { meters: 8000, units: MI, expected: '5.0 mi' },
    { meters: 5000, units: MI, expected: '3.1 mi' },
  ])('formats $meters m in $units as $expected', ({ meters, units, expected }) => {
    expect(formatDistanceMeters(meters, units)).toBe(expected)
  })

  it.each([
    { meters: 0, label: 'zero' },
    { meters: -100, label: 'negative' },
    { meters: Number.NaN, label: 'NaN' },
  ])('returns null for a $label distance in either unit', ({ meters }) => {
    expect(formatDistanceMeters(meters, KM)).toBeNull()
    expect(formatDistanceMeters(meters, MI)).toBeNull()
  })
})

describe('formatDistanceMeters — km path agrees with the km-pinned history formatDistanceKm delegate', () => {
  // As of slice 4C-units the legacy `formatDistanceKm` delegates to this
  // module pinned to Kilometers, so this block now guards that the delegation
  // passes Kilometers (a Miles-pinned delegate would diverge here) rather than
  // independently re-verifying the km formatting — the literal-expectation tests
  // above pin the byte output, including the null (skipped-run) contract.
  it.each([8000, 5000, 42195, 1, 0, -100])(
    'matches history formatDistanceKm for %d metres',
    (meters) => {
      expect(formatDistanceMeters(meters, KM)).toBe(historyFormatDistanceKm(meters))
    },
  )
})

describe('formatPaceSecPerKm — km path', () => {
  it.each([
    { input: 0, expected: '00:00/km' },
    { input: 59, expected: '00:59/km' },
    { input: 60, expected: '01:00/km' },
    { input: 600, expected: '10:00/km' },
  ])('renders $input sec/km as $expected', ({ input, expected }) => {
    expect(formatPaceSecPerKm(input, KM)).toBe(expected)
  })

  it.each([
    { input: 300, expected: '05:00/km' },
    { input: 240, expected: '04:00/km' },
    { input: 99 * 60 + 59, expected: '99:59/km' },
  ])('renders $input sec/km as $expected', ({ input, expected }) => {
    expect(formatPaceSecPerKm(input, KM)).toBe(expected)
  })

  it('rounds fractional km inputs to the nearest whole second', () => {
    expect(formatPaceSecPerKm(330.4, KM)).toBe('05:30/km')
    expect(formatPaceSecPerKm(330.5, KM)).toBe('05:31/km')
  })
})

describe('formatPaceSecPerKm — miles path uses the net-new inverse conversion', () => {
  it.each([
    // secPerMile = secPerKm * 1.609344, rounded to whole seconds.
    { input: 300, expected: '08:03/mi' }, // 482.8032 -> 483 -> 8:03
    { input: 240, expected: '06:26/mi' }, // 386.24256 -> 386 -> 6:26
    { input: 330, expected: '08:51/mi' }, // 531.08352 -> 531 -> 8:51
  ])('converts $input sec/km to $expected', ({ input, expected }) => {
    expect(formatPaceSecPerKm(input, MI)).toBe(expected)
  })

  it('rounds the converted sec/mi to a whole second', () => {
    // 200 sec/km -> 321.8688 sec/mi -> 322 -> 05:22/mi
    expect(formatPaceSecPerKm(200, MI)).toBe('05:22/mi')
  })
})

describe('formatPaceSecPerKm — ceiling and invalid contract', () => {
  it('returns null for a pace above the km ceiling in km mode', () => {
    expect(formatPaceSecPerKm(99 * 60 + 60, KM)).toBeNull()
  })

  it('returns null for a pace above the mile-equivalent ceiling in miles mode', () => {
    // 6000 sec/km -> 9656.064 sec/mi > 5999 * 1.609344 ceiling.
    expect(formatPaceSecPerKm(6000, MI)).toBeNull()
  })

  it('accepts the km-ceiling pace in miles mode (mile-equivalent, not km ceiling)', () => {
    // 5999 sec/km -> 9654.45 sec/mi -> rounds to 9654, at/below the mile ceiling.
    expect(formatPaceSecPerKm(99 * 60 + 59, MI)).not.toBeNull()
  })

  it.each([
    { input: -1, label: 'negative' },
    { input: Number.NaN, label: 'NaN' },
    { input: Number.POSITIVE_INFINITY, label: 'infinite' },
    { input: Number.NEGATIVE_INFINITY, label: 'negative infinite' },
  ])('returns null for a $label pace in either unit', ({ input }) => {
    expect(formatPaceSecPerKm(input, KM)).toBeNull()
    expect(formatPaceSecPerKm(input, MI)).toBeNull()
  })
})

describe('formatPaceRangeSecPerKm — km path', () => {
  it('renders the faster pace first with a single /km suffix', () => {
    expect(formatPaceRangeSecPerKm(240, 330, KM)).toBe('04:00-05:30/km')
  })

  it('reorders so the faster pace is first when called slow then fast', () => {
    expect(formatPaceRangeSecPerKm(330, 240, KM)).toBe('04:00-05:30/km')
  })

  it('collapses to a single pace when both bounds round equal in km', () => {
    expect(formatPaceRangeSecPerKm(300, 300, KM)).toBe('05:00/km')
    expect(formatPaceRangeSecPerKm(300.1, 299.9, KM)).toBe('05:00/km')
  })
})

describe('formatPaceRangeSecPerKm — miles path', () => {
  it('renders the faster pace first with a single /mi suffix', () => {
    expect(formatPaceRangeSecPerKm(240, 330, MI)).toBe('06:26-08:51/mi')
  })

  it('reorders so the faster pace is first when called slow then fast', () => {
    expect(formatPaceRangeSecPerKm(330, 240, MI)).toBe('06:26-08:51/mi')
  })

  it('collapses to a single pace when both bounds round equal in miles', () => {
    expect(formatPaceRangeSecPerKm(300, 300, MI)).toBe('08:03/mi')
  })

  it('orders a fractional miles range faster-first regardless of argument order', () => {
    // 300.4 sec/km -> 483 sec/mi (08:03); 300.6 sec/km -> 484 sec/mi (08:04):
    // distinct in the display unit, so ordering is observable and must be stable
    // under argument order.
    expect(formatPaceRangeSecPerKm(300.4, 300.6, MI)).toBe('08:03-08:04/mi')
    expect(formatPaceRangeSecPerKm(300.6, 300.4, MI)).toBe('08:03-08:04/mi')
  })
})

describe('formatPaceRangeSecPerKm — invalid contract', () => {
  it.each([KM, MI])(
    'returns the valid side when the first bound is invalid (units %d)',
    (units) => {
      expect(formatPaceRangeSecPerKm(Number.NaN, 330, units)).toBe(formatPaceSecPerKm(330, units))
    },
  )

  it.each([KM, MI])(
    'returns the valid side when the second bound is invalid (units %d)',
    (units) => {
      expect(formatPaceRangeSecPerKm(240, -1, units)).toBe(formatPaceSecPerKm(240, units))
    },
  )

  it.each([KM, MI])('returns null when both bounds are invalid (units %d)', (units) => {
    expect(formatPaceRangeSecPerKm(Number.NaN, Number.POSITIVE_INFINITY, units)).toBeNull()
  })
})
