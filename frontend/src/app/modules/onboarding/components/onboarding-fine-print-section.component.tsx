import { useWatch } from 'react-hook-form'

import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSwitchField } from './onboarding-switch-field.component'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingFinePrintSectionProps {
  control: OnboardingFormControl
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
      <OnboardingNuanceSection
        control={control}
        label={'+ Add detail \u2014 past injuries, preferences'}
        triggerTestId="fine-print-detail-trigger"
      >
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
      </OnboardingNuanceSection>
    </OnboardingSection>
  )
}
