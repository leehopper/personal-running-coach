import { useCallback, type ReactElement } from 'react'
import { useNavigate } from 'react-router-dom'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import {
  useConfirmConversationalLogMutation,
  useGetConversationTimelineQuery,
} from '~/api/conversation.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import {
  useCoachStream,
  type CoachSafetyNotice,
  type CoachStreamError,
} from '~/modules/coaching/hooks/use-coach-stream.hooks'
import {
  CONVERSATION_ROLE,
  CONVERSATION_TIMELINE_TURN_KIND,
  SAFETY_TIER,
  type ConversationTimelineTurnDto,
} from '~/modules/coaching/models/conversation.model'
import { AdaptationTurn } from './adaptation-turn.component'
import { CoachComposer } from './coach-composer.component'
import { LogConfirmationCard } from './log-confirmation-card.component'
import { MessageBubble, type MessageRole } from './message-bubble.component'
import { SafetyTurn } from './safety-turn.component'
import { TranscriptScroller } from './transcript-scroller.component'

// The interactive streaming-conversation panel. It unions the composed timeline
// (interactive chat bubbles + the proactive adaptation/safety turns, reusing the
// existing components) with the live in-flight exchange from `useCoachStream`,
// the confirmation card, and the composer. The streamed coach reply renders as
// plain text via the shared `MessageBubble` (`whitespace-pre-wrap`) — no
// markdown, no raw HTML.

const NO_TURNS: readonly ConversationTimelineTurnDto[] = []

interface ChatBubbleProps {
  role: MessageRole
  text: string
  pending?: boolean
}

const ChatBubble = ({ role, text, pending }: ChatBubbleProps): ReactElement => (
  <div className="flex w-full">
    <MessageBubble role={role} content={[{ type: 'text', text }]} pending={pending} />
  </div>
)

const ErroredCoachNote = (): ReactElement => (
  <p data-testid="coach-errored-turn" className="px-1 text-sm text-muted-foreground">
    That reply didn&apos;t go through.
  </p>
)

const SafetyNotice = ({ notice }: { notice: CoachSafetyNotice }): ReactElement => (
  <div
    role="alert"
    data-testid="coach-safety-notice"
    className={`rounded-md border border-l-2 bg-card p-4 text-sm whitespace-pre-wrap text-card-foreground ${
      notice.tier === SAFETY_TIER.red ? 'border-l-destructive' : 'border-l-warning'
    }`}
  >
    {notice.content}
  </div>
)

interface RetryAffordanceProps {
  error: CoachStreamError
  onRetry: () => void
}

const RetryAffordance = ({ error, onRetry }: RetryAffordanceProps): ReactElement => (
  <div
    role="alert"
    data-testid="coach-error"
    className="flex items-center justify-between gap-3 rounded-md border border-border bg-secondary px-3 py-2 text-sm"
  >
    <span className="text-destructive">{error.message}</span>
    {error.retryable && (
      <Button type="button" variant="outline" size="sm" onClick={onRetry}>
        Retry
      </Button>
    )}
  </div>
)

const TimelineRow = ({ turn }: { turn: ConversationTimelineTurnDto }): ReactElement | null => {
  // Narrow on the payload null-ness — exactly one of interactive/proactive is set.
  if (turn.interactive !== null) {
    if (turn.interactive.isErrored) return <ErroredCoachNote />
    const role: MessageRole =
      turn.kind === CONVERSATION_TIMELINE_TURN_KIND.user ? 'user' : 'assistant'
    return <ChatBubble role={role} text={turn.interactive.content} />
  }
  return turn.proactive.role === CONVERSATION_ROLE.systemSafety ? (
    <SafetyTurn turn={turn.proactive} />
  ) : (
    <AdaptationTurn turn={turn.proactive} />
  )
}

export const CoachChat = (): ReactElement => {
  const { data } = useGetConversationTimelineQuery(undefined)
  const timeline = data?.turns ?? NO_TURNS
  const {
    pendingUserMessage,
    streamingText,
    isStreaming,
    safety,
    card,
    error,
    send,
    retry,
    dismissCard,
  } = useCoachStream()
  const [confirmLog, { isLoading: isConfirming }] = useConfirmConversationalLogMutation()
  const navigate = useNavigate()

  const handleConfirm = useCallback(async (): Promise<void> => {
    if (card === null) return
    try {
      // A 200 with `adaptation.kind === Error` still means the log committed —
      // `unwrap` only throws on an HTTP failure, so the card dismisses and the
      // success-gated invalidation refetches the timeline + plan.
      await confirmLog({ draft: card.draft, clientMessageId: card.clientMessageId }).unwrap()
    } catch (error) {
      // The awaited `.unwrap()` rejection is a *handled* rejection, invisible to
      // the global error reporter + error boundary, so forward it explicitly and
      // tell the user — the card stays open for retry.
      reportClientError({
        kind: 'unhandled-rejection',
        error: error instanceof Error ? error : new Error(String(error)),
      })
      toast.error('We could not save your log. Try again in a moment.')
      return
    }
    dismissCard()
  }, [card, confirmLog, dismissCard])

  const handleEdit = useCallback((): void => {
    if (card === null) return
    navigate('/log', { state: { draft: card.draft } })
    dismissCard()
  }, [card, dismissCard, navigate])

  // TranscriptScroller auto-scrolls on `turnCount` changes only; folding the
  // streaming text length in keeps the live partial pinned to the bottom.
  const scrollKey = timeline.length + (pendingUserMessage !== null ? 1 : 0) + streamingText.length

  return (
    <section
      aria-labelledby="coach-chat-heading"
      data-testid="coach-chat"
      className="flex flex-col gap-3"
    >
      <h2 id="coach-chat-heading" className="text-lg font-semibold text-foreground">
        Coach
      </h2>
      <TranscriptScroller
        turnCount={scrollKey}
        className="max-h-[28rem] rounded-md border border-border bg-card p-4"
      >
        {timeline.map((turn) => (
          <TimelineRow key={turn.turnId} turn={turn} />
        ))}
        {pendingUserMessage !== null && <ChatBubble role="user" text={pendingUserMessage} />}
        {streamingText.length > 0 && <ChatBubble role="assistant" text={streamingText} pending />}
        {safety !== null && <SafetyNotice notice={safety} />}
      </TranscriptScroller>
      {error !== null && (
        <RetryAffordance
          error={error}
          onRetry={() => {
            void retry()
          }}
        />
      )}
      {card !== null && (
        <LogConfirmationCard
          card={card}
          isConfirming={isConfirming}
          onConfirm={() => {
            void handleConfirm()
          }}
          onEdit={handleEdit}
          onCancel={dismissCard}
        />
      )}
      <CoachComposer onSend={send} isStreaming={isStreaming} />
    </section>
  )
}
