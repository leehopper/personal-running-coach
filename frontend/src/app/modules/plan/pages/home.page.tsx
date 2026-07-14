import type { ReactElement } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useGetWorkoutLogHistoryInfiniteQuery } from '~/api/workout-log.api'
// Cross-module import — mirrors the codebase's existing bidirectional
// cross-module precedent (`before-after-diff.helpers.ts` already imports
// FROM `plan` INTO `coaching`; this is the same pattern in reverse).
import { CoachDigest } from '~/modules/coaching/components/coach-digest.component'
import {
  computePhaseRanges,
  findNextWorkoutAfter,
  findWorkoutForDay,
  getSlotForToday,
  labelForPhase,
  phaseForWeek,
  toUtcMidnight,
} from '~/modules/plan/components/plan-display.helpers'
import { TheBlock } from '~/modules/plan/components/the-block.component'
import { TheWeek } from '~/modules/plan/components/the-week.component'
import { isDateLogged } from '~/modules/plan/components/the-week.helpers'
import { TodayHeader } from '~/modules/plan/components/today-header.component'
import { UpNext } from '~/modules/plan/components/up-next.component'
import {
  WorkoutHero,
  type WorkoutHeroContent,
} from '~/modules/plan/components/workout-hero.component'
import {
  findCurrentMesoWeek,
  findCurrentWeekWorkouts,
  resolveCurrentWeek,
  usePlan,
} from '~/modules/plan/hooks/use-plan.hooks'
import type {
  MesoDaySlotDto,
  MicroWorkoutCardDto,
  PlanProjectionDto,
} from '~/modules/plan/models/plan.model'
import { usePreferredUnits } from '~/modules/settings/hooks/use-preferred-units.hooks'

/**
 * Top-level container for the protected home route (`/`). Composes the
 * Today screen's six sections in order: the header (wordmark + week/phase),
 * the workout hero (today's prominent workout, its rest-day variant, or a
 * graceful "not ready yet" state), THE WEEK (a 7-cell day grid + logged-km
 * progress), FROM YOUR COACH (the latest conversation exchange, a
 * tap-through to `/coach`), UP NEXT (the remainder of this week's
 * workouts), and THE BLOCK (the whole macro-cycle at a glance). The
 * interactive coach chat itself lives on its own `/coach` route (spec §
 * AD3) — home is the plan-render surface only, navigated to and from via
 * the `TabBar`.
 *
 * Behaviour:
 *   - On mount calls `getCurrentPlan` via the `usePlan` hook.
 *   - On 200 renders all six sections.
 *   - On 404 renders the "no plan yet" defensive state with a CTA back
 *     to `/onboarding` — should not appear in normal flow because the
 *     top-level route guard catches the unborn-plan case first
 *     (spec § Unit 4 R04.3).
 *   - On other errors renders a generic failure surface.
 *
 * Spec § Unit 4 R04.1, R04.3, R04.4, R04.5, R04.6, R04.7 (Slice 1); Slice 2
 * §1 PR-D for the six-section recomposition.
 */
export const HomePage = (): ReactElement => {
  const { plan, isLoading, isError, isNotFound } = usePlan()

  if (isLoading) {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex min-h-full items-center justify-center bg-background"
      >
        <span className="text-sm text-muted-foreground">Loading…</span>
      </div>
    )
  }

  if (isNotFound) {
    return <NoPlanYetState />
  }

  if (isError || plan === undefined) {
    return (
      <main
        className="flex min-h-full flex-col items-center justify-center gap-4 bg-background px-4"
        data-testid="home-page-error"
      >
        <h1 className="text-2xl font-semibold text-foreground">Something went wrong</h1>
        <p className="max-w-md text-center text-sm text-muted-foreground">
          We could not load your plan right now. Reload the page in a moment.
        </p>
      </main>
    )
  }

  return <PlanLayout plan={plan} />
}

interface PlanLayoutProps {
  plan: PlanProjectionDto
}

/**
 * Constructs `WorkoutHero`'s discriminated-union content (Slice 2 §2.1,
 * "C3 fix"; extended by the F1 fix's `logged` kind). `slot === undefined`
 * and `slot.slotType === 'Run'` with an absent `workout` (a data-integrity
 * edge case not reachable under a healthy plan) both fall back to
 * `unavailable` rather than lying about having a `workout`. Written as an
 * if/else chain, not nested ternaries (sonarjs/no-nested-conditional) —
 * same branches, same outcomes as the spec's pseudocode.
 *
 * `isTodayLogged` is derived by the CALLER from the exact same log join
 * `TheWeek`'s day-cell state uses (`isDateLogged`, `the-week.helpers.ts`) —
 * this function only threads the already-resolved boolean into the `run` vs.
 * `logged` choice, it never re-derives "is today logged" itself. That single
 * shared derivation is what makes it impossible for THE WEEK's today cell
 * and the hero to disagree about whether today's run has been logged (F1
 * fix — see `home.page.spec.tsx`'s cross-section agreement test).
 */
const resolveHeroContent = (
  slot: MesoDaySlotDto | undefined,
  workout: MicroWorkoutCardDto | undefined,
  nextWorkout: MicroWorkoutCardDto | undefined,
  isTodayLogged: boolean,
): WorkoutHeroContent => {
  if (slot === undefined) {
    return { kind: 'unavailable' }
  }
  if (slot.slotType !== 'Run') {
    return { kind: 'rest', slot, nextWorkout }
  }
  if (workout === undefined) {
    return { kind: 'unavailable' }
  }
  return isTodayLogged ? { kind: 'logged', slot, workout } : { kind: 'run', slot, workout }
}

/**
 * Renders the populated plan view. Extracted from `HomePage` so the
 * loading / error / not-found branches stay flat and readable.
 *
 * The `targetEvent`-null / general-fitness path renders a plan whose macro
 * `goalDescription` reflects the absence of a named race, and THE BLOCK
 * renders no goal chip — no special-casing required at the page level.
 *
 * This is the page's single `new Date()` call site (Slice 2 §2.1): `today`
 * (a raw wall-clock `Date`) fans into `todayUtc` (a UTC-midnight epoch)
 * exactly once, immediately, and every downstream section reads one or the
 * other — never a second `new Date()`.
 */
const PlanLayout = ({ plan }: PlanLayoutProps): ReactElement => {
  const today = new Date()
  const todayUtc = toUtcMidnight(today)
  // Explicit 2nd arg — do NOT rely on `resolveCurrentWeek`'s
  // `referenceDate = new Date()` default here, which would silently
  // reintroduce a second `new Date()` instantiation.
  const currentWeek = resolveCurrentWeek(plan, today)
  const currentWeekTemplate = findCurrentMesoWeek(plan, currentWeek)
  const currentWeekWorkouts = findCurrentWeekWorkouts(plan, currentWeek)
  const units = usePreferredUnits()

  // Sources THE WEEK's `logs` prop from the SAME cache entry `/history`
  // already uses (same hook, same `undefined` arg) — never calls
  // `fetchNextPage`, which would permanently append pages to that shared
  // cache entry and break the history page's own pagination (§2.5/C7).
  const { data: historyData } = useGetWorkoutLogHistoryInfiniteQuery(undefined)
  const logs = historyData?.pages.flatMap((page) => page.logs) ?? []

  // LOCAL getters only — `dayOfWeek` is a plain-local-training-date concept
  // (PlanCalendar.cs's docstring), must NOT be UTC-normalized.
  const slot: MesoDaySlotDto | undefined =
    currentWeekTemplate === undefined
      ? undefined
      : getSlotForToday(currentWeekTemplate, today.getDay())

  const workout: MicroWorkoutCardDto | undefined =
    slot?.slotType === 'Run' ? findWorkoutForDay(currentWeekWorkouts, today.getDay()) : undefined

  const nextWorkout = findNextWorkoutAfter(currentWeekWorkouts, today.getDay())

  // SAME predicate, SAME `logs` array, SAME `todayUtc` epoch `TheWeek` below
  // uses for its today cell's `done` state (F1 fix) — never a second,
  // parallel "is today logged" check re-derived here.
  const isTodayLogged = isDateLogged(logs, todayUtc)

  // Constructed HERE, where `slot`/`workout` are both in scope as local
  // consts — this is the one place that can decide what to do when they
  // disagree.
  const heroContent = resolveHeroContent(slot, workout, nextWorkout, isTodayLogged)

  const phaseLabel =
    plan.macro === null
      ? null
      : labelForPhase(
          phaseForWeek(computePhaseRanges(plan.macro.phases), currentWeek)?.phaseType ?? 'Base',
        )

  return (
    <main
      className="mx-auto flex min-h-full w-full max-w-3xl flex-col gap-8 bg-background px-4 py-8"
      data-testid="home-page"
    >
      <TodayHeader
        weekNumber={currentWeek}
        totalWeeks={plan.macro === null ? null : plan.macro.totalWeeks}
        phaseLabel={phaseLabel}
      />

      <WorkoutHero todayUtc={todayUtc} units={units} {...heroContent} />

      <TheWeek
        currentWeek={currentWeekTemplate}
        currentWeekNumber={currentWeek}
        planStartDate={plan.planStartDate}
        logs={logs}
        todayUtc={todayUtc}
        units={units}
      />

      <CoachDigest currentWeek={currentWeek} units={units} />

      <UpNext currentWeekWorkouts={currentWeekWorkouts} today={today} units={units} />

      <TheBlock
        macro={plan.macro}
        mesoWeeks={plan.mesoWeeks}
        currentWeek={currentWeek}
        targetEventDistanceKm={plan.targetEventDistanceKm}
        targetEventDate={plan.targetEventDate}
        units={units}
      />
    </main>
  )
}

/**
 * Defensive "no plan yet" state. The route guard normally redirects
 * incomplete-onboarding users to `/onboarding` before this surface ever
 * renders, but a 404 from `getCurrentPlan` (e.g. a server-side
 * race between projection commit and the read) still needs a humane
 * fallback.
 */
const NoPlanYetState = (): ReactElement => (
  <main
    className="flex min-h-full flex-col items-center justify-center gap-4 bg-background px-4"
    data-testid="home-page-no-plan"
  >
    <h1 className="text-2xl font-semibold text-foreground">No plan yet</h1>
    <p className="max-w-md text-center text-sm text-muted-foreground">
      Your training plan has not been generated. Finish onboarding to get started.
    </p>
    <Button asChild>
      <Link to="/onboarding">Go to onboarding</Link>
    </Button>
  </main>
)

export default HomePage
