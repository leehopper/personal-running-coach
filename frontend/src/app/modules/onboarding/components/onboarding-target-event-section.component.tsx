import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingTargetEventSectionProps {
  control: OnboardingFormControl
  /** Distance unit label for the race-distance field (`km` / `mi`). */
  unitLabel: string
}

/**
 * TargetEvent topic — revealed only for a race-training goal (the form watches
 * `goal`). No free-text nuance box (adding one would edit the frozen
 * `OnboardingSchema` and bust the DEC-074 manifest — DP-3). The optional goal
 * finish time is entered as `H:MM:SS`.
 */
export const OnboardingTargetEventSection = ({
  control,
  unitLabel,
}: OnboardingTargetEventSectionProps) => (
  <OnboardingSection
    title="Your goal race"
    description="Tell us about the event you're training for."
    testId="onboarding-section-target-event"
  >
    <OnboardingTextField
      control={control}
      name="eventName"
      label="Race name"
      placeholder="e.g. Berlin Marathon"
    />
    <OnboardingTextField
      control={control}
      name="eventDistance"
      label={`Race distance (${unitLabel})`}
      inputMode="decimal"
      placeholder="e.g. 42.2"
    />
    <OnboardingTextField control={control} name="eventDate" label="Race date" type="date" />
    <OnboardingTextField
      control={control}
      name="targetFinishTime"
      label="Goal finish time (optional)"
      placeholder="H:MM:SS"
    />
  </OnboardingSection>
)
