import { describe, expect, it } from 'vitest'

import { CompletionStatus, PreferredUnits } from '~/api/generated'
import {
  COMPLETION_STATUS_LABELS,
  formatHistoryDistanceKm,
  formatDuration,
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

describe('COMPLETION_STATUS_LABELS', () => {
  it('maps each completion status to a user-facing label', () => {
    expect(COMPLETION_STATUS_LABELS[CompletionStatus.Complete]).toBe('Completed')
    expect(COMPLETION_STATUS_LABELS[CompletionStatus.Partial]).toBe('Partial')
    expect(COMPLETION_STATUS_LABELS[CompletionStatus.Skipped]).toBe('Skipped')
  })
})
