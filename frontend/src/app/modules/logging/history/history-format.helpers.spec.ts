import { afterEach, describe, expect, it } from 'vitest'

import { CompletionStatus, PreferredUnits } from '~/api/generated'
import {
  COMPLETION_STATUS_LABELS,
  formatHistoryDistanceKm,
  formatDuration,
  formatLedgerDayParts,
  formatLogDate,
  formatLogPace,
} from './history-format.helpers'

describe('formatHistoryDistanceKm', () => {
  it('renders metres as one-decimal kilometres by default', () => {
    expect(formatHistoryDistanceKm(5000)).toBe('5.0 km')
    expect(formatHistoryDistanceKm(1234)).toBe('1.2 km')
  })

  it('renders metres as one-decimal miles under a Miles preference', () => {
    // 5000 m / 1609.344 = 3.107... -> 3.1 mi
    expect(formatHistoryDistanceKm(5000, PreferredUnits.Miles)).toBe('3.1 mi')
  })

  it('returns null for a non-positive distance (e.g. a skipped run)', () => {
    expect(formatHistoryDistanceKm(0)).toBeNull()
    expect(formatHistoryDistanceKm(-10)).toBeNull()
    expect(formatHistoryDistanceKm(Number.NaN)).toBeNull()
  })
})

describe('formatDuration', () => {
  it('renders sub-hour durations as M:SS', () => {
    expect(formatDuration(1800)).toBe('30:00')
    expect(formatDuration(65)).toBe('1:05')
  })

  it('renders hour-plus durations as H:MM:SS', () => {
    expect(formatDuration(5400)).toBe('1:30:00')
    expect(formatDuration(3661)).toBe('1:01:01')
  })

  it('returns null for a non-positive duration', () => {
    expect(formatDuration(0)).toBeNull()
    expect(formatDuration(Number.NaN)).toBeNull()
  })
})

describe('formatLogPace', () => {
  it('derives average pace as MM:SS/km from distance + duration by default', () => {
    // 5 km in 1800 s -> 360 s/km -> 06:00/km.
    expect(formatLogPace(5000, 1800)).toBe('06:00/km')
  })

  it('derives average pace as MM:SS/mi under a Miles preference', () => {
    // 360 s/km * 1.609344 = 579.36 -> 579 -> 09:39/mi.
    expect(formatLogPace(5000, 1800, PreferredUnits.Miles)).toBe('09:39/mi')
  })

  it('returns null when distance or duration is non-positive', () => {
    expect(formatLogPace(0, 1800)).toBeNull()
    expect(formatLogPace(5000, 0)).toBeNull()
  })
})

describe('formatLogDate', () => {
  it('formats an ISO date-only string as a local weekday/month/day label', () => {
    // 2026-06-07 is a Sunday (local-calendar parse, never UTC-shifted).
    expect(formatLogDate('2026-06-07')).toBe('Sun, Jun 7')
    expect(formatLogDate('2026-06-01')).toBe('Mon, Jun 1')
  })
})

describe('formatLedgerDayParts', () => {
  it('splits an ISO date-only string into a zero-padded day + weekday (local-calendar parse)', () => {
    // 2026-07-08 is a Wednesday. Parsed via local Y/M/D construction (never
    // UTC) — see the "non-UTC timezone safety" suite below for the assertion
    // that actually forces a non-UTC process timezone.
    expect(formatLedgerDayParts('2026-07-08')).toEqual({ dayNum: '08', weekday: 'Wed' })
  })

  it('pads single-digit days', () => {
    expect(formatLedgerDayParts('2026-06-01')).toEqual({ dayNum: '01', weekday: 'Mon' })
  })

  describe('non-UTC timezone safety (DEC-076 local training dates)', () => {
    const originalTz = process.env.TZ

    afterEach(() => {
      // `originalTz` is `undefined` when TZ was unset before this suite ran —
      // assigning `undefined` back to `process.env.TZ` would coerce it to the
      // literal string "undefined" (env vars are always strings), leaking a
      // bogus TZ into later tests in the same worker. Delete the key instead
      // to restore the true "unset" state. Mirrors formatDateChipLabel's
      // established TZ-forcing pattern.
      if (originalTz === undefined) {
        delete process.env.TZ
      } else {
        process.env.TZ = originalTz
      }
    })

    it('reflects the LOCAL calendar day under a negative-offset process timezone', () => {
      // A UTC-parse (`new Date(occurredOn)`) read back with local getters
      // would roll this date BACKWARD a day under a negative UTC offset —
      // this function must not do that; it parses the Y/M/D fields directly
      // as local, so a regression to the UTC-ISO-parse form would fail here
      // even though it'd pass on a UTC CI runner.
      process.env.TZ = 'America/Los_Angeles' // UTC-8 (winter) / UTC-7 (summer)
      expect(formatLedgerDayParts('2026-07-08')).toEqual({ dayNum: '08', weekday: 'Wed' })
    })

    it('reflects the LOCAL calendar day under a positive-offset process timezone', () => {
      // Same bug, opposite direction: a UTC-parse read back locally would
      // roll this date FORWARD a day under a positive UTC offset.
      process.env.TZ = 'Pacific/Kiritimati' // UTC+14
      expect(formatLedgerDayParts('2026-07-08')).toEqual({ dayNum: '08', weekday: 'Wed' })
    })
  })
})

describe('COMPLETION_STATUS_LABELS', () => {
  it('maps each completion status to a user-facing label', () => {
    expect(COMPLETION_STATUS_LABELS[CompletionStatus.Complete]).toBe('Completed')
    expect(COMPLETION_STATUS_LABELS[CompletionStatus.Partial]).toBe('Partial')
    expect(COMPLETION_STATUS_LABELS[CompletionStatus.Skipped]).toBe('Skipped')
  })
})
