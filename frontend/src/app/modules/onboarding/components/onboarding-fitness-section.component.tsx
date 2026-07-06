import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingFitnessSectionProps {
  control: OnboardingFormControl
  /** Distance unit label for the volume/distance fields (`km` / `mi`). */
  unitLabel: string
}

/**
 * CurrentFitness topic: recent volume + longest run (both required, they anchor
 * the pace/volume model) and an optional recent race result that sharpens the
 * pace zones, plus a free-text note. Distances are entered in the runner's unit.
 */
export const OnboardingFitnessSection = ({ control, unitLabel }: OnboardingFitnessSectionProps) => (
  <OnboardingSection
    title="Where you're at"
    description="Your recent running, so the plan starts from the right place."
    testId="onboarding-section-fitness"
  >
    <OnboardingTextField
      control={control}
      name="typicalWeekly"
      label={`Typical weekly volume (${unitLabel})`}
      inputMode="decimal"
      placeholder="e.g. 40"
    />
    <OnboardingTextField
      control={control}
      name="longestRecentRun"
      label={`Longest recent run (${unitLabel})`}
      inputMode="decimal"
      placeholder="e.g. 18"
    />
    <OnboardingTextField
      control={control}
      name="recentRaceDistance"
      label={`Recent race distance (${unitLabel}, optional)`}
      inputMode="decimal"
      placeholder="e.g. 10"
    />
    <OnboardingTextField
      control={control}
      name="recentRaceTime"
      label="Recent race time (optional)"
      placeholder="H:MM:SS"
    />
    <OnboardingNuanceSection
      control={control}
      name="fitnessDescription"
      label="Add detail"
      placeholder="Anything else about your recent running?"
    />
  </OnboardingSection>
)
