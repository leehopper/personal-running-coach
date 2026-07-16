import { afterEach, describe, expect, it } from 'vitest'

import {
  formatDividerLabel,
  formatDurationSeconds,
  formatReceiptDate,
  formatTurnTime,
  groupTurnsByLocalDay,
} from './transcript-time.helpers'

describe('formatTurnTime', () => {
  it('formats a local time as 24h, zero-padded HH:MM', () => {
    const d = new Date(2026, 5, 29, 6, 58) // local: Jun 29 2026, 06:58
    expect(formatTurnTime(d.toISOString())).toBe('06:58')
  })

  it('zero-pads a single-digit hour and minute', () => {
    const d = new Date(2026, 5, 29, 1, 5)
    expect(formatTurnTime(d.toISOString())).toBe('01:05')
  })

  it('renders midnight as 00:00, not 24:00 or 12:00', () => {
    const d = new Date(2026, 5, 29, 0, 0)
    expect(formatTurnTime(d.toISOString())).toBe('00:00')
  })

  it('renders 23:59 (last minute of the day) correctly', () => {
    const d = new Date(2026, 5, 29, 23, 59)
    expect(formatTurnTime(d.toISOString())).toBe('23:59')
  })
})

describe('formatDividerLabel', () => {
  it('formats a past day as "{WEEKDAY} {MONTH} {DAY}", no TODAY prefix', () => {
    const dayDate = new Date(2026, 5, 30) // Tuesday Jun 30 2026
    const todayDate = new Date(2026, 6, 8) // Wed Jul 8 2026
    expect(formatDividerLabel(dayDate, todayDate)).toBe('TUE JUN 30')
  })

  it('prefixes TODAY — when dayDate is the same local calendar day as todayDate', () => {
    const dayDate = new Date(2026, 6, 8, 9, 0) // Wed Jul 8, 09:00
    const todayDate = new Date(2026, 6, 8, 23, 0) // same day, different time
    expect(formatDividerLabel(dayDate, todayDate)).toBe('TODAY — WED JUL 8')
  })

  it('does not prefix TODAY when the day differs even by one calendar day', () => {
    const dayDate = new Date(2026, 6, 7)
    const todayDate = new Date(2026, 6, 8)
    expect(formatDividerLabel(dayDate, todayDate)).toBe('TUE JUL 7')
  })
})

describe('groupTurnsByLocalDay', () => {
  const turn = (createdAt: string, turnId: string): { createdAt: string; turnId: string } => ({
    createdAt,
    turnId,
  })

  it('returns an empty array for an empty timeline', () => {
    expect(groupTurnsByLocalDay([])).toEqual([])
  })

  it('groups all turns from a single calendar day into one group', () => {
    const turns = [
      turn(new Date(2026, 5, 29, 9, 0).toISOString(), 't1'),
      turn(new Date(2026, 5, 29, 15, 0).toISOString(), 't2'),
    ]
    const groups = groupTurnsByLocalDay(turns)
    expect(groups).toHaveLength(1)
    expect(groups[0].turns).toHaveLength(2)
  })

  it('splits turns spanning two local calendar days into two oldest-first groups', () => {
    const turns = [
      turn(new Date(2026, 5, 29, 9, 0).toISOString(), 't1'),
      turn(new Date(2026, 5, 30, 9, 0).toISOString(), 't2'),
    ]
    const groups = groupTurnsByLocalDay(turns)
    expect(groups).toHaveLength(2)
    expect(groups[0].turns.map((t) => t.turnId)).toEqual(['t1'])
    expect(groups[1].turns.map((t) => t.turnId)).toEqual(['t2'])
  })

  it('re-opens a new group when a day recurs non-consecutively (buckets consecutive runs only)', () => {
    const turns = [
      turn(new Date(2026, 5, 29, 9, 0).toISOString(), 't1'),
      turn(new Date(2026, 5, 30, 9, 0).toISOString(), 't2'),
      turn(new Date(2026, 5, 29, 10, 0).toISOString(), 't3'),
    ]
    const groups = groupTurnsByLocalDay(turns)
    expect(groups).toHaveLength(3)
  })

  describe('TZ-offset local-midnight grouping', () => {
    const originalTz = process.env.TZ

    afterEach(() => {
      // `originalTz` is `undefined` when TZ was unset before this suite ran —
      // assigning `undefined` back would coerce it to the literal string
      // "undefined" (env vars are always strings), leaking a bogus TZ into
      // later tests in the same worker. Delete the key instead.
      if (originalTz === undefined) {
        delete process.env.TZ
      } else {
        process.env.TZ = originalTz
      }
    })

    it('groups a near-midnight UTC turn under its LOCAL day in a TZ far ahead of UTC', () => {
      process.env.TZ = 'Pacific/Kiritimati' // UTC+14
      // 23:30 UTC on Jul 8 is 13:30 local on Jul 9 under UTC+14 — must
      // bucket under Jul 9 locally, not Jul 8 (the UTC day).
      const turns = [turn('2026-07-08T23:30:00Z', 't1')]
      const groups = groupTurnsByLocalDay(turns)
      expect(groups).toHaveLength(1)
      expect(groups[0].label).toMatch(/JUL 9$/)
    })

    it('groups a near-midnight UTC turn under its LOCAL day in a TZ far behind UTC', () => {
      process.env.TZ = 'Pacific/Niue' // UTC-11
      // 00:30 UTC on Jul 8 is 13:30 local on Jul 7 under UTC-11 — must
      // bucket under Jul 7 locally, not Jul 8 (the UTC day).
      const turns = [turn('2026-07-08T00:30:00Z', 't1')]
      const groups = groupTurnsByLocalDay(turns)
      expect(groups).toHaveLength(1)
      expect(groups[0].label).toMatch(/JUL 7$/)
    })
  })
})

describe('formatDurationSeconds', () => {
  it('formats sub-minute durations as m:ss', () => {
    expect(formatDurationSeconds(45)).toBe('0:45')
  })

  it('formats a duration under an hour as m:ss', () => {
    expect(formatDurationSeconds(41 * 60)).toBe('41:00')
  })

  it('zero-pads the seconds component', () => {
    expect(formatDurationSeconds(61)).toBe('1:01')
  })

  it('formats durations at or beyond an hour as h:mm:ss', () => {
    expect(formatDurationSeconds(3661)).toBe('1:01:01')
  })

  it('rounds a fractional-second input defensively', () => {
    expect(formatDurationSeconds(45.6)).toBe('0:46')
  })

  it('formats exactly zero seconds', () => {
    expect(formatDurationSeconds(0)).toBe('0:00')
  })
})

describe('formatReceiptDate', () => {
  it('formats a pure YYYY-MM-DD calendar date as MONTH DAY, UTC-safe', () => {
    expect(formatReceiptDate('2026-07-08')).toBe('JUL 8')
  })

  it('returns null for an unparseable occurredOn instead of crashing or rendering the epoch', () => {
    expect(formatReceiptDate('not-a-date')).toBeNull()
  })

  it('returns null for an invalid calendar date (e.g. Feb 30)', () => {
    expect(formatReceiptDate('2026-02-30')).toBeNull()
  })
})
