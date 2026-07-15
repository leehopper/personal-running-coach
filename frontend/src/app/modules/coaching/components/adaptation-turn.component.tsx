import type { ReactElement } from 'react'

import { PreferredUnits } from '~/api/generated'
import {
  ADAPTATION_KIND,
  type AdaptationTurnDto,
} from '~/modules/coaching/models/conversation.model'
import { BeforeAfterDiff } from './before-after-diff.component'

export interface AdaptationTurnProps {
  turn: AdaptationTurnDto
  /**
   * Display unit for the restructure diff's distances. Defaults to
   * Kilometers so callers that predate the unit preference (and isolated
   * tests) render the km form unchanged.
   */
  units?: PreferredUnits
  /**
   * The owning plan's calendar anchor, threaded through to `BeforeAfterDiff`
   * (spec §3 PR-C). `undefined` degrades every diff row's locus to the
   * week-index form.
   */
  planStartDate?: string
  /** The turn's local wall-clock `HH:MM`, supplied by the caller (`formatTurnTime(turn.createdAt)`). */
  time: string
}

/**
 * One adaptation-explanation turn in the read-only panel, rendered by
 * `AdaptationKind` (spec 17 § Unit 7, OI-5):
 *
 *   - absorb — never persisted as a turn; rendered as nothing defensively
 *     should one ever arrive.
 *   - nudge — a quiet inline one-liner, no block chrome, no diff.
 *   - restructure — a `PLAN ADJUSTED` card carrying the coach's rationale
 *     (validate → what I saw → what I changed → path back, authored
 *     server-side) with the collapsible before/after diff and a 2px clay
 *     left-edge marker (`--clay-marker`, border/fill-only — no loud badges;
 *     severity is conveyed by the copy itself, not the accent).
 */
export const AdaptationTurn = ({
  turn,
  units = PreferredUnits.Kilometers,
  planStartDate,
  time,
}: AdaptationTurnProps): ReactElement | null => {
  if (turn.adaptationKind === ADAPTATION_KIND.absorb) {
    return null
  }

  if (turn.adaptationKind === ADAPTATION_KIND.nudge) {
    return (
      <p data-testid="nudge-turn" className="font-body text-[14px] text-foreground">
        {turn.content}
      </p>
    )
  }

  return (
    <article
      data-testid="restructure-turn"
      className="rounded-lg border border-border border-l-2 border-l-clay-marker bg-card p-[14px]"
    >
      <div className="flex items-baseline justify-between">
        <span className="font-condensed text-[12px] font-semibold tracking-[0.16em] text-clay-text uppercase">
          PLAN ADJUSTED
        </span>
        <span className="font-mono text-[10px] text-[var(--alp-faint)]">{time}</span>
      </div>
      <p className="mt-2 font-body text-[14px] leading-[1.55] whitespace-pre-wrap text-foreground">
        {turn.content}
      </p>
      <BeforeAfterDiff diff={turn.diff} units={units} planStartDate={planStartDate} />
    </article>
  )
}
