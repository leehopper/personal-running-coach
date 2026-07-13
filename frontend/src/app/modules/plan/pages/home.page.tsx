import type { ReactElement } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
// Cross-module import — mirrors the codebase's existing bidirectional
// cross-module precedent (`before-after-diff.helpers.ts` already imports
// FROM `plan` INTO `coaching`; this is the same pattern in reverse).
import { CoachDigest } from '~/modules/coaching/components/coach-digest.component'
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
import { usePreferredUnits } from '~/modules/settings/hooks/use-preferred-units.hooks'

/**
 * Top-level container for the protected home route (`/`). Composes the
 * plan-render sections: the macro periodisation strip, today's prominent
 * workout (or rest-day variant), and the upcoming stack (rest-of-week +
 * meso summaries). The interactive coach chat now lives on its own
 * `/coach` route (spec § AD3) — home is the plan-render surface only,
 * navigated to and from via the `TabBar`.
 *
 * Behaviour:
 *   - On mount calls `getCurrentPlan` via the `usePlan` hook.
 *   - On 200 renders all three sections.
 *   - On 404 renders the "no plan yet" defensive state with a CTA back
 *     to `/onboarding` — should not appear in normal flow because the
 *     top-level route guard catches the unborn-plan case first
 *     (spec § Unit 4 R04.3).
 *   - On other errors renders a generic failure surface.
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
 * Renders the populated plan view. Extracted from `HomePage` so the
 * loading / error / not-found branches stay flat and readable.
 *
 * The `targetEvent`-null / general-fitness path renders a plan whose macro
 * `goalDescription` reflects the absence of a named race — no special-casing
 * required at the page level.
 */
const PlanLayout = ({ plan }: PlanLayoutProps): ReactElement => {
  const currentWeek = resolveCurrentWeek(plan)
  const currentWeekTemplate = findCurrentMesoWeek(plan, currentWeek)
  const currentWeekWorkouts = findCurrentWeekWorkouts(plan, currentWeek)
  const units = usePreferredUnits()

  return (
    <main
      className="mx-auto flex min-h-full w-full max-w-3xl flex-col gap-8 bg-background px-4 py-8"
      data-testid="home-page"
    >
      {plan.macro === null ? null : (
        <MacroPhaseStrip macro={plan.macro} currentWeek={currentWeek} />
      )}

      {currentWeekTemplate === undefined ? null : (
        <TodayCard currentWeek={currentWeekTemplate} workouts={currentWeekWorkouts} units={units} />
      )}

      <CoachDigest currentWeek={currentWeek} units={units} />

      <UpcomingList
        currentWeekWorkouts={currentWeekWorkouts}
        weeks={plan.mesoWeeks}
        currentWeek={currentWeek}
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
