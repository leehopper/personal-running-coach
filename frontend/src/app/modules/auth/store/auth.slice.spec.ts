import { describe, expect, it } from 'vitest'
import { authReducer, authSlice, loggedOut, sessionVerified } from './auth.slice'
import type { AuthState } from '~/modules/auth/models/auth.model'

const initialState: AuthState = { status: 'unknown', user: null }

describe('authSlice', () => {
  it('starts in the unknown status with no user', () => {
    const state = authReducer(undefined, { type: '@@INIT' })
    expect(state).toEqual(initialState)
  })

  describe('sessionVerified', () => {
    it('populates the user and flips status to authenticated', () => {
      const user = { userId: 'usr_abc', email: 'runner@example.com' }
      const state = authReducer(initialState, sessionVerified(user))
      expect(state.status).toBe('authenticated')
      expect(state.user).toEqual(user)
    })

    it('overwrites an existing user payload', () => {
      const prior: AuthState = {
        status: 'authenticated',
        user: { userId: 'usr_old', email: 'old@example.com' },
      }
      const next = { userId: 'usr_new', email: 'new@example.com' }
      const state = authReducer(prior, sessionVerified(next))
      expect(state.user).toEqual(next)
    })
  })

  describe('loggedOut', () => {
    it('clears the user and flips status to unauthenticated', () => {
      const prior: AuthState = {
        status: 'authenticated',
        user: { userId: 'usr_abc', email: 'runner@example.com' },
      }
      const state = authReducer(prior, loggedOut())
      expect(state.status).toBe('unauthenticated')
      expect(state.user).toBeNull()
    })

    it('remains unauthenticated-with-null-user when invoked from the unknown bootstrap state', () => {
      const state = authReducer(initialState, loggedOut())
      expect(state.status).toBe('unauthenticated')
      expect(state.user).toBeNull()
    })
  })

  // Spec §Unit 3 line 112 — no token-storage primitive. Walk every key the
  // slice exposes (including nested `user`) and assert nothing looks like a
  // bearer / refresh / access token field. This is the canonical regression
  // guard: if a future refactor adds a `token` field to the slice, it fails
  // here before any E2E test runs.
  describe('no token-shaped fields exist in the state tree', () => {
    const walkKeys = (value: unknown): string[] => {
      if (value === null || typeof value !== 'object') return []
      const keys = Object.keys(value as Record<string, unknown>)
      const nested = keys.flatMap((key) => walkKeys((value as Record<string, unknown>)[key]))
      return keys.concat(nested)
    }

    const tokenPattern = /token|jwt|accessToken|refreshToken/i

    it('initial state has zero token-shaped keys', () => {
      const state = authReducer(undefined, { type: '@@INIT' })
      const offenders = walkKeys(state).filter((key) => tokenPattern.test(key))
      expect(offenders).toEqual([])
    })

    it('authenticated state has zero token-shaped keys', () => {
      const state = authReducer(
        initialState,
        sessionVerified({ userId: 'usr_abc', email: 'runner@example.com' }),
      )
      const offenders = walkKeys(state).filter((key) => tokenPattern.test(key))
      expect(offenders).toEqual([])
    })

    it('slice action-creator keys carry no token-shaped names', () => {
      const actionTypes = Object.values(authSlice.actions).map((creator) => creator.type)
      const offenders = actionTypes.filter((type) => tokenPattern.test(type))
      expect(offenders).toEqual([])
    })
  })
})
