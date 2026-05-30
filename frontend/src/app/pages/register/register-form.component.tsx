import type { UseFormReturn } from 'react-hook-form'

import { AuthFormShell } from '~/modules/auth/components/auth-form-shell.component'
import { AuthTextField } from '~/modules/auth/components/auth-text-field.component'
import type { RegisterRequest } from '~/api/generated'

export interface RegisterFormProps {
  form: UseFormReturn<RegisterRequest>
  onSubmit: (values: RegisterRequest) => Promise<void> | void
  isLoading: boolean
  formAlert: string | null
}

export const RegisterForm = ({ form, onSubmit, isLoading, formAlert }: RegisterFormProps) => (
  <AuthFormShell
    heading="Create your account"
    formAlert={formAlert}
    form={form}
    onSubmit={onSubmit}
    isLoading={isLoading}
    submitLabel="Create account"
    pendingLabel="Creating account…"
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
      autoComplete="new-password"
      description="At least 12 characters. The server also requires an uppercase letter, a lowercase letter, a digit, and a non-alphanumeric character; a weaker password is rejected at submit with a server-side error."
    />
  </AuthFormShell>
)
