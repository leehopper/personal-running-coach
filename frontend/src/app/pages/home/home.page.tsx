import { useDispatch } from 'react-redux'
import { useNavigate } from 'react-router-dom'
import { useLogoutMutation } from '~/api/auth.api'
import { useAuth } from '~/modules/auth/hooks/auth.hooks'
import { postLogoutBroadcast } from '~/modules/auth/lib/broadcast-auth'
import { loggedOut } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'

const HomePage = () => {
  const dispatch = useDispatch<AppDispatch>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const [logout, { isLoading }] = useLogoutMutation()

  const onLogout = async (): Promise<void> => {
    try {
      await logout(undefined).unwrap()
    } catch {
      // Even a server-side failure should flip the local session to
      // unauthenticated — the worst case is a stale server cookie that
      // the browser will reject on the next honored request anyway.
    }
    dispatch(loggedOut())
    postLogoutBroadcast()
    navigate('/login', { replace: true })
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4">
      <h1 className="text-4xl font-bold">RunCoach</h1>
      <p className="text-slate-700" data-testid="home-greeting">
        Logged in as {user?.email ?? ''}
      </p>
      <button
        type="button"
        onClick={() => {
          void onLogout()
        }}
        disabled={isLoading}
        className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
      >
        {isLoading ? 'Signing out…' : 'Sign out'}
      </button>
    </main>
  )
}

export default HomePage
export { HomePage }
