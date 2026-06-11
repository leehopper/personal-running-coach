import type { ReactElement } from 'react'

import {
  ADAPTATION_KIND,
  type AdaptationTurnDto,
} from '~/modules/coaching/models/conversation.model'
import { BeforeAfterDiff } from './before-after-diff.component'

export interface AdaptationTurnProps {
  turn: AdaptationTurnDto
}

/**
 * One adaptation-explanation turn in the read-only panel, rendered by
 * `AdaptationKind` (spec 17 § Unit 7, OI-5):
 *
 *   - absorb — never persisted as a turn; rendered as nothing defensively
 *     should one ever arrive.
 *   - nudge — a quiet inline one-liner, no block chrome, no diff.
 *   - restructure — an expandable block carrying the coach's rationale
 *     (validate → what I saw → what I changed → path back, authored
 *     server-side) with the collapsible before/after diff and a subtle
 *     left-edge amber accent. No loud badges — the `--warning` accent is a
 *     supplementary indicator; severity is conveyed by the copy itself.
 */
export const AdaptationTurn = ({ turn }: AdaptationTurnProps): ReactElement | null => {
  if (turn.adaptationKind === ADAPTATION_KIND.absorb) {
    return null
  }

  if (turn.adaptationKind === ADAPTATION_KIND.nudge) {
    return (
      <p data-testid="nudge-turn" className="px-1 text-sm text-foreground">
        {turn.content}
      </p>
    )
  }

  return (
    <article
      data-testid="restructure-turn"
      className="flex flex-col gap-2 rounded-md border border-l-2 border-l-warning bg-card p-4"
    >
      <p className="text-sm whitespace-pre-wrap text-card-foreground">{turn.content}</p>
      <BeforeAfterDiff diff={turn.diff} />
    </article>
  )
}
