import type { ReactElement } from 'react'
import { cn } from '@/lib/utils'
import type { PreferredUnits } from '~/api/generated'
import { formatDistanceKm } from '~/modules/common/utils/unit-format.helpers'
import { SectionRule } from '~/modules/common/components/section-rule/section-rule.component'
import type { MacroPhaseDto, MesoWeekTemplateDto } from '~/modules/plan/models/plan.model'
import { computePhaseRanges, isCurrentRange, labelForPhase } from './plan-display.helpers'
import { formatGoalChip, resolveBlockFillTiers, type BlockFillTier } from './the-block-fill.helpers'

/** Props for {@link TheBlock}. */
export interface TheBlockProps {
  macro: MacroPhaseDto | null
  mesoWeeks: readonly MesoWeekTemplateDto[]
  /** 1-based current training week, from `resolveCurrentWeek`. */
  currentWeek: number
  targetEventDistanceKm: number | null
  targetEventDate: string | null
  units: PreferredUnits
  className?: string
}

const FILL_TIER_CLASSES: Record<BlockFillTier, string> = {
  current: 'bg-primary',
  currentPhase: 'border border-border bg-muted',
  nextPhase: 'bg-card',
  distant: 'bg-surface-dim',
}

/**
 * Today screen's "THE BLOCK": the whole macro-cycle at a glance — one cell
 * per week (proximity-to-current-week fill tier), one label per named phase,
 * and a list of upcoming weeks with their volume + DELOAD tag. `macro ===
 * null` renders a bare section rule with no grid (a per-section defensive
 * fallback, not a whole-page one).
 */
export const TheBlock = ({
  macro,
  mesoWeeks,
  currentWeek,
  targetEventDistanceKm,
  targetEventDate,
  units,
  className,
}: TheBlockProps): ReactElement => {
  if (macro === null) {
    return (
      <section
        data-testid="the-block"
        data-state="unavailable"
        className={cn('flex flex-col gap-4', className)}
      >
        <SectionRule label="THE BLOCK" />
      </section>
    )
  }

  const ranges = computePhaseRanges(macro.phases)
  const tiers = resolveBlockFillTiers({ ranges, currentWeek, totalWeeks: macro.totalWeeks })
  const goalChip = formatGoalChip({ targetEventDistanceKm, targetEventDate })
  // §2.3's consumer-side guard: filter empty (zero-week) spans before
  // rendering a phase label — computePhaseRanges deliberately keeps them in
  // the array so `ranges.at(-1)`-equivalent lookups still resolve.
  const phaseLabelRows = ranges
    .filter((range) => range.endWeek >= range.startWeek)
    .map((range) => ({
      phaseType: range.phase.phaseType,
      startWeek: range.startWeek,
      text: `${labelForPhase(range.phase.phaseType)} ${range.startWeek}${
        range.startWeek === range.endWeek ? '' : `–${range.endWeek}`
      }`,
      isCurrent: isCurrentRange(range, currentWeek),
    }))
  // "Upcoming" is a literal weekNumber >= currentWeek filter — an
  // already-completed week never renders under this list. Weeks absent from
  // `mesoWeeks` (beyond whatever's currently populated) are skipped
  // silently, no placeholder row — the permanent shape under DEC-090's
  // rolling horizon, not a stopgap (§11).
  const upcomingWeeks = mesoWeeks.filter((week) => week.weekNumber >= currentWeek)

  return (
    <section data-testid="the-block" className={cn('flex flex-col gap-4', className)}>
      {/*
       * SectionRule's empty-slot trap: it only omits its right-slot wrapper
       * when `children === undefined` — passing `null`/`false` still
       * renders an empty wrapper div. Branching BEFORE the JSX into two
       * structurally different SectionRule calls (rather than passing a
       * conditional expression as `children`) is the required pattern.
       */}
      {goalChip === null ? (
        <SectionRule label="THE BLOCK" />
      ) : (
        <SectionRule label="THE BLOCK">
          <span className="font-mono text-[11px] font-medium text-muted-foreground">
            {goalChip}
          </span>
        </SectionRule>
      )}

      {/*
       * `TotalWeeks` is a runtime value, not known at build time, so
       * Tailwind's static class scanner cannot pre-generate a
       * `grid-cols-[repeat(N,1fr)]` utility for it — the column count is
       * genuinely dynamic (DU-7's 16-week case), so this is inline style,
       * not a hardcoded arbitrary-value class.
       */}
      <div
        className="grid gap-1"
        style={{ gridTemplateColumns: `repeat(${macro.totalWeeks}, 1fr)` }}
      >
        {tiers.map((tier, index) => (
          <div
            // `tiers` is index-aligned to week number (index i ↔ week i+1,
            // per `resolveBlockFillTiers`'s own contract) — the week number
            // is the stable business identity, already computed on
            // `data-week` below, so key off that instead of the array index.
            key={index + 1}
            aria-hidden="true"
            data-testid="the-block-cell"
            data-week={index + 1}
            data-tier={tier}
            className={cn('h-[18px] rounded-xs', FILL_TIER_CLASSES[tier])}
          />
        ))}
      </div>

      <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
        {phaseLabelRows.map((row) => (
          <span
            // Composite key — a phase type can recur (e.g. Base -> Build ->
            // Base again in a periodized plan), so phaseType alone is not a
            // stable unique key.
            key={`${row.phaseType}-${row.startWeek}`}
            data-testid="the-block-phase-label"
            data-phase={row.phaseType}
            className={cn(
              'font-mono text-[11px] font-medium uppercase tracking-[0.05em]',
              row.isCurrent ? 'text-muted-foreground' : 'text-[color:var(--alp-faint)]',
            )}
          >
            {row.text}
          </span>
        ))}
      </div>

      {upcomingWeeks.length > 0 ? (
        <div className="flex flex-col">
          {upcomingWeeks.map((week, index) => (
            <div
              key={week.weekNumber}
              data-testid="the-block-week-row"
              data-week-number={week.weekNumber}
              className={cn(
                'flex items-baseline justify-between gap-3 py-2',
                index < upcomingWeeks.length - 1 ? 'border-b border-border' : null,
              )}
            >
              <span className="flex min-w-0 items-baseline gap-3">
                {/*
                 * `--muted-foreground`, not `--alp-faint`: this label is the
                 * row's ONLY identifier of which week its summary/volume
                 * refer to — essential text, not decoration (frontend
                 * CLAUDE.md's "must never carry essential text" rule; see
                 * spec §8's FIX 4 note).
                 */}
                <span className="font-condensed text-[13px] font-semibold whitespace-nowrap tracking-[0.1em] text-muted-foreground">
                  WK {week.weekNumber}
                </span>
                {week.isDeloadWeek ? (
                  <span
                    data-testid="the-block-deload-tag"
                    className="rounded-xs border border-positive px-[7px] py-0.5 font-mono text-[10px] font-semibold tracking-[0.08em] text-positive"
                  >
                    DELOAD
                  </span>
                ) : null}
                <span className="min-w-0 flex-1 truncate font-body text-[13.5px] text-muted-foreground">
                  {week.weekSummary}
                </span>
              </span>
              <span className="font-condensed text-[15px] font-semibold whitespace-nowrap text-muted-foreground">
                {formatDistanceKm(week.weeklyTargetKm, units) ?? '—'}
              </span>
            </div>
          ))}
        </div>
      ) : null}
    </section>
  )
}
