import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingCheckboxField } from './onboarding-checkbox-field.component'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSection } from './onboarding-section.component'

export interface OnboardingPreferencesSectionProps {
  control: OnboardingFormControl
}

/**
 * Preferences topic: terrain + intensity tolerance and a free-text note. Units
 * were captured first as a display preference (`UserSettings`, canonical), so
 * there is no unit control here — the submit populates the record's vestigial
 * `preferredUnits` from the resolved preference (DP-4).
 */
export const OnboardingPreferencesSection = ({ control }: OnboardingPreferencesSectionProps) => (
  <OnboardingSection
    title="Preferences"
    description="How you like to train."
    testId="onboarding-section-preferences"
  >
    <OnboardingCheckboxField
      control={control}
      name="preferTrail"
      label="I prefer trails where possible"
    />
    <OnboardingCheckboxField
      control={control}
      name="comfortableWithIntensity"
      label="I'm comfortable with hard, structured workouts"
    />
    <OnboardingNuanceSection
      control={control}
      name="preferencesDescription"
      label="Add detail"
      placeholder="Anything else about how you like to run?"
    />
  </OnboardingSection>
)
