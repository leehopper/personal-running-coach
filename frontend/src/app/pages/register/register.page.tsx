import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useDispatch } from 'react-redux'
import { Link, useNavigate } from 'react-router-dom'

import { zodResolver } from '@hookform/resolvers/zod'

import { registerRequestSchema, type RegisterRequest } from '~/api/generated'
import { useLoginMutation, useRegisterMutation } from '~/api/auth.api'
import { AuthPosterHeader } from '~/modules/auth/components/auth-poster-header.component'
import { parseProblem } from '~/modules/auth/helpers/problem-details.helpers'
import { sessionVerified } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'
import { RegisterForm } from './register-form.component'

// Form values mirror the generated `RegisterRequest` shape inferred from
// the codegen'd Zod schema. Renames or shape drift in `RegisterRequestDto`
// on the backend surface here as a TypeScript error rather than a silent UX
// regression. The schema mirrors the C# DataAnnotations (DEC-066 / R-071):
// maxLength 254 + email format on email, minLength 12 + maxLength 128 on
// password.
type RegisterFormValues = RegisterRequest

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
    resolver: zodResolver(registerRequestSchema),
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
    <main className="mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center gap-[26px] bg-background px-[26px] pb-10">
      <AuthPosterHeader />
      <RegisterForm form={form} onSubmit={onSubmit} isLoading={isLoading} formAlert={formAlert} />

      <p className="font-body text-sm text-muted-foreground">
        Already on the plan?{' '}
        <Link
          to="/login"
          className="relative hit-target-44 rounded-sm font-condensed font-bold uppercase tracking-[0.12em] text-clay-text outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22]"
        >
          Sign in {'\u2192'}
        </Link>
      </p>
    </main>
  )
}

export default RegisterPage
export { RegisterPage }
