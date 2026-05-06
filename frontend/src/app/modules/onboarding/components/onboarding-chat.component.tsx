import { type ReactElement } from 'react'
import { MessageBubble } from '~/modules/coaching/components/message-bubble.component'
import { TranscriptScroller } from '~/modules/coaching/components/transcript-scroller.component'
import { InputForTopic } from './input-for-topic.component'
import { TopicProgressIndicator } from './topic-progress-indicator.component'
import {
  OnboardingTopic,
  SuggestedInputType,
  type SuggestedInputType as SuggestedInputTypeValue,
} from '~/modules/onboarding/models/onboarding.model'
import type { InputSubmissionPayload, InputProps } from './input-for-topic.types'
import type { OnboardingTurn } from '~/modules/onboarding/store/onboarding.slice'

export interface OnboardingChatProps {
  turns: readonly OnboardingTurn[]
  currentTopic: OnboardingTopic | null
  suggestedInputType: SuggestedInputTypeValue | null
  completedTopics: readonly OnboardingTopic[]
  isSubmitting: boolean
  hasFailedTurn: boolean
  onSubmit: (payload: InputSubmissionPayload) => Promise<void>
  onRetry: () => Promise<void>
}

/**
 * Composition layer for the onboarding chat surface — wires together the
 * shared `MessageBubble` + `TranscriptScroller` primitives, the
 * `TopicProgressIndicator`, and the per-topic input dispatcher. This
 * component is presentational only; all state lives in the Redux
 * `onboardingSlice` and is supplied via props by the page-level container.
 *
 * Spec § Unit 3 R03.5–R03.10 — the input dispatcher is keyed on
 * `suggestedInputType`; the bubble's pending opacity is driven by per-turn
 * `status === 'pending' | 'building-plan'`; the Retry affordance only
 * surfaces when the most-recent submission `status === 'failed'`.
 */
export const OnboardingChat = ({
  turns,
  currentTopic,
  suggestedInputType,
  completedTopics,
  isSubmitting,
  hasFailedTurn,
  onSubmit,
  onRetry,
}: OnboardingChatProps): ReactElement => {
  // Default the input dispatcher to a single-select on first paint so the
  // empty-stream onboarding flow renders the canned PrimaryGoal options
  // straight away — every server-driven turn afterwards overrides this.
  const effectiveInputType: SuggestedInputTypeValue =
    suggestedInputType ?? SuggestedInputType.SingleSelect

  return (
    <main
      data-testid="onboarding-chat"
      className="mx-auto flex min-h-screen w-full max-w-2xl flex-col gap-4 bg-slate-50 px-4 py-6"
    >
      <header className="flex flex-col gap-3">
        <h1 className="text-2xl font-semibold text-slate-900">Tell me about your running</h1>
        <TopicProgressIndicator completedTopics={completedTopics} currentTopic={currentTopic} />
      </header>
      <TranscriptScroller
        turnCount={turns.length}
        className="flex-1 rounded border border-slate-200 bg-white p-4"
      >
        {turns.map((turn) => (
          <TurnRow key={turn.id} turn={turn} />
        ))}
      </TranscriptScroller>
      {hasFailedTurn && <RetryAffordance onRetry={onRetry} isSubmitting={isSubmitting} />}
      <footer className="flex flex-col gap-2">
        <InputForTopic
          suggestedInputType={effectiveInputType}
          topic={currentTopic}
          onSubmit={(payload) => onSubmit(payload)}
          isSubmitting={isSubmitting}
        />
      </footer>
    </main>
  )
}

interface TurnRowProps {
  turn: OnboardingTurn
}

const TurnRow = ({ turn }: TurnRowProps): ReactElement => {
  const isPending = turn.status === 'pending' || turn.status === 'building-plan'
  return (
    <div
      data-testid={`turn-row-${turn.role}`}
      data-status={turn.status}
      className={turn.status === 'building-plan' ? 'flex w-full animate-pulse' : 'flex w-full'}
    >
      <MessageBubble role={turn.role} content={turn.content} pending={isPending} />
    </div>
  )
}

interface RetryAffordanceProps {
  onRetry: () => Promise<void>
  isSubmitting: boolean
}

const RetryAffordance = ({ onRetry, isSubmitting }: RetryAffordanceProps): ReactElement => (
  <div
    role="alert"
    data-testid="onboarding-retry"
    className="flex items-center justify-between rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900"
  >
    <span>That didn’t go through. Try again?</span>
    <button
      type="button"
      onClick={() => {
        void onRetry()
      }}
      disabled={isSubmitting}
      className="rounded bg-slate-900 px-3 py-1 text-xs font-medium text-white disabled:cursor-not-allowed disabled:opacity-50"
    >
      {isSubmitting ? 'Retrying…' : 'Retry'}
    </button>
  </div>
)

// Placeholder export so the file's barrel signal is unambiguous to
// maintainers grepping for InputProps.
export type { InputProps }
