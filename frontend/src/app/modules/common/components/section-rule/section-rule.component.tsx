import type { ReactElement, ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Props for {@link SectionRule}. */
export interface SectionRuleProps {
  /**
   * Section label text. Rendered uppercase/tracked via `.t-section-label`;
   * pass sentence-case source copy — the caps transform is presentation
   * only, never bake uppercase into the string itself.
   */
  label: string
  /**
   * Heading element for the label. Defaults to `h2`; use `h3` when nesting
   * under another SectionRule-headed region.
   */
  as?: 'h2' | 'h3'
  /** Optional right-aligned slot (e.g. a chip or short summary) beside the label. */
  children?: ReactNode
  className?: string
}

/**
 * Section opener: a 2px `--rule` top border carrying a condensed, uppercase
 * section label, with an optional right-aligned slot for a chip or short
 * summary. Used to open the named regions of a screen (e.g. "THE WEEK",
 * "THE BLOCK", numbered onboarding/settings sections).
 */
export const SectionRule = ({
  label,
  as: Heading = 'h2',
  children,
  className,
}: SectionRuleProps): ReactElement => (
  <div
    data-testid="section-rule"
    className={cn('flex items-center justify-between gap-3 border-t-2 border-rule pt-2', className)}
  >
    <Heading className="t-section-label text-foreground">{label}</Heading>
    {children !== undefined ? (
      <div data-testid="section-rule-slot" className="shrink-0">
        {children}
      </div>
    ) : null}
  </div>
)
