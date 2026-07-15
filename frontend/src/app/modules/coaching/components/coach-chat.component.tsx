import { Fragment, useCallback, useState, type ReactElement } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'

import { Button } from '@/components/ui/button'
import type { PreferredUnits } from '~/api/generated'
import {
  useConfirmConversationalLogMutation,
  useGetConversationTimelineQuery,
} from '~/api/conversation.api'
import { reportClientError } from '~/error-boundary/report-client-error'
import {
  useCoachStream,
  type CoachStreamError,
} from '~/modules/coaching/hooks/use-coach-stream.hooks'
import {
  CONVERSATION_ROLE,
  CONVERSATION_TIMELINE_TURN_KIND,
  type ConversationTimelineTurnDto,
} from '~/modules/coaching/models/conversation.model'
import { usePlan } from '~/modules/plan/hooks/use-plan.hooks'
import { usePreferredUnits } from '~/modules/settings/hooks/use-preferred-units.hooks'
import { AdaptationTurn } from './adaptation-turn.component'
import { CoachComposer } from './coach-composer.component'
import { CoachTextTurn } from './coach-text-turn.component'
import { DateDivider } from './date-divider.component'
import { LogConfirmationCard } from './log-confirmation-card.component'
import { SafetyTurn } from './safety-turn.component'
import { formatTurnTime, groupTurnsByLocalDay } from './transcript-time.helpers'
import { TranscriptScroller } from './transcript-scroller.component'
import { UserTurn } from './user-turn.component'

// The interactive streaming-conversation panel. It unions the composed
// timeline (turn-kind components + the proactive adaptation/safety turns,
// reusing the existing components) with the live in-flight exchange from
// `useCoachStream`, the confirmation card, and the composer. Persisted
// user/coach turns render via `UserTurn`/`CoachTextTurn` (Slice 3 PR-B) —
// both are plain-text (`whitespace-pre-wrap`), never markdown, never raw
// HTML.

const NO_TURNS: readonly ConversationTimelineTurnDto[] = []

/**
 * `router.state` shape carried by a `navigate('/coach', { state: {...} })`
 * call. `prefill` seeds the composer's text; `focusComposer` focuses it
 * without seeding text. Plain `TabBar` navigation carries no `state`, so
 * `state` is `null` and neither applies.
 */
interface CoachChatLocationState {
  prefill?: string
  focusComposer?: boolean
}

// `location.state` is typed loosely by react-router-dom (it has to accommodate
// any shape a caller passes to `navigate(to, { state })`), so a plain `as`
// cast here would silently accept a differently-shaped `state` from a future
// caller. This guard actually checks the two optional fields' types before
// the cast-free narrowing below trusts them.
const isCoachChatLocationState = (value: unknown): value is CoachChatLocationState =>
  typeof value === 'object' &&
  value !== null &&
  (!('prefill' in value) || typeof (value as { prefill?: unknown }).prefill === 'string') &&
  (!('focusComposer' in value) ||
    typeof (value as { focusComposer?: unknown }).focusComposer === 'boolean')

// A persisted historical errored turn has no live `retry()` to call — RETRY
// belongs only to the live-stream `RetryAffordance` below. Restyled as a
// `CoachTextTurn`-shaped block (mono `COACH` label, plain body) so it reads
// as part of the transcript rather than a bare disconnected line.
const ErroredCoachNote = (): ReactElement => (
  <div data-testid="coach-errored-turn" className="flex flex-col gap-1">
    <span className="font-mono text-[10px] font-semibold tracking-[0.1em] text-clay-text">
      COACH
    </span>
    <p className="font-body text-[14.5px] leading-[1.55] text-muted-foreground">
      That reply didn&apos;t go through.
    </p>
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
    className="flex items-center justify-between gap-3 rounded-md border-l-[3px] border-l-destructive bg-danger-surface px-3 py-2"
  >
    <span className="font-mono text-[12px] text-foreground">{error.message}</span>
    {error.retryable && (
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={onRetry}
        className="font-condensed text-[11px] font-semibold tracking-[0.08em] uppercase"
      >
        Retry
      </Button>
    )}
  </div>
)

const TimelineRow = ({
  turn,
  units,
  planStartDate,
}: {
  turn: ConversationTimelineTurnDto
  units: PreferredUnits
  planStartDate: string | undefined
}): ReactElement | null => {
  // Narrow on the payload null-ness — exactly one of interactive/proactive is set.
  if (turn.interactive !== null) {
    if (turn.interactive.isErrored) return <ErroredCoachNote />
    const time = formatTurnTime(turn.createdAt)
    if (turn.kind === CONVERSATION_TIMELINE_TURN_KIND.user) {
      return <UserTurn content={turn.interactive.content} time={time} />
    }
    return (
      <CoachTextTurn
        content={turn.interactive.content}
        time={time}
        loggedRun={turn.interactive.loggedRun}
        units={units}
      />
    )
  }
  return turn.proactive.role === CONVERSATION_ROLE.systemSafety ? (
    <SafetyTurn tier={turn.proactive.safetyTier} content={turn.proactive.content} />
  ) : (
    <AdaptationTurn
      turn={turn.proactive}
      units={units}
      planStartDate={planStartDate}
      time={formatTurnTime(turn.proactive.createdAt)}
    />
  )
}

export const CoachChat = (): ReactElement => {
  const { data } = useGetConversationTimelineQuery(undefined)
  const timeline = data?.turns ?? NO_TURNS
  const units = usePreferredUnits()
  const { plan } = usePlan()
  const location = useLocation()
  const locationState = isCoachChatLocationState(location.state) ? location.state : null
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

  // Both live rows (the optimistic user bubble, the streaming coach block)
  // share one client-captured `HH:MM` so the pair doesn't visibly disagree
  // as tokens arrive. Captured once per exchange via React's blessed
  // "adjust state during render" pattern (comparing against a snapshot of
  // the previous render's `isStreaming`, tracked in state rather than a ref
  // — `react-hooks/refs` forbids reading `ref.current` during render): on
  // EVERY false -> true transition of `isStreaming`, `liveTime` gets a fresh
  // `new Date()` capture. `isStreaming` flips on every `start` action — a
  // fresh send, a new message sent right after a prior error, and a RETRY
  // that re-sends the identical text all trigger it, so (unlike keying off
  // `pendingUserMessage`'s string value) an identical-text retry still gets
  // a fresh timestamp instead of inheriting the original failed attempt's.
  // This `new Date()` read is the ONE sanctioned live wall-clock read —
  // every persisted turn always uses its server `createdAt` instead.
  const [prevIsStreaming, setPrevIsStreaming] = useState(isStreaming)
  const [liveTime, setLiveTime] = useState('')
  if (isStreaming !== prevIsStreaming) {
    setPrevIsStreaming(isStreaming)
    if (isStreaming) setLiveTime(formatTurnTime(new Date().toISOString()))
  }

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

  // Date dividers are computed over the persisted timeline turns only — the
  // live pending/streaming exchange belongs to "today" and renders after
  // the last group with no live-introduced divider (§4.1/§6).
  const dayGroups = groupTurnsByLocalDay(timeline)

  return (
    <section
      aria-labelledby="coach-chat-heading"
      data-testid="coach-chat"
      className="flex min-h-0 flex-1 flex-col gap-3"
    >
      <h2 id="coach-chat-heading" className="text-lg font-semibold text-foreground">
        Coach
      </h2>
      <TranscriptScroller
        turnCount={scrollKey}
        className="min-h-0 flex-1 rounded-md border border-border bg-card p-4"
      >
        {dayGroups.map((group) => (
          <Fragment key={`day-${group.turns[0].turnId}`}>
            <DateDivider label={group.label} />
            {group.turns.map((turn) => (
              <TimelineRow
                key={turn.turnId}
                turn={turn}
                units={units}
                planStartDate={plan?.planStartDate}
              />
            ))}
          </Fragment>
        ))}
        {pendingUserMessage !== null && <UserTurn content={pendingUserMessage} time={liveTime} />}
        {streamingText.length > 0 && (
          <CoachTextTurn content={streamingText} time={liveTime} streaming />
        )}
        {safety !== null && (
          <SafetyTurn
            tier={safety.tier}
            content={safety.content}
            role="alert"
            testId="coach-safety-notice"
          />
        )}
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
      <CoachComposer
        // React Router mints a fresh `location.key` on every navigation,
        // including a same-URL replace triggered by re-tapping the active
        // TabBar tab — keying on `location.key` unconditionally would remount
        // the composer on that replace and discard an in-progress draft. A
        // fresh instance is only needed when router state actually delivers
        // a prefill/focus seed, so key on `location.key` only in that case;
        // a stable key otherwise leaves the composer (and its draft) mounted
        // across a null-state re-render or navigation.
        key={locationState === null ? 'coach-composer' : location.key}
        onSend={send}
        isStreaming={isStreaming}
        initialValue={locationState?.prefill ?? ''}
        autoFocus={Boolean(locationState?.prefill) || Boolean(locationState?.focusComposer)}
      />
    </section>
  )
}
