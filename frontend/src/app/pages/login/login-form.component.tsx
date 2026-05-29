import type { UseFormReturn } from 'react-hook-form'

import { AuthFormShell } from '~/modules/auth/components/auth-form-shell.component'
import { AuthTextField } from '~/modules/auth/components/auth-text-field.component'
import type { LoginFormValues } from '~/modules/auth/schemas/auth.schema'

export interface LoginFormProps {
  form: UseFormReturn<LoginFormValues>
  onSubmit: (values: LoginFormValues) => Promise<void> | void
  isLoading: boolean
  formAlert: string | null
}

export const LoginForm = ({ form, onSubmit, isLoading, formAlert }: LoginFormProps) => (
  <AuthFormShell
    heading="Sign in to RunCoach"
    formAlert={formAlert}
    form={form}
    onSubmit={onSubmit}
    isLoading={isLoading}
    submitLabel="Sign in"
    pendingLabel="Signing in…"
  >
    <AuthTextField
      control={form.control}
      name="email"
      label="Email"
      type="email"
      autoComplete="email"
      autoFocus
    />
    <AuthTextField
      control={form.control}
      name="password"
      label="Password"
      type="password"
      autoComplete="current-password"
    />
  </AuthFormShell>
)
