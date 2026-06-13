import { configureStore } from '@reduxjs/toolkit'
import { renderHook, act } from '@testing-library/react'
import { createElement, type ReactNode } from 'react'
import { Provider } from 'react-redux'
import { describe, expect, it, vi } from 'vitest'
import { SuggestedInputType } from '~/api/generated'
import {
  AnthropicContentBlockType,
  OnboardingTopic,
  OnboardingTurnKind,
} from '~/modules/onboarding/models/onboarding.model'
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
  ({ children }: { children: ReactNode }) =>
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
    submitUnwrap.mockResolvedValue({ kind: OnboardingTurnKind.Error, errorMessage: 'wire error' })
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

  it('appends an assistant Ask turn and updates topic state on a successful Ask response', async () => {
    submitUnwrap.mockResolvedValue({
      kind: OnboardingTurnKind.Ask,
      assistantBlocks: [
        { type: AnthropicContentBlockType.Text, text: 'When is your target event?' },
      ],
      topic: OnboardingTopic.TargetEvent,
      suggestedInputType: SuggestedInputType.Date,
      progress: { completedTopics: 1, totalTopics: 6 },
      planId: null,
    })
    const store = makeStore()
    const { result } = renderHook(() => useOnboardingTurn(), {
      wrapper: makeWrapper(store),
    })

    await act(async () => {
      await result.current.submitTurn({ text: 'training for a race' })
    })

    const state = store.getState().onboarding
    // User turn flips to delivered, assistant Ask turn lands behind it.
    expect(state.turns).toHaveLength(2)
    expect(state.turns[0].role).toBe('user')
    expect(state.turns[0].status).toBe('delivered')
    expect(state.turns[1].role).toBe('assistant')
    expect(state.turns[1].status).toBe('delivered')
    expect(state.turns[1].content).toEqual([{ type: 'text', text: 'When is your target event?' }])
    expect(state.currentTopic).toBe(OnboardingTopic.TargetEvent)
    expect(state.suggestedInputType).toBe(SuggestedInputType.Date)
    expect(state.completedTopics).toEqual([OnboardingTopic.PrimaryGoal])
    expect(state.isComplete).toBe(false)
    expect(state.isSubmitting).toBe(false)
  })

  it('flips isComplete and clears currentTopic on a successful Complete response', async () => {
    submitUnwrap.mockResolvedValue({
      kind: OnboardingTurnKind.Complete,
      assistantBlocks: [
        { type: AnthropicContentBlockType.Text, text: 'All set — your plan is ready.' },
      ],
      topic: null,
      suggestedInputType: null,
      progress: { completedTopics: 6, totalTopics: 6 },
      planId: '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234',
    })
    const store = makeStore()
    const { result } = renderHook(() => useOnboardingTurn(), {
      wrapper: makeWrapper(store),
    })

    await act(async () => {
      await result.current.submitTurn({ text: 'no other preferences' })
    })

    const state = store.getState().onboarding
    expect(state.isComplete).toBe(true)
    expect(state.isSubmitting).toBe(false)
    expect(state.currentTopic).toBeNull()
    expect(state.suggestedInputType).toBeNull()
    // User turn delivered + assistant Complete turn appended.
    expect(state.turns).toHaveLength(2)
    expect(state.turns[1].role).toBe('assistant')
    expect(state.turns[1].status).toBe('delivered')
    expect(state.turns[1].content).toEqual([
      { type: 'text', text: 'All set — your plan is ready.' },
    ])
    expect(state.completedTopics).toEqual([
      OnboardingTopic.PrimaryGoal,
      OnboardingTopic.TargetEvent,
      OnboardingTopic.CurrentFitness,
      OnboardingTopic.WeeklySchedule,
      OnboardingTopic.InjuryHistory,
      OnboardingTopic.Preferences,
    ])
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
