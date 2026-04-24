import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useDispatch } from 'react-redux'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useLoginMutation } from '~/api/auth.api'
import { parseProblem } from '~/modules/auth/helpers/problem-details.helpers'
import { loginSchema, type LoginFormValues } from '~/modules/auth/schemas/auth.schema'
import { sessionVerified } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'

// Strict narrowing against `location.state` (typed `unknown` by React
// Router when external) — only trust a string `next` that looks like an
// in-app path. Blocks state-injection redirects to external origins.
const readNextPath = (state: unknown): string | null => {
  if (state === null || typeof state !== 'object') return null
  const candidate = (state as { next?: unknown }).next
  if (typeof candidate !== 'string') return null
  if (!candidate.startsWith('/')) return null
  if (candidate.startsWith('//')) return null
  return candidate
}

const LoginPage = () => {
  const dispatch = useDispatch<AppDispatch>()
  const navigate = useNavigate()
  const location = useLocation()
  const [login, { isLoading }] = useLoginMutation()
  const [formAlert, setFormAlert] = useState<string | null>(null)

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    mode: 'onChange',
    defaultValues: { email: '', password: '' },
  })

  const onSubmit = async (values: LoginFormValues): Promise<void> => {
    setFormAlert(null)
    try {
      const response = await login(values).unwrap()
      dispatch(sessionVerified({ userId: response.userId, email: response.email }))
      const next = readNextPath(location.state) ?? '/'
      navigate(next, { replace: true })
    } catch (error) {
      const parsed = parseProblem(error)
      for (const [field, messages] of Object.entries(parsed.fieldErrors)) {
        if ((field === 'email' || field === 'password') && messages[0]) {
          form.setError(field, { type: 'server', message: messages[0] })
        }
      }
      setFormAlert(parsed.title ?? 'Login failed. Please try again.')
    }
  }

  const isSubmitDisabled = !form.formState.isValid || isLoading

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <section className="w-full max-w-sm rounded-lg bg-white p-6 shadow">
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
              aria-invalid={form.formState.errors.email !== undefined}
              aria-describedby={
                form.formState.errors.email !== undefined ? 'login-email-error' : undefined
              }
              className="mt-1 block w-full rounded border border-slate-300 px-3 py-2 text-sm"
              {...form.register('email')}
            />
            {form.formState.errors.email !== undefined && (
              <p id="login-email-error" role="alert" className="mt-1 text-xs text-red-700">
                {form.formState.errors.email.message}
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
              aria-invalid={form.formState.errors.password !== undefined}
              aria-describedby={
                form.formState.errors.password !== undefined ? 'login-password-error' : undefined
              }
              className="mt-1 block w-full rounded border border-slate-300 px-3 py-2 text-sm"
              {...form.register('password')}
            />
            {form.formState.errors.password !== undefined && (
              <p id="login-password-error" role="alert" className="mt-1 text-xs text-red-700">
                {form.formState.errors.password.message}
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

        <p className="mt-4 text-sm text-slate-600">
          Need an account?{' '}
          <Link to="/register" className="font-medium text-slate-900 underline">
            Create one
          </Link>
        </p>
      </section>
    </main>
  )
}

export default LoginPage
export { LoginPage }
