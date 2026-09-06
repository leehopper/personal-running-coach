import { useState, type ComponentProps, type HTMLInputTypeAttribute, type ReactNode } from 'react'
import type { Control, ControllerRenderProps, FieldPath, FieldValues } from 'react-hook-form'

import { cn } from '@/lib/utils'
import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { PasswordVisibilityToggle } from './password-visibility-toggle.component'

export interface AuthTextFieldProps<TValues extends FieldValues> {
  control: Control<TValues>
  name: FieldPath<TValues>
  label: string
  type: HTMLInputTypeAttribute
  autoComplete: NonNullable<ComponentProps<'input'>['autoComplete']>
  autoFocus?: boolean
  description?: ReactNode
}

interface AuthInputControlProps<TValues extends FieldValues> {
  field: ControllerRenderProps<TValues, FieldPath<TValues>>
  type: HTMLInputTypeAttribute
  autoComplete: NonNullable<ComponentProps<'input'>['autoComplete']>
  autoFocus: boolean
}

const AuthInputControl = <TValues extends FieldValues>({
  field,
  type,
  autoComplete,
  autoFocus,
}: AuthInputControlProps<TValues>) => {
  const [isPasswordVisible, setIsPasswordVisible] = useState(false)
  const isPassword = type === 'password'
  const inputType = isPassword && isPasswordVisible ? 'text' : type
  const input = (
    <Input
      type={inputType}
      autoComplete={autoComplete}
      autoFocus={autoFocus}
      className={cn('h-12 px-[14px]', isPassword && 'pr-12')}
      {...field}
    />
  )

  if (!isPassword) {
    return <FormControl>{input}</FormControl>
  }

  return (
    <div className="relative">
      <FormControl>{input}</FormControl>
      <PasswordVisibilityToggle
        isVisible={isPasswordVisible}
        onToggle={() => {
          setIsPasswordVisible((current) => !current)
        }}
      />
    </div>
  )
}

/**
 * A labeled text input wired into react-hook-form for the auth surfaces.
 * Renders the FormItem/Label/Control/Message stack, with an optional
 * description shown between the input and the validation message.
 */
export const AuthTextField = <TValues extends FieldValues>({
  control,
  name,
  label,
  type,
  autoComplete,
  autoFocus = false,
  description,
}: AuthTextFieldProps<TValues>) => (
  <FormField
    control={control}
    name={name}
    render={({ field }) => (
      <FormItem>
        <FormLabel className="t-data-label">{label}</FormLabel>
        <AuthInputControl
          field={field}
          type={type}
          autoComplete={autoComplete}
          autoFocus={autoFocus}
        />
        {description !== undefined && <FormDescription>{description}</FormDescription>}
        <FormMessage />
      </FormItem>
    )}
  />
)
