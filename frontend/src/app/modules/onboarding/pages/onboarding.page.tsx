import { useEffect, useMemo, type ReactElement } from 'react'
import { useDispatch, useSelector } from 'react-redux'
import { useNavigate } from 'react-router-dom'
import { useGetOnboardingStateQuery } from '~/api/onboarding.api'
import { OnboardingChat } from '~/modules/onboarding/components/onboarding-chat.component'
import { useOnboardingTurn } from '~/modules/onboarding/hooks/use-onboarding-turn.hooks'
import { OnboardingTopic, SuggestedInputType } from '~/modules/onboarding/models/onboarding.model'
import {
  transcriptCleared,
  transcriptReplaced,
  type OnboardingChatState,
} from '~/modules/onboarding/store/onboarding.slice'
import type { AppDispatch, RootState } from '~/modules/app/app.store'

/**
 * Top-level container for the `/onboarding` route. Owns the
 *   1. mount-time `getOnboardingState` query (404 → fresh start; 200 →
 *      transcript replay);
 *   2. completion-redirect to `/` when the server reports onboarding is
 *      already finished;
 *   3. submit + retry wiring through the `useOnboardingTurn` hook.
 *
 * The component is intentionally thin — the chat surface itself
 * (`OnboardingChat`) is presentational + driven by the Redux slice.
 *
 * Spec § Unit 3 R03.2 / R03.3 / R03.9 / R03.10.
 */
export const OnboardingPage = (): ReactElement => {
  const dispatch = useDispatch<AppDispatch>()
  const navigate = useNavigate()
  const { submitTurn, retryLastFailedTurn } = useOnboardingTurn()
  const { data: stateDto, isLoading, isError, error } = useGetOnboardingStateQuery(undefined)

  const chatState = useSelector((state: RootState) => state.onboarding)

  // 1. Replay the persisted server state into the Redux slice on first
  //    successful load. The chat slice is the source of truth for the
  //    rendered transcript; the RTK Query cache holds the wire payload.
  // 2. On 404 (no stream yet), reset the slice to a clean initial state
  //    so a previous run's turns from another tab cannot leak in.
  useEffect(() => {
    if (stateDto !== undefined) {
      // The Slice 1 wire shape does not surface the verbatim transcript
      // (no `messages[]` field) — the server returns lifecycle / progress
      // counters only. Replay therefore reconstructs the progress
      // indicator + current topic / input hint, but starts the visible
      // transcript empty (the next Ask turn lands as soon as the user
      // submits). This intentionally keeps the slice honest about what
      // the server has confirmed; cross-refresh transcript text comes in
      // a follow-up endpoint.
      const replayedTopics = canonicalTopicsForCount(stateDto.completedTopics)
      const hasOutstandingClarification =
        stateDto.currentTopic !== null &&
        stateDto.outstandingClarifications.includes(stateDto.currentTopic)
      const replay: OnboardingChatState = {
        turns: [],
        currentTopic: stateDto.currentTopic,
        // When the current topic has an outstanding clarification, the
        // canned single/multi/numeric/date control can't carry the
        // free-form follow-up the runner needs to provide. Fall back to
        // Text on resume so the runner can answer the assistant's
        // outstanding clarifying question; the next ask turn will return
        // the canonical control once the clarification clears.
        suggestedInputType: hasOutstandingClarification
          ? SuggestedInputType.Text
          : pickInputTypeForTopic(stateDto.currentTopic),
        completedTopics: replayedTopics,
        isSubmitting: false,
        isComplete: stateDto.isComplete,
      }
      dispatch(transcriptReplaced(replay))
    } else if (isErrorIndicatesNoStream(isError, error)) {
      dispatch(transcriptCleared())
    }
  }, [dispatch, stateDto, isError, error])

  // 3. If the server confirms onboarding is already done, redirect home.
  //    The route also redirects post-completion, so this guard catches
  //    the deep-link / bookmark case.
  useEffect(() => {
    if (stateDto?.isComplete === true) {
      navigate('/', { replace: true })
    }
  }, [navigate, stateDto?.isComplete])

  // 4. The Redux `isComplete` flag flips on `kind: complete`; the page
  //    navigates to `/` once the slice has settled the final transcript.
  useEffect(() => {
    if (chatState.isComplete) {
      navigate('/', { replace: true })
    }
  }, [chatState.isComplete, navigate])

  const hasFailedTurn = useMemo(
    () => chatState.turns.some((turn) => turn.status === 'failed'),
    [chatState.turns],
  )

  if (isLoading) {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex min-h-screen items-center justify-center bg-slate-50"
      >
        <span className="text-sm text-slate-500">Loading…</span>
      </div>
    )
  }

  return (
    <OnboardingChat
      turns={chatState.turns}
      currentTopic={chatState.currentTopic}
      suggestedInputType={chatState.suggestedInputType}
      completedTopics={chatState.completedTopics}
      isSubmitting={chatState.isSubmitting}
      hasFailedTurn={hasFailedTurn}
      onSubmit={({ text }) => submitTurn({ text })}
      onRetry={() => retryLastFailedTurn()}
    />
  )
}

/**
 * Heuristic input-type hint while the very-first server turn is in flight
 * (or after a refresh that has not yet fetched the transcript text). The
 * canonical topic order maps cleanly to the most useful input control;
 * the server-supplied `suggestedInputType` overrides this on every
 * subsequent turn.
 */
const pickInputTypeForTopic = (topic: OnboardingTopic | null) => {
  if (topic === null) return null
  switch (topic) {
    case OnboardingTopic.PrimaryGoal:
      return SuggestedInputType.SingleSelect
    case OnboardingTopic.TargetEvent:
      return SuggestedInputType.Date
    case OnboardingTopic.CurrentFitness:
      return SuggestedInputType.Numeric
    case OnboardingTopic.WeeklySchedule:
      return SuggestedInputType.MultiSelect
    case OnboardingTopic.InjuryHistory:
      return SuggestedInputType.Text
    case OnboardingTopic.Preferences:
      return SuggestedInputType.Text
    default:
      return SuggestedInputType.Text
  }
}

const canonicalTopicsForCount = (count: number): OnboardingTopic[] => {
  const order: OnboardingTopic[] = [
    OnboardingTopic.PrimaryGoal,
    OnboardingTopic.TargetEvent,
    OnboardingTopic.CurrentFitness,
    OnboardingTopic.WeeklySchedule,
    OnboardingTopic.InjuryHistory,
    OnboardingTopic.Preferences,
  ]
  const clamped = Math.min(Math.max(count, 0), order.length)
  return order.slice(0, clamped)
}

/**
 * RTK Query surfaces an opaque `FetchBaseQueryError | SerializedError`
 * union. We treat 404 as "no onboarding stream yet — start fresh"; any
 * other error keeps the cleared-but-pending UI visible until a retry.
 */
const isErrorIndicatesNoStream = (isError: boolean, error: unknown): boolean => {
  if (!isError) return false
  if (typeof error !== 'object' || error === null) return false
  const candidate = error as { status?: unknown }
  return candidate.status === 404
}

export default OnboardingPage
