import { describe, expect, it } from 'vitest'
import {
  onboardingReducer,
  assistantTurnAppended,
  buildingPlanStarted,
  onboardingCompleted,
  submitFailed,
  submitRetryStarted,
  submitStarted,
  transcriptCleared,
  transcriptReplaced,
  userTurnDelivered,
} from './onboarding.slice'
import type { OnboardingChatState, OnboardingTurn } from './onboarding.slice'
import { SuggestedInputType } from '~/api/generated'
import { OnboardingTopic } from '~/modules/onboarding/models/onboarding.model'

const emptyState: OnboardingChatState = {
  turns: [],
  currentTopic: null,
  suggestedInputType: null,
  completedTopics: [],
  isSubmitting: false,
  isComplete: false,
}

const makeTurn = (overrides: Partial<OnboardingTurn>): OnboardingTurn => ({
  id: 'turn-1',
  role: 'user',
  content: [{ type: 'text', text: 'Hello' }],
  status: 'delivered',
  ...overrides,
})

const buildingPlanTurn = (): OnboardingTurn =>
  makeTurn({
    id: 'placeholder-1',
    role: 'assistant',
    content: [{ type: 'text', text: 'Building your plan…' }],
    status: 'building-plan',
  })

describe('onboardingSlice', () => {
  it('initialises with empty turns and all flags false', () => {
    const state = onboardingReducer(undefined, { type: '@@INIT' })
    expect(state).toEqual(emptyState)
  })

  // ─── transcriptCleared ───────────────────────────────────────────────────

  describe('transcriptCleared', () => {
    it('resets a non-empty slice back to initial state', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        turns: [makeTurn({})],
        currentTopic: OnboardingTopic.PrimaryGoal,
        isSubmitting: true,
        isComplete: true,
      }
      const state = onboardingReducer(prior, transcriptCleared())
      expect(state).toEqual(emptyState)
    })
  })

  // ─── transcriptReplaced ───────────────────────────────────────────────────

  describe('transcriptReplaced', () => {
    it('fully replaces a non-empty turns array with the payload', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        turns: [makeTurn({ id: 'old-1' }), makeTurn({ id: 'old-2' })],
        currentTopic: OnboardingTopic.WeeklySchedule,
        isSubmitting: true,
      }
      const incoming: OnboardingChatState = {
        turns: [makeTurn({ id: 'new-1' })],
        currentTopic: OnboardingTopic.TargetEvent,
        suggestedInputType: SuggestedInputType.Text,
        completedTopics: [OnboardingTopic.PrimaryGoal],
        isSubmitting: false,
        isComplete: false,
      }
      const state = onboardingReducer(prior, transcriptReplaced(incoming))
      expect(state).toEqual(incoming)
      expect(state.turns).toHaveLength(1)
      expect(state.turns[0].id).toBe('new-1')
    })
  })

  // ─── submitStarted ────────────────────────────────────────────────────────

  describe('submitStarted', () => {
    it('appends a pending user turn and sets isSubmitting=true', () => {
      const state = onboardingReducer(
        emptyState,
        submitStarted({ id: 'turn-a', idempotencyKey: 'ikey-a', text: 'I want to run a marathon' }),
      )
      expect(state.isSubmitting).toBe(true)
      expect(state.turns).toHaveLength(1)
      const turn = state.turns[0]
      expect(turn.id).toBe('turn-a')
      expect(turn.role).toBe('user')
      expect(turn.status).toBe('pending')
      expect(turn.content).toEqual([{ type: 'text', text: 'I want to run a marathon' }])
      expect(turn.idempotencyKey).toBe('ikey-a')
    })
  })

  // ─── submitFailed ─────────────────────────────────────────────────────────

  describe('submitFailed', () => {
    it('strips a building-plan placeholder and marks the matching turn as failed', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'pending' }), buildingPlanTurn()],
      }
      const state = onboardingReducer(prior, submitFailed({ id: 'user-1' }))
      expect(state.isSubmitting).toBe(false)
      expect(state.turns.some((t) => t.status === 'building-plan')).toBe(false)
      const userTurn = state.turns.find((t) => t.id === 'user-1')
      expect(userTurn?.status).toBe('failed')
    })

    it('strips a building-plan placeholder even when the failed turn id is unknown', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        turns: [buildingPlanTurn()],
      }
      const state = onboardingReducer(prior, submitFailed({ id: 'nonexistent-id' }))
      expect(state.isSubmitting).toBe(false)
      expect(state.turns).toHaveLength(0)
    })

    it('stores errorMessage on the failed turn when provided', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'pending' })],
      }
      const state = onboardingReducer(
        prior,
        submitFailed({ id: 'user-1', errorMessage: 'Plan generation is not available right now.' }),
      )
      const userTurn = state.turns.find((t) => t.id === 'user-1')
      expect(userTurn?.status).toBe('failed')
      expect(userTurn?.errorMessage).toBe('Plan generation is not available right now.')
    })

    it('leaves errorMessage undefined on the failed turn when not provided', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'pending' })],
      }
      const state = onboardingReducer(prior, submitFailed({ id: 'user-1' }))
      const userTurn = state.turns.find((t) => t.id === 'user-1')
      expect(userTurn?.status).toBe('failed')
      expect(userTurn?.errorMessage).toBeUndefined()
    })
  })

  // ─── submitRetryStarted ───────────────────────────────────────────────────

  describe('submitRetryStarted', () => {
    it('sets the matching failed turn back to pending and sets isSubmitting=true', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'failed' })],
      }
      const state = onboardingReducer(prior, submitRetryStarted({ id: 'user-1' }))
      expect(state.isSubmitting).toBe(true)
      expect(state.turns[0].status).toBe('pending')
    })

    it('with an unknown turn id sets isSubmitting=true without throwing or mutating other turns', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: false,
        turns: [makeTurn({ id: 'user-1', status: 'failed' })],
      }
      const state = onboardingReducer(prior, submitRetryStarted({ id: 'nonexistent-id' }))
      expect(state.isSubmitting).toBe(true)
      expect(state.turns[0].status).toBe('failed')
    })
  })

  // ─── userTurnDelivered ────────────────────────────────────────────────────

  describe('userTurnDelivered', () => {
    it('flips a pending user turn to delivered', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'pending' })],
      }
      const state = onboardingReducer(prior, userTurnDelivered({ id: 'user-1' }))
      expect(state.turns[0].status).toBe('delivered')
    })
  })

  // ─── buildingPlanStarted ──────────────────────────────────────────────────

  describe('buildingPlanStarted', () => {
    it('appends a building-plan assistant turn and keeps isSubmitting=true', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'pending' })],
      }
      const state = onboardingReducer(prior, buildingPlanStarted({ id: 'placeholder-1' }))
      expect(state.isSubmitting).toBe(true)
      expect(state.turns).toHaveLength(2)
      const placeholder = state.turns[1]
      expect(placeholder.id).toBe('placeholder-1')
      expect(placeholder.role).toBe('assistant')
      expect(placeholder.status).toBe('building-plan')
      expect(placeholder.content).toEqual([{ type: 'text', text: 'Building your plan…' }])
    })
  })

  // ─── assistantTurnAppended ────────────────────────────────────────────────

  describe('assistantTurnAppended', () => {
    it('strips any outstanding building-plan placeholder before pushing the new assistant turn', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        turns: [makeTurn({ id: 'user-1', role: 'user', status: 'pending' }), buildingPlanTurn()],
      }
      const state = onboardingReducer(
        prior,
        assistantTurnAppended({
          id: 'asst-1',
          blocks: [{ type: 'text', text: 'What is your primary goal?' }],
          topic: OnboardingTopic.PrimaryGoal,
          suggestedInputType: SuggestedInputType.SingleSelect,
          completedTopics: [],
        }),
      )
      expect(state.turns.some((t) => t.status === 'building-plan')).toBe(false)
      const asstTurn = state.turns.find((t) => t.id === 'asst-1')
      expect(asstTurn).toBeDefined()
      expect(asstTurn?.role).toBe('assistant')
      expect(asstTurn?.status).toBe('delivered')
      expect(state.isSubmitting).toBe(false)
    })

    it('updates currentTopic, suggestedInputType, and completedTopics from payload', () => {
      const state = onboardingReducer(
        emptyState,
        assistantTurnAppended({
          id: 'asst-2',
          blocks: [{ type: 'text', text: 'Tell me about your schedule.' }],
          topic: OnboardingTopic.WeeklySchedule,
          suggestedInputType: SuggestedInputType.Numeric,
          completedTopics: [OnboardingTopic.PrimaryGoal, OnboardingTopic.TargetEvent],
        }),
      )
      expect(state.currentTopic).toBe(OnboardingTopic.WeeklySchedule)
      expect(state.suggestedInputType).toBe(SuggestedInputType.Numeric)
      expect(state.completedTopics).toEqual([
        OnboardingTopic.PrimaryGoal,
        OnboardingTopic.TargetEvent,
      ])
    })
  })

  // ─── onboardingCompleted ──────────────────────────────────────────────────

  describe('onboardingCompleted', () => {
    it('strips a building-plan placeholder, sets isComplete=true, clears currentTopic/suggestedInputType', () => {
      const prior: OnboardingChatState = {
        ...emptyState,
        isSubmitting: true,
        currentTopic: OnboardingTopic.Preferences,
        suggestedInputType: SuggestedInputType.Text,
        turns: [
          makeTurn({ id: 'user-final', role: 'user', status: 'pending' }),
          buildingPlanTurn(),
        ],
      }
      const state = onboardingReducer(
        prior,
        onboardingCompleted({
          id: 'asst-complete',
          blocks: [{ type: 'text', text: 'Your plan is ready!' }],
          topic: null,
          suggestedInputType: null,
          completedTopics: [
            OnboardingTopic.PrimaryGoal,
            OnboardingTopic.TargetEvent,
            OnboardingTopic.CurrentFitness,
            OnboardingTopic.WeeklySchedule,
            OnboardingTopic.InjuryHistory,
            OnboardingTopic.Preferences,
          ],
        }),
      )
      expect(state.isComplete).toBe(true)
      expect(state.isSubmitting).toBe(false)
      expect(state.currentTopic).toBeNull()
      expect(state.suggestedInputType).toBeNull()
      expect(state.turns.some((t) => t.status === 'building-plan')).toBe(false)
      const completeTurn = state.turns.find((t) => t.id === 'asst-complete')
      expect(completeTurn?.status).toBe('delivered')
      expect(completeTurn?.role).toBe('assistant')
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
})
