import { type ReactElement, type ReactNode, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { cn } from '@/lib/utils'
import { PreferredUnits } from '~/api/generated'
import {
  formatDistanceKm,
  formatPaceRangeSecPerKm,
} from '~/modules/common/utils/unit-format.helpers'
import { StatBand, StatCell } from '~/modules/common/components/stat-band/stat-band.component'
import type { MesoDaySlotDto, MicroWorkoutCardDto } from '~/modules/plan/models/plan.model'
import { MicroWorkoutSegmentRow } from './micro-workout-segment-row.component'
import {
  DAY_OF_WEEK_LABELS,
  composeWorkoutSummary,
  formatHeroEyebrowDate,
  resolveHeroDistanceStat,
  resolveHeroThirdStat,
} from './plan-display.helpers'

// C3 fix: `slot`/`workout` were originally two SEPARATE, uncorrelated
// optional props (`slot: MesoDaySlotDto | undefined`, `workout:
// MicroWorkoutCardDto | undefined`). A correct branch-2 implementation
// narrows `slot.slotType === 'Run'` and then dereferences `workout.title` /
// `composeWorkoutSummary(workout)` / `workout.segments.map(...)` — but
// narrowing `slot` tells the compiler NOTHING about the separately-optional
// `workout` prop, so every `workout.*` access fails strict-mode compilation
// ("'workout' is possibly 'undefined'"). Fixed by collapsing the two
// correlated fields into one discriminated union so the impossible state
// (`kind: 'run'` with no `workout`) is unrepresentable — narrowing
// `props.kind` narrows the whole props type, `workout`/`nextWorkout`
// included, no non-null assertions needed. `home.page.tsx`'s mount site
// constructs this union instead of passing `slot`/`workout`/`nextWorkout` as
// three flat props.
export type WorkoutHeroContent =
  | { kind: 'unavailable' }
  | { kind: 'run'; slot: MesoDaySlotDto; workout: MicroWorkoutCardDto }
  | { kind: 'rest'; slot: MesoDaySlotDto; nextWorkout: MicroWorkoutCardDto | undefined }

export type WorkoutHeroProps = {
  /**
   * Pre-normalized UTC-midnight epoch from `home.page.tsx`'s single
   * `new Date()` call site — NEVER re-derive a second `Date` here.
   */
  todayUtc: number
  units: PreferredUnits
  className?: string
} & WorkoutHeroContent

interface WorkoutHeroShellProps {
  variant: 'unavailable' | 'run' | 'rest'
  className?: string
  children: ReactNode
}

/** Root element shared by all 3 render branches — the testid/variant contract lives here once. */
const WorkoutHeroShell = ({
  variant,
  className,
  children,
}: WorkoutHeroShellProps): ReactElement => (
  <section
    data-testid="workout-hero"
    data-variant={variant}
    className={cn('flex flex-col gap-4', className)}
  >
    {children}
  </section>
)

interface WorkoutHeroRunContentProps {
  todayUtc: number
  units: PreferredUnits
  workout: MicroWorkoutCardDto
}

const WorkoutHeroRunContent = ({
  todayUtc,
  units,
  workout,
}: WorkoutHeroRunContentProps): ReactElement => {
  const [open, setOpen] = useState(false)
  const isMiles = units === PreferredUnits.Miles
  const thirdStat = resolveHeroThirdStat(workout)
  const paceValue =
    formatPaceRangeSecPerKm(
      workout.targetPaceFastSecPerKm,
      workout.targetPaceEasySecPerKm,
      units,
    ) ?? '—'

  return (
    <>
      <p
        data-testid="workout-hero-eyebrow"
        className="font-condensed text-[12px] font-semibold uppercase tracking-[0.18em] text-clay-text"
      >
        {formatHeroEyebrowDate(todayUtc)} — ON THE SCHEDULE
      </p>
      <h1 className="t-display">{workout.title}</h1>
      <p className="t-body text-muted-foreground">{composeWorkoutSummary(workout)}</p>
      <StatBand variant="hero">
        <StatCell
          variant="hero"
          value={resolveHeroDistanceStat(workout.targetDistanceKm, units)}
          label={isMiles ? 'MILES' : 'KILOMETERS'}
        />
        <StatCell variant="hero" value={paceValue} label={isMiles ? 'PACE /MI' : 'PACE /KM'} />
        <StatCell variant="hero" value={thirdStat.value} label={thirdStat.label} />
      </StatBand>
      <div className="flex flex-wrap items-center gap-3">
        <Button asChild size="lg" className="flex-1" data-testid="workout-hero-log-action">
          <Link to="/log">Log run</Link>
        </Button>
        {/*
         * Spec's locked markup: `<Collapsible>` wraps ONLY the DETAILS
         * trigger + `CollapsibleContent` — LOG RUN is a sibling OUTSIDE it,
         * never nested inside. `className="contents"` takes the Radix
         * Root's own box out of the layout tree so the trigger still lays
         * out inline in this row (next to LOG RUN, not visually wrapped in
         * an extra box); `CollapsibleContent`'s `w-full basis-full` forces
         * it onto its own full-width line when the row wraps open.
         */}
        <Collapsible open={open} onOpenChange={setOpen} className="contents">
          {/* BD3, decided: text-only affordance — no chevron, no icon of any
           * kind. `aria-expanded`/`aria-controls`/`data-state` are supplied
           * by Radix's `CollapsibleTrigger asChild` for free. */}
          <CollapsibleTrigger asChild>
            <Button variant="outline" size="lg" data-testid="workout-hero-details-trigger">
              Details
            </Button>
          </CollapsibleTrigger>
          <CollapsibleContent
            data-testid="workout-hero-details-content"
            className="w-full basis-full data-[state=open]:animate-in data-[state=closed]:animate-out motion-reduce:animate-none"
          >
            <ul
              aria-label="Workout segments"
              data-testid="micro-workout-segments"
              className="flex flex-col gap-1"
            >
              {workout.segments.map((segment, index) => (
                <MicroWorkoutSegmentRow
                  key={`${segment.segmentType}-${index}`}
                  segment={segment}
                  index={index}
                  units={units}
                />
              ))}
            </ul>
          </CollapsibleContent>
        </Collapsible>
      </div>
    </>
  )
}

interface WorkoutHeroRestContentProps {
  todayUtc: number
  units: PreferredUnits
  nextWorkout: MicroWorkoutCardDto | undefined
}

const WorkoutHeroRestContent = ({
  todayUtc,
  units,
  nextWorkout,
}: WorkoutHeroRestContentProps): ReactElement => (
  <>
    <p
      data-testid="workout-hero-eyebrow"
      className="font-condensed text-[12px] font-semibold uppercase tracking-[0.18em] text-clay-text"
    >
      {formatHeroEyebrowDate(todayUtc)}
    </p>
    {/* Source copy stays sentence case; `.t-display` applies uppercase via CSS. */}
    <h1 className="t-display">Rest day</h1>
    <p className="t-body text-muted-foreground">Recovery is training.</p>
    {nextWorkout === undefined ? null : (
      // Styled like UpNext's row markup (mono day abbrev + row-title,
      // right-aligned distance) — NOT the pre-redesign TodayCard's prose
      // form. No mock covers the rest-day hero; this is the spec's proposed
      // default (§9 open question #5).
      <div
        data-testid="workout-hero-next-workout"
        className="flex items-baseline justify-between gap-3 border-t border-border pt-3"
      >
        <span className="flex items-baseline gap-3">
          <span className="t-data-label shrink-0 text-muted-foreground">
            {DAY_OF_WEEK_LABELS[nextWorkout.dayOfWeek].slice(0, 3)}
          </span>
          <span className="t-row-title text-foreground">{nextWorkout.title}</span>
        </span>
        <span className="t-numeral text-muted-foreground">
          {formatDistanceKm(nextWorkout.targetDistanceKm, units) ?? '—'}
        </span>
      </div>
    )}
  </>
)

/**
 * Today screen's hero: today's workout (or rest-day variant, or a graceful
 * "not ready yet" state when no slot data is available). `props.kind`
 * discriminates the 3 render branches (§ Slice 2 PR-B) — narrow on it before
 * touching any of `slot`/`workout`/`nextWorkout`, none of which exists on
 * every union member.
 */
export const WorkoutHero = (props: WorkoutHeroProps): ReactElement => {
  if (props.kind === 'unavailable') {
    return (
      <WorkoutHeroShell variant="unavailable" className={props.className}>
        <p className="t-body text-muted-foreground">This week's plan isn't ready yet.</p>
      </WorkoutHeroShell>
    )
  }

  if (props.kind === 'run') {
    const { todayUtc, units, workout, className } = props
    return (
      <WorkoutHeroShell variant="run" className={className}>
        <WorkoutHeroRunContent todayUtc={todayUtc} units={units} workout={workout} />
      </WorkoutHeroShell>
    )
  }

  const { todayUtc, units, nextWorkout, className } = props
  return (
    <WorkoutHeroShell variant="rest" className={className}>
      <WorkoutHeroRestContent todayUtc={todayUtc} units={units} nextWorkout={nextWorkout} />
    </WorkoutHeroShell>
  )
}
