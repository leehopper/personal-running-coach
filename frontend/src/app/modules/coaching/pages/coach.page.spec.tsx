import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { PreferredUnits } from '~/api/generated'
import type { UseCoachStreamReturn } from '~/modules/coaching/hooks/use-coach-stream.hooks'

const { timelineMock, streamMock, preferredUnitsMock, usePlanMock } = vi.hoisted(() => ({
  timelineMock: vi.fn(),
  streamMock: vi.fn(),
  preferredUnitsMock: vi.fn<() => PreferredUnits>(),
  usePlanMock: vi.fn(),
}))

vi.mock('~/api/conversation.api', () => ({
  useGetConversationTimelineQuery: () => timelineMock(),
  useConfirmConversationalLogMutation: () => [vi.fn(), { isLoading: false }],
}))
vi.mock('~/modules/coaching/hooks/use-coach-stream.hooks', () => ({
  useCoachStream: () => streamMock(),
}))
vi.mock('~/modules/settings/hooks/use-preferred-units.hooks', () => ({
  usePreferredUnits: () => preferredUnitsMock(),
}))
vi.mock('~/modules/plan/hooks/use-plan.hooks', () => ({
  usePlan: () => usePlanMock(),
}))

import { CoachPage } from './coach.page'

const idleStream = (): UseCoachStreamReturn => ({
  pendingUserMessage: null,
  streamingText: '',
  isStreaming: false,
  safety: null,
  card: null,
  error: null,
  send: vi.fn(),
  retry: vi.fn(),
  dismissCard: vi.fn(),
})

const renderCoachPage = () =>
  render(
    <MemoryRouter initialEntries={['/coach']}>
      <CoachPage />
    </MemoryRouter>,
  )

describe('CoachPage', () => {
  beforeEach(() => {
    timelineMock.mockReturnValue({ data: { turns: [] }, isLoading: false, isError: false })
    streamMock.mockReturnValue(idleStream())
    preferredUnitsMock.mockReturnValue(PreferredUnits.Kilometers)
    usePlanMock.mockReturnValue({ plan: undefined })
  })

  it('mounts under the coach-page testid', () => {
    renderCoachPage()
    expect(screen.getByTestId('coach-page')).toBeInTheDocument()
  })

  it('mounts the full existing CoachChat experience — transcript + composer', () => {
    renderCoachPage()
    expect(screen.getByTestId('coach-chat')).toBeInTheDocument()
    expect(screen.getByTestId('transcript-scroller')).toBeInTheDocument()
    expect(screen.getByTestId('coach-composer')).toBeInTheDocument()
  })
})
