import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useDispatch } from 'react-redux'
import { Link, useLocation, useNavigate } from 'react-router-dom'

import { zodResolver } from '@hookform/resolvers/zod'

import { useLoginMutation } from '~/api/auth.api'
import { parseProblem } from '~/modules/auth/helpers/problem-details.helpers'
import { loginSchema, type LoginFormValues } from '~/modules/auth/schemas/auth.schema'
import { sessionVerified } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'
import { LoginForm } from './login-form.component'

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
        const firstMessage = messages[0]
        if ((field === 'email' || field === 'password') && firstMessage !== undefined) {
          form.setError(field, { type: 'server', message: firstMessage })
        }
      }
      setFormAlert(parsed.title ?? 'Login failed. Please try again.')
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <section className="w-full max-w-sm rounded-lg bg-white p-6 shadow">
        <LoginForm form={form} onSubmit={onSubmit} isLoading={isLoading} formAlert={formAlert} />

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
