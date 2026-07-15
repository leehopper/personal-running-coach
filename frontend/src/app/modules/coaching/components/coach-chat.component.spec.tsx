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

    expect(screen.getByText('how was my run?')).toBeInTheDocument()
    expect(screen.getByText('You ran well.')).toBeInTheDocument()
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
    await user.click(screen.getByRole('button', { name: /retry/i }))

    expect(retry).toHaveBeenCalledOnce()
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
