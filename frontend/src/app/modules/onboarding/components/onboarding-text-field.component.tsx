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
 * A single string-backed onboarding input wired through RHF and the Alpine
 * `Form` primitives. Numeric fields pass `inputMode` while values stay strings.
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
        <FormLabel className="t-data-label text-foreground">{label}</FormLabel>
        <FormControl>
          <Input
            type={type}
            inputMode={inputMode}
            placeholder={placeholder}
            data-testid={testId ?? `${name}-field`}
            className="h-[46px]"
            {...field}
          />
        </FormControl>
        {description !== undefined ? (
          <FormDescription className="t-data-label text-muted-foreground">
            {description}
          </FormDescription>
        ) : null}
        <FormMessage />
      </FormItem>
    )}
  />
)
