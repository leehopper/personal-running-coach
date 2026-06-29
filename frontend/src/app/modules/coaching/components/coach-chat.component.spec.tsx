import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { UseCoachStreamReturn } from '~/modules/coaching/hooks/use-coach-stream.hooks'
import type {
  ConversationTimelineDto,
  ConversationTimelineTurnDto,
} from '~/modules/coaching/models/conversation.model'

const { timelineMock, streamMock, confirmTrigger, confirmUnwrap, navigateMock } = vi.hoisted(
  () => ({
    timelineMock: vi.fn(),
    streamMock: vi.fn(),
    confirmTrigger: vi.fn(),
    confirmUnwrap: vi.fn(),
    navigateMock: vi.fn(),
  }),
)

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
  interactive: { content, isErrored: false },
  proactive: null,
})

const coachTurn = (content: string): ConversationTimelineTurnDto => ({
  kind: 1,
  turnId: 'c1',
  createdAt: '2026-06-29T10:00:01Z',
  interactive: { content, isErrored: false },
  proactive: null,
})

const restructureTurn = (): ConversationTimelineTurnDto => ({
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
    diff: { workoutChanges: [], weeklyTargetChanges: [] },
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

  it('renders streamed coach prose as plain text and never mentions VDOT', () => {
    setTimeline([coachTurn('Run **easy** today, not your pace-zone limit.')])
    streamMock.mockReturnValue(idleStream())

    renderChat()

    // Markdown is not rendered — the asterisks render literally.
    expect(screen.getByText(/Run \*\*easy\*\* today/)).toBeInTheDocument()
    expect(screen.queryByText(/vdot/i)).toBeNull()
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
})
