import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PreferredUnits } from '~/api/generated'
import { renderInBothThemes } from '~/modules/common/test-utils/render-in-both-themes'
import type {
  ConversationTimelineDto,
  ConversationTimelineTurnDto,
  PlanAdaptationDiffDto,
} from '~/modules/coaching/models/conversation.model'
import {
  buildCoachTimelineTurn,
  buildDiffWorkout,
  buildErroredLatestTimeline,
  buildNoPrecedentUserTimeline,
  buildNudgeTimelineTurn,
  buildRestructureLatestTimeline,
  buildTimeline,
  buildUserTimelineTurn,
} from './conversation.fixture'

const { timelineMock, navigateMock } = vi.hoisted(() => ({
  timelineMock: vi.fn(),
  navigateMock: vi.fn(),
}))

vi.mock('~/api/conversation.api', () => ({
  useGetConversationTimelineQuery: () => timelineMock(),
}))
vi.mock('react-router-dom', async (importActual) => ({
  ...(await importActual<typeof import('react-router-dom')>()),
  useNavigate: () => navigateMock,
}))

import { CoachDigest } from './coach-digest.component'

const setTimeline = (turns: ConversationTimelineTurnDto[]): void => {
  const data: ConversationTimelineDto = { turns }
  timelineMock.mockReturnValue({ data, isLoading: false, isError: false })
}

const renderDigest = (currentWeek = 3, units: PreferredUnits = PreferredUnits.Kilometers) =>
  render(
    <MemoryRouter>
      <CoachDigest currentWeek={currentWeek} units={units} />
    </MemoryRouter>,
  )

const testidsIn = (container: HTMLElement): (string | null)[] =>
  [...container.querySelectorAll('[data-testid]')]
    .map((el) => el.getAttribute('data-testid'))
    .sort()

const weeklyTargetOnlyDiff: PlanAdaptationDiffDto = {
  workoutChanges: [],
  weeklyTargetChanges: [{ weekNumber: 3, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 26 }],
}

// A realistic `RestructureDiffCalculator` outcome — BOTH a weeklyTargetChange
// AND a workoutChange — which composes to the 2-sentence ceiling
// `composeAdaptationHeadline` supports ("This week 30.0 km → 26.0 km.
// Saturday trims to 8.0 km.", ~54 chars). This is the fixture the
// short-single-sentence `weeklyTargetOnlyDiff` above cannot exercise: at a
// realistic mobile card width, 2 sentences wraps to a second line unless the
// summary is genuinely clamped to one line.
const twoSentenceDiff: PlanAdaptationDiffDto = {
  weeklyTargetChanges: [{ weekNumber: 3, beforeWeeklyTargetKm: 30, afterWeeklyTargetKm: 26 }],
  workoutChanges: [
    {
      weekNumber: 3,
      dayOfWeek: 6,
      before: buildDiffWorkout({ targetDistanceKm: 14 }),
      after: buildDiffWorkout({ targetDistanceKm: 8 }),
    },
  ],
}

describe('CoachDigest', () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  describe('state 1/2 — short/long reply with a precedent user turn', () => {
    it('renders the You: line, the coach line, Open →, and the tap-through Link', () => {
      setTimeline(buildTimeline())
      renderDigest()

      expect(screen.getByTestId('coach-digest')).toBeInTheDocument()
      expect(screen.getByTestId('coach-digest-tap-through')).toHaveAttribute('href', '/coach')
      expect(screen.getByTestId('coach-digest-user-line')).toHaveTextContent('You: How was my run?')
      expect(screen.getByTestId('coach-digest-coach-line')).toHaveTextContent('You ran well.')
      expect(screen.getByText('Open →')).toBeInTheDocument()
      expect(screen.getByTestId('coach-digest-composer-stub')).toBeInTheDocument()
    })

    it('renders the pathological-content clamp classes (truncate / line-clamp-3) on very long user/coach content', () => {
      // jsdom performs no real layout, so `getBoundingClientRect().height`
      // is always 0 regardless of content length — a height-parity
      // assertion here would be `0 === 0`, always true, and would not
      // fail if the clamp classes below were removed. The class-presence
      // assertions are the only load-bearing check this test can make.
      const longUserLine = 'x'.repeat(400)
      const longCoachReply = Array.from({ length: 13 }, (_, i) => `Sentence number ${i + 1}.`).join(
        ' ',
      )
      setTimeline([
        buildUserTimelineTurn(longUserLine),
        buildCoachTimelineTurn({ content: longCoachReply }),
      ])
      renderDigest()

      const userLines = screen.getAllByTestId('coach-digest-user-line')
      const coachLines = screen.getAllByTestId('coach-digest-coach-line')
      expect(userLines[userLines.length - 1]).toHaveClass('truncate')
      expect(coachLines[coachLines.length - 1]).toHaveClass('line-clamp-3')
    })
  })

  describe('userLine one-step-lookback rule', () => {
    it('renders no "You:" line when turns[-2] is not a user turn (adaptation interposed)', () => {
      setTimeline(buildNoPrecedentUserTimeline())
      renderDigest()

      expect(screen.queryByTestId('coach-digest-user-line')).not.toBeInTheDocument()
      expect(screen.getByTestId('coach-digest-coach-line')).toHaveTextContent(
        'Also, nice work this week.',
      )
    })

    it('renders only the "You:" line, with no coach paragraph, when the latest turn is itself a user turn', () => {
      setTimeline([buildUserTimelineTurn('Hello coach')])
      renderDigest()

      expect(screen.getByTestId('coach-digest-user-line')).toHaveTextContent('You: Hello coach')
      expect(screen.queryByTestId('coach-digest-coach-line')).not.toBeInTheDocument()
    })
  })

  describe('errored latest coach turn', () => {
    it('renders the literal "didn\'t go through" copy, never a blank paragraph, with the precedent You: line intact', () => {
      setTimeline(buildErroredLatestTimeline())
      renderDigest()

      expect(screen.getByTestId('coach-digest-user-line')).toHaveTextContent('You: How was my run?')
      expect(screen.getByTestId('coach-digest-coach-line')).toHaveTextContent(
        "That reply didn't go through.",
      )
      expect(screen.getByText('Open →')).toBeInTheDocument()
    })
  })

  describe('nudge adaptation (folds into 1/2)', () => {
    it('renders a normal clamped coach line, no PLAN ADJUSTED card, and no You: line', () => {
      setTimeline([buildUserTimelineTurn('How was my run?'), buildNudgeTimelineTurn()])
      renderDigest()

      expect(screen.queryByTestId('coach-digest-user-line')).not.toBeInTheDocument()
      expect(screen.getByTestId('coach-digest-coach-line')).toHaveTextContent(
        'Nudged tomorrow easier.',
      )
      expect(screen.queryByTestId('coach-digest-adaptation-card')).not.toBeInTheDocument()
      expect(screen.queryByText(/plan adjusted/i)).not.toBeInTheDocument()
    })
  })

  describe('state 3 — restructure adaptation headline', () => {
    it('renders the PLAN ADJUSTED card with the deterministic composed headline and a chevron', () => {
      setTimeline(buildRestructureLatestTimeline(weeklyTargetOnlyDiff))
      const { container } = renderDigest(3, PreferredUnits.Kilometers)

      const card = screen.getByTestId('coach-digest-adaptation-card')
      expect(card).toHaveTextContent(/plan adjusted/i)
      expect(card).toHaveTextContent('This week 30.0 km → 26.0 km.')
      expect(container.querySelector('svg')).not.toBeNull()
      // The border-left text block is NOT nested inside the card's markup.
      expect(screen.queryByTestId('coach-digest-user-line')).not.toBeInTheDocument()
      expect(screen.queryByTestId('coach-digest-coach-line')).not.toBeInTheDocument()
      expect(screen.getByText('Open →')).toBeInTheDocument()
    })

    it('threads the Miles preference into the headline', () => {
      setTimeline(buildRestructureLatestTimeline(weeklyTargetOnlyDiff))
      renderDigest(3, PreferredUnits.Miles)

      expect(screen.getByTestId('coach-digest-adaptation-card')).toHaveTextContent(
        'This week 18.6 mi → 16.2 mi.',
      )
    })

    it('clamps a realistic 2-sentence composition (weeklyTargetChange + workoutChange) to a single line, and the clamp is not neutralized by an unbounded ancestor', () => {
      setTimeline(buildRestructureLatestTimeline(twoSentenceDiff))
      renderDigest(3, PreferredUnits.Kilometers)

      const headline = screen.getByTestId('coach-digest-adaptation-headline')
      expect(headline).toHaveTextContent('This week 30.0 km → 26.0 km. Saturday trims to 8.0 km.')
      // The load-bearing half: `truncate` (whitespace-nowrap + overflow-hidden
      // + text-ellipsis) genuinely clips only when every flex ancestor along
      // the ROW axis lets the box shrink below its content's min-content
      // width. This card sits in a flex-ROW (`justify-between`, headline
      // column + chevron), unlike states 1/2's flex-COLUMN ancestry, so its
      // text column needs `min-w-0` — without it a flex item's default
      // `min-width: auto` pins the box to the unwrapped text's full width and
      // `truncate` is inert (this is exactly what let the layout bug through
      // jsdom, which performs no real layout/clipping).
      expect(headline).toHaveClass('truncate')
      const textColumn = headline.parentElement
      expect(textColumn).toHaveClass('min-w-0')
    })
  })

  describe('state 4 — empty', () => {
    it('renders the exact empty copy, two chips, no Open →, and no composer stub', () => {
      setTimeline([])
      renderDigest()

      expect(
        screen.getByText('Nothing yet. Tell me how training feels, or hand me a run to log.'),
      ).toBeInTheDocument()
      expect(screen.queryByText('Open →')).not.toBeInTheDocument()
      expect(screen.queryByTestId('coach-digest-composer-stub')).not.toBeInTheDocument()
      expect(screen.getAllByTestId('coach-digest-chip')).toHaveLength(2)
    })

    it('does not wrap its content in a Link — chips are independent buttons, not nested interactive elements', () => {
      setTimeline([])
      renderDigest()

      expect(screen.queryByTestId('coach-digest-tap-through')).not.toBeInTheDocument()
      expect(screen.queryByRole('link')).not.toBeInTheDocument()
      for (const chip of screen.getAllByTestId('coach-digest-chip')) {
        expect(chip.closest('a')).toBeNull()
      }
    })

    it('navigates to /coach with the matching prefill when a chip is activated', async () => {
      const user = userEvent.setup()
      setTimeline([])
      renderDigest()

      await user.click(screen.getByText("How's my week look?"))
      expect(navigateMock).toHaveBeenCalledExactlyOnceWith('/coach', {
        state: { prefill: "How's my week look?" },
      })

      navigateMock.mockClear()
      await user.click(screen.getByText("Log this morning's run"))
      expect(navigateMock).toHaveBeenCalledExactlyOnceWith('/coach', {
        state: { prefill: "Log this morning's run" },
      })
    })
  })

  describe('composer stub', () => {
    it('navigates to /coach with focusComposer:true when activated, and never accepts text input', () => {
      setTimeline(buildTimeline())
      renderDigest()

      const stub = screen.getByTestId('coach-digest-composer-stub')
      expect(stub.tagName).toBe('BUTTON')
      expect(stub).toHaveAttribute('type', 'button')
    })

    it('activating the composer stub navigates with focusComposer:true', async () => {
      const user = userEvent.setup()
      setTimeline(buildTimeline())
      renderDigest()

      await user.click(screen.getByTestId('coach-digest-composer-stub'))
      expect(navigateMock).toHaveBeenCalledExactlyOnceWith('/coach', {
        state: { focusComposer: true },
      })
    })
  })

  describe('dual-theme parity', () => {
    it('renders state 1 identically in both themes with zero raw colour literals', () => {
      setTimeline(buildTimeline())
      const { dark, light } = renderInBothThemes(
        <MemoryRouter>
          <CoachDigest currentWeek={3} units={PreferredUnits.Kilometers} />
        </MemoryRouter>,
      )
      for (const result of [dark, light]) {
        expect(result.getByTestId('coach-digest')).toBeInTheDocument()
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })

    it('renders state 4 (empty) identically in both themes with zero raw colour literals', () => {
      setTimeline([])
      const { dark, light } = renderInBothThemes(
        <MemoryRouter>
          <CoachDigest currentWeek={3} units={PreferredUnits.Kilometers} />
        </MemoryRouter>,
      )
      for (const result of [dark, light]) {
        expect(result.getByTestId('coach-digest')).toBeInTheDocument()
        expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
      }
      expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
    })
  })

  it('contains zero VDOT references in the rendered DOM (trademark rule)', () => {
    setTimeline(buildRestructureLatestTimeline(weeklyTargetOnlyDiff))
    const { container } = renderDigest()
    expect(container.textContent ?? '').not.toMatch(/vdot/iu)
  })
})
