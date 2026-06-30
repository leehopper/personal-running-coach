import { useCallback, useEffect, useReducer, useRef, type Dispatch } from 'react'
import { useDispatch } from 'react-redux'

import { conversationApi } from '~/api/conversation.api'
import { XSRF_COOKIE_NAME } from '~/api/base-query'
import type {
  CandidatePrescriptionDto,
  CoachStreamFrame,
} from '~/modules/coaching/models/coach-stream.model'
import {
  CONVERSATION_TIMELINE_TURN_KIND,
  type ConversationTimelineTurnDto,
  type ReferralCategory,
  type SafetyTier,
} from '~/modules/coaching/models/conversation.model'
import type { StructuredLogDraft } from '~/api/generated'
import { loggedOut } from '~/modules/auth/store/auth.slice'
import type { AppDispatch } from '~/modules/app/app.store'

import { createSseDecoder, toCoachStreamFrame } from '~/modules/coaching/coach-stream.helpers'

// The hand-rolled SSE reader for the streaming Q&A endpoint
// (`POST /api/v1/conversation/messages`). `fetchBaseQuery` cannot stream, so
// this replicates what `baseQueryWith401Handler` does for RTK mutations:
// `credentials: 'include'`, the `X-XSRF-TOKEN` double-submit header read from
// the `__Host-Xsrf-Request` cookie, a lazy `GET /v1/auth/xsrf` seed on a cold
// first submit, and `401 → loggedOut`. The live coach reply renders from local
// React state as `token` frames arrive (never a per-token cache write) and is
// reconciled into the timeline cache exactly once on `done`. A `card` frame
// raises the confirmation card; an `error` frame is terminal and the retry
// re-sends with a fresh client message id (the partial answer is discarded).

const MESSAGES_URL = '/api/v1/conversation/messages'
const XSRF_SEED_URL = '/api/v1/auth/xsrf'
const XSRF_HEADER_NAME = 'X-XSRF-TOKEN'

const SESSION_EXPIRED_MESSAGE = 'Your session expired. Sign in and try again.'
const GENERIC_ERROR_MESSAGE = 'Something went wrong reaching your coach. Try again in a moment.'

/** A deterministic safety message surfaced before any LLM output. */
export interface CoachSafetyNotice {
  content: string
  tier: SafetyTier
  category: ReferralCategory
}

/** A parsed workout-log draft awaiting an explicit Confirm. */
export interface CoachCard {
  draft: StructuredLogDraft
  prescription: CandidatePrescriptionDto | null
  clientMessageId: string
}

/** A terminal stream failure with its retry hint. */
export interface CoachStreamError {
  message: string
  retryable: boolean
  retryAfterSeconds: number | null
}

export interface UseCoachStreamReturn {
  /** The optimistic user bubble for the in-flight exchange, or null when idle. */
  pendingUserMessage: string | null
  /** The coach reply accumulating token-by-token (local state, not the cache). */
  streamingText: string
  /** True while a request is open — the composer disables on this. */
  isStreaming: boolean
  safety: CoachSafetyNotice | null
  card: CoachCard | null
  error: CoachStreamError | null
  send: (message: string) => Promise<void>
  retry: () => Promise<void>
  dismissCard: () => void
}

interface CoachStreamState {
  pendingUserMessage: string | null
  streamingText: string
  isStreaming: boolean
  safety: CoachSafetyNotice | null
  card: CoachCard | null
  error: CoachStreamError | null
}

const INITIAL_STATE: CoachStreamState = {
  pendingUserMessage: null,
  streamingText: '',
  isStreaming: false,
  safety: null,
  card: null,
  error: null,
}

type CoachStreamAction =
  | { type: 'start'; message: string }
  | { type: 'token'; delta: string }
  | { type: 'safety'; notice: CoachSafetyNotice }
  | { type: 'card'; card: CoachCard }
  | { type: 'error'; error: CoachStreamError }
  | { type: 'reconciled' }
  | { type: 'streamEnded' }
  | { type: 'dismissCard' }

const reducer = (state: CoachStreamState, action: CoachStreamAction): CoachStreamState => {
  switch (action.type) {
    case 'start':
      return {
        pendingUserMessage: action.message,
        streamingText: '',
        isStreaming: true,
        safety: null,
        card: null,
        error: null,
      }
    case 'token':
      return { ...state, streamingText: state.streamingText + action.delta }
    case 'safety':
      return { ...state, safety: action.notice }
    case 'card':
      // The user turn is reconciled into the cache; clear the live bubble and
      // settle the stream while the card awaits Confirm.
      return {
        ...state,
        card: action.card,
        pendingUserMessage: null,
        streamingText: '',
        isStreaming: false,
      }
    case 'error':
      // The partial answer is discarded from view (and never reconciled); the
      // user bubble stays beside the retry affordance.
      return { ...state, error: action.error, streamingText: '', isStreaming: false }
    case 'reconciled':
      // `done`: the user + coach turns are now in the timeline cache.
      return { ...state, pendingUserMessage: null, streamingText: '', isStreaming: false }
    case 'streamEnded':
      // Normally a no-op — the card flow already cleared the live state and a
      // done/error frame returns before this. Defense in depth: if the body
      // closed after tokens with no terminal frame, surface a retry rather than
      // freezing a partial bubble.
      if (state.pendingUserMessage === null && state.streamingText.length === 0) {
        return { ...state, isStreaming: false }
      }
      return {
        ...state,
        isStreaming: false,
        pendingUserMessage: null,
        streamingText: '',
        error: { message: GENERIC_ERROR_MESSAGE, retryable: true, retryAfterSeconds: null },
      }
    case 'dismissCard':
      return { ...state, card: null }
    default:
      return state
  }
}

const readXsrfCookie = (): string | null => {
  if (typeof document === 'undefined') return null
  const prefix = `${XSRF_COOKIE_NAME}=`
  const hit = document.cookie
    .split(';')
    .map((segment) => segment.trim())
    .find((segment) => segment.startsWith(prefix))
  if (hit === undefined) return null
  try {
    return decodeURIComponent(hit.slice(prefix.length))
  } catch {
    return null
  }
}

/** Lazily seed the antiforgery cookie — a fast first submit can outrun the boot seed. */
const ensureXsrfSeed = async (): Promise<void> => {
  if (readXsrfCookie() === null) {
    await fetch(XSRF_SEED_URL, { method: 'GET', credentials: 'include' })
  }
}

const buildMessageHeaders = (): Record<string, string> => {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  const token = readXsrfCookie()
  if (token !== null) headers[XSRF_HEADER_NAME] = token
  return headers
}

const interactiveTurn = (
  kind: typeof CONVERSATION_TIMELINE_TURN_KIND.user | typeof CONVERSATION_TIMELINE_TURN_KIND.coach,
  turnId: string,
  content: string,
): ConversationTimelineTurnDto => ({
  kind,
  turnId,
  createdAt: new Date().toISOString(),
  interactive: { content, isErrored: false },
  proactive: null,
})

interface FrameContext {
  message: string
  clientMessageId: string
  dispatch: Dispatch<CoachStreamAction>
  appendTimelineTurn: (turn: ConversationTimelineTurnDto) => void
  coach: { text: string }
}

/** Applies one frame; returns true when the stream is terminal (`done`/`error`). */
const processFrame = (frame: CoachStreamFrame, ctx: FrameContext): boolean => {
  switch (frame.event) {
    case 'token':
      ctx.coach.text += frame.delta
      ctx.dispatch({ type: 'token', delta: frame.delta })
      return false
    case 'safety':
      ctx.dispatch({
        type: 'safety',
        notice: { content: frame.content, tier: frame.tier, category: frame.category },
      })
      return false
    case 'card':
      ctx.appendTimelineTurn(
        interactiveTurn(CONVERSATION_TIMELINE_TURN_KIND.user, ctx.clientMessageId, ctx.message),
      )
      ctx.dispatch({
        type: 'card',
        card: {
          draft: frame.draft,
          prescription: frame.prescription,
          clientMessageId: ctx.clientMessageId,
        },
      })
      return false
    case 'error':
      ctx.dispatch({
        type: 'error',
        error: {
          message: frame.message,
          retryable: frame.retryable,
          retryAfterSeconds: frame.retryAfterSeconds,
        },
      })
      return true
    case 'done':
      ctx.appendTimelineTurn(
        interactiveTurn(CONVERSATION_TIMELINE_TURN_KIND.user, ctx.clientMessageId, ctx.message),
      )
      if (ctx.coach.text.length > 0) {
        ctx.appendTimelineTurn(
          interactiveTurn(CONVERSATION_TIMELINE_TURN_KIND.coach, frame.turnId, ctx.coach.text),
        )
      }
      ctx.dispatch({ type: 'reconciled' })
      return true
  }
}

const readStream = async (
  reader: ReadableStreamDefaultReader<Uint8Array>,
  ctx: FrameContext,
): Promise<void> => {
  const decoder = createSseDecoder()
  const textDecoder = new TextDecoder()
  for (;;) {
    const { done, value } = await reader.read()
    if (done) {
      ctx.dispatch({ type: 'streamEnded' })
      return
    }
    for (const raw of decoder.push(textDecoder.decode(value, { stream: true }))) {
      const frame = toCoachStreamFrame(raw)
      if (frame !== null && processFrame(frame, ctx)) return
    }
  }
}

export const useCoachStream = (): UseCoachStreamReturn => {
  const reduxDispatch = useDispatch<AppDispatch>()
  const [state, dispatch] = useReducer(reducer, INITIAL_STATE)
  const abortRef = useRef<AbortController | null>(null)
  const lastMessageRef = useRef<string | null>(null)

  useEffect(() => () => abortRef.current?.abort(), [])

  const appendTimelineTurn = useCallback(
    (turn: ConversationTimelineTurnDto): void => {
      const patch = reduxDispatch(
        conversationApi.util.updateQueryData('getConversationTimeline', undefined, (draft) => {
          draft.turns.push(turn)
        }),
      )
      // Cold-start race: when the mount-time timeline GET is still in flight (or
      // was never initiated), the cache entry has no `data`, so `updateQueryData`
      // silently no-ops — patching nothing. The optimistic turn would be lost and,
      // because this streaming endpoint invalidates no tag, nothing would refetch
      // it back. Fall back to invalidating the `Conversation` tag: RTK defers the
      // invalidation behind the in-flight GET and refetches the server-persisted
      // turns once it settles. On the warm path the recipe patches in place and
      // produces patches, so this fallback is skipped (no extra fetch).
      if (patch.patches.length === 0) {
        reduxDispatch(conversationApi.util.invalidateTags(['Conversation']))
      }
    },
    [reduxDispatch],
  )

  const runStream = useCallback(
    async (message: string, clientMessageId: string): Promise<void> => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      dispatch({ type: 'start', message })

      try {
        await ensureXsrfSeed()
        const response = await fetch(MESSAGES_URL, {
          method: 'POST',
          credentials: 'include',
          headers: buildMessageHeaders(),
          body: JSON.stringify({ message, clientMessageId }),
          signal: controller.signal,
        })

        if (response.status === 401) {
          reduxDispatch(loggedOut())
          dispatch({
            type: 'error',
            error: { message: SESSION_EXPIRED_MESSAGE, retryable: false, retryAfterSeconds: null },
          })
          return
        }
        if (!response.ok || response.body === null) {
          dispatch({
            type: 'error',
            error: { message: GENERIC_ERROR_MESSAGE, retryable: true, retryAfterSeconds: null },
          })
          return
        }

        await readStream(response.body.getReader(), {
          message,
          clientMessageId,
          dispatch,
          appendTimelineTurn,
          coach: { text: '' },
        })
      } catch {
        // An intentional cancel/unmount aborts silently; everything else is a
        // network failure the user can retry.
        if (controller.signal.aborted) return
        dispatch({
          type: 'error',
          error: { message: GENERIC_ERROR_MESSAGE, retryable: true, retryAfterSeconds: null },
        })
      } finally {
        if (abortRef.current === controller) abortRef.current = null
      }
    },
    [appendTimelineTurn, reduxDispatch],
  )

  const send = useCallback(
    async (message: string): Promise<void> => {
      lastMessageRef.current = message
      await runStream(message, crypto.randomUUID())
    },
    [runStream],
  )

  const retry = useCallback(async (): Promise<void> => {
    const message = lastMessageRef.current
    if (message === null) return
    await runStream(message, crypto.randomUUID())
  }, [runStream])

  const dismissCard = useCallback((): void => dispatch({ type: 'dismissCard' }), [])

  return {
    pendingUserMessage: state.pendingUserMessage,
    streamingText: state.streamingText,
    isStreaming: state.isStreaming,
    safety: state.safety,
    card: state.card,
    error: state.error,
    send,
    retry,
    dismissCard,
  }
}
