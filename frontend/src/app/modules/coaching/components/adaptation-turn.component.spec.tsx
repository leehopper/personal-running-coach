import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import {
  ADAPTATION_KIND,
  CONVERSATION_ROLE,
  type AdaptationTurnDto,
} from '~/modules/coaching/models/conversation.model'
import { buildDiff } from './conversation.fixture'
import { AdaptationTurn } from './adaptation-turn.component'

const baseTurn = (overrides: Partial<AdaptationTurnDto> = {}): AdaptationTurnDto => ({
  triggeringPlanEventId: 'evt-1',
  role: CONVERSATION_ROLE.assistantAdaptation,
  content: 'I cut this week to help you recover.',
  escalationLevel: 2,
  safetyTier: 0,
  referralCategory: 0,
  adaptationKind: ADAPTATION_KIND.restructure,
  diff: buildDiff(),
  triggeringWorkoutLogId: 'w1',
  createdAt: '2026-06-30T06:58:00Z',
  ...overrides,
})

describe('AdaptationTurn', () => {
  it('renders a restructure turn with the clay left edge, PLAN ADJUSTED label, timestamp, and explanation', () => {
    render(<AdaptationTurn turn={baseTurn()} planStartDate="2026-06-28" time="06:58" />)

    const article = screen.getByTestId('restructure-turn')
    expect(article).toHaveClass('border-l-clay-marker')
    expect(screen.getByText('PLAN ADJUSTED')).toBeInTheDocument()
    expect(screen.getByText('06:58')).toBeInTheDocument()
    expect(screen.getByText('I cut this week to help you recover.')).toBeInTheDocument()
    // Collapsed by default.
    expect(screen.getByTestId('diff-toggle')).toBeInTheDocument()
    expect(screen.getByTestId('before-after-diff')).not.toBeVisible()
  })

  it('resolves calendar-date-anchored loci for every diff-row kind when the expander opens', async () => {
    const user = userEvent.setup()
    render(<AdaptationTurn turn={baseTurn()} planStartDate="2026-06-28" time="06:58" />)

    await user.click(screen.getByTestId('diff-toggle'))

    // Workout swap: week 1, dayOfWeek 2 (Tuesday) -> 2026-06-30.
    expect(screen.getByText('WK JUN 30 · TUESDAY')).toBeInTheDocument()
    // Current-week (week 1) volume change: Sunday anchor -> 2026-06-28.
    expect(screen.getByText('WK JUN 28 · VOLUME')).toBeInTheDocument()
    // Upcoming-week (week 2) volume change: Sunday anchor -> 2026-07-05.
    expect(screen.getByText('WK JUL 5 · VOLUME')).toBeInTheDocument()
    // Every before -> after value line renders the arrow in clay.
    const clayArrows = document.querySelectorAll('.text-clay-text')
    expect(Array.from(clayArrows).some((el) => el.textContent === '→')).toBe(true)
  })

  it('degrades every locus to the week-index form when planStartDate is undefined', async () => {
    const user = userEvent.setup()
    render(<AdaptationTurn turn={baseTurn()} time="06:58" />)

    await user.click(screen.getByTestId('diff-toggle'))

    expect(screen.getByText('WK 1 · TUESDAY')).toBeInTheDocument()
    expect(screen.getAllByText('WK 1 · VOLUME')[0]).toBeInTheDocument()
    expect(screen.getByText('WK 2 · VOLUME')).toBeInTheDocument()
  })

  it('degrades every locus to the week-index form when planStartDate is unparseable', async () => {
    const user = userEvent.setup()
    render(<AdaptationTurn turn={baseTurn()} planStartDate="not-a-date" time="06:58" />)

    await user.click(screen.getByTestId('diff-toggle'))

    expect(screen.getByText('WK 1 · TUESDAY')).toBeInTheDocument()
    expect(screen.getByText('WK 2 · VOLUME')).toBeInTheDocument()
  })

  it('renders a nudge turn as a plain coach line with no card', () => {
    render(
      <AdaptationTurn
        turn={baseTurn({
          adaptationKind: ADAPTATION_KIND.nudge,
          content: 'Nudged tomorrow easier.',
        })}
        time="06:58"
      />,
    )

    expect(screen.getByTestId('nudge-turn')).toHaveTextContent('Nudged tomorrow easier.')
    expect(screen.queryByTestId('restructure-turn')).not.toBeInTheDocument()
  })

  it('renders nothing for an absorb turn (defensive)', () => {
    const { container } = render(
      <AdaptationTurn turn={baseTurn({ adaptationKind: ADAPTATION_KIND.absorb })} time="06:58" />,
    )

    expect(container.firstChild).toBeNull()
  })

  it('threads the unit preference into the diff', async () => {
    const user = userEvent.setup()
    render(
      <AdaptationTurn
        turn={baseTurn({
          diff: {
            workoutChanges: [],
            weeklyTargetChanges: [
              { weekNumber: 1, beforeWeeklyTargetKm: 36, afterWeeklyTargetKm: 28 },
            ],
          },
        })}
        units={PreferredUnits.Miles}
        time="06:58"
      />,
    )

    await user.click(screen.getByTestId('diff-toggle'))

    // 36 km -> 22.4 mi ; 28 km -> 17.4 mi
    const weeklyTargetRow = screen.getByTestId('diff-weekly-target-change')
    expect(weeklyTargetRow).toHaveTextContent('22.4 mi')
    expect(weeklyTargetRow).toHaveTextContent('17.4 mi')
  })
})
