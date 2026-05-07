import { fireEvent, render, screen } from '@testing-library/react'
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

  it('closes the dialog when the Escape key is pressed while idle', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('does not close on Escape while the regenerate mutation is in flight', () => {
    mutationStateRef.isLoading = true
    const onClose = vi.fn()
    renderDialog(onClose)
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes the dialog when the backdrop is clicked while idle', async () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    await userEvent.click(screen.getByTestId('regenerate-plan-backdrop'))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('does not close on backdrop click while the mutation is in flight', async () => {
    mutationStateRef.isLoading = true
    const onClose = vi.fn()
    renderDialog(onClose)
    await userEvent.click(screen.getByTestId('regenerate-plan-backdrop'))
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes the dialog when Enter is pressed on the backdrop', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    const backdrop = screen.getByTestId('regenerate-plan-backdrop')
    const prevented = !fireEvent.keyDown(backdrop, { key: 'Enter' })
    expect(prevented).toBe(true)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('closes the dialog when Space is pressed on the backdrop', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    const backdrop = screen.getByTestId('regenerate-plan-backdrop')
    const prevented = !fireEvent.keyDown(backdrop, { key: ' ' })
    expect(prevented).toBe(true)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('ignores backdrop key events that are not Enter or Space', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    const backdrop = screen.getByTestId('regenerate-plan-backdrop')
    fireEvent.keyDown(backdrop, { key: 'a' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('shows 500 characters remaining when textarea is empty, 250 at half capacity, and 0 at limit', () => {
    renderDialog(vi.fn())
    const textarea = screen.getByTestId('regenerate-plan-intent')

    expect(screen.getByText('500 characters remaining')).toBeInTheDocument()

    fireEvent.change(textarea, { target: { value: 'a'.repeat(250) } })
    expect(screen.getByText('250 characters remaining')).toBeInTheDocument()

    fireEvent.change(textarea, { target: { value: 'a'.repeat(500) } })
    expect(screen.getByText('0 characters remaining')).toBeInTheDocument()
  })

  it('uses the same idempotency key on retry after a transient failure', async () => {
    // Override the constant stub with an incrementing mock so that a broken
    // implementation calling randomUUID() per-submit would produce different
    // values and the assertion would catch it.
    let callCount = 0
    vi.spyOn(crypto, 'randomUUID').mockImplementation(
      () =>
        `00000000-0000-0000-0000-${String(++callCount).padStart(12, '0')}` as ReturnType<
          typeof crypto.randomUUID
        >,
    )

    const onClose = vi.fn()
    regenerateMock
      .mockReturnValueOnce({ unwrap: () => Promise.reject(new Error('transient')) })
      .mockReturnValue({
        unwrap: () => Promise.resolve({ planId: 'plan-retry', status: 'generated' }),
      })

    renderDialog(onClose)

    // First submit — fails
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(await screen.findByRole('alert')).toBeInTheDocument()

    // Second submit — succeeds
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(onClose).toHaveBeenCalledTimes(1)

    expect(regenerateMock).toHaveBeenCalledTimes(2)
    const [firstCall, secondCall] = regenerateMock.mock.calls as [
      [{ idempotencyKey: string }],
      [{ idempotencyKey: string }],
    ]
    expect(firstCall[0].idempotencyKey).toBe(secondCall[0].idempotencyKey)
  })
})
