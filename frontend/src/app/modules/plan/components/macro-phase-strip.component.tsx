import type { ReactElement } from 'react'
import type { MacroPhaseDto } from '~/modules/plan/models/plan.model'
import {
  computePhaseRanges,
  isCurrentRange,
  labelForPhase,
} from '~/modules/plan/components/plan-display.helpers'
import './macro-phase-strip.component.css'

/** Props for {@link MacroPhaseStrip}. */
export interface MacroPhaseStripProps {
  /** Macro periodisation root from `PlanProjectionDto.macro`. */
  macro: MacroPhaseDto
  /**
   * 1-based week the runner is currently on. The matching segment renders
   * a "current week" marker and the `data-current="true"` attribute. Pass
   * `null` when no week applies (e.g. rendering a preview before the first
   * micro week starts).
   */
  currentWeek: number | null
  className?: string
}

/**
 * Horizontal periodisation strip rendered at the top of the home view.
 * Each segment shows a phase label (`Base` / `Build` / `Peak` / `Taper` /
 * `Race` / `Recovery` / `Maintenance`) plus its absolute start/end week,
 * with a current-week marker on the segment containing `currentWeek`.
 *
 * Visual styling deliberately stays simple — this component renders no
 * pace strings, so the trademark rule is satisfied by the phase-label map
 * alone (see `plan-display.helpers.ts`).
 */
export const MacroPhaseStrip = ({
  macro,
  currentWeek,
  className,
}: MacroPhaseStripProps): ReactElement => {
  const ranges = computePhaseRanges(macro.phases)
  const totalWeeks = macro.totalWeeks > 0 ? macro.totalWeeks : 1

  return (
    <ol
      aria-label="Training phases"
      data-testid="macro-phase-strip"
      className={`flex w-full items-stretch gap-1 rounded-lg border border-slate-200 bg-slate-50 p-2 ${className ?? ''}`}
    >
      {ranges.map((range) => {
        const widthPercent = (range.phase.weeks / totalWeeks) * 100
        const current = isCurrentRange(range, currentWeek)
        return (
          <li
            // Phase ordering is stable for a generated plan, so phase type +
            // start week pairs uniquely identify a segment without needing
            // a synthetic id (no array-index sin).
            key={`${range.phase.phaseType}-${range.startWeek}`}
            data-testid="macro-phase-segment"
            data-phase={range.phase.phaseType}
            data-current={current ? 'true' : 'false'}
            aria-current={current ? 'step' : undefined}
            className={`macro-phase-segment flex min-w-[80px] flex-col items-center justify-center rounded-md px-3 py-2 text-xs font-medium transition-colors duration-200 ease-out ${
              current
                ? 'bg-slate-900 text-slate-50 ring-2 ring-slate-900'
                : 'bg-white text-slate-700'
            }`}
            ref={(el) => {
              if (el !== null) {
                el.style.setProperty('--phase-width', `${widthPercent}%`)
              }
            }}
          >
            <span className="text-sm font-semibold">{labelForPhase(range.phase.phaseType)}</span>
            <span className="text-[10px] uppercase tracking-wide opacity-80">
              {range.startWeek === range.endWeek
                ? `Week ${range.startWeek}`
                : `Weeks ${range.startWeek}–${range.endWeek}`}
            </span>
            {current && currentWeek !== null ? (
              <span
                data-testid="macro-phase-current-marker"
                className="mt-1 inline-block rounded-full bg-slate-50 px-2 py-0.5 text-[10px] font-bold text-slate-900"
              >
                Week {currentWeek}
              </span>
            ) : null}
          </li>
        )
      })}
    </ol>
  )
}
