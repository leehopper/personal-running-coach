import type { ReactNode } from 'react'

import { cn } from '@/lib/utils'
import { SectionRule } from '~/modules/common/components/section-rule/section-rule.component'

export interface OnboardingSectionProps {
  title: string
  description?: string
  children: ReactNode
  testId?: string
  className?: string
}

/**
 * A numbered Alpine section opener grouping one onboarding topic's fields.
 * Presentational shell shared by every section so spacing stays consistent.
 */
export const OnboardingSection = ({
  title,
  description,
  children,
  testId,
  className,
}: OnboardingSectionProps) => (
  <section className={cn('flex flex-col gap-4', className)} data-testid={testId}>
    <SectionRule label={title} />
    {description !== undefined ? (
      <p className="t-body text-muted-foreground">{description}</p>
    ) : null}
    {children}
  </section>
)
