import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

interface MutationResult {
  unwrap: () => Promise<{ planId: string; status: string }>
}

const { regenerateMock, mutationStateRef } = vi.hoisted(() => ({
  regenerateMock: vi.fn<(arg: unknown) => MutationResult>(),
  mutationStateRef: { isLoading: false },
}))

vi.mock('~/api/plan.api', () => ({
  useRegeneratePlanMutation: () => [regenerateMock, mutationStateRef],
}))

import { RegeneratePlanDialog } from './regenerate-plan-dialog.component'

const renderDialog = (onClose: () => void) =>
  render(<RegeneratePlanDialog isOpen={true} onClose={onClose} />)

describe('RegeneratePlanDialog', () => {
  beforeEach(() => {
    regenerateMock.mockReset()
    mutationStateRef.isLoading = false
    // Stable idempotency key so we can assert exact body shape.
    vi.spyOn(crypto, 'randomUUID').mockReturnValue('00000000-0000-0000-0000-00000000abcd')
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('renders nothing when isOpen is false', () => {
    render(<RegeneratePlanDialog isOpen={false} onClose={vi.fn()} />)
    expect(screen.queryByTestId('regenerate-plan-dialog')).not.toBeInTheDocument()
  })

  it('renders the dialog with replacement copy and an optional intent textarea', () => {
    renderDialog(vi.fn())
    expect(screen.getByTestId('regenerate-plan-dialog')).toBeInTheDocument()
    expect(screen.getByText('This replaces your current plan.')).toBeInTheDocument()
    expect(screen.getByLabelText(/anything we should know/i)).toBeInTheDocument()
    const textarea = screen.getByTestId('regenerate-plan-intent') as HTMLTextAreaElement
    expect(textarea.maxLength).toBe(500)
  })

  it('submits without an intent block when textarea is empty', async () => {
    const onClose = vi.fn()
    regenerateMock.mockReturnValue({
      unwrap: () => Promise.resolve({ planId: 'plan-1', status: 'generated' }),
    })
    renderDialog(onClose)
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(regenerateMock).toHaveBeenCalledWith({
      idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
    })
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('submits the trimmed intent free-text and closes on success', async () => {
    const onClose = vi.fn()
    regenerateMock.mockReturnValue({
      unwrap: () => Promise.resolve({ planId: 'plan-2', status: 'generated' }),
    })
    renderDialog(onClose)
    await userEvent.type(
      screen.getByTestId('regenerate-plan-intent'),
      '  reducing volume due to injury  ',
    )
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(regenerateMock).toHaveBeenCalledWith({
      idempotencyKey: '00000000-0000-0000-0000-00000000abcd',
      intent: { freeText: 'reducing volume due to injury' },
    })
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('shows an error message when the mutation fails and keeps the dialog open', async () => {
    const onClose = vi.fn()
    regenerateMock.mockReturnValue({
      unwrap: () => Promise.reject(new Error('boom')),
    })
    renderDialog(onClose)
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not regenerate/i)
    expect(onClose).not.toHaveBeenCalled()
  })

  it('disables the submit button and shows progress copy while regenerating', () => {
    mutationStateRef.isLoading = true
    renderDialog(vi.fn())
    const submit = screen.getByTestId('regenerate-plan-submit')
    expect(submit).toBeDisabled()
    expect(submit).toHaveTextContent(/regenerating/i)
  })

  it('invokes onClose when Cancel is clicked', async () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })
})
