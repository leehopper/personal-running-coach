import { useState, type ReactElement } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { useGetCurrentPlanQuery } from '~/api/plan.api'
import { RegeneratePlanDialog } from '~/modules/settings/components/regenerate-plan-dialog.component'
import { ThemeToggle } from '~/modules/settings/components/theme-toggle.component'

/**
 * `/settings` route surface. Renders the "Plan" section: current plan's
 * `generatedAt` timestamp, an optional previous-plan button when
 * `previousPlanId` is non-null (currently a placeholder with no destination
 * route; spec 13 § Unit 5 R05.6 tracks the destination route), and a
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
      className="mx-auto flex min-h-screen w-full max-w-3xl flex-col gap-8 bg-background px-4 py-8"
      data-testid="settings-page"
    >
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-foreground">Settings</h1>
        <Link to="/" className="text-sm font-medium text-muted-foreground hover:text-foreground">
          Back to plan
        </Link>
      </header>

      <Card className="gap-2 p-6" data-testid="settings-plan-section">
        <h2 className="text-lg font-semibold text-foreground">Plan</h2>

        <PlanSummary
          isLoading={isLoading}
          isError={isError}
          generatedAt={plan?.generatedAt}
          previousPlanId={plan?.previousPlanId ?? null}
        />

        <div className="mt-4">
          <Button
            type="button"
            onClick={() => setIsDialogOpen(true)}
            data-testid="settings-regenerate-button"
          >
            Regenerate plan
          </Button>
        </div>
      </Card>

      <Card className="gap-2 p-6" data-testid="settings-appearance-section">
        <h2 className="text-lg font-semibold text-foreground">Appearance</h2>
        <p className="text-sm text-muted-foreground">
          Choose how RunCoach looks. System follows your device setting.
        </p>
        <ThemeToggle />
      </Card>

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
      <p className="mt-2 text-sm text-muted-foreground" role="status" aria-live="polite">
        Loading plan details…
      </p>
    )
  }

  if (isError || generatedAt === undefined) {
    return (
      <p className="mt-2 text-sm text-muted-foreground">
        We could not load your current plan details right now.
      </p>
    )
  }

  return (
    <div className="mt-2 flex flex-col gap-1 text-sm text-muted-foreground">
      <p data-testid="settings-plan-generated-at">
        Current plan generated <time dateTime={generatedAt}>{formatGeneratedAt(generatedAt)}</time>
      </p>
      {previousPlanId !== null ? (
        <button
          type="button"
          className="text-left text-sm text-muted-foreground underline-offset-2 hover:underline"
          data-testid="settings-previous-plan-link"
        >
          View previous plan
        </button>
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
