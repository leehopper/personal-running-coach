import { configureStore } from '@reduxjs/toolkit'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Provider } from 'react-redux'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  OnboardingStatus,
  OnboardingTopic,
  OnboardingTurnKind,
  AnthropicContentBlockType,
  SuggestedInputType,
  type OnboardingStateDto,
  type OnboardingTurnAskResponse,
  type OnboardingTurnCompleteResponse,
} from '~/modules/onboarding/models/onboarding.model'
import { onboardingSlice } from '~/modules/onboarding/store/onboarding.slice'

interface SubmitArgs {
  idempotencyKey: string
  text: string
}

const { getStateMock, submitMock, submitUnwrap, navigateMock } = vi.hoisted(() => {
  const unwrap = vi.fn()
  return {
    getStateMock: vi.fn(),
    submitMock: vi.fn((args: { idempotencyKey: string; text: string }) => ({ unwrap, args })),
    submitUnwrap: unwrap,
    navigateMock: vi.fn(),
  }
})

vi.mock('~/api/onboarding.api', () => ({
  useGetOnboardingStateQuery: () => getStateMock(),
  useSubmitOnboardingTurnMutation: () => [submitMock, { isLoading: false }],
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

import { OnboardingPage } from './onboarding.page'

const makeStore = () =>
  configureStore({ reducer: { [onboardingSlice.name]: onboardingSlice.reducer } })

const renderPage = () => {
  const store = makeStore()
  return {
    store,
    user: userEvent.setup(),
    ...render(
      <Provider store={store}>
        <MemoryRouter initialEntries={['/onboarding']}>
          <OnboardingPage />
        </MemoryRouter>
      </Provider>,
    ),
  }
}

const noStreamYetState: OnboardingStateDto = {
  userId: 'user-1',
  status: OnboardingStatus.NotStarted,
  currentTopic: null,
  completedTopics: 0,
  totalTopics: 6,
  isComplete: false,
  outstandingClarifications: [],
  primaryGoal: null,
  targetEvent: null,
  currentFitness: null,
  weeklySchedule: null,
  injuryHistory: null,
  preferences: null,
  currentPlanId: null,
}

const inProgressState: OnboardingStateDto = {
  ...noStreamYetState,
  status: OnboardingStatus.InProgress,
  currentTopic: OnboardingTopic.WeeklySchedule,
  completedTopics: 3,
}

const askResponse: OnboardingTurnAskResponse = {
  kind: OnboardingTurnKind.Ask,
  assistantBlocks: [
    { type: AnthropicContentBlockType.Text, text: 'How many days per week do you run?' },
  ],
  topic: OnboardingTopic.WeeklySchedule,
  suggestedInputType: SuggestedInputType.MultiSelect,
  progress: { completedTopics: 1, totalTopics: 6 },
  planId: null,
}

const completeResponse: OnboardingTurnCompleteResponse = {
  kind: OnboardingTurnKind.Complete,
  assistantBlocks: [
    { type: AnthropicContentBlockType.Text, text: 'All set — building your plan.' },
  ],
  topic: null,
  suggestedInputType: null,
  progress: { completedTopics: 6, totalTopics: 6 },
  planId: '8a4b9b2a-1d3f-4f1c-9aab-5e2c1f0b1234',
}

describe('OnboardingPage', () => {
  beforeEach(() => {
    submitUnwrap.mockReset()
    submitMock.mockClear()
    getStateMock.mockReset()
    navigateMock.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('shows the loading state while the initial state query is in flight', () => {
    getStateMock.mockReturnValue({ data: undefined, isLoading: true, isError: false })
    renderPage()
    expect(screen.getByRole('status')).toHaveTextContent(/loading/i)
  })

  it('renders the chat surface with the default progress indicator when no stream exists yet (404)', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })
    renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })
    expect(screen.getByTestId('topic-progress-indicator')).toBeInTheDocument()
    expect(screen.getByTestId('single-select-turn-input')).toBeInTheDocument()
  })

  it('replays progress + current topic when state returns 200 mid-flow', async () => {
    getStateMock.mockReturnValue({ data: inProgressState, isLoading: false, isError: false })
    renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })
    // Three completed topics rendered as the canonical prefix.
    const segments = screen.getAllByRole('listitem')
    expect(segments.filter((segment) => segment.dataset.state === 'completed')).toHaveLength(3)
    // The 4th topic (WeeklySchedule) is current.
    const currentSegment = segments.find((segment) => segment.dataset.state === 'current')
    expect(currentSegment).toBeDefined()
    expect(currentSegment?.dataset.topic).toBe(String(OnboardingTopic.WeeklySchedule))
  })

  it('redirects to "/" when the state query reports onboarding is already complete', async () => {
    getStateMock.mockReturnValue({
      data: { ...noStreamYetState, isComplete: true, status: OnboardingStatus.Completed },
      isLoading: false,
      isError: false,
    })
    renderPage()
    await waitFor(() => {
      expect(navigateMock).toHaveBeenCalledWith('/', { replace: true })
    })
  })

  it('appends a pending user bubble immediately on submit and flips it to delivered after the server responds', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })
    submitUnwrap.mockResolvedValue(askResponse)
    const { user, store } = renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })
    await user.click(screen.getAllByRole('radio')[0])
    await user.click(screen.getByRole('button', { name: /send/i }))

    await waitFor(() => {
      const turns = store.getState().onboarding.turns
      expect(turns).toHaveLength(2)
      expect(turns[0].role).toBe('user')
      expect(turns[0].status).toBe('delivered')
      expect(turns[1].role).toBe('assistant')
    })
    // submit was invoked with a UUID-shaped idempotency key
    expect(submitMock).toHaveBeenCalledTimes(1)
    const submitArg = submitMock.mock.calls[0]?.[0] as SubmitArgs | undefined
    expect(submitArg?.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/i)
  })

  it('flips a failed turn to status=failed and surfaces the Retry affordance', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })
    submitUnwrap.mockRejectedValue({ status: 502 })
    const { user, store } = renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })
    await user.click(screen.getAllByRole('radio')[0])
    await user.click(screen.getByRole('button', { name: /send/i }))

    await waitFor(() => {
      const turns = store.getState().onboarding.turns
      expect(turns).toHaveLength(1)
      expect(turns[0].status).toBe('failed')
    })
    expect(screen.getByTestId('onboarding-retry')).toBeInTheDocument()
  })

  it('reuses the SAME idempotency key when retrying a failed turn', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })
    submitUnwrap.mockRejectedValueOnce({ status: 502 }).mockResolvedValueOnce(askResponse)
    const { user } = renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })
    await user.click(screen.getAllByRole('radio')[0])
    await user.click(screen.getByRole('button', { name: /send/i }))

    await waitFor(() => {
      expect(screen.getByTestId('onboarding-retry')).toBeInTheDocument()
    })
    const firstKey = (submitMock.mock.calls[0]?.[0] as SubmitArgs | undefined)?.idempotencyKey

    await user.click(screen.getByRole('button', { name: /retry/i }))
    await waitFor(() => {
      expect(submitMock).toHaveBeenCalledTimes(2)
    })
    const secondKey = (submitMock.mock.calls[1]?.[0] as SubmitArgs | undefined)?.idempotencyKey
    expect(secondKey).toBe(firstKey)
    expect(firstKey).toMatch(/^[0-9a-f-]{36}$/i)
  })

  it('navigates to "/" when the server returns kind=complete', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })
    submitUnwrap.mockResolvedValue(completeResponse)
    const { user } = renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })
    await user.click(screen.getAllByRole('radio')[0])
    await user.click(screen.getByRole('button', { name: /send/i }))

    await waitFor(() => {
      expect(navigateMock).toHaveBeenCalledWith('/', { replace: true })
    })
  })

  it('advances the progress indicator after a successful submit (0 → 1 completed segment)', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })
    submitUnwrap.mockResolvedValue(askResponse)
    const { user } = renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })

    // Before submit: zero segments are completed (404 → fresh stream).
    const segmentsBefore = screen.getAllByRole('listitem')
    expect(segmentsBefore.filter((segment) => segment.dataset.state === 'completed')).toHaveLength(
      0,
    )

    await user.click(screen.getAllByRole('radio')[0])
    await user.click(screen.getByRole('button', { name: /send/i }))

    // After submit: progress.completedTopics === 1 → exactly one segment
    // is `completed`, the next one (WeeklySchedule, the askResponse topic)
    // is highlighted as `current`, and the remaining four stay `pending`.
    await waitFor(() => {
      const segments = screen.getAllByRole('listitem')
      expect(segments.filter((segment) => segment.dataset.state === 'completed')).toHaveLength(1)
      const currentSegment = segments.find((segment) => segment.dataset.state === 'current')
      expect(currentSegment?.dataset.topic).toBe(String(OnboardingTopic.WeeklySchedule))
      expect(segments.filter((segment) => segment.dataset.state === 'pending')).toHaveLength(4)
    })
  })

  it('renders the "building your plan" placeholder while the final turn is in flight before navigation', async () => {
    getStateMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: { status: 404 },
    })

    // Hold the final-turn POST open so the page renders the
    // programmatic "building your plan" assistant turn before the
    // mutation resolves and navigation fires. Real timers are kept so
    // user-event's microtask chain doesn't deadlock — the 1500ms grace
    // inside `useOnboardingTurn` is small enough to wait through.
    let resolveSubmit: (value: OnboardingTurnCompleteResponse) => void = () => {}
    submitUnwrap.mockImplementation(
      () =>
        new Promise<OnboardingTurnCompleteResponse>((resolve) => {
          resolveSubmit = resolve
        }),
    )

    const { user } = renderPage()
    await waitFor(() => {
      expect(screen.getByTestId('onboarding-chat')).toBeInTheDocument()
    })

    await user.click(screen.getAllByRole('radio')[0])
    await user.click(screen.getByRole('button', { name: /send/i }))

    // Wait past the 1500ms grace window inside `useOnboardingTurn`
    // so the buildingPlanStarted action lands in the slice and the
    // shimmered "building your plan…" assistant turn paints. The
    // longer `findByText` timeout absorbs jsdom variance.
    const buildingTurn = await screen.findByText(/building your plan/i, undefined, {
      timeout: 3000,
    })
    expect(buildingTurn).toBeInTheDocument()
    const buildingRow = buildingTurn.closest('[data-status="building-plan"]')
    expect(buildingRow).not.toBeNull()
    expect(navigateMock).not.toHaveBeenCalledWith('/', { replace: true })

    // Now resolve the POST — the final assistant turn replaces the
    // placeholder and the page navigates home.
    resolveSubmit(completeResponse)
    await waitFor(() => {
      expect(navigateMock).toHaveBeenCalledWith('/', { replace: true })
    })
  }, 10000)
})
