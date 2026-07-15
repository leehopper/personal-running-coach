// Transcript timestamp + local-day-grouping helpers (spec §4.1) — LOCAL
// wall-clock. A message shows the time it was sent in
// the READER's timezone, not the server's or the plan's anchor timezone, so
// every getter here is a local (`getHours`/`getDay`/`getMonth`/`getDate`)
// read — never a `getUTC*` one.
//
// `formatDurationSeconds` (§4.3) is the receipt formatter's home file per
// the spec.

const WEEKDAY_ABBR = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'] as const

const MONTH_ABBR = [
  'JAN',
  'FEB',
  'MAR',
  'APR',
  'MAY',
  'JUN',
  'JUL',
  'AUG',
  'SEP',
  'OCT',
  'NOV',
  'DEC',
] as const

const pad2 = (value: number): string => value.toString().padStart(2, '0')

/**
 * Formats an ISO `createdAt` timestamp as a 24-hour, zero-padded local
 * `HH:MM` (e.g. `"06:58"`). Reads the reader's local wall-clock via
 * `getHours`/`getMinutes` — never `getUTCHours`/`getUTCMinutes`.
 */
export function formatTurnTime(createdAtIso: string): string {
  const d = new Date(createdAtIso)
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}`
}

/**
 * Formats a local-calendar-day divider label, e.g. `"TUE JUN 30"`, or
 * `"TODAY — TUE JUN 30"` when `dayDate` falls on the same local calendar
 * day as `todayDate`. Both dates are read via LOCAL getters
 * (`getFullYear`/`getMonth`/`getDate`/`getDay`) — this is the reader's
 * wall-clock day, not a UTC one.
 */
export function formatDividerLabel(dayDate: Date, todayDate: Date): string {
  const label = `${WEEKDAY_ABBR[dayDate.getDay()]} ${MONTH_ABBR[dayDate.getMonth()]} ${dayDate.getDate()}`
  const isToday =
    dayDate.getFullYear() === todayDate.getFullYear() &&
    dayDate.getMonth() === todayDate.getMonth() &&
    dayDate.getDate() === todayDate.getDate()
  return isToday ? `TODAY — ${label}` : label
}

/** One local-calendar-day group of turns, oldest-first, produced by {@link groupTurnsByLocalDay}. */
export interface LocalDayGroup<T> {
  label: string
  turns: T[]
}

const localDayKey = (date: Date): string =>
  `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`

/**
 * Groups an oldest-first array of turns into consecutive local-calendar-day
 * buckets, one group per distinct local day, each labelled via
 * {@link formatDividerLabel} against "now" at call time. Turns are grouped
 * by their `createdAt`'s LOCAL calendar day — a turn near local midnight
 * under a non-UTC `TZ` groups under its local day, not the UTC day (the
 * turn's `createdAt` is a UTC instant on the wire; only the local
 * interpretation of it decides the bucket).
 *
 * `turns` must already be oldest-first (the composed timeline's own
 * order) — this function does not sort, only buckets consecutive runs.
 */
export function groupTurnsByLocalDay<T extends { createdAt: string }>(
  turns: readonly T[],
): LocalDayGroup<T>[] {
  const today = new Date()
  const groups: LocalDayGroup<T>[] = []
  let currentKey: string | null = null

  for (const turn of turns) {
    const date = new Date(turn.createdAt)
    const key = localDayKey(date)
    if (key !== currentKey) {
      groups.push({ label: formatDividerLabel(date, today), turns: [turn] })
      currentKey = key
    } else {
      groups[groups.length - 1].turns.push(turn)
    }
  }

  return groups
}

/**
 * Formats a duration in seconds as `m:ss`, or `h:mm:ss` once the total
 * reaches an hour. `s` is the wire's `durationSeconds` (a `double`) — a
 * fractional part is not expected in practice, but `Math.round` guards
 * defensively. Local `pad2` reuse only.
 */
export function formatDurationSeconds(s: number): string {
  const total = Math.round(s)
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const sec = total % 60
  return h >= 1 ? `${h}:${pad2(m)}:${pad2(sec)}` : `${m}:${pad2(sec)}`
}
