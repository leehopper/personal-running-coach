import type { ReactNode } from 'react'
import type { Control, FieldPath, FieldValues } from 'react-hook-form'

import {
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'

export interface AuthTextFieldProps<TValues extends FieldValues> {
  control: Control<TValues>
  name: FieldPath<TValues>
  label: string
  type: string
  autoComplete: string
  autoFocus?: boolean
  description?: ReactNode
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
        <FormLabel>{label}</FormLabel>
        <FormControl>
          <Input type={type} autoComplete={autoComplete} autoFocus={autoFocus} {...field} />
        </FormControl>
        {description !== undefined && <FormDescription>{description}</FormDescription>}
        <FormMessage />
      </FormItem>
    )}
  />
)
