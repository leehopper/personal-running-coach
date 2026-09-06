import { MonoLabel } from '~/modules/common/components/mono-label/mono-label.component'
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
 * Current-fitness fields and an optional recent race result. Distances are
 * entered in the runner's selected unit.
 */
export const OnboardingFitnessSection = ({ control, unitLabel }: OnboardingFitnessSectionProps) => (
  <OnboardingSection title={"03 \u2014 Where you're at"} testId="onboarding-section-fitness">
    <div className="grid grid-cols-2 gap-3">
      <OnboardingTextField
        control={control}
        name="typicalWeekly"
        label={`Weekly volume \u00B7 ${unitLabel}`}
        inputMode="decimal"
        placeholder="e.g. 40"
      />
      <OnboardingTextField
        control={control}
        name="longestRecentRun"
        label={`Longest recent \u00B7 ${unitLabel}`}
        inputMode="decimal"
        placeholder="e.g. 18"
      />
      <OnboardingTextField
        control={control}
        name="recentRaceDistance"
        label={`Recent race \u00B7 ${unitLabel}`}
        inputMode="decimal"
        placeholder="e.g. 10"
        description="Optional"
      />
      <OnboardingTextField
        control={control}
        name="recentRaceTime"
        label="Race time"
        placeholder="H:MM:SS"
      />
    </div>
    <MonoLabel>{'A recent race sharpens your pace zones. No race \u2014 no problem.'}</MonoLabel>
    <OnboardingNuanceSection
      control={control}
      name="fitnessDescription"
      label="+ Add detail"
      fieldLabel="+ Add detail"
      placeholder="Anything else about your recent running?"
    />
  </OnboardingSection>
)
