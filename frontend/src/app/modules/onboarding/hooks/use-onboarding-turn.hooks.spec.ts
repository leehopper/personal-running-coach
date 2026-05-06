import { configureStore } from '@reduxjs/toolkit'
import { renderHook, act } from '@testing-library/react'
import { createElement } from 'react'
import { Provider } from 'react-redux'
import { describe, expect, it, vi } from 'vitest'
import { OnboardingTurnKind } from '~/modules/onboarding/models/onboarding.model'
import { onboardingSlice } from '~/modules/onboarding/store/onboarding.slice'

// `submitUnwrap` is the promise returned by `submit(...).unwrap()` inside
// `dispatchPostSubmit`. Hoisted so the mock factory can close over it.
const { submitUnwrap } = vi.hoisted(() => ({
  submitUnwrap: vi.fn(),
}))

vi.mock('~/api/onboarding.api', () => ({
  useSubmitOnboardingTurnMutation: () => [
    vi.fn(() => ({ unwrap: submitUnwrap })),
    { isLoading: false },
  ],
  // `useOnboardingTurn` calls `onboardingApi.util.updateQueryData` on the
  // Complete branch. These tests don't exercise that branch, but the import
  // must resolve without crashing.
  onboardingApi: {
    util: {
      updateQueryData: vi.fn(() => () => undefined),
    },
  },
}))

import { useOnboardingTurn } from './use-onboarding-turn.hooks'

const makeStore = () =>
  configureStore({ reducer: { [onboardingSlice.name]: onboardingSlice.reducer } })

const makeWrapper =
  (store: ReturnType<typeof makeStore>) =>
  ({ children }: { children: React.ReactNode }) =>
    createElement(Provider, { store, children })

describe('useOnboardingTurn', () => {
  it('dispatches submitFailed and flips the user turn to failed when the server returns a malformed payload', async () => {
    submitUnwrap.mockResolvedValue({ kind: 99, junk: true })
    const store = makeStore()
    const { result } = renderHook(() => useOnboardingTurn(), {
      wrapper: makeWrapper(store),
    })

    await act(async () => {
      await result.current.submitTurn({ text: 'hello' })
    })

    const turns = store.getState().onboarding.turns
    expect(turns).toHaveLength(1)
    expect(turns[0].role).toBe('user')
    expect(turns[0].status).toBe('failed')
  })

  it('dispatches submitFailed and flips the user turn to failed when the server returns kind=Error', async () => {
    submitUnwrap.mockResolvedValue({ kind: OnboardingTurnKind.Error, message: 'wire error' })
    const store = makeStore()
    const { result } = renderHook(() => useOnboardingTurn(), {
      wrapper: makeWrapper(store),
    })

    await act(async () => {
      await result.current.submitTurn({ text: 'hello' })
    })

    const turns = store.getState().onboarding.turns
    expect(turns).toHaveLength(1)
    expect(turns[0].role).toBe('user')
    expect(turns[0].status).toBe('failed')
  })
})

describe('retryLastFailedTurn', () => {
  it('is a no-op when no turn has been submitted yet (lastSubmissionRef is null)', async () => {
    submitUnwrap.mockReset()
    const store = makeStore()
    const stateBefore = store.getState()

    const { result } = renderHook(() => useOnboardingTurn(), {
      wrapper: makeWrapper(store),
    })

    await act(async () => {
      await result.current.retryLastFailedTurn()
    })

    // submit mutation must never have been called
    expect(submitUnwrap).not.toHaveBeenCalled()
    // Redux state must be byte-identical — no actions were dispatched
    expect(store.getState()).toEqual(stateBefore)
  })
})
