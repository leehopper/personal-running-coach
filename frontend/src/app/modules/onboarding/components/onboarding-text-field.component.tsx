import type { HTMLInputTypeAttribute } from 'react'

import { Input } from '@/components/ui/input'
import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import type {
  OnboardingFormControl,
  OnboardingStringFieldName,
} from '~/modules/onboarding/schemas/onboarding-form.schema'

export interface OnboardingTextFieldProps {
  control: OnboardingFormControl
  name: OnboardingStringFieldName
  label: string
  type?: HTMLInputTypeAttribute
  inputMode?: 'text' | 'numeric' | 'decimal'
  placeholder?: string
  description?: string
  /** Applied to the input for E2E/unit-test targeting (`{name}-field` convention). */
  testId?: string
}

/**
 * A single string-backed onboarding input (text / date / numeric) wired through
 * an RHF `Controller` and the shadcn `Form` primitives — the label, control, and
 * `FormMessage` share the ARIA wiring `form.tsx` provides. Numeric fields pass
 * `inputMode` for a numeric soft keyboard while the value stays a string
 * (DEC-075).
 */
export const OnboardingTextField = ({
  control,
  name,
  label,
  type = 'text',
  inputMode,
  placeholder,
  description,
  testId,
}: OnboardingTextFieldProps) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem>
        <FormLabel>{label}</FormLabel>
        <FormControl>
          <Input
            type={type}
            inputMode={inputMode}
            placeholder={placeholder}
            data-testid={testId ?? `${name}-field`}
            {...field}
          />
        </FormControl>
        {description !== undefined ? <FormDescription>{description}</FormDescription> : null}
        <FormMessage />
      </FormItem>
    )}
  />
)
