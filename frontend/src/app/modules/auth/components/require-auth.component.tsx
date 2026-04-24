import type { ReactElement } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '~/modules/auth/hooks/auth.hooks'

export interface RequireAuthProps {
  children: ReactElement
}

// Route guard wrapper (spec §Unit 3 line 111).
//
//   status === 'unknown'          → loading fallback while app-boot `me`
//                                   is in flight (first-tick only).
//   status === 'unauthenticated'  → <Navigate> to /login with `state.next`
//                                   so LoginPage can restore the intended
//                                   destination after login.
//   status === 'authenticated'    → render protected children.
export const RequireAuth = ({ children }: RequireAuthProps) => {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'unknown') {
    return (
      <div
        className="flex min-h-screen items-center justify-center"
        role="status"
        aria-live="polite"
      >
        <span className="text-sm text-slate-500">Loading…</span>
      </div>
    )
  }

  if (status === 'unauthenticated') {
    const next = location.pathname + location.search + location.hash
    return <Navigate to="/login" state={{ next }} replace />
  }

  return children
}
