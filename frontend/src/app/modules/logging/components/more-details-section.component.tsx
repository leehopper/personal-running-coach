import { useState } from 'react'
import { ChevronDownIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { FORM_METRIC_KEYS, metricFieldLabel } from '~/modules/logging/metric-meta'
import type { WorkoutLogFormControl } from '~/modules/logging/schemas/workout-log-form.schema'
import { LogNumericField } from './log-numeric-field.component'

export interface MoreDetailsSectionProps {
  control: WorkoutLogFormControl
}

/**
 * The optional-metrics section of the log form, behind a "More details"
 * collapsible (default closed, DEC-075). The metric fields are ALWAYS part of
 * the form (seeded in `defaultValues`); collapsing them only hides the DOM —
 * `shouldUnregister: false` retains their values, so a filled-then-collapsed
 * metric still submits. Each field maps to a canonical `metrics` bag key
 * (`FORM_METRIC_KEYS`) with its label from the shared metric-meta map.
 */
export const MoreDetailsSection = ({ control }: MoreDetailsSectionProps) => {
  const [open, setOpen] = useState(false)

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="flex flex-col gap-3">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          className="w-full justify-between px-3"
          data-testid="log-more-details-trigger"
        >
          More details
          <ChevronDownIcon
            aria-hidden="true"
            className={`size-4 transition-transform duration-200 ease-out motion-reduce:transition-none ${
              open ? 'rotate-180' : ''
            }`}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="flex flex-col gap-4 data-[state=open]:animate-in data-[state=closed]:animate-out motion-reduce:animate-none">
        {FORM_METRIC_KEYS.map((key) => (
          <LogNumericField key={key} control={control} name={key} label={metricFieldLabel(key)} />
        ))}
      </CollapsibleContent>
    </Collapsible>
  )
}
