import { useCallback, useRef } from 'react'
import { useDispatch } from 'react-redux'
import { useSubmitOnboardingTurnMutation } from '~/api/onboarding.api'
import type { MessageContentBlock } from '~/modules/coaching/components/message-bubble.component'
import {
  AnthropicContentBlockType,
  OnboardingTopic,
  OnboardingTurnKind,
  type AnthropicContentBlock,
  type OnboardingTurnResponse,
} from '~/modules/onboarding/models/onboarding.model'
import { onboardingTurnResponseSchema } from '~/modules/onboarding/schemas/onboarding-turn-response.schema'
import {
  assistantTurnAppended,
  buildingPlanStarted,
  onboardingCompleted,
  submitFailed,
  submitRetryStarted,
  submitStarted,
  userTurnDelivered,
} from '~/modules/onboarding/store/onboarding.slice'
import type { AppDispatch } from '~/modules/app/app.store'

const DEFAULT_TOTAL_TOPICS = 6

export interface SubmitTurnArgs {
  text: string
}

export interface UseOnboardingTurnReturn {
  submitTurn: (args: SubmitTurnArgs) => Promise<void>
  /**
   * Re-submit the most recently failed user turn using the SAME
   * idempotency key per spec § Unit 3 R03.9. Returns a no-op promise if
   * there is no failed turn to retry.
   */
  retryLastFailedTurn: () => Promise<void>
}

/**
 * Adapt the wire-format `AnthropicContentBlock` (integer-enum'd) into the
 * `MessageContentBlock` shape consumed by the shared `MessageBubble`
 * primitive. `MessageBubble` is reused verbatim by Slice 4, so the adapter
 * lives at this seam — never inside the renderer.
 */
const toRenderableContent = (blocks: AnthropicContentBlock[]): MessageContentBlock[] =>
  blocks.map((block) => {
    if (block.type === AnthropicContentBlockType.Text) {
      return { type: 'text', text: block.text }
    }
    // Anthropic `thinking` blocks must NEVER reach the runner per spec
    // § Unit 3 R03.6 — the bubble already filters non-text blocks, but
    // the adapter keeps the contract explicit and forward-compatible
    // when more block kinds land in Slice 4.
    return { type: 'thinking', thinking: block.text }
  })

/**
 * Encapsulates one full pessimistic-UI submit cycle for an onboarding
 * turn:
 *   1. mint an idempotency key + turn id;
 *   2. dispatch `submitStarted` so the user bubble paints immediately as
 *      `pending`;
 *   3. POST `/api/v1/onboarding/turns` carrying the same key;
 *   4. on success → flip the user bubble to `delivered`, append the
 *      assistant turn, and (when `kind: complete`) replace the in-flight
 *      "building plan" placeholder with the final assistant blocks;
 *   5. on failure → flip the user bubble to `failed` for the Retry
 *      affordance, reusing the same key on retry per spec § Unit 3 R03.9.
 *
 * The hook owns the in-flight idempotency key + turn id ref so that the
 * Retry path POSTs with byte-identical payload — the server's
 * idempotency-store keys on this UUID and short-circuits duplicates.
 */
export const useOnboardingTurn = (): UseOnboardingTurnReturn => {
  const dispatch = useDispatch<AppDispatch>()
  const [submit] = useSubmitOnboardingTurnMutation()
  // Tracks the most-recently submitted turn's correlation pair so the
  // Retry affordance can POST with the SAME idempotency key + turn id.
  const lastSubmissionRef = useRef<{
    turnId: string
    idempotencyKey: string
    text: string
  } | null>(null)

  const handleResponse = useCallback(
    (turnId: string, raw: OnboardingTurnResponse): void => {
      // Defense in depth: the discriminated-union Zod schema is the
      // contract gate. The RTK Query response is typed but the wire could
      // drift; `safeParse` keeps the page resilient to bad payloads
      // without throwing into the React render path.
      const parsed = onboardingTurnResponseSchema.safeParse(raw)
      if (!parsed.success) {
        dispatch(submitFailed({ id: turnId }))
        return
      }
      const response = parsed.data
      if (response.kind === OnboardingTurnKind.Error) {
        dispatch(submitFailed({ id: turnId }))
        return
      }
      // Either Ask or Complete from this point — the user bubble is now
      // server-acknowledged.
      dispatch(userTurnDelivered({ id: turnId }))
      const assistantBlocks = toRenderableContent(response.assistantBlocks)
      const completedTopics: OnboardingTopic[] = inferCompletedTopics(
        response.progress.completedTopics,
      )
      const assistantTurnId = crypto.randomUUID()
      if (response.kind === OnboardingTurnKind.Complete) {
        dispatch(
          onboardingCompleted({
            id: assistantTurnId,
            blocks: assistantBlocks,
            topic: null,
            suggestedInputType: null,
            completedTopics,
          }),
        )
        return
      }
      const askResponse = response
      dispatch(
        assistantTurnAppended({
          id: assistantTurnId,
          blocks: assistantBlocks,
          topic: askResponse.topic,
          suggestedInputType: askResponse.suggestedInputType,
          completedTopics,
        }),
      )
    },
    [dispatch],
  )

  const dispatchPostSubmit = useCallback(
    async (turnId: string, idempotencyKey: string, text: string): Promise<void> => {
      // Spec § Unit 3 R03.10 — the FINAL turn's POST blocks for the
      // duration of plan generation. Show the programmatic "building your
      // plan" assistant turn after a brief grace period so a fast Ask
      // response doesn't flicker the placeholder. The grace period is a
      // setTimeout so the placeholder only paints when the request truly
      // is slow; canceled the moment the response lands.
      const buildingPlanId = crypto.randomUUID()
      const buildingPlanTimeoutId = window.setTimeout(() => {
        dispatch(buildingPlanStarted({ id: buildingPlanId }))
      }, 1500)

      try {
        const response = await submit({ idempotencyKey, text }).unwrap()
        window.clearTimeout(buildingPlanTimeoutId)
        handleResponse(turnId, response)
      } catch {
        // RTK Query's `unwrap()` throws on any non-2xx — Zod parse failures
        // also surface here because the schema is invoked inside
        // `handleResponse`. Treat both as `failed` so the Retry affordance
        // appears.
        window.clearTimeout(buildingPlanTimeoutId)
        dispatch(submitFailed({ id: turnId }))
      }
    },
    [dispatch, handleResponse, submit],
  )

  const submitTurn = useCallback(
    async ({ text }: SubmitTurnArgs): Promise<void> => {
      const turnId = crypto.randomUUID()
      const idempotencyKey = crypto.randomUUID()
      lastSubmissionRef.current = { turnId, idempotencyKey, text }
      dispatch(submitStarted({ id: turnId, idempotencyKey, text }))
      await dispatchPostSubmit(turnId, idempotencyKey, text)
    },
    [dispatch, dispatchPostSubmit],
  )

  const retryLastFailedTurn = useCallback(async (): Promise<void> => {
    const last = lastSubmissionRef.current
    if (last === null) {
      return
    }
    dispatch(submitRetryStarted({ id: last.turnId }))
    await dispatchPostSubmit(last.turnId, last.idempotencyKey, last.text)
  }, [dispatch, dispatchPostSubmit])

  return { submitTurn, retryLastFailedTurn }
}

/**
 * Map a server-provided `completedTopics` count to the canonical topic
 * list. The wire shape is `{ completedTopics: number, totalTopics: number }`
 * (count only); the slice and progress indicator both consume the actual
 * topic enum values, so this helper expands the count into the canonical
 * DEC-047 prefix. Order matches the topic ordering the assistant follows.
 */
const inferCompletedTopics = (completedCount: number): OnboardingTopic[] => {
  const ordered: OnboardingTopic[] = [
    OnboardingTopic.PrimaryGoal,
    OnboardingTopic.TargetEvent,
    OnboardingTopic.CurrentFitness,
    OnboardingTopic.WeeklySchedule,
    OnboardingTopic.InjuryHistory,
    OnboardingTopic.Preferences,
  ]
  const clamped = Math.min(Math.max(completedCount, 0), DEFAULT_TOTAL_TOPICS)
  return ordered.slice(0, clamped)
}
