import type { ReactNode } from 'react'

export interface OnboardingSectionProps {
  title: string
  description?: string
  children: ReactNode
  testId?: string
}

/**
 * A titled card grouping one onboarding topic's fields. Presentational shell
 * shared by every section so headings, spacing, and surface styling stay
 * consistent across the single-page form.
 */
export const OnboardingSection = ({
  title,
  description,
  children,
  testId,
}: OnboardingSectionProps) => (
  <section
    className="flex flex-col gap-4 rounded-lg border border-border bg-card p-4"
    data-testid={testId}
  >
    <div className="flex flex-col gap-1">
      <h2 className="text-base font-semibold text-foreground">{title}</h2>
      {description !== undefined ? (
        <p className="text-sm text-muted-foreground">{description}</p>
      ) : null}
    </div>
    {children}
  </section>
)
