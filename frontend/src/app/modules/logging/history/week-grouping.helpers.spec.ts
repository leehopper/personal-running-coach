import { describe, expect, it } from 'vitest'

import type { WorkoutLogDto } from '~/api/generated'
import { CompletionStatus } from '~/api/generated'
import { getIsoWeekStart, groupLogsByIsoWeek, parseIsoDateOnly } from './week-grouping.helpers'

const log = (occurredOn: string, workoutLogId = occurredOn): WorkoutLogDto => ({
  workoutLogId,
  occurredOn,
  distanceMeters: 5000,
  durationSeconds: 1800,
  completionStatus: CompletionStatus.Complete,
})

describe('parseIsoDateOnly', () => {
  it('parses YYYY-MM-DD as a local-calendar date (no UTC shift)', () => {
    const date = parseIsoDateOnly('2026-06-07')
    expect(date.getFullYear()).toBe(2026)
    expect(date.getMonth()).toBe(5) // June, 0-based
    expect(date.getDate()).toBe(7)
  })
})

describe('getIsoWeekStart', () => {
  it('returns the Monday of the ISO week', () => {
    // 2026-06-07 is a Sunday; its ISO week starts Monday 2026-06-01.
    expect(getIsoWeekStart(parseIsoDateOnly('2026-06-07')).getDate()).toBe(1)
    // 2026-06-01 is itself a Monday — its own week start.
    expect(getIsoWeekStart(parseIsoDateOnly('2026-06-01')).getDate()).toBe(1)
    // 2026-06-08 is the next Monday.
    expect(getIsoWeekStart(parseIsoDateOnly('2026-06-08')).getDate()).toBe(8)
  })
})

describe('groupLogsByIsoWeek', () => {
  it('buckets logs of one ISO week (Mon–Sun) under a single group', () => {
    const groups = groupLogsByIsoWeek([log('2026-06-07'), log('2026-06-03'), log('2026-06-01')])
    expect(groups).toHaveLength(1)
    expect(groups[0].label).toBe('Week of Jun 1, 2026')
    expect(groups[0].logs.map((l) => l.occurredOn)).toEqual([
      '2026-06-07',
      '2026-06-03',
      '2026-06-01',
    ])
  })

  it('returns week groups newest-first with newest-first logs within each', () => {
    const groups = groupLogsByIsoWeek([log('2026-06-08'), log('2026-06-07'), log('2026-05-31')])
    expect(groups.map((g) => g.label)).toEqual([
      'Week of Jun 8, 2026',
      'Week of Jun 1, 2026',
      'Week of May 25, 2026',
    ])
  })

  it('merges logs of the same ISO week split across two fetched pages into one group', () => {
    // Page 1 (newest) then page 2 (older), flattened — the boundary splits one
    // ISO week. Grouping over the merged flat list must NOT duplicate the header.
    const page1 = [log('2026-06-07'), log('2026-06-03')]
    const page2 = [log('2026-06-01'), log('2026-05-31')]
    const groups = groupLogsByIsoWeek([...page1, ...page2])

    expect(groups.map((g) => g.label)).toEqual(['Week of Jun 1, 2026', 'Week of May 25, 2026'])
    expect(groups[0].logs).toHaveLength(3) // 06-07, 06-03, 06-01 — single header
    expect(groups[1].logs).toHaveLength(1) // 05-31
  })

  it('sorts defensively so an out-of-order merged page still groups newest-first', () => {
    const groups = groupLogsByIsoWeek([log('2026-06-01'), log('2026-06-07'), log('2026-06-03')])
    expect(groups[0].logs.map((l) => l.occurredOn)).toEqual([
      '2026-06-07',
      '2026-06-03',
      '2026-06-01',
    ])
  })

  it('returns an empty array for no logs', () => {
    expect(groupLogsByIsoWeek([])).toEqual([])
  })
})
