import { useState } from 'react'
import type { ReactElement } from 'react'
import { ChevronDownIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { PreferredUnits } from '~/api/generated'
import type { PlanAdaptationDiffDto } from '~/modules/coaching/models/conversation.model'
import { DAY_OF_WEEK_LABELS } from '~/modules/plan/components/plan-display.helpers'
import { formatChangeLocusDate } from './adaptation-digest.helpers'
import {
  describeWeeklyTargetChangeParts,
  describeWorkoutChangeParts,
  type ChangeDescriptionParts,
} from './before-after-diff.helpers'

export interface BeforeAfterDiffProps {
  diff: PlanAdaptationDiffDto
  /**
   * Display unit for the before/after distances. Defaults to Kilometers so
   * callers that predate the unit preference (and isolated tests) render the
   * km form unchanged.
   */
  units?: PreferredUnits
  /**
   * The owning plan's calendar anchor, threaded from `AdaptationTurn` (spec
   * §3 PR-C). Row loci resolve a real calendar date against it; `undefined`
   * or an unparseable value degrades to the week-index locus rather than
   * crashing or showing an epoch.
   */
  planStartDate?: string
}

/** Renders one value-line's before/after copy, clay-coloring the `→` glyph when present. */
const ChangeValueLine = ({ parts }: { parts: ChangeDescriptionParts }): ReactElement =>
  parts.kind === 'arrow' ? (
    <>
      {parts.before} <span className="text-clay-text">→</span> {parts.after}
    </>
  ) : (
    <>{parts.text}</>
  )

/**
 * The collapsible `WHAT CHANGED` before/after expander on a restructure turn
 * (spec §3 PR-C). Collapsed by default; renders the structured
 * `PlanAdaptationDiff` payload — per-workout micro-week swaps and per-week
 * meso volume-target edits — as a definition list, never parsed prose. An
 * empty diff renders nothing (no dangling toggle). Row loci are calendar-
 * date-anchored against `planStartDate` (§4.2 UTC plan-calendar math), and
 * degrade to a week-index locus when `planStartDate` is absent/unparseable or
 * the `dayOfWeek` is out of range.
 */
export const BeforeAfterDiff = ({
  diff,
  units = PreferredUnits.Kilometers,
  planStartDate,
}: BeforeAfterDiffProps): ReactElement | null => {
  const [isOpen, setIsOpen] = useState(false)

  if (diff.workoutChanges.length === 0 && diff.weeklyTargetChanges.length === 0) {
    return null
  }

  // The week-label side of both loci: the calendar date when `planStartDate`
  // resolves, else the raw week index. Shared so the two locus builders can't
  // drift apart. An out-of-range `dayOfWeek` (a malformed payload's 7/-1) has
  // no calendar meaning — `resolveCalendarDateUtc` does raw epoch math with no
  // bounds check and would roll it into an adjacent week, rendering a
  // plausible-but-wrong date — so it degrades to the raw week index too.
  // `weeklyTargetLocus` always passes `dayOfWeek` 0 (in range), so only a
  // malformed workout day degrades here.
  const resolveWeekLabel = (weekNumber: number, dayOfWeek: number): string => {
    const dayInRange = DAY_OF_WEEK_LABELS[dayOfWeek] !== undefined
    if (planStartDate === undefined || !dayInRange) {
      return String(weekNumber)
    }
    return formatChangeLocusDate({ planStartDate, weekNumber, dayOfWeek }) ?? String(weekNumber)
  }

  // `DAY_OF_WEEK_LABELS[dayOfWeek]` is `undefined` for an out-of-range index
  // (a malformed payload's 7/-1); the `?? Day N` guard keeps `.toUpperCase()`
  // from throwing and crashing the whole card.
  const weekdayLabel = (dayOfWeek: number): string =>
    (DAY_OF_WEEK_LABELS[dayOfWeek] ?? `Day ${dayOfWeek}`).toUpperCase()

  const workoutLocus = (weekNumber: number, dayOfWeek: number): string =>
    `WK ${resolveWeekLabel(weekNumber, dayOfWeek)} · ${weekdayLabel(dayOfWeek)}`

  const weeklyTargetLocus = (weekNumber: number): string =>
    `WK ${resolveWeekLabel(weekNumber, 0)} · VOLUME`

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen} className="flex flex-col gap-2">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-fit justify-start gap-1 px-2 font-mono text-[10px] font-semibold tracking-[0.1em] text-muted-foreground"
          data-testid="diff-toggle"
        >
          WHAT CHANGED
          <ChevronDownIcon
            aria-hidden="true"
            className={`size-4 transition-transform duration-200 ease-out motion-reduce:transition-none ${
              isOpen ? 'rotate-180' : ''
            }`}
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent
        data-testid="before-after-diff"
        className="data-[state=open]:animate-in data-[state=closed]:animate-out motion-reduce:animate-none"
      >
        <dl className="flex flex-col gap-2">
          {diff.workoutChanges.map((change) => {
            const parts = describeWorkoutChangeParts(change, units)
            if (parts === null) {
              return null
            }
            return (
              <div key={`workout-${change.weekNumber}-${change.dayOfWeek}`}>
                <dt className="font-mono text-[9.5px] font-medium tracking-[0.06em] text-[var(--alp-faint)]">
                  {workoutLocus(change.weekNumber, change.dayOfWeek)}
                </dt>
                <dd
                  data-testid="diff-workout-change"
                  className="font-body text-[13px] text-muted-foreground"
                >
                  <ChangeValueLine parts={parts} />
                </dd>
              </div>
            )
          })}
          {diff.weeklyTargetChanges.map((change) => (
            <div key={`weekly-target-${change.weekNumber}`}>
              <dt className="font-mono text-[9.5px] font-medium tracking-[0.06em] text-[var(--alp-faint)]">
                {weeklyTargetLocus(change.weekNumber)}
              </dt>
              <dd
                data-testid="diff-weekly-target-change"
                className="font-body text-[13px] text-muted-foreground"
              >
                <ChangeValueLine parts={describeWeeklyTargetChangeParts(change, units)} />
              </dd>
            </div>
          ))}
        </dl>
      </CollapsibleContent>
    </Collapsible>
  )
}
