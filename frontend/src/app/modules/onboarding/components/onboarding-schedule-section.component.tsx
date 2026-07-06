import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingDayToggleField } from './onboarding-day-toggle-field.component'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingScheduleSectionProps {
  control: OnboardingFormControl
}

/**
 * WeeklySchedule topic: run-day commitment, typical session length, the
 * preferred-day toggle group, and a free-text note. Co-submitting every field
 * at once is what structurally dissolves the old WeeklySchedule slot-merge loop
 * (R-085 R6).
 */
export const OnboardingScheduleSection = ({ control }: OnboardingScheduleSectionProps) => (
  <OnboardingSection
    title="Your week"
    description="How much time you have to train."
    testId="onboarding-section-schedule"
  >
    <OnboardingTextField
      control={control}
      name="maxRunDays"
      label="Days per week you can run"
      inputMode="numeric"
      placeholder="1–7"
    />
    <OnboardingTextField
      control={control}
      name="sessionMinutes"
      label="Typical session length (minutes)"
      inputMode="numeric"
      placeholder="e.g. 45"
    />
    <OnboardingDayToggleField control={control} />
    <OnboardingNuanceSection
      control={control}
      name="scheduleDescription"
      label="Add detail"
      placeholder="e.g. no early mornings, long runs on Sunday"
    />
  </OnboardingSection>
)
