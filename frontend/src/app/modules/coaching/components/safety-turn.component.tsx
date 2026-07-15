import type { ReactElement } from 'react'

import { cn } from '@/lib/utils'
import { SAFETY_TIER, type SafetyTier } from '~/modules/coaching/models/conversation.model'

export interface SafetyTurnProps {
  tier: SafetyTier
  content: string
  /** Set by the LIVE path so a new safety message announces; omitted for persisted history. */
  role?: 'alert'
  /** Defaults to `'safety-turn'`; the live path passes `'coach-safety-notice'`. */
  testId?: string
  className?: string
}

/** Wire-tier → human-readable name, exposed as `data-tier` for tests/E2E. */
const TIER_NAMES: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: 'green',
  [SAFETY_TIER.amber]: 'amber',
  [SAFETY_TIER.red]: 'red',
}

/** Client-derived heading copy per tier (spec §3 PR-C table) — never on the wire. */
const TIER_HEADINGS: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: '',
  [SAFETY_TIER.amber]: 'WORTH A PROFESSIONAL LOOK',
  [SAFETY_TIER.red]: 'STOP — GET SEEN',
}

/** The left-edge accent per tier — supplementary; the heading + content carry the severity. */
const TIER_EDGE: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: 'border-l-2 border-l-border',
  [SAFETY_TIER.amber]: 'border-l-[3px] border-l-warning',
  [SAFETY_TIER.red]: 'border-l-[3px] border-l-destructive',
}

/** The card surface per tier — red gets the dedicated danger wash, amber/green stay on `--card`. */
const TIER_SURFACE: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: 'bg-card',
  [SAFETY_TIER.amber]: 'bg-card',
  [SAFETY_TIER.red]: 'bg-danger-surface',
}

/**
 * The heading's text color per tier. Red uses the dedicated `--danger-text`
 * token, NOT `--destructive` — plain `--destructive` measures only ~4.05:1
 * (dark) / ~3.24:1 (light) against `--danger-surface`, short of the 4.5:1 AA
 * text threshold (spec §9 #3 option (a): an AA-passing on-danger foreground
 * variant, chosen over exempting the heading).
 */
const TIER_HEADING_COLOR: Record<SafetyTier, string> = {
  [SAFETY_TIER.green]: 'text-muted-foreground',
  [SAFETY_TIER.amber]: 'text-warning',
  [SAFETY_TIER.red]: 'text-danger-text',
}

/**
 * The shared presentational core for a safety turn (D5, AX-01), rendered by
 * BOTH the persisted timeline path (`safety-turn`, no `role`) and the live
 * in-flight notice (`coach-safety-notice`, `role="alert"`). The scripted
 * `content` (crisis resources, emergency referral, or Amber injury/RED-S
 * referral) always renders in full — never truncated, clamped, or collapsed,
 * and with no competing CTA — decoupled from any plan-change escalation
 * level. The left-edge accent + heading are supplementary; the content
 * itself always carries the severity.
 */
export const SafetyTurn = ({
  tier,
  content,
  role,
  testId,
  className,
}: SafetyTurnProps): ReactElement => {
  const heading = TIER_HEADINGS[tier]
  return (
    <article
      data-testid={testId ?? 'safety-turn'}
      data-tier={TIER_NAMES[tier]}
      role={role}
      className={cn(
        'flex flex-col gap-2 rounded-lg border border-border p-[14px]',
        TIER_EDGE[tier],
        TIER_SURFACE[tier],
        className,
      )}
    >
      {heading !== '' && (
        <span
          className={cn(
            'font-condensed text-[12px] font-semibold tracking-[0.16em] uppercase',
            TIER_HEADING_COLOR[tier],
          )}
        >
          {heading}
        </span>
      )}
      <p className="font-body text-[14px] leading-[1.55] whitespace-pre-wrap text-foreground">
        {content}
      </p>
    </article>
  )
}
