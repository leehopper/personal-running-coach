import type { ReactNode } from 'react'
import type { FieldValues, UseFormReturn } from 'react-hook-form'

import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'

export interface AuthFormShellProps<TValues extends FieldValues> {
  heading: string
  headingVisuallyHidden?: boolean
  formAlert: string | null
  form: UseFormReturn<TValues>
  onSubmit: (values: TValues) => Promise<void> | void
  isLoading: boolean
  submitLabel: string
  pendingLabel: string
  children: ReactNode
}

/**
 * Shared scaffolding for the auth-surface forms (sign in / create account):
 * heading, non-field-level alert, the react-hook-form provider + native form
 * wrapper, and the full-width submit button. Field rows are supplied as
 * children so each surface only declares the inputs that differ.
 */
export const AuthFormShell = <TValues extends FieldValues>({
  heading,
  headingVisuallyHidden = false,
  formAlert,
  form,
  onSubmit,
  isLoading,
  submitLabel,
  pendingLabel,
  children,
}: AuthFormShellProps<TValues>) => {
  const isSubmitDisabled = !form.formState.isValid || form.formState.isSubmitting || isLoading

  return (
    <>
      <h1 className={headingVisuallyHidden ? 'sr-only' : 't-screen-title text-foreground'}>
        {heading}
      </h1>

      {formAlert !== null && (
        <div
          role="alert"
          data-testid="form-alert"
          className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {formAlert}
        </div>
      )}

      <Form {...form}>
        <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          {children}
          <Button
            type="submit"
            disabled={isSubmitDisabled}
            className="w-full h-[52px] text-[17px] tracking-[0.14em]"
          >
            {isLoading ? pendingLabel : submitLabel}
          </Button>
          <div aria-hidden="true" data-testid="auth-oauth-reserve" className="min-h-[52px]" />
        </form>
      </Form>
    </>
  )
}
