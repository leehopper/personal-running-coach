import { afterEach, describe, expect, it } from 'vitest'

import { PreferredUnits } from '~/api/generated'

import { deriveDisplayPace, formatDateChipLabel, formatPaceRange } from './log-derivations.helpers'

const KM = PreferredUnits.Kilometers
const MI = PreferredUnits.Miles

describe('deriveDisplayPace', () => {
  it('derives a display pace in km from raw distance/duration strings', () => {
    // 5 km in 30 min -> 6:00/km.
    expect(deriveDisplayPace('5', '30', KM)).toBe('06:00/km')
  })

  it('derives a display pace in miles from raw distance/duration strings', () => {
    // 5 mi in 30 min -> 6:00/mi (the raw distance is interpreted in the
    // preferred unit, so the same "5" reads as miles here).
    expect(deriveDisplayPace('5', '30', MI)).toBe('06:00/mi')
  })

  it('trims surrounding whitespace before parsing either field', () => {
    expect(deriveDisplayPace(' 5 ', ' 30 ', KM)).toBe('06:00/km')
  })

  it('flips the rendered unit suffix when the preferred unit flips', () => {
    const km = deriveDisplayPace('5', '30', KM)
    const mi = deriveDisplayPace('5', '30', MI)
    expect(km).not.toBe(mi)
    expect(km).toMatch(/\/km$/)
    expect(mi).toMatch(/\/mi$/)
  })

  it.each([
    { distanceRaw: '0', label: 'zero distance' },
    { distanceRaw: '-5', label: 'negative distance' },
    { distanceRaw: 'abc', label: 'garbage distance' },
    { distanceRaw: '', label: 'empty distance' },
    { distanceRaw: '   ', label: 'whitespace-only distance' },
  ])('returns null for a $label (never NaN or 00:00)', ({ distanceRaw }) => {
    expect(deriveDisplayPace(distanceRaw, '30', KM)).toBeNull()
  })

  it.each([
    { durationRaw: '0', label: 'zero duration' },
    { durationRaw: '-30', label: 'negative duration' },
    { durationRaw: 'xyz', label: 'garbage duration' },
    { durationRaw: '', label: 'empty duration' },
    { durationRaw: '   ', label: 'whitespace-only duration' },
  ])('returns null for a $label (never NaN or 00:00)', ({ durationRaw }) => {
    expect(deriveDisplayPace('5', durationRaw, KM)).toBeNull()
  })

  it('never throws on non-numeric input', () => {
    expect(() => deriveDisplayPace('not-a-number', 'also-not', KM)).not.toThrow()
  })
})

describe('formatDateChipLabel', () => {
  it('formats an ISO date-only string as an uppercase "WEEKDAY, MON DAY" label', () => {
    // 2026-07-08 is a Wednesday.
    expect(formatDateChipLabel('2026-07-08')).toBe('WED, JUL 8')
  })

  it('formats a single-digit day with no leading zero', () => {
    // 2026-06-01 is a Monday.
    expect(formatDateChipLabel('2026-06-01')).toBe('MON, JUN 1')
  })

  it('formats a double-digit day', () => {
    // 2026-06-18 is a Thursday.
    expect(formatDateChipLabel('2026-06-18')).toBe('THU, JUN 18')
  })

  it.each([
    { occurredOn: '', label: 'empty string' },
    { occurredOn: 'not-a-date', label: 'non-numeric garbage' },
    { occurredOn: '2026-13-40', label: 'out-of-range month/day' },
  ])(
    'returns the "SELECT DATE" placeholder for $label (never throws, never a garbage label)',
    ({ occurredOn }) => {
      expect(formatDateChipLabel(occurredOn)).toBe('SELECT DATE')
      expect(() => formatDateChipLabel(occurredOn)).not.toThrow()
    },
  )

  describe('non-UTC timezone safety (DEC-076 local training dates)', () => {
    const originalTz = process.env.TZ

    afterEach(() => {
      // `originalTz` is `undefined` when TZ was unset before this suite ran —
      // assigning `undefined` back to `process.env.TZ` would coerce it to the
      // literal string "undefined" (env vars are always strings), leaking a
      // bogus TZ into later tests in the same worker. Delete the key instead
      // to restore the true "unset" state.
      if (originalTz === undefined) {
        delete process.env.TZ
      } else {
        process.env.TZ = originalTz
      }
    })

    it('reflects the LOCAL calendar day under a negative-offset process timezone', () => {
      // A UTC-parse (`new Date(iso)`) read back with local getters would roll
      // this date BACKWARD a day under a negative UTC offset — this function
      // must not do that; it parses the Y/M/D fields directly as local.
      process.env.TZ = 'America/Los_Angeles' // UTC-8 (winter) / UTC-7 (summer)
      expect(formatDateChipLabel('2026-07-08')).toBe('WED, JUL 8')
    })

    it('reflects the LOCAL calendar day under a positive-offset process timezone', () => {
      // Same bug, opposite direction: a UTC-parse read back locally would
      // roll this date FORWARD a day under a positive UTC offset.
      process.env.TZ = 'Pacific/Kiritimati' // UTC+14
      expect(formatDateChipLabel('2026-07-08')).toBe('WED, JUL 8')
    })
  })
})

describe('formatPaceRange', () => {
  it('renders the faster pace first, joined by an en dash, with a single unit suffix', () => {
    const result = formatPaceRange(240, 270, KM)
    expect(result).toBe('04:00–04:30/km')
    // En dash (U+2013), never a plain hyphen, and no surrounding spaces.
    expect(result).toContain('–')
    expect(result).not.toContain(' ')
    expect(result).not.toContain('-')
    // The unit suffix appears exactly once.
    expect(result.match(/\/km/g)).toHaveLength(1)
  })

  it('renders the miles path with the /mi suffix shown once', () => {
    const result = formatPaceRange(240, 270, MI)
    expect(result).toBe('06:26–07:15/mi')
    expect(result.match(/\/mi/g)).toHaveLength(1)
  })

  it('orders the faster pace first regardless of argument order', () => {
    expect(formatPaceRange(270, 240, KM)).toBe('04:00–04:30/km')
  })

  it('collapses to a single formatted pace when both bounds round to the same MM:SS', () => {
    expect(formatPaceRange(300, 300, KM)).toBe('05:00/km')
    expect(formatPaceRange(300.1, 299.9, KM)).toBe('05:00/km')
  })

  it('falls back to the valid side when the other bound is invalid', () => {
    expect(formatPaceRange(Number.NaN, 270, KM)).toBe('04:30/km')
    expect(formatPaceRange(240, Number.NaN, KM)).toBe('04:00/km')
  })

  it('falls back to an em-dash placeholder when both bounds are invalid', () => {
    expect(formatPaceRange(Number.NaN, Number.NaN, KM)).toBe('—')
  })
})
