import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useDispatch } from 'react-redux'
import { Link, useNavigate } from 'react-router-dom'
import { useLoginMutation, useRegisterMutation } from '~/api/auth.api'
import { parseProblem } from '~/modules/auth/helpers/problem-details.helpers'
import { registerSchema, type RegisterFormValues } from '~/modules/auth/schemas/auth.schema'
import { sessionVerified } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'

// Register → Login chained flow. The backend `register` endpoint
// deliberately does NOT auto-authenticate (AuthController.Register
// returns 201 + AuthResponseDto only; no Set-Cookie). After a successful
// register, immediately log the user in with the same credentials so the
// UX lands on `/` authenticated.
const RegisterPage = () => {
  const dispatch = useDispatch<AppDispatch>()
  const navigate = useNavigate()
  const [registerUser, registerState] = useRegisterMutation()
  const [login, loginState] = useLoginMutation()
  const [formAlert, setFormAlert] = useState<string | null>(null)

  const form = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    mode: 'onChange',
    defaultValues: { email: '', password: '' },
  })

  const onSubmit = async (values: RegisterFormValues): Promise<void> => {
    setFormAlert(null)
    try {
      await registerUser(values).unwrap()
    } catch (error) {
      const parsed = parseProblem(error)
      for (const [field, messages] of Object.entries(parsed.fieldErrors)) {
        const firstMessage = messages[0]
        if ((field === 'email' || field === 'password') && firstMessage !== undefined) {
          form.setError(field, { type: 'server', message: firstMessage })
        }
      }
      setFormAlert(parsed.title ?? 'Registration failed. Please try again.')
      return
    }

    try {
      const response = await login(values).unwrap()
      dispatch(sessionVerified({ userId: response.userId, email: response.email }))
      navigate('/', { replace: true })
    } catch (error) {
      // Account was created but auto-login failed — surface the issue and
      // let the user log in manually via /login.
      const parsed = parseProblem(error)
      setFormAlert(
        parsed.title ?? 'Account created but sign-in failed. Please sign in from the login page.',
      )
    }
  }

  const isLoading = registerState.isLoading || loginState.isLoading
  const isSubmitDisabled = !form.formState.isValid || isLoading

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <section className="w-full max-w-sm rounded-lg bg-white p-6 shadow">
        <h1 className="mb-4 text-2xl font-semibold">Create your account</h1>

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
            <label htmlFor="register-email" className="block text-sm font-medium">
              Email
            </label>
            <input
              id="register-email"
              type="email"
              autoComplete="email"
              autoFocus
              aria-invalid={form.formState.errors.email !== undefined}
              aria-describedby={
                form.formState.errors.email === undefined ? undefined : 'register-email-error'
              }
              className="mt-1 block w-full rounded border border-slate-300 px-3 py-2 text-sm"
              {...form.register('email')}
            />
            {form.formState.errors.email !== undefined && (
              <p id="register-email-error" role="alert" className="mt-1 text-xs text-red-700">
                {form.formState.errors.email.message}
              </p>
            )}
          </div>

          <div>
            <label htmlFor="register-password" className="block text-sm font-medium">
              Password
            </label>
            <input
              id="register-password"
              type="password"
              autoComplete="new-password"
              aria-invalid={form.formState.errors.password !== undefined}
              aria-describedby={
                form.formState.errors.password === undefined
                  ? 'register-password-hint'
                  : 'register-password-error'
              }
              className="mt-1 block w-full rounded border border-slate-300 px-3 py-2 text-sm"
              {...form.register('password')}
            />
            {form.formState.errors.password === undefined ? (
              <p id="register-password-hint" className="mt-1 text-xs text-slate-500">
                At least 12 characters, with upper &amp; lower case letters, a digit, and a
                non-alphanumeric character.
              </p>
            ) : (
              <p id="register-password-error" role="alert" className="mt-1 text-xs text-red-700">
                {form.formState.errors.password.message}
              </p>
            )}
          </div>

          <button
            type="submit"
            disabled={isSubmitDisabled}
            className="w-full rounded bg-slate-900 px-3 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isLoading ? 'Creating account…' : 'Create account'}
          </button>
        </form>

        <p className="mt-4 text-sm text-slate-600">
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-slate-900 underline">
            Sign in
          </Link>
        </p>
      </section>
    </main>
  )
}

export default RegisterPage
export { RegisterPage }
