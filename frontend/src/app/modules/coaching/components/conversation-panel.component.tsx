import type { ReactElement } from 'react'

import {
  ADAPTATION_KIND,
  CONVERSATION_ROLE,
  type ConversationTurnDto,
} from '~/modules/coaching/models/conversation.model'
import { AdaptationTurn } from './adaptation-turn.component'
import { SafetyTurn } from './safety-turn.component'

export interface ConversationPanelProps {
  /** The runner's turns, newest-first (the wire order is preserved). */
  turns: readonly ConversationTurnDto[]
}

// Safety turns always render; an absorb adaptation is silent by contract
// (it never persists a turn — this filter is the defensive guard).
const isRenderable = (turn: ConversationTurnDto): boolean =>
  turn.role === CONVERSATION_ROLE.systemSafety || turn.adaptationKind !== ADAPTATION_KIND.absorb

/**
 * The read-only "Explain-the-change" panel on the home surface (spec 17
 * § Unit 7, OI-5). Renders the coach's adaptation explanations and safety
 * messages newest-first; accepts no input of any kind — no message box, no
 * send control, no accept/reject affordance (open conversation is Slice 4).
 * With nothing to show it renders nothing at all: an on-plan week stays
 * silent rather than presenting an empty shell.
 */
export const ConversationPanel = ({ turns }: ConversationPanelProps): ReactElement | null => {
  const visibleTurns = turns.filter(isRenderable)

  if (visibleTurns.length === 0) {
    return null
  }

  return (
    <section
      aria-labelledby="conversation-panel-heading"
      data-testid="conversation-panel"
      className="flex flex-col gap-3"
    >
      <h2 id="conversation-panel-heading" className="text-lg font-semibold text-foreground">
        Coach
      </h2>
      <ol className="flex list-none flex-col gap-3 p-0">
        {visibleTurns.map((turn) => (
          <li key={turn.triggeringPlanEventId}>
            {turn.role === CONVERSATION_ROLE.systemSafety ? (
              <SafetyTurn turn={turn} />
            ) : (
              <AdaptationTurn turn={turn} />
            )}
          </li>
        ))}
      </ol>
    </section>
  )
}
