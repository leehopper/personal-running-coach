// Shared adaptation-diff presentation helper: owns the one-sentence
// adaptation headline `CoachDigest` renders.

import { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import { DAY_OF_WEEK_LABELS } from '~/modules/plan/components/plan-display.helpers'
import type { PlanAdaptationDiffDto } from '~/modules/coaching/models/conversation.model'

/**
 * Composes the digest's one-line `PLAN ADJUSTED` headline, deterministically,
 * from the typed {@link PlanAdaptationDiffDto} — NEVER clamped LLM prose,
 * NEVER parsed out of prose. Capped at 2 sentences: the first entry of
 * `weeklyTargetChanges` (deterministic array order), then the first
 * "interesting" entry of `workoutChanges` (the first with a non-null
 * `after`, so a removed-then-added pair doesn't surface the removal alone).
 * Falls back to a generic sentence when both collections are empty — not
 * reachable under a real restructure-kind adaptation, but the type permits
 * an empty {@link PlanAdaptationDiffDto}, so this guards defensively.
 *
 * Reuses the shared, unit-aware {@link formatDistanceKm} rather than a bare
 * integer formatter — a deliberate trade of exact copy fidelity to the
 * design mock (`"30 → 26 km"`) for zero new distance-formatting code:
 * output reads `"30.0 → 26.0 km"`.
 */
export function composeAdaptationHeadline(params: {
  diff: PlanAdaptationDiffDto
  currentWeek: number
  units: PreferredUnits
}): string {
  const { diff, currentWeek, units } = params
  const sentences: string[] = []

  if (diff.weeklyTargetChanges.length > 0) {
    const change = diff.weeklyTargetChanges[0]
    const locus = change.weekNumber === currentWeek ? 'This week' : `Week ${change.weekNumber}`
    const before = formatDistanceKm(change.beforeWeeklyTargetKm, units) ?? '—'
    const after = formatDistanceKm(change.afterWeeklyTargetKm, units) ?? '—'
    sentences.push(`${locus} ${before} → ${after}.`)
  }

  if (diff.workoutChanges.length > 0) {
    const change =
      diff.workoutChanges.find((candidate) => candidate.after !== null) ?? diff.workoutChanges[0]
    const weekday = DAY_OF_WEEK_LABELS[change.dayOfWeek]
    if (change.after === null) {
      sentences.push(`${weekday} is removed.`)
    } else if (change.before === null) {
      sentences.push(
        `${weekday} adds ${formatDistanceKm(change.after.targetDistanceKm, units) ?? 'a session'}.`,
      )
    } else if (change.after.targetDistanceKm < change.before.targetDistanceKm) {
      sentences.push(
        `${weekday} trims to ${formatDistanceKm(change.after.targetDistanceKm, units) ?? '—'}.`,
      )
    } else if (change.after.targetDistanceKm > change.before.targetDistanceKm) {
      sentences.push(
        `${weekday} extends to ${formatDistanceKm(change.after.targetDistanceKm, units) ?? '—'}.`,
      )
    } else {
      sentences.push(`${weekday} is adjusted.`)
    }
  }

  if (sentences.length === 0) {
    return 'Your plan was adjusted.'
  }

  return sentences.slice(0, 2).join(' ')
}
