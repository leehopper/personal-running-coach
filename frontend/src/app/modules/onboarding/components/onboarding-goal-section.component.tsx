import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingGoalField } from './onboarding-goal-field.component'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSection } from './onboarding-section.component'

export interface OnboardingGoalSectionProps {
  control: OnboardingFormControl
}

/** Primary-goal topic: the goal single-select plus an optional free-text note. */
export const OnboardingGoalSection = ({ control }: OnboardingGoalSectionProps) => (
  <OnboardingSection title="Your goal" testId="onboarding-section-goal">
    <OnboardingGoalField control={control} />
    <OnboardingNuanceSection
      control={control}
      name="goalDescription"
      label="Add detail"
      placeholder="Anything else about what you're chasing?"
    />
  </OnboardingSection>
)
