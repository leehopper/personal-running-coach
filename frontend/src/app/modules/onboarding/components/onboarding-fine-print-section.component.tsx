import { useState } from 'react'
import { ChevronDownIcon } from 'lucide-react'
import { useWatch } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingSwitchField } from './onboarding-switch-field.component'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingFinePrintSectionProps {
  control: OnboardingFormControl
}

interface OnboardingFinePrintDetailsProps {
  control: OnboardingFormControl
}

const OnboardingFinePrintDetails = ({ control }: OnboardingFinePrintDetailsProps) => {
  const [open, setOpen] = useState(false)

  return (
    <Collapsible open={open} onOpenChange={setOpen} className="flex flex-col gap-2">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-fit gap-1 px-2 font-mono text-[11px] tracking-[0.1em] text-clay-text motion-reduce:active:scale-100"
          data-testid="fine-print-detail-trigger"
        >
          {'+ Add detail \u2014 past injuries, preferences'}
          <ChevronDownIcon
            aria-hidden="true"
            className={`size-4 transition-transform duration-200 ease-out motion-reduce:transition-none ${
              open ? 'rotate-180' : ''
            }`}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="overflow-hidden">
        <div className="flex flex-col gap-3">
          <OnboardingTextField
            control={control}
            name="pastInjurySummary"
            label="Past injuries"
            placeholder="Recurring issues the plan should keep in mind"
          />
          <OnboardingTextField
            control={control}
            name="preferencesDescription"
            label="Preferences"
            placeholder="Anything else about how you like to run?"
          />
        </div>
      </CollapsibleContent>
    </Collapsible>
  )
}

/** Injury and preference switches merged into the final onboarding section. */
export const OnboardingFinePrintSection = ({ control }: OnboardingFinePrintSectionProps) => {
  const hasActiveInjury = useWatch({ control, name: 'hasActiveInjury' })

  return (
    <OnboardingSection title={'05 \u2014 The fine print'} testId="onboarding-section-fine-print">
      <div className="flex flex-col gap-1">
        <OnboardingSwitchField
          control={control}
          name="hasActiveInjury"
          label="Current injury or limitation"
        />
        <OnboardingSwitchField
          control={control}
          name="comfortableWithIntensity"
          label="Comfortable with hard, structured work"
        />
        <OnboardingSwitchField
          control={control}
          name="preferTrail"
          label="Prefer trails where possible"
        />
      </div>
      {hasActiveInjury ? (
        <div className="animate-in fade-in-0 motion-reduce:animate-none">
          <OnboardingTextField
            control={control}
            name="activeInjuryDescription"
            label="What's bothering you right now?"
            placeholder="e.g. left calf strain"
          />
        </div>
      ) : null}
      <OnboardingFinePrintDetails control={control} />
    </OnboardingSection>
  )
}
