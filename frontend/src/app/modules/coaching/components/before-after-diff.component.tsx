import { useState } from 'react'
import type { ReactElement } from 'react'
import { ChevronDownIcon } from 'lucide-react'

import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { PreferredUnits } from '~/api/generated'
import type { PlanAdaptationDiffDto } from '~/modules/coaching/models/conversation.model'
import {
  describeWeeklyTargetChange,
  describeWorkoutChange,
  weeklyTargetChangeLocus,
  workoutChangeLocus,
} from './before-after-diff.helpers'

export interface BeforeAfterDiffProps {
  diff: PlanAdaptationDiffDto
  /**
   * Display unit for the before/after distances. Defaults to Kilometers so
   * callers that predate the unit preference (and isolated tests) render the
   * km form unchanged.
   */
  units?: PreferredUnits
}

/**
 * The collapsible "Show what changed" before/after expander on a restructure
 * turn (spec 17 § Unit 7, OI-5). Collapsed by default; renders the structured
 * `PlanAdaptationDiff` payload — per-workout micro-week swaps and per-week
 * meso volume-target edits — as a definition list, never parsed prose. An
 * empty diff renders nothing (no dangling toggle).
 */
export const BeforeAfterDiff = ({
  diff,
  units = PreferredUnits.Kilometers,
}: BeforeAfterDiffProps): ReactElement | null => {
  const [isOpen, setIsOpen] = useState(false)

  if (diff.workoutChanges.length === 0 && diff.weeklyTargetChanges.length === 0) {
    return null
  }

  return (
    <Collapsible open={isOpen} onOpenChange={setIsOpen} className="flex flex-col gap-2">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="w-fit justify-start px-2"
          data-testid="diff-toggle"
        >
          Show what changed
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
        <dl className="flex flex-col gap-2 text-sm">
          {diff.workoutChanges.map((change) => {
            const description = describeWorkoutChange(change, units)
            if (description === null) {
              return null
            }
            return (
              <div key={`workout-${change.weekNumber}-${change.dayOfWeek}`}>
                <dt className="text-muted-foreground">{workoutChangeLocus(change)}</dt>
                <dd data-testid="diff-workout-change">{description}</dd>
              </div>
            )
          })}
          {diff.weeklyTargetChanges.map((change) => (
            <div key={`weekly-target-${change.weekNumber}`}>
              <dt className="text-muted-foreground">{weeklyTargetChangeLocus(change)}</dt>
              <dd data-testid="diff-weekly-target-change">
                {describeWeeklyTargetChange(change, units)}
              </dd>
            </div>
          ))}
        </dl>
      </CollapsibleContent>
    </Collapsible>
  )
}
