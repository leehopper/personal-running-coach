import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { renderInBothThemes } from '~/modules/common/test-utils/render-in-both-themes'
import type {
  MacroPhaseDto,
  MesoDaySlotDto,
  MesoWeekTemplateDto,
  PhaseType,
  PlanPhaseDto,
} from '~/modules/plan/models/plan.model'
import { TheBlock, type TheBlockProps } from './the-block.component'

const buildPhase = (phaseType: PhaseType, weeks: number): PlanPhaseDto => ({
  phaseType,
  weeks,
  weeklyDistanceStartKm: 20,
  weeklyDistanceEndKm: 30,
  intensityDistribution: '80/20',
  allowedWorkoutTypes: ['Easy'],
  targetPaceEasySecPerKm: 360,
  targetPaceFastSecPerKm: 300,
  notes: '',
  includesDeload: false,
})

const buildMacro = (phases: PlanPhaseDto[]): MacroPhaseDto => ({
  totalWeeks: phases.reduce((sum, phase) => sum + phase.weeks, 0),
  goalDescription: '',
  rationale: '',
  warnings: '',
  phases,
})

const restSlot: MesoDaySlotDto = { slotType: 'Rest', workoutType: null, notes: '' }
const runSlot: MesoDaySlotDto = { slotType: 'Run', workoutType: 'Easy', notes: '' }

const buildMesoWeek = (
  weekNumber: number,
  overrides: Partial<MesoWeekTemplateDto> = {},
): MesoWeekTemplateDto => ({
  weekNumber,
  phaseType: 'Base',
  weeklyTargetKm: 30,
  isDeloadWeek: false,
  sunday: restSlot,
  monday: runSlot,
  tuesday: restSlot,
  wednesday: runSlot,
  thursday: restSlot,
  friday: runSlot,
  saturday: runSlot,
  weekSummary: `Week ${weekNumber} summary.`,
  ...overrides,
})

const TWELVE_WEEK_MACRO = buildMacro([
  buildPhase('Base', 4),
  buildPhase('Build', 5),
  buildPhase('Peak', 2),
  buildPhase('Taper', 1),
])

const baseProps = (overrides: Partial<TheBlockProps> = {}): TheBlockProps => ({
  macro: TWELVE_WEEK_MACRO,
  mesoWeeks: [1, 2, 3, 4].map((weekNumber) => buildMesoWeek(weekNumber)),
  currentWeek: 1,
  targetEventDistanceKm: null,
  targetEventDate: null,
  units: PreferredUnits.Kilometers,
  ...overrides,
})

const tierByWeek = (cells: HTMLElement[]): Record<string, string | undefined> =>
  Object.fromEntries(cells.map((cell) => [cell.dataset.week, cell.dataset.tier]))

const testidsIn = (container: HTMLElement): (string | null)[] =>
  [...container.querySelectorAll('[data-testid]')]
    .map((el) => el.getAttribute('data-testid'))
    .sort()

describe('TheBlock', () => {
  it('renders the unavailable state, no grid, when macro is null', () => {
    render(<TheBlock {...baseProps({ macro: null })} />)
    const block = screen.getByTestId('the-block')
    expect(block.dataset.state).toBe('unavailable')
    expect(screen.queryByTestId('the-block-cell')).not.toBeInTheDocument()
    expect(screen.queryByTestId('the-block-phase-label')).not.toBeInTheDocument()
  })

  it('resolves the root testid on the populated render, with no data-state attribute', () => {
    render(<TheBlock {...baseProps()} />)
    const block = screen.getByTestId('the-block')
    expect(block).toBeInTheDocument()
    expect(block.dataset.state).toBeUndefined()
  })

  it('renders 12 cells with the correct fill tiers and phase labels at currentWeek 1', () => {
    render(<TheBlock {...baseProps()} />)
    const cells = screen.getAllByTestId('the-block-cell')
    expect(cells).toHaveLength(12)
    const tiers = tierByWeek(cells)
    expect(tiers['1']).toBe('current')
    expect(tiers['2']).toBe('currentPhase')
    expect(tiers['3']).toBe('currentPhase')
    expect(tiers['4']).toBe('currentPhase')
    expect(tiers['5']).toBe('nextPhase')
    expect(tiers['9']).toBe('nextPhase')
    expect(tiers['10']).toBe('distant')
    expect(tiers['12']).toBe('distant')

    const labels = screen.getAllByTestId('the-block-phase-label')
    // Source copy stays sentence case — `uppercase` is applied via CSS only.
    expect(labels.map((label) => label.textContent)).toEqual([
      'Base 1–4',
      'Build 5–9',
      'Peak 10–11',
      'Taper 12',
    ])
    // `--muted-foreground` on every label, current phase or not — essential
    // text (a runner's only way to tell which weeks belong to which named
    // phase) never carries the decorative `--alp-faint` token (spec §8 FIX 5).
    for (const label of labels) {
      expect(label).toHaveClass('text-muted-foreground')
      expect(label).not.toHaveClass('text-[color:var(--alp-faint)]')
    }
  })

  it('scales the grid to an arbitrary TotalWeeks, not hardcoded to 12', () => {
    render(
      <TheBlock {...baseProps({ macro: buildMacro([buildPhase('Base', 16)]), currentWeek: 1 })} />,
    )
    expect(screen.getAllByTestId('the-block-cell')).toHaveLength(16)
  })

  it('renders the WHOLE current phase (before and after currentWeek) as currentPhase', () => {
    render(<TheBlock {...baseProps({ currentWeek: 7 })} />)
    const tiers = tierByWeek(screen.getAllByTestId('the-block-cell'))
    expect(tiers['5']).toBe('currentPhase')
    expect(tiers['6']).toBe('currentPhase')
    expect(tiers['7']).toBe('current')
    expect(tiers['8']).toBe('currentPhase')
    expect(tiers['9']).toBe('currentPhase')
  })

  it('renders weeks in an earlier phase than the current one as currentPhase (BD2), not distant', () => {
    render(<TheBlock {...baseProps({ currentWeek: 7 })} />)
    const tiers = tierByWeek(screen.getAllByTestId('the-block-cell'))
    expect(tiers['1']).toBe('currentPhase')
    expect(tiers['4']).toBe('currentPhase')
  })

  it('filters a zero-week phase out of the phase-label row without a phantom inverted label', () => {
    const macro = buildMacro([buildPhase('Base', 4), buildPhase('Peak', 0), buildPhase('Taper', 4)])
    render(<TheBlock {...baseProps({ macro, currentWeek: 1, mesoWeeks: [] })} />)
    const labels = screen.getAllByTestId('the-block-phase-label')
    expect(labels.map((label) => label.dataset.phase)).toEqual(['Base', 'Taper'])
    expect(labels.map((label) => label.textContent)).toEqual(['Base 1–4', 'Taper 5–8'])
  })

  it('skips a zero-week phase between two real ones when resolving nextRange (regression)', () => {
    const macro = buildMacro([
      buildPhase('Build', 5),
      buildPhase('Peak', 0),
      buildPhase('Taper', 4),
    ])
    render(<TheBlock {...baseProps({ macro, currentWeek: 3, mesoWeeks: [] })} />)
    const tiers = tierByWeek(screen.getAllByTestId('the-block-cell'))
    expect(tiers['6']).toBe('nextPhase')
    expect(tiers['9']).toBe('nextPhase')
  })

  it('does not let a zero-week phase before the current phase shift which weeks read currentPhase', () => {
    const macro = buildMacro([
      buildPhase('Base', 4),
      buildPhase('Recovery', 0),
      buildPhase('Build', 5),
    ])
    render(<TheBlock {...baseProps({ macro, currentWeek: 7, mesoWeeks: [] })} />)
    const tiers = tierByWeek(screen.getAllByTestId('the-block-cell'))
    expect(tiers['1']).toBe('currentPhase')
    expect(tiers['4']).toBe('currentPhase')
    expect(tiers['7']).toBe('current')
  })

  it('renders the goal chip exactly as "10K — OCT 3" for a race-training plan', () => {
    render(
      <TheBlock {...baseProps({ targetEventDistanceKm: 10, targetEventDate: '2026-10-03' })} />,
    )
    expect(screen.getByTestId('the-block')).toHaveTextContent('10K — OCT 3')
  })

  it('renders no right slot at all when both target-event fields are null', () => {
    render(<TheBlock {...baseProps()} />)
    expect(screen.queryByTestId('section-rule-slot')).not.toBeInTheDocument()
  })

  it('renders no right slot for a non-null but unparseable targetEventDate — not a crash, not an epoch-derived chip', () => {
    render(
      <TheBlock {...baseProps({ targetEventDistanceKm: 10, targetEventDate: 'not-a-date' })} />,
    )
    expect(screen.queryByTestId('section-rule-slot')).not.toBeInTheDocument()
    expect(screen.getByTestId('the-block').textContent ?? '').not.toMatch(/1970/)
  })

  it('shows the DELOAD tag only on deload weeks', () => {
    render(
      <TheBlock
        {...baseProps({
          mesoWeeks: [
            buildMesoWeek(1, { isDeloadWeek: true }),
            buildMesoWeek(2, { isDeloadWeek: false }),
          ],
        })}
      />,
    )
    const tags = screen.getAllByTestId('the-block-deload-tag')
    expect(tags).toHaveLength(1)
    const rows = screen.getAllByTestId('the-block-week-row')
    expect(rows[0]).toContainElement(tags[0])
  })

  it('shows only the weeks actually present in mesoWeeks (populated 1-4 of a 12-week plan)', () => {
    render(<TheBlock {...baseProps({ currentWeek: 1 })} />)
    const rows = screen.getAllByTestId('the-block-week-row')
    expect(rows.map((row) => row.dataset.weekNumber)).toEqual(['1', '2', '3', '4'])
  })

  it('excludes already-completed weeks (weekNumber < currentWeek) from the upcoming-week-row list', () => {
    render(<TheBlock {...baseProps({ currentWeek: 3 })} />)
    const rows = screen.getAllByTestId('the-block-week-row')
    expect(rows.map((row) => row.dataset.weekNumber)).toEqual(['3', '4'])
  })

  it('renders the upcoming-week-row volume in miles under the Miles preference, with zero residual km text', () => {
    render(
      <TheBlock
        {...baseProps({
          mesoWeeks: [buildMesoWeek(1, { weeklyTargetKm: 30 })],
          currentWeek: 1,
          units: PreferredUnits.Miles,
        })}
      />,
    )
    expect(screen.getByTestId('the-block')).toHaveTextContent('18.6 mi')
    expect(screen.queryByText(/\d\.\d km/u)).not.toBeInTheDocument()
  })

  describe('dual-theme parity', () => {
    it('renders the DU-7 12-cell scenario identically in both themes with zero raw colour literals', () => {
      const { dark, light } = renderInBothThemes(<TheBlock {...baseProps()} />)
      for (const result of [dark, light]) {
        expect(result.getByTestId('the-block')).toBeInTheDocument()
        expect(result.getAllByTestId('the-block-cell')).toHaveLength(12)
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    const { container } = render(
      <TheBlock {...baseProps({ targetEventDistanceKm: 10, targetEventDate: '2026-10-03' })} />,
    )
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
