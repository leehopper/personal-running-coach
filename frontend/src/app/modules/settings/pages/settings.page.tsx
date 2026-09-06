import { useState, type ReactElement } from 'react'
import { Button } from '@/components/ui/button'
import { useGetCurrentPlanQuery } from '~/api/plan.api'
import { MonoLabel } from '~/modules/common/components/mono-label/mono-label.component'
import { SectionRule } from '~/modules/common/components/section-rule/section-rule.component'
import { useAuth, useSignOut } from '~/modules/auth/hooks/auth.hooks'
import type { PlanProjectionDto } from '~/modules/plan/models/plan.model'
import { RegeneratePlanDialog } from '~/modules/settings/components/regenerate-plan-dialog.component'
import { ThemeToggle } from '~/modules/settings/components/theme-toggle.component'
import { UnitsToggle } from '~/modules/settings/components/units-toggle.component'

/**
 * `/settings` route surface. The route guard supplies the authenticated user;
 * this page owns the settings sections and the regenerate dialog trigger.
 */
export const SettingsPage = (): ReactElement => {
  const { data: plan, isLoading, isError } = useGetCurrentPlanQuery(undefined)
  const { user } = useAuth()
  const { signOut, isSigningOut } = useSignOut()
  const [isDialogOpen, setIsDialogOpen] = useState(false)

  const handleSignOut = (): void => {
    const signOutRequest = async (): Promise<void> => {
      await signOut()
    }
    void signOutRequest()
  }

  return (
    <main
      className="mx-auto flex min-h-full w-full max-w-md flex-col gap-5 bg-background px-[22px] py-8"
      data-testid="settings-page"
    >
      <header className="flex flex-col gap-2.5">
        <h1 className="t-screen-title text-foreground">Settings</h1>
        <div className="h-0.5 w-full bg-rule" />
      </header>

      <section className="flex flex-col gap-3" data-testid="settings-plan-section">
        <SectionRule label="The plan" />
        <div className="flex flex-col gap-0.5">
          <MonoLabel tone="muted">Current plan</MonoLabel>
          <PlanSummary plan={plan} isLoading={isLoading} isError={isError} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Button
            type="button"
            onClick={() => setIsDialogOpen(true)}
            variant="outline"
            className="h-12 border-clay-text text-clay-text dark:border-clay-text dark:bg-background active:text-secondary-foreground"
            data-testid="settings-regenerate-button"
          >
            Regenerate plan
          </Button>
          <p className="t-data-label text-muted-foreground">
            Replaces your current plan. The coach starts fresh from your log book.
          </p>
        </div>
      </section>

      <section className="flex flex-col gap-3" data-testid="settings-appearance-section">
        <SectionRule label="Appearance" />
        <ThemeToggle />
      </section>

      <section className="flex flex-col gap-3" data-testid="settings-units-section">
        <SectionRule label="Units" />
        <UnitsToggle />
      </section>

      <section className="flex flex-col gap-3" data-testid="settings-account-section">
        <SectionRule label="Account" />
        <div className="flex items-center justify-between gap-4">
          <div className="flex min-w-0 flex-col gap-0.5">
            <MonoLabel tone="muted">Signed in as</MonoLabel>
            <p data-testid="settings-account-email" className="t-body text-foreground">
              {user?.email ?? ''}
            </p>
          </div>
          <Button
            type="button"
            variant="outline"
            className="min-h-11"
            data-testid="settings-sign-out-button"
            disabled={isSigningOut}
            onClick={handleSignOut}
          >
            Sign out
          </Button>
        </div>
      </section>

      <RegeneratePlanDialog isOpen={isDialogOpen} onClose={() => setIsDialogOpen(false)} />

      <footer>
        <p
          data-testid="settings-version"
          className="t-data-label pt-2 text-center text-muted-foreground"
        >
          Split {import.meta.env.VITE_APP_VERSION} {'\u2014'} MVP
        </p>
      </footer>
    </main>
  )
}

interface PlanSummaryProps {
  plan: PlanProjectionDto | undefined
  isLoading: boolean
  isError: boolean
}

/**
 * Renders the small "current plan was generated at …" summary block above
 * the Regenerate button. Extracted so the page-level component stays under
 * the 100-line guideline.
 */
const PlanSummary = ({ plan, isLoading, isError }: PlanSummaryProps): ReactElement => {
  if (isLoading) {
    return (
      <p className="mt-2 text-sm text-muted-foreground" role="status" aria-live="polite">
        Loading plan details{'\u2026'}
      </p>
    )
  }

  if (isError || plan?.generatedAt === undefined) {
    return (
      <p className="mt-2 text-sm text-muted-foreground">
        We could not load your current plan details right now.
      </p>
    )
  }

  const { generatedAt, macro } = plan
  const segments = [
    <span key="generated">
      Generated <time dateTime={generatedAt}>{formatGeneratedAt(generatedAt)}</time>
    </span>,
    hasMeaningfulValue(macro?.totalWeeks) ? `${macro.totalWeeks} weeks` : null,
    hasMeaningfulValue(macro?.goalDescription) ? macro.goalDescription : null,
  ].filter((segment): segment is ReactElement | string => segment !== null)

  return (
    <p data-testid="settings-plan-generated-at" className="t-body text-muted-foreground">
      {segments.map((segment, index) => (
        <span key={typeof segment === 'string' ? segment : 'generated-segment'}>
          {index > 0 ? ' \u00B7 ' : null}
          {segment}
        </span>
      ))}
    </p>
  )
}

const hasMeaningfulValue = (value: number | string | null | undefined): value is number | string =>
  typeof value === 'number' || (typeof value === 'string' && value.trim().length > 0)

/**
 * Render the ISO-8601 `generatedAt` string in a stable en-US calendar format.
 * Falls back to the raw string if `Date` parsing fails.
 */
const formatGeneratedAt = (generatedAt: string): string => {
  const parsed = new Date(generatedAt)
  if (Number.isNaN(parsed.getTime())) {
    return generatedAt
  }
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(parsed)
}

export default SettingsPage
