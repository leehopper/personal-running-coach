import { createSlice, type PayloadAction } from '@reduxjs/toolkit'
import type { MessageContentBlock } from '~/modules/coaching/components/message-bubble.component'
import type {
  OnboardingTopic,
  SuggestedInputType,
} from '~/modules/onboarding/models/onboarding.model'

// Lifecycle of a transcript turn. `pending` = optimistic user message
// awaiting server response; `failed` = pending message whose POST returned
// 5xx (Retry affordance reuses the same idempotency key per spec § Unit 3
// R03.9); `delivered` = server-acknowledged content (user echoes after
// success, plus every assistant turn). `building-plan` is the programmatic
// "we're generating your plan" assistant turn rendered while the final
// `submitOnboardingTurn` is in flight (spec § Unit 3 R03.10).
export type TurnLifecycle = 'pending' | 'failed' | 'delivered' | 'building-plan'

export type TurnRole = 'user' | 'assistant'

export interface OnboardingTurn {
  // Stable identifier for React lists. User turns reuse the idempotency
  // key; assistant turns get a generated UUID per arrival.
  id: string
  role: TurnRole
  // String-typed content blocks ready for the shared `MessageBubble`
  // primitive (`MessageContentBlock`). The wire format uses integer enums
  // (`AnthropicContentBlockType`), so the page-level hook adapts at the
  // boundary before dispatching here. `MessageBubble` ignores non-text
  // blocks per spec § Unit 3 R03.6.
  content: MessageContentBlock[]
  status: TurnLifecycle
  // Idempotency key carried by every user turn. Retries reuse the SAME
  // key per spec § Unit 3 R03.9.
  idempotencyKey?: string
}

export interface OnboardingChatState {
  turns: OnboardingTurn[]
  // Topic the assistant is currently asking about (null until the first
  // Ask turn arrives, or when onboarding has completed).
  currentTopic: OnboardingTopic | null
  // The frontend mirror of the server's `Ask.suggestedInputType` so the
  // input dispatcher can render the right control even after a refresh.
  suggestedInputType: SuggestedInputType | null
  // Topics whose Ask turn has been answered, in arrival order. Drives the
  // six-segment progress indicator.
  completedTopics: OnboardingTopic[]
  // True while a `submitOnboardingTurn` POST is in flight. Used to disable
  // the input row + apply the `pending` flag to the in-flight user bubble.
  isSubmitting: boolean
  // True once `kind: complete` lands and the page is navigating to `/`.
  isComplete: boolean
}

const initialState: OnboardingChatState = {
  turns: [],
  currentTopic: null,
  suggestedInputType: null,
  completedTopics: [],
  isSubmitting: false,
  isComplete: false,
}

export interface AppendUserTurnPayload {
  id: string
  idempotencyKey: string
  text: string
}

export interface AppendAssistantTurnPayload {
  id: string
  blocks: MessageContentBlock[]
  topic: OnboardingTopic | null
  suggestedInputType: SuggestedInputType | null
  // Topics now considered completed after the new turn lands. The slice
  // stores the array verbatim because the server response is the source of
  // truth (see `progress.completedTopics`).
  completedTopics: OnboardingTopic[]
}

export const onboardingSlice = createSlice({
  name: 'onboarding',
  initialState,
  reducers: {
    transcriptReplaced: (_state, action: PayloadAction<OnboardingChatState>) => {
      // Used by the page-level effect when `getOnboardingState` returns
      // 200 with an existing transcript — the slice fully replaces local
      // state with the server-derived view so a mid-flow refresh resumes.
      return action.payload
    },
    transcriptCleared: () => initialState,
    submitStarted: (state, action: PayloadAction<AppendUserTurnPayload>) => {
      state.isSubmitting = true
      state.turns.push({
        id: action.payload.id,
        idempotencyKey: action.payload.idempotencyKey,
        role: 'user',
        // Echo the runner's text as a single text content block so
        // `MessageBubble` can render it through the same code path it
        // uses for assistant text blocks.
        content: [{ type: 'text', text: action.payload.text }],
        status: 'pending',
      })
    },
    submitFailed: (state, action: PayloadAction<{ id: string }>) => {
      state.isSubmitting = false
      const turn = state.turns.find((candidate) => candidate.id === action.payload.id)
      if (turn !== undefined) {
        turn.status = 'failed'
      }
    },
    submitRetryStarted: (state, action: PayloadAction<{ id: string }>) => {
      state.isSubmitting = true
      const turn = state.turns.find((candidate) => candidate.id === action.payload.id)
      if (turn !== undefined) {
        turn.status = 'pending'
      }
    },
    userTurnDelivered: (state, action: PayloadAction<{ id: string }>) => {
      const turn = state.turns.find((candidate) => candidate.id === action.payload.id)
      if (turn !== undefined) {
        turn.status = 'delivered'
      }
    },
    assistantTurnAppended: (state, action: PayloadAction<AppendAssistantTurnPayload>) => {
      state.isSubmitting = false
      state.turns.push({
        id: action.payload.id,
        role: 'assistant',
        content: action.payload.blocks,
        status: 'delivered',
      })
      state.currentTopic = action.payload.topic
      state.suggestedInputType = action.payload.suggestedInputType
      state.completedTopics = action.payload.completedTopics
    },
    buildingPlanStarted: (state, action: PayloadAction<{ id: string }>) => {
      state.isSubmitting = true
      state.turns.push({
        id: action.payload.id,
        role: 'assistant',
        // Programmatic content — the "building your plan" copy is the
        // single source of truth on the frontend, NOT an LLM string.
        content: [{ type: 'text', text: 'Building your plan…' }],
        status: 'building-plan',
      })
    },
    onboardingCompleted: (state, action: PayloadAction<AppendAssistantTurnPayload>) => {
      // Strip any in-flight building-plan placeholder so the final
      // assistant message is what the user reads in the brief moment
      // before the navigate('/') call.
      state.turns = state.turns.filter((candidate) => candidate.status !== 'building-plan')
      state.turns.push({
        id: action.payload.id,
        role: 'assistant',
        content: action.payload.blocks,
        status: 'delivered',
      })
      state.currentTopic = null
      state.suggestedInputType = null
      state.completedTopics = action.payload.completedTopics
      state.isSubmitting = false
      state.isComplete = true
    },
  },
})

export const {
  transcriptReplaced,
  transcriptCleared,
  submitStarted,
  submitFailed,
  submitRetryStarted,
  userTurnDelivered,
  assistantTurnAppended,
  buildingPlanStarted,
  onboardingCompleted,
} = onboardingSlice.actions

export const onboardingReducer = onboardingSlice.reducer
