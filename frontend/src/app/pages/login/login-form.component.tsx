import type { UseFormReturn } from 'react-hook-form'
import type { LoginFormValues } from '~/modules/auth/schemas/auth.schema'

export interface LoginFormProps {
  form: UseFormReturn<LoginFormValues>
  onSubmit: (values: LoginFormValues) => Promise<void> | void
  isLoading: boolean
  formAlert: string | null
}

export const LoginForm = ({ form, onSubmit, isLoading, formAlert }: LoginFormProps) => {
  const isSubmitDisabled = !form.formState.isValid || isLoading
  const emailError = form.formState.errors.email
  const passwordError = form.formState.errors.password

  return (
    <>
      <h1 className="mb-4 text-2xl font-semibold">Sign in to RunCoach</h1>

      {formAlert !== null && (
        <div
          role="alert"
          data-testid="form-alert"
          className="mb-4 rounded border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800"
        >
          {formAlert}
        </div>
      )}

      <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label htmlFor="login-email" className="block text-sm font-medium">
            Email
          </label>
          <input
            id="login-email"
            type="email"
            autoComplete="email"
            autoFocus
            aria-invalid={emailError !== undefined}
            aria-describedby={emailError === undefined ? undefined : 'login-email-error'}
            className="mt-1 block w-full rounded border border-slate-300 px-3 py-2 text-sm"
            {...form.register('email')}
          />
          {emailError !== undefined && (
            <p id="login-email-error" role="alert" className="mt-1 text-xs text-red-700">
              {emailError.message}
            </p>
          )}
        </div>

        <div>
          <label htmlFor="login-password" className="block text-sm font-medium">
            Password
          </label>
          <input
            id="login-password"
            type="password"
            autoComplete="current-password"
            aria-invalid={passwordError !== undefined}
            aria-describedby={passwordError === undefined ? undefined : 'login-password-error'}
            className="mt-1 block w-full rounded border border-slate-300 px-3 py-2 text-sm"
            {...form.register('password')}
          />
          {passwordError !== undefined && (
            <p id="login-password-error" role="alert" className="mt-1 text-xs text-red-700">
              {passwordError.message}
            </p>
          )}
        </div>

        <button
          type="submit"
          disabled={isSubmitDisabled}
          className="w-full rounded bg-slate-900 px-3 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isLoading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </>
  )
}
