import type { OnboardingFormControl } from '~/modules/onboarding/schemas/onboarding-form.schema'
import { OnboardingDayToggleField } from './onboarding-day-toggle-field.component'
import { OnboardingNuanceSection } from './onboarding-nuance-section.component'
import { OnboardingSection } from './onboarding-section.component'
import { OnboardingTextField } from './onboarding-text-field.component'

export interface OnboardingScheduleSectionProps {
  control: OnboardingFormControl
}

/**
 * Weekly schedule fields, preferred-day chips, and an optional free-text note.
 */
export const OnboardingScheduleSection = ({ control }: OnboardingScheduleSectionProps) => (
  <OnboardingSection title={'04 \u2014 Your week'} testId="onboarding-section-schedule">
    <div className="grid grid-cols-2 gap-3">
      <OnboardingTextField
        control={control}
        name="maxRunDays"
        label="Run days / week"
        inputMode="numeric"
        placeholder={'1\u20137'}
      />
      <OnboardingTextField
        control={control}
        name="sessionMinutes"
        label={'Session length \u00B7 min'}
        inputMode="numeric"
        placeholder="e.g. 45"
      />
    </div>
    <OnboardingDayToggleField control={control} />
    <OnboardingNuanceSection
      control={control}
      name="scheduleDescription"
      label="+ Add detail"
      fieldLabel="+ Add detail"
      placeholder="e.g. no early mornings, long runs on Sunday"
    />
  </OnboardingSection>
)
