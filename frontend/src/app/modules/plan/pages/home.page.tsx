import type { ReactElement } from 'react'
import { Link } from 'react-router-dom'
import { MacroPhaseStrip } from '~/modules/plan/components/macro-phase-strip.component'
import { TodayCard } from '~/modules/plan/components/today-card.component'
import { UpcomingList } from '~/modules/plan/components/upcoming-list.component'
import {
  findCurrentMesoWeek,
  findCurrentWeekWorkouts,
  resolveCurrentWeek,
  usePlan,
} from '~/modules/plan/hooks/use-plan.hooks'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'

/**
 * Top-level container for the protected home route (`/`). Composes the
 * three Slice 1 plan-render sections: the macro periodisation strip,
 * today's prominent workout (or rest-day variant), and the upcoming
 * stack (rest-of-week + meso summaries).
 *
 * Behaviour:
 *   - On mount calls `getCurrentPlan` via the `usePlan` hook.
 *   - On 200 renders all three sections.
 *   - On 404 renders the "no plan yet" defensive state with a CTA back
 *     to `/onboarding` — should not appear in normal flow because the
 *     top-level route guard catches the unborn-plan case first
 *     (spec § Unit 4 R04.3).
 *   - On other errors renders a generic failure surface so the
 *     `npm run build` strict check has somewhere to land.
 *
 * Spec § Unit 4 R04.1, R04.3, R04.4, R04.5, R04.6, R04.7.
 */
export const HomePage = (): ReactElement => {
  const { plan, isLoading, isError, isNotFound } = usePlan()

  if (isLoading) {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex min-h-screen items-center justify-center bg-slate-50"
      >
        <span className="text-sm text-slate-500">Loading…</span>
      </div>
    )
  }

  if (isNotFound) {
    return <NoPlanYetState />
  }

  if (isError || plan === undefined) {
    return (
      <main
        className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4"
        data-testid="home-page-error"
      >
        <h1 className="text-2xl font-semibold text-slate-900">Something went wrong</h1>
        <p className="max-w-md text-center text-sm text-slate-600">
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
 * Renders the populated plan view. Extracted from `HomePage` so the
 * loading / error / not-found branches stay flat and readable.
 *
 * Slice 1 always populates `microWorkoutsByWeek[1]` and exactly four
 * `mesoWeeks`. The `targetEvent`-null / general-fitness path simply
 * renders a plan whose macro `goalDescription` reflects the absence of
 * a named race — no special-casing required at the page level.
 */
const PlanLayout = ({ plan }: PlanLayoutProps): ReactElement => {
  const currentWeek = resolveCurrentWeek(plan)
  const currentWeekTemplate = findCurrentMesoWeek(plan, currentWeek)
  const currentWeekWorkouts = findCurrentWeekWorkouts(plan, currentWeek)

  return (
    <main
      className="mx-auto flex min-h-screen w-full max-w-3xl flex-col gap-8 bg-slate-50 px-4 py-8"
      data-testid="home-page"
    >
      {plan.macro !== null ? (
        <MacroPhaseStrip macro={plan.macro} currentWeek={currentWeek} />
      ) : null}

      {currentWeekTemplate !== undefined ? (
        <TodayCard currentWeek={currentWeekTemplate} workouts={currentWeekWorkouts} />
      ) : null}

      <UpcomingList
        currentWeekWorkouts={currentWeekWorkouts}
        weeks={plan.mesoWeeks}
        currentWeek={currentWeek}
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
    className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4"
    data-testid="home-page-no-plan"
  >
    <h1 className="text-2xl font-semibold text-slate-900">No plan yet</h1>
    <p className="max-w-md text-center text-sm text-slate-600">
      Your training plan has not been generated. Finish onboarding to get started.
    </p>
    <Link
      to="/onboarding"
      className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white"
    >
      Go to onboarding
    </Link>
  </main>
)

export default HomePage
