import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'

import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useDispatch } from 'react-redux'

import { useLoginMutation, useRegisterMutation } from '~/api/auth.api'
import { parseProblem } from '~/modules/auth/helpers/problem-details.helpers'
import { registerSchema, type RegisterFormValues } from '~/modules/auth/schemas/auth.schema'
import { sessionVerified } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'
import { RegisterForm } from './register-form.component'

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

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <section className="w-full max-w-sm rounded-lg bg-white p-6 shadow">
        <RegisterForm form={form} onSubmit={onSubmit} isLoading={isLoading} formAlert={formAlert} />

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
