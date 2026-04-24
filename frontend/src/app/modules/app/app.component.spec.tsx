import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

// `useAuthBootstrap` dispatches real RTK Query `initiate` calls for
// `xsrf` and `me`; in jsdom those hit a missing fetch stub and pollute
// test output with unhandled rejections. `useAuthBroadcastListener`
// subscribes to a BroadcastChannel. Neither side effect is under test
// here — this spec only asserts the top-level route table renders and
// that the auth slice's initial `unknown` status shows the RequireAuth
// loading fallback. Full bootstrap behavior is exercised in the
// dedicated auth-hooks/RequireAuth specs.
vi.mock('~/modules/auth/hooks/auth.hooks', async () => {
  const actual = await vi.importActual<typeof import('~/modules/auth/hooks/auth.hooks')>(
    '~/modules/auth/hooks/auth.hooks',
  )
  return {
    ...actual,
    useAuthBootstrap: () => undefined,
    useAuthBroadcastListener: () => undefined,
  }
})

import { App } from './app.component'

describe('App', () => {
  it('mounts and renders the RequireAuth loading fallback on first render', () => {
    render(<App />)
    // Initial auth slice status === 'unknown' → RequireAuth shows the
    // loading fallback.
    expect(screen.getByRole('status')).toBeInTheDocument()
  })
})
