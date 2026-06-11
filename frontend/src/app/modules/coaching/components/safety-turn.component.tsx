import type { ReactElement } from 'react'

import {
  SAFETY_TIER,
  type SafetyTier,
  type SafetyTurnDto,
} from '~/modules/coaching/models/conversation.model'

export interface SafetyTurnProps {
  turn: SafetyTurnDto
}

/** Wire-tier → human-readable name, exposed as `data-tier` for tests/E2E. */
const TIER_NAMES: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: 'green',
  [SAFETY_TIER.amber]: 'amber',
  [SAFETY_TIER.red]: 'red',
}

// Severity styling per tier. Red is the always-prominent case (DEC-079): the
// scripted crisis/emergency copy renders in full with the strongest accent.
// Amber referrals keep the same subtle treatment as a restructure block. A
// Green safety turn is never emitted (SafetySignalRaised is non-Green only);
// the neutral entry keeps the map total for the defensive case.
const TIER_STYLES: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: 'border-l-2 border-l-border',
  [SAFETY_TIER.amber]: 'border-l-2 border-l-warning',
  [SAFETY_TIER.red]: 'border-l-4 border-l-destructive bg-destructive/5',
}

/**
 * One deterministic safety turn in the read-only panel (spec 17 § Unit 7,
 * DEC-079). The scripted `content` (crisis resources, emergency referral, or
 * Amber injury/RED-S referral) always renders in full — never truncated or
 * collapsed — decoupled from any plan-change escalation level. The left-edge
 * accent is supplementary; the message itself carries the severity.
 */
export const SafetyTurn = ({ turn }: SafetyTurnProps): ReactElement => (
  <article
    data-testid="safety-turn"
    data-tier={TIER_NAMES[turn.safetyTier]}
    className={`flex flex-col gap-2 rounded-md border bg-card p-4 ${TIER_STYLES[turn.safetyTier]}`}
  >
    <p className="text-sm whitespace-pre-wrap text-card-foreground">{turn.content}</p>
  </article>
)
