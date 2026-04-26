import { useState, type ReactElement } from 'react'
import { Link } from 'react-router-dom'
import { useGetCurrentPlanQuery } from '~/api/plan.api'
import { RegeneratePlanDialog } from '~/modules/settings/components/regenerate-plan-dialog.component'

/**
 * `/settings` route surface — Slice 1's only Settings content is the "Plan"
 * section: current plan's `generatedAt` timestamp, an optional placeholder
 * link to the previous plan when `previousPlanId` is non-null (no-op target
 * in Slice 1; lands when adaptation history matters in Slice 3+), and a
 * "Regenerate plan" button that opens `RegeneratePlanDialog`.
 *
 * The page is gated by `<RequireAuth>` at the route table level (spec 13
 * § Unit 5 R05.6 / R05.7 / R05.8).
 */
export const SettingsPage = (): ReactElement => {
  const { data: plan, isLoading, isError } = useGetCurrentPlanQuery(undefined)
  const [isDialogOpen, setIsDialogOpen] = useState(false)

  return (
    <main
      className="mx-auto flex min-h-screen w-full max-w-3xl flex-col gap-8 bg-slate-50 px-4 py-8"
      data-testid="settings-page"
    >
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">Settings</h1>
        <Link to="/" className="text-sm font-medium text-slate-600 hover:text-slate-900">
          Back to plan
        </Link>
      </header>

      <section
        className="rounded-lg border border-slate-200 bg-white p-6"
        data-testid="settings-plan-section"
      >
        <h2 className="text-lg font-semibold text-slate-900">Plan</h2>

        <PlanSummary
          isLoading={isLoading}
          isError={isError}
          generatedAt={plan?.generatedAt}
          previousPlanId={plan?.previousPlanId ?? null}
        />

        <div className="mt-4">
          <button
            type="button"
            onClick={() => setIsDialogOpen(true)}
            className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white"
            data-testid="settings-regenerate-button"
          >
            Regenerate plan
          </button>
        </div>
      </section>

      <RegeneratePlanDialog isOpen={isDialogOpen} onClose={() => setIsDialogOpen(false)} />
    </main>
  )
}

interface PlanSummaryProps {
  isLoading: boolean
  isError: boolean
  generatedAt: string | undefined
  previousPlanId: string | null
}

/**
 * Renders the small "current plan was generated at …" summary block above
 * the Regenerate button. Extracted so the page-level component stays under
 * the 100-line guideline.
 */
const PlanSummary = ({
  isLoading,
  isError,
  generatedAt,
  previousPlanId,
}: PlanSummaryProps): ReactElement => {
  if (isLoading) {
    return (
      <p className="mt-2 text-sm text-slate-500" role="status" aria-live="polite">
        Loading plan details…
      </p>
    )
  }

  if (isError || generatedAt === undefined) {
    return (
      <p className="mt-2 text-sm text-slate-500">
        We could not load your current plan details right now.
      </p>
    )
  }

  return (
    <div className="mt-2 flex flex-col gap-1 text-sm text-slate-600">
      <p data-testid="settings-plan-generated-at">
        Current plan generated <time dateTime={generatedAt}>{formatGeneratedAt(generatedAt)}</time>
      </p>
      {previousPlanId !== null ? (
        // The link target is intentionally a no-op (`#`) in Slice 1; viewing
        // historical plans lands when adaptation history matters in Slice 3+.
        <a
          href="#"
          className="text-sm text-slate-500 underline-offset-2 hover:underline"
          data-testid="settings-previous-plan-link"
          onClick={(event) => event.preventDefault()}
        >
          View previous plan
        </a>
      ) : null}
    </div>
  )
}

/**
 * Render the ISO-8601 `generatedAt` string in the user's locale. Falls back
 * to the raw string if `Date` parsing fails (e.g. a future schema bump
 * sneaks a non-ISO value through).
 */
const formatGeneratedAt = (generatedAt: string): string => {
  const parsed = new Date(generatedAt)
  if (Number.isNaN(parsed.getTime())) {
    return generatedAt
  }
  return parsed.toLocaleString()
}

export default SettingsPage
