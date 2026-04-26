import type { ReactElement } from 'react'
import type {
  MesoDaySlotDto,
  MesoWeekTemplate,
  MicroWorkoutCard as MicroWorkoutDto,
} from '~/modules/plan/models/plan.model'
import {
  DAY_OF_WEEK_LABELS,
  DAY_SLOT_KEYS,
  findNextWorkoutAfter,
  findWorkoutForDay,
} from './plan-display.helpers'
import { MicroWorkoutCard } from './micro-workout-card.component'

export interface TodayCardProps {
  /** Current week's meso template (used to read the day-slot kind for today). */
  currentWeek: MesoWeekTemplate
  /** Detailed workouts for the current week from `microWorkoutsByWeek`. */
  workouts: readonly MicroWorkoutDto[]
  /**
   * Local date used to derive today's day-of-week index. Defaulting here
   * keeps callers test-friendly: pass a fixed `Date` from a fixture for a
   * deterministic snapshot, or omit in production to use `new Date()`.
   */
  today?: Date
  className?: string
}

const dayOfWeekIndex = (date: Date): number => date.getDay()

const getSlotForToday = (week: MesoWeekTemplate, dayIndex: number): MesoDaySlotDto =>
  week[DAY_SLOT_KEYS[dayIndex]]

/**
 * Prominent "today" card. Reads the current day-of-week from `today` (or
 * `new Date()` when omitted), looks up the matching slot on the current
 * `MesoWeekTemplate`, and either:
 *
 * - renders the detailed `MicroWorkoutCard` for the day, when the slot
 *   resolves to a `Run` with a matching workout in `microWorkoutsByWeek`;
 * - renders a "rest day" variant otherwise, calling out the day-of-week
 *   for the next scheduled workout (per spec § Unit 4 R04.7).
 */
export const TodayCard = ({
  currentWeek,
  workouts,
  today,
  className,
}: TodayCardProps): ReactElement => {
  const date = today ?? new Date()
  const todayIndex = dayOfWeekIndex(date)
  const todayLabel = DAY_OF_WEEK_LABELS[todayIndex] ?? ''
  const slot = getSlotForToday(currentWeek, todayIndex)
  const todaysWorkout =
    slot.slotType === 'Run' ? findWorkoutForDay(workouts, todayIndex) : undefined

  if (todaysWorkout !== undefined) {
    return (
      <section
        aria-label="Today's workout"
        data-testid="today-card"
        data-variant="workout"
        className={`flex flex-col gap-2 ${className ?? ''}`}
      >
        <header className="flex items-baseline justify-between">
          <h2 className="text-lg font-semibold text-slate-900">Today</h2>
          <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {todayLabel}
          </span>
        </header>
        <MicroWorkoutCard workout={todaysWorkout} emphasized={true} />
      </section>
    )
  }

  const nextWorkout = findNextWorkoutAfter(workouts, todayIndex)
  const nextDayLabel = nextWorkout !== undefined ? DAY_OF_WEEK_LABELS[nextWorkout.dayOfWeek] : null

  return (
    <section
      aria-label="Today's workout"
      data-testid="today-card"
      data-variant="rest"
      className={`flex flex-col gap-3 rounded-lg border-2 border-slate-200 bg-slate-50 p-5 ${className ?? ''}`}
    >
      <header className="flex items-baseline justify-between">
        <h2 className="text-lg font-semibold text-slate-900">Today</h2>
        <span className="text-xs font-medium uppercase tracking-wide text-slate-500">
          {todayLabel}
        </span>
      </header>
      <p className="text-base font-semibold text-slate-700">Rest day — recover well.</p>
      {nextDayLabel !== null && nextWorkout !== undefined ? (
        <p data-testid="today-card-next-workout" className="text-sm leading-snug text-slate-600">
          Next workout: <strong className="font-semibold">{nextDayLabel}</strong> ·{' '}
          {nextWorkout.title}
        </p>
      ) : null}
      {slot.notes.trim().length > 0 ? (
        <p className="text-xs leading-snug text-slate-500">{slot.notes}</p>
      ) : null}
    </section>
  )
}
