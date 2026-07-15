import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import type { UseCoachStreamReturn } from '~/modules/coaching/hooks/use-coach-stream.hooks'
import type {
  ConversationTimelineDto,
  ConversationTimelineTurnDto,
  PlanAdaptationDiffDto,
} from '~/modules/coaching/models/conversation.model'

interface MockLocation {
  state: unknown
  key: string
}

const {
  timelineMock,
  streamMock,
  confirmTrigger,
  confirmUnwrap,
  navigateMock,
  locationMock,
  toastErrorMock,
  reportClientErrorMock,
  preferredUnitsMock,
} = vi.hoisted(() => ({
  timelineMock: vi.fn(),
  streamMock: vi.fn(),
  confirmTrigger: vi.fn(),
  confirmUnwrap: vi.fn(),
  navigateMock: vi.fn(),
  locationMock: vi.fn<() => MockLocation>(),
  toastErrorMock: vi.fn(),
  reportClientErrorMock: vi.fn(),
  preferredUnitsMock: vi.fn<() => PreferredUnits>(),
}))

vi.mock('~/api/conversation.api', () => ({
  useGetConversationTimelineQuery: () => timelineMock(),
  useConfirmConversationalLogMutation: () => [confirmTrigger, { isLoading: false }],
}))
vi.mock('~/modules/coaching/hooks/use-coach-stream.hooks', () => ({
  useCoachStream: () => streamMock(),
}))
vi.mock('react-router-dom', async (importActual) => ({
  ...(await importActual<typeof import('react-router-dom')>()),
  useNavigate: () => navigateMock,
  useLocation: () => locationMock(),
}))
vi.mock('sonner', () => ({ toast: { error: toastErrorMock } }))
vi.mock('~/error-boundary/report-client-error', () => ({
  reportClientError: reportClientErrorMock,
}))
// `CoachChat` reads the unit preference via this hook, which wraps a real RTK
// Query hook; the component renders here without a Redux store, so stub it
// through a mockable ref (see `preferredUnitsMock` above).
vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnits: () => preferredUnitsMock(),
}))

import { CoachChat } from './coach-chat.component'

const idleStream = (overrides: Partial<UseCoachStreamReturn> = {}): UseCoachStreamReturn => ({
  pendingUserMessage: null,
  streamingText: '',
  isStreaming: false,
  safety: null,
  card: null,
  error: null,
  send: vi.fn(),
  retry: vi.fn(),
  dismissCard: vi.fn(),
  ...overrides,
})

const userTurn = (content: string): ConversationTimelineTurnDto => ({
  kind: 0,
  turnId: 'u1',
  createdAt: '2026-06-29T10:00:00Z',
  interactive: { content, isErrored: false, loggedRun: null },
  proactive: null,
})

const coachTurn = (content: string): ConversationTimelineTurnDto => ({
  kind: 1,
  turnId: 'c1',
  createdAt: '2026-06-29T10:00:01Z',
  interactive: { content, isErrored: false, loggedRun: null },
  proactive: null,
})

const erroredCoachTurn = (): ConversationTimelineTurnDto => ({
  kind: 1,
  turnId: 'ce1',
  createdAt: '2026-06-29T10:00:01Z',
  interactive: { content: '', isErrored: true, loggedRun: null },
  proactive: null,
})

/**
 * A raw interactive (user/coach) timeline turn with a caller-controlled
 * `turnId`/`createdAt` — used by the date-divider test, which needs several
 * turns spanning two distinct local calendar days without colliding on the
 * fixed `turnId`s the `userTurn`/`coachTurn` helpers above hardcode.
 * `createdAt` is round-tripped through `new Date(y, m, d, h).toISOString()`
 * by callers so the local-day grouping under test is TZ-invariant — the
 * ISO string always re-parses back to the SAME local calendar day under
 * whatever timezone the test process itself runs in.
 */
const turnAt = (
  kind: 0 | 1,
  turnId: string,
  createdAt: string,
  content: string,
): ConversationTimelineTurnDto => ({
  kind,
  turnId,
  createdAt,
  interactive: { content, isErrored: false, loggedRun: null },
  proactive: null,
})

const restructureTurn = (
  diff: PlanAdaptationDiffDto = { workoutChanges: [], weeklyTargetChanges: [] },
): ConversationTimelineTurnDto => ({
  kind: 2,
  turnId: 'a1',
  createdAt: '2026-06-29T10:00:02Z',
  interactive: null,
  proactive: {
    triggeringPlanEventId: 'a1',
    role: 0,
    content: 'I cut this week to recover.',
    escalationLevel: 2,
    safetyTier: 0,
    referralCategory: 0,
    adaptationKind: 2,
    diff,
    triggeringWorkoutLogId: 'w1',
    createdAt: '2026-06-29T10:00:02Z',
  },
})

const safetyTimelineTurn = (): ConversationTimelineTurnDto => ({
  kind: 3,
  turnId: 's1',
  createdAt: '2026-06-29T10:00:03Z',
  interactive: null,
  proactive: {
    triggeringPlanEventId: 's1',
    role: 1,
    content: 'Call 988 if you are in crisis.',
    escalationLevel: null,
    safetyTier: 2,
    referralCategory: 1,
    adaptationKind: null,
    diff: null,
    triggeringWorkoutLogId: 'w2',
    createdAt: '2026-06-29T10:00:03Z',
  },
})

const setTimeline = (turns: ConversationTimelineTurnDto[]): void => {
  const data: ConversationTimelineDto = { turns }
  timelineMock.mockReturnValue({ data, isLoading: false, isError: false })
}

const sampleCard = {
  clientMessageId: 'cm-1',
  prescription: null,
  draft: {
    occurredOn: '2026-06-29',
    distanceValue: 5,
    distanceUnit: 0 as const,
    durationHours: 0,
    durationMinutes: 25,
    durationSeconds: 0,
    completionStatus: 0 as const,
    notes: null,
  },
}

const renderChat = (): void => {
  render(
    <MemoryRouter>
      <CoachChat />
    </MemoryRouter>,
  )
}

describe('CoachChat', () => {
  beforeEach(() => {
    preferredUnitsMock.mockReturnValue(PreferredUnits.Kilometers)
    // Plain `TabBar` navigation carries no `state` — the default for every
    // test that doesn't care about the composer receiver contract.
    locationMock.mockReturnValue({ state: null, key: 'default' })
  })

  afterEach(() => {
    vi.clearAllMocks()
    vi.useRealTimers()
  })

  it('renders interactive turns as chat bubbles and proactive turns via the existing components', () => {
    setTimeline([
      userTurn('how was my run?'),
      coachTurn('You ran well.'),
      restructureTurn(),
      safetyTimelineTurn(),
    ])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    expect(within(screen.getByTestId('user-turn')).getByText('how was my run?')).toBeInTheDocument()
    expect(
      within(screen.getByTestId('coach-text-turn')).getByText('You ran well.'),
    ).toBeInTheDocument()
    expect(
      within(screen.getByTestId('restructure-turn')).getByText('I cut this week to recover.'),
    ).toBeInTheDocument()
    expect(within(screen.getByTestId('safety-turn')).getByText(/call 988/i)).toBeInTheDocument()
    expect(screen.getByTestId('transcript-scroller')).toBeInTheDocument()
  })

  it('threads the unit preference into the restructure diff when units=Miles', async () => {
    const user = userEvent.setup()
    preferredUnitsMock.mockReturnValueOnce(PreferredUnits.Miles)
    setTimeline([
      restructureTurn({
        workoutChanges: [],
        weeklyTargetChanges: [{ weekNumber: 1, beforeWeeklyTargetKm: 36, afterWeeklyTargetKm: 28 }],
      }),
    ])
    streamMock.mockReturnValue(idleStream())

    renderChat()
    await user.click(within(screen.getByTestId('restructure-turn')).getByTestId('diff-toggle'))

    // 36 km / 1.609344 = 22.37... -> 22.4 mi ; 28 km -> 17.4 mi
    expect(screen.getByText('22.4 mi → 17.4 mi')).toBeInTheDocument()
  })

  it('renders streamed coach prose as plain text using approved pace-zone wording', () => {
    setTimeline([coachTurn('Run **easy** today, not your pace-zone limit.')])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    // Markdown is not rendered — the asterisks render literally.
    expect(screen.getByText(/Run \*\*easy\*\* today/)).toBeInTheDocument()
    // Approved exercise-physiology wording renders verbatim.
    expect(screen.getByText(/pace-zone limit/)).toBeInTheDocument()
  })

  it('shows the live user bubble and streaming coach bubble during an exchange', () => {
    setTimeline([])
    streamMock.mockReturnValue(
      idleStream({
        pendingUserMessage: 'tell me about tempo runs',
        streamingText: 'A tempo run is',
        isStreaming: true,
      }),
    )

    renderChat()

    expect(screen.getByText('tell me about tempo runs')).toBeInTheDocument()
    expect(screen.getByText('A tempo run is')).toBeInTheDocument()
  })

  it('captures one shared client-side HH:MM for the live pending user turn and streaming coach turn', () => {
    setTimeline([])
    streamMock.mockReturnValue(idleStream())

    const { rerender } = render(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )

    // Transition pendingUserMessage null -> non-null across a rerender —
    // this is what actually drives the live-time capture in the component
    // (a fresh mount that is ALREADY mid-exchange never exercises the
    // transition at all).
    streamMock.mockReturnValue(
      idleStream({
        pendingUserMessage: 'tell me about tempo runs',
        streamingText: 'A tempo run is',
        isStreaming: true,
      }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )

    const timePattern = /\d{2}:\d{2}/
    const userTime = screen.getByTestId('turn-meta').textContent?.match(timePattern)?.[0]
    const coachTime = screen.getByTestId('coach-text-turn').textContent?.match(timePattern)?.[0]
    // The exact clock value is non-deterministic (a live `new Date()` read) —
    // only that both live rows share the SAME captured value is asserted.
    expect(userTime).toBeDefined()
    expect(coachTime).toBeDefined()
    expect(userTime).toBe(coachTime)
  })

  it('refreshes liveTime on a retry that resends identical text, not just on a message-text change', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 5, 29, 10, 0))
    setTimeline([])
    streamMock.mockReturnValue(idleStream())

    // Mount idle, THEN transition into streaming — a fresh mount that is
    // already mid-exchange never exercises the false -> true transition.
    const { rerender } = render(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )
    streamMock.mockReturnValue(
      idleStream({ pendingUserMessage: 'flaky message', isStreaming: true }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )
    expect(screen.getByTestId('turn-meta')).toHaveTextContent('10:00')

    // The `error` reducer branch leaves `pendingUserMessage` unchanged — the
    // bubble stays beside the retry affordance — while `isStreaming` flips
    // to false.
    streamMock.mockReturnValue(
      idleStream({
        pendingUserMessage: 'flaky message',
        isStreaming: false,
        error: { message: 'boom', retryable: true, retryAfterSeconds: null },
      }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )

    // RETRY re-sends `lastMessageRef.current` — the identical text — so
    // `pendingUserMessage` goes X -> X while `isStreaming` flips back to
    // true. A fix keyed on message-text equality would miss this transition
    // and leave the original attempt's stale 10:00 timestamp.
    vi.setSystemTime(new Date(2026, 5, 29, 10, 5))
    streamMock.mockReturnValue(
      idleStream({ pendingUserMessage: 'flaky message', isStreaming: true }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )

    expect(screen.getByTestId('turn-meta')).toHaveTextContent('10:05')
    vi.useRealTimers()
  })

  it('refreshes liveTime on a direct non-null -> different-non-null pendingUserMessage transition', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 5, 29, 10, 0))
    setTimeline([])
    streamMock.mockReturnValue(idleStream())

    const { rerender } = render(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )
    streamMock.mockReturnValue(
      idleStream({ pendingUserMessage: 'first message', isStreaming: true }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )
    expect(screen.getByTestId('turn-meta')).toHaveTextContent('10:00')

    streamMock.mockReturnValue(
      idleStream({
        pendingUserMessage: 'first message',
        isStreaming: false,
        error: { message: 'boom', retryable: true, retryAfterSeconds: null },
      }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )

    // A NEW message, sent right after the prior error without going through
    // null.
    vi.setSystemTime(new Date(2026, 5, 29, 10, 5))
    streamMock.mockReturnValue(
      idleStream({ pendingUserMessage: 'a different message', isStreaming: true }),
    )
    rerender(
      <MemoryRouter>
        <CoachChat />
      </MemoryRouter>,
    )

    expect(screen.getByTestId('turn-meta')).toHaveTextContent('10:05')
    vi.useRealTimers()
  })

  it('groups a proactive turn into the same local-day bucket as neighbouring interactive turns across a day boundary', () => {
    const dayOneIso = new Date(2026, 5, 28, 9, 0).toISOString()
    const dayTwoIso = new Date(2026, 5, 29, 9, 0).toISOString()
    setTimeline([
      turnAt(0, 'u1', dayOneIso, 'day one message'),
      { ...restructureTurn(), turnId: 'a1', createdAt: dayTwoIso },
      turnAt(0, 'u2', dayTwoIso, 'day two message'),
    ])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    const dividers = screen.getAllByTestId('date-divider')
    expect(dividers).toHaveLength(2)
    expect(dividers[0]).toHaveTextContent('JUN 28')
    expect(dividers[1]).toHaveTextContent('JUN 29')

    // `Fragment` renders no wrapping DOM node, so bucketing is verified by
    // DOM order rather than a parent-container query: the proactive turn
    // must sit AFTER the second (JUN 29) divider, not the first — i.e. it
    // buckets alongside `turnAt`'s day-two interactive turn, not day one.
    const orderedTestIds = within(screen.getByTestId('transcript-scroller'))
      .getAllByTestId(/^(date-divider|restructure-turn|user-turn)$/)
      .map((el) => el.getAttribute('data-testid'))
    expect(orderedTestIds).toEqual([
      'date-divider',
      'user-turn',
      'date-divider',
      'restructure-turn',
      'user-turn',
    ])
  })

  it('renders a date divider before each new local-calendar-day group of persisted turns', () => {
    const dayOneIso = new Date(2026, 5, 28, 9, 0).toISOString()
    const dayTwoIso = new Date(2026, 5, 29, 9, 0).toISOString()
    setTimeline([
      turnAt(0, 'u1', dayOneIso, 'day one message'),
      turnAt(1, 'c1', dayOneIso, 'day one reply'),
      turnAt(0, 'u2', dayTwoIso, 'day two message'),
    ])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    const dividers = screen.getAllByTestId('date-divider')
    expect(dividers).toHaveLength(2)
    expect(dividers[0]).toHaveTextContent('JUN 28')
    expect(dividers[1]).toHaveTextContent('JUN 29')
  })

  it('renders no date divider for an empty timeline', () => {
    setTimeline([])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    expect(screen.queryByTestId('date-divider')).not.toBeInTheDocument()
  })

  it('renders a persisted errored coach turn with its copy and no RETRY control', () => {
    setTimeline([userTurn('how was my run?'), erroredCoachTurn()])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    const erroredNote = screen.getByTestId('coach-errored-turn')
    expect(within(erroredNote).getByText("That reply didn't go through.")).toBeInTheDocument()
    expect(within(erroredNote).queryByRole('button')).not.toBeInTheDocument()
  })

  it('wires the composer send to the stream', async () => {
    const user = userEvent.setup()
    const send = vi.fn()
    setTimeline([])
    streamMock.mockReturnValue(idleStream({ send }))

    renderChat()
    await user.type(screen.getByLabelText(/message your coach/i), 'how was my run?')
    await user.click(screen.getByRole('button', { name: /send/i }))

    expect(send).toHaveBeenCalledExactlyOnceWith('how was my run?')
  })

  it('confirms a card through the mutation and dismisses it on success', async () => {
    const user = userEvent.setup()
    const dismissCard = vi.fn()
    confirmUnwrap.mockResolvedValue({
      workoutLogId: 'l1',
      adaptation: { kind: 0, adaptationKind: 0, retryable: false },
    })
    confirmTrigger.mockReturnValue({ unwrap: confirmUnwrap })
    setTimeline([])
    streamMock.mockReturnValue(idleStream({ card: sampleCard, dismissCard }))

    renderChat()
    await user.click(screen.getByRole('button', { name: /^confirm$/i }))

    expect(confirmTrigger).toHaveBeenCalledExactlyOnceWith({
      draft: sampleCard.draft,
      clientMessageId: 'cm-1',
    })
    expect(dismissCard).toHaveBeenCalledOnce()
  })

  it('keeps the card open and surfaces an error when the confirm fails', async () => {
    const user = userEvent.setup()
    const dismissCard = vi.fn()
    confirmUnwrap.mockRejectedValue(new Error('confirm boom'))
    confirmTrigger.mockReturnValue({ unwrap: confirmUnwrap })
    setTimeline([])
    streamMock.mockReturnValue(idleStream({ card: sampleCard, dismissCard }))

    renderChat()
    await user.click(screen.getByRole('button', { name: /^confirm$/i }))

    // The card stays for retry; the user is told it failed and a diagnostic
    // trail is left for the handled `.unwrap()` rejection (otherwise invisible
    // to the global error reporter).
    expect(dismissCard).not.toHaveBeenCalled()
    expect(toastErrorMock).toHaveBeenCalledOnce()
    expect(reportClientErrorMock).toHaveBeenCalledOnce()
    expect(screen.getByTestId('log-confirmation-card')).toBeInTheDocument()
  })

  it('opens the log form pre-filled on Edit and dismisses the card', async () => {
    const user = userEvent.setup()
    const dismissCard = vi.fn()
    setTimeline([])
    streamMock.mockReturnValue(idleStream({ card: sampleCard, dismissCard }))

    renderChat()
    await user.click(screen.getByRole('button', { name: /^edit$/i }))

    expect(navigateMock).toHaveBeenCalledExactlyOnceWith('/log', {
      state: { draft: sampleCard.draft },
    })
    expect(dismissCard).toHaveBeenCalledOnce()
  })

  it('surfaces a retry affordance on error and retries through the stream', async () => {
    const user = userEvent.setup()
    const retry = vi.fn()
    setTimeline([])
    streamMock.mockReturnValue(
      idleStream({
        error: { message: 'My end broke.', retryable: true, retryAfterSeconds: 5 },
        retry,
      }),
    )

    renderChat()
    expect(screen.getByText('My end broke.')).toBeInTheDocument()
    expect(screen.getByTestId('coach-error')).toHaveClass('border-l-destructive')
    await user.click(screen.getByRole('button', { name: /retry/i }))

    expect(retry).toHaveBeenCalledOnce()
  })

  it('hides the RETRY control on the live error surface when the error is not retryable', () => {
    setTimeline([])
    streamMock.mockReturnValue(
      idleStream({
        error: { message: 'Cannot retry this one.', retryable: false, retryAfterSeconds: 0 },
      }),
    )

    renderChat()

    expect(screen.getByTestId('coach-error')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument()
  })

  it('renders the deterministic safety notice during an exchange', () => {
    setTimeline([])
    streamMock.mockReturnValue(
      idleStream({ safety: { content: 'Call 988 now.', tier: 2, category: 1 } }),
    )

    renderChat()
    expect(
      within(screen.getByTestId('coach-safety-notice')).getByText(/call 988 now/i),
    ).toBeInTheDocument()
  })

  describe('composer prefill/focus receiver', () => {
    it('seeds the composer and focuses it when navigation state carries a prefill', () => {
      locationMock.mockReturnValue({ state: { prefill: "How's my week look?" }, key: 'nav-1' })
      setTimeline([])
      streamMock.mockReturnValue(idleStream())

      renderChat()

      const input = screen.getByLabelText(/message your coach/i)
      expect(input).toHaveValue("How's my week look?")
      expect(input).toHaveFocus()
    })

    it('focuses the empty composer when navigation state carries focusComposer with no prefill', () => {
      locationMock.mockReturnValue({ state: { focusComposer: true }, key: 'nav-1' })
      setTimeline([])
      streamMock.mockReturnValue(idleStream())

      renderChat()

      const input = screen.getByLabelText(/message your coach/i)
      expect(input).toHaveValue('')
      expect(input).toHaveFocus()
    })

    it('neither prefills nor autofocuses the composer on plain TabBar navigation (no state)', () => {
      locationMock.mockReturnValue({ state: null, key: 'default' })
      setTimeline([])
      streamMock.mockReturnValue(idleStream())

      renderChat()

      const input = screen.getByLabelText(/message your coach/i)
      expect(input).toHaveValue('')
      expect(input).not.toHaveFocus()
    })

    it('forces a fresh CoachComposer mount on each navigation via key={location.key}, applying the new prefill without merging stale state', () => {
      setTimeline([])
      streamMock.mockReturnValue(idleStream())
      locationMock.mockReturnValue({ state: { prefill: 'first message' }, key: 'nav-1' })

      const { rerender } = render(
        <MemoryRouter>
          <CoachChat />
        </MemoryRouter>,
      )
      expect(screen.getByLabelText(/message your coach/i)).toHaveValue('first message')

      locationMock.mockReturnValue({ state: { prefill: 'second message' }, key: 'nav-2' })
      rerender(
        <MemoryRouter>
          <CoachChat />
        </MemoryRouter>,
      )

      expect(screen.getByLabelText(/message your coach/i)).toHaveValue('second message')
    })

    it('does not remount the composer on a same-URL re-render/navigation carrying no state, preserving an in-progress draft', async () => {
      const user = userEvent.setup()
      setTimeline([])
      streamMock.mockReturnValue(idleStream())
      // A same-URL REPLACE (e.g. re-tapping the active COACH tab) still mints
      // a fresh `location.key`, but carries no state — the composer must not
      // remount, or a runner's in-progress draft is silently discarded.
      locationMock.mockReturnValue({ state: null, key: 'default' })

      const { rerender } = render(
        <MemoryRouter>
          <CoachChat />
        </MemoryRouter>,
      )
      await user.type(screen.getByLabelText(/message your coach/i), 'my unsent draft')
      expect(screen.getByLabelText(/message your coach/i)).toHaveValue('my unsent draft')

      locationMock.mockReturnValue({ state: null, key: 'nav-replace' })
      rerender(
        <MemoryRouter>
          <CoachChat />
        </MemoryRouter>,
      )

      expect(screen.getByLabelText(/message your coach/i)).toHaveValue('my unsent draft')
    })
  })

  describe('isCoachChatLocationState rejection path', () => {
    it('treats a malformed state (wrong field types) as no state — empty, unfocused composer', () => {
      locationMock.mockReturnValue({ state: { prefill: 42 }, key: 'nav-1' })
      setTimeline([])
      streamMock.mockReturnValue(idleStream())

      renderChat()

      const input = screen.getByLabelText(/message your coach/i)
      expect(input).toHaveValue('')
      expect(input).not.toHaveFocus()
    })

    it('treats a non-object state (a bare string) as no state — empty, unfocused composer', () => {
      locationMock.mockReturnValue({ state: 'not-an-object', key: 'nav-1' })
      setTimeline([])
      streamMock.mockReturnValue(idleStream())

      renderChat()

      const input = screen.getByLabelText(/message your coach/i)
      expect(input).toHaveValue('')
      expect(input).not.toHaveFocus()
    })
  })
})
