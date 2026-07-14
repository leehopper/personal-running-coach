import type { ReactElement } from 'react'
import { ChevronRight } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { cn } from '@/lib/utils'
import type { PreferredUnits } from '~/api/generated'
import { useGetConversationTimelineQuery } from '~/api/conversation.api'
import { MonoLabel } from '~/modules/common/components/mono-label/mono-label.component'
import { SectionRule } from '~/modules/common/components/section-rule/section-rule.component'
import {
  ADAPTATION_KIND,
  CONVERSATION_ROLE,
  CONVERSATION_TIMELINE_TURN_KIND,
  type ConversationTimelineTurnDto,
  type PlanAdaptationDiffDto,
} from '~/modules/coaching/models/conversation.model'
import { composeAdaptationHeadline } from './adaptation-digest.helpers'

/** Props for {@link CoachDigest}. */
export interface CoachDigestProps {
  /** 1-based current training week, from `resolveCurrentWeek` — threaded into the state-3 headline. */
  currentWeek: number
  units: PreferredUnits
  className?: string
}

// Source copy stays sentence case ("Open →") — `uppercase` renders it
// `OPEN →`, the same presentation-only-caps rule every other label in this
// slice follows (frontend CLAUDE.md § Typography).
const OPEN_ARROW_CLASS =
  'font-condensed text-[12px] font-semibold tracking-[0.1em] text-clay-text uppercase'

const CHIP_CLASS =
  't-button min-h-11 rounded-full border border-border px-4 text-foreground transition-colors duration-200 ease-out focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] focus-visible:outline-none active:scale-[0.98] motion-reduce:transition-none'

/**
 * The digest's resolved body shape — a pure derivation from the composed
 * timeline's last two turns (Slice 2 spec §1 PR-C "Branch:" list).
 *
 * `restructure` fully replaces the border-left text block (DU-5's "card
 * fully replaces… not nested inside it"), so it carries only the raw
 * `diff` — `composeAdaptationHeadline` is called at render time with the
 * live `currentWeek`/`units` props. `plain` covers states 1/2 AND the
 * nudge/safety/errored sub-branches, which all fold into the same clamped
 * treatment; `coachLine === null` is the "no reply yet" case (`latest.kind
 * === user`), which renders no coach paragraph at all.
 */
type CoachDigestBody =
  | { kind: 'empty' }
  | { kind: 'restructure'; diff: PlanAdaptationDiffDto }
  | { kind: 'plain'; userLine: string | null; coachLine: string | null }

/**
 * The fixed one-step lookback (Slice 2 spec §1 PR-C "userLine rule"): reads
 * `turns[turns.length - 2]` as its own array element, never a field nested on
 * the latest turn. Index arithmetic, not `Array.prototype.at` (ES2020
 * target — see `plan-display.helpers.ts`'s `phaseForWeek` for the same
 * convention).
 */
const resolveDigestBody = (turns: readonly ConversationTimelineTurnDto[]): CoachDigestBody => {
  const latest = turns[turns.length - 1]
  if (latest === undefined) {
    return { kind: 'empty' }
  }

  const previous = turns[turns.length - 2]
  const priorUserLine =
    previous !== undefined && previous.kind === CONVERSATION_TIMELINE_TURN_KIND.user
      ? previous.interactive.content
      : null

  if (latest.interactive !== null) {
    if (latest.kind === CONVERSATION_TIMELINE_TURN_KIND.user) {
      // No reply yet — no lookback happens, no coach paragraph renders.
      return { kind: 'plain', userLine: latest.interactive.content, coachLine: null }
    }
    const coachLine = latest.interactive.isErrored
      ? "That reply didn't go through."
      : latest.interactive.content
    return { kind: 'plain', userLine: priorUserLine, coachLine }
  }

  if (
    latest.proactive.role === CONVERSATION_ROLE.assistantAdaptation &&
    latest.proactive.adaptationKind === ADAPTATION_KIND.restructure
  ) {
    return { kind: 'restructure', diff: latest.proactive.diff }
  }

  // Nudge, safety, and the structurally-unreachable absorb case all fold
  // into the plain treatment — the userLine rule is scoped to
  // `latest.kind === coach` only, so no "You:" line here regardless of
  // `previous`.
  return { kind: 'plain', userLine: null, coachLine: latest.proactive.content }
}

interface EmptyStateProps {
  className?: string
}

const EmptyState = ({ className }: EmptyStateProps): ReactElement => {
  const navigate = useNavigate()
  return (
    <div data-testid="coach-digest" className={cn('flex flex-col gap-3', className)}>
      <SectionRule as="h2" label="From your coach" />
      <p className="t-body text-muted-foreground">
        Nothing yet. Tell me how training feels, or hand me a run to log.
      </p>
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          data-testid="coach-digest-chip"
          className={CHIP_CLASS}
          onClick={() => navigate('/coach', { state: { prefill: "How's my week look?" } })}
        >
          How&apos;s my week look?
        </button>
        <button
          type="button"
          data-testid="coach-digest-chip"
          className={CHIP_CLASS}
          onClick={() => navigate('/coach', { state: { prefill: "Log this morning's run" } })}
        >
          Log this morning&apos;s run
        </button>
      </div>
    </div>
  )
}

interface DigestCardProps {
  body: Extract<CoachDigestBody, { kind: 'restructure' | 'plain' }>
  currentWeek: number
  units: PreferredUnits
  className?: string
}

/** States 1–3: the tap-through card + the always-present composer stub. */
const DigestCard = ({ body, currentWeek, units, className }: DigestCardProps): ReactElement => {
  const navigate = useNavigate()
  return (
    <div data-testid="coach-digest" className={cn('flex flex-col gap-3', className)}>
      <Link
        to="/coach"
        data-testid="coach-digest-tap-through"
        className="flex flex-col gap-3 rounded-md transition-colors duration-200 ease-out outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] motion-reduce:transition-none"
      >
        <SectionRule as="h2" label="From your coach">
          <span className={OPEN_ARROW_CLASS}>Open →</span>
        </SectionRule>
        {body.kind === 'restructure' ? (
          <div
            data-testid="coach-digest-adaptation-card"
            className="flex items-center justify-between gap-3"
          >
            <div className="flex min-w-0 flex-col gap-1">
              <MonoLabel tone="clay">Plan adjusted</MonoLabel>
              <p
                data-testid="coach-digest-adaptation-headline"
                className="t-body truncate text-foreground"
              >
                {composeAdaptationHeadline({ diff: body.diff, currentWeek, units })}
              </p>
            </div>
            <ChevronRight
              aria-hidden="true"
              className="size-4 shrink-0 text-[color:var(--alp-faint)]"
            />
          </div>
        ) : (
          <div className="flex flex-col gap-2 border-l-2 border-clay-marker pl-3">
            {body.userLine !== null ? (
              <p data-testid="coach-digest-user-line" className="truncate text-sm text-foreground">
                You: {body.userLine}
              </p>
            ) : null}
            {body.coachLine !== null ? (
              <p
                data-testid="coach-digest-coach-line"
                className="t-body line-clamp-3 text-foreground"
              >
                {body.coachLine}
              </p>
            ) : null}
          </div>
        )}
      </Link>
      <button
        type="button"
        data-testid="coach-digest-composer-stub"
        onClick={() => navigate('/coach', { state: { focusComposer: true } })}
        className="h-[46px] w-full rounded-md border border-border bg-input-fill px-3 text-left text-sm text-muted-foreground transition-colors duration-200 ease-out focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22] focus-visible:outline-none motion-reduce:transition-none"
      >
        Message your coach…
      </button>
    </div>
  )
}

/**
 * Today screen's "FROM YOUR COACH" digest — the latest exchange only, never
 * the full transcript. The user line is a 1-line ellipsis (`truncate`) and
 * the coach text is `line-clamp-3`: pathological input (a very long user
 * line, a multi-paragraph coach reply) never changes this module's height —
 * nothing in it ever scrolls (Slice 2 spec §3a / DEC-089 D3).
 *
 * A restructure-level adaptation renders as a one-line `PLAN ADJUSTED`
 * headline card whose summary is composed CLIENT-SIDE, DETERMINISTICALLY,
 * from the typed `PlanAdaptationDiff` via `composeAdaptationHeadline` — never
 * clamped LLM prose, never parsed out of prose. That summary is itself
 * `truncate`d to a single line (same "never grows the module" contract as
 * the states above): a 2-sentence composition (a weeklyTargetChange AND a
 * workoutChange, `composeAdaptationHeadline`'s realistic ceiling) reads as
 * the headline sentence plus an ellipsis, not a wrapped second line — the
 * card is a teaser, the full detail lives behind the tap-through to
 * `/coach`. The row it sits in needs `min-w-0` on this text column (a
 * flex-ROW item, unlike the flex-COLUMN ancestry states 1/2 sit in) or
 * `truncate` silently does nothing, since a flex item's default min-width
 * is its content's min-content size, not `0`. A nudge-level adaptation (or
 * a safety turn, or an errored coach reply) renders as a normal clamped
 * coach line instead.
 */
export const CoachDigest = ({ currentWeek, units, className }: CoachDigestProps): ReactElement => {
  const { data } = useGetConversationTimelineQuery(undefined)
  const body = resolveDigestBody(data?.turns ?? [])

  if (body.kind === 'empty') {
    return <EmptyState className={className} />
  }

  return <DigestCard body={body} currentWeek={currentWeek} units={units} className={className} />
}
