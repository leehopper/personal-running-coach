import { useWatch } from 'react-hook-form'

import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingCheckboxField } from './onboarding-checkbox-field.component'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingInjurySectionProps {
  control: OnboardingFormControl
}

/**
 * InjuryHistory topic: a current-injury flag that reveals a required description,
 * plus the past-injury summary (this topic's free-text field, FR-1.6). The
 * active-injury description is watched so it only shows — and is only required —
 * when the runner reports a current limitation.
 */
export const OnboardingInjurySection = ({ control }: OnboardingInjurySectionProps) => {
  const hasActiveInjury = useWatch({ control, name: 'hasActiveInjury' })

  return (
    <OnboardingSection
      title="Injuries"
      description="Anything the plan should work around."
      testId="onboarding-section-injury"
    >
      <OnboardingCheckboxField
        control={control}
        name="hasActiveInjury"
        label="I have a current injury or limitation"
      />
      {hasActiveInjury ? (
        <OnboardingTextField
          control={control}
          name="activeInjuryDescription"
          label="What's bothering you right now?"
          placeholder="e.g. left calf strain"
        />
      ) : null}
      <OnboardingNuanceSection
        control={control}
        name="pastInjurySummary"
        label="Add past injuries"
        placeholder="Recurring issues the plan should keep in mind"
      />
    </OnboardingSection>
  )
}
