import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

interface MutationResult {
  unwrap: () => Promise<{ planId: string; status: string }>
}

const { regenerateMock, mutationStateRef, navigateMock } = vi.hoisted(() => ({
  regenerateMock: vi.fn<(arg: unknown) => MutationResult>(),
  mutationStateRef: { isLoading: false },
  navigateMock: vi.fn(),
}))

vi.mock('~/api/plan.api', () => ({
  useRegeneratePlanMutation: () => [regenerateMock, mutationStateRef],
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

import { RegeneratePlanDialog } from './regenerate-plan-dialog.component'

const renderDialog = (onClose: () => void) =>
  render(<RegeneratePlanDialog isOpen={true} onClose={onClose} />)

describe('RegeneratePlanDialog', () => {
  beforeEach(() => {
    regenerateMock.mockReset()
    mutationStateRef.isLoading = false
    navigateMock.mockReset()
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
    expect(
      screen.getByText(
        "This replaces your current plan. The coach starts fresh from your log book \u2014 nothing you've logged is lost.",
      ),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Anything I should know? \u2014 optional')).toBeInTheDocument()
    const textarea = screen.getByTestId('regenerate-plan-intent') as HTMLTextAreaElement
    expect(textarea.maxLength).toBe(500)
    expect(textarea).toHaveAttribute(
      'placeholder',
      'e.g. coming back from a calf strain, want to focus on long runs\u2026',
    )
    expect(screen.getByTestId('regenerate-plan-cancel')).toBeInTheDocument()
  })

  it('keeps the dialog ARIA wiring and the panel classes', () => {
    renderDialog(vi.fn())
    const dialog = screen.getByTestId('regenerate-plan-dialog')
    const title = screen.getByRole('heading', { name: 'Regenerate plan', level: 2 })
    const description = document.getElementById('regenerate-plan-description')
    const backdrop = screen.getByTestId('regenerate-plan-backdrop')
    const cancel = screen.getByTestId('regenerate-plan-cancel')
    const submit = screen.getByTestId('regenerate-plan-submit')

    expect(dialog).toHaveAttribute('role', 'dialog')
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAttribute('aria-labelledby', 'regenerate-plan-title')
    expect(dialog).toHaveAttribute('aria-describedby', 'regenerate-plan-description')
    expect(document.getElementById('regenerate-plan-title')).toBe(title)
    expect(description).toBeInTheDocument()
    expect(document.getElementById('regenerate-plan-description')).toBe(description)
    expect(dialog.className).toBe(
      'flex w-[calc(100%-40px)] max-w-[350px] flex-col gap-3 rounded-xl border border-border bg-card p-[18px]',
    )
    expect(backdrop.className).toBe(
      'fixed inset-0 z-50 flex items-center justify-center bg-foreground/40 px-4',
    )
    expect(cancel).toHaveClass('min-h-11')
    expect(submit).toHaveClass('min-h-11')
    expect(cancel.parentElement?.className).toBe('flex items-center justify-end gap-2.5')
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
    expect(navigateMock).not.toHaveBeenCalled()
  })

  it('shows the building surface while regeneration is in flight', () => {
    mutationStateRef.isLoading = true
    renderDialog(vi.fn())
    expect(screen.getByTestId('settings-regenerate-building')).toHaveClass(
      'fixed',
      'inset-0',
      'z-50',
    )
    expect(screen.getByRole('status')).toHaveTextContent('BUILDING YOUR PLAN')
    expect(screen.getByRole('status')).toHaveTextContent('Reworking your plan from the log book.')
    expect(screen.queryByTestId('regenerate-plan-dialog')).not.toBeInTheDocument()
    expect(screen.queryByTestId('regenerate-plan-backdrop')).not.toBeInTheDocument()
  })

  it('invokes onClose when Cancel is clicked', async () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }))
    expect(onClose).toHaveBeenCalledTimes(1)
    expect(navigateMock).not.toHaveBeenCalled()
  })

  it('closes the dialog when the Escape key is pressed while idle', () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('cannot be dismissed while the mutation is in flight', () => {
    mutationStateRef.isLoading = true
    const onClose = vi.fn()
    renderDialog(onClose)
    expect(screen.queryByTestId('regenerate-plan-backdrop')).not.toBeInTheDocument()
    fireEvent.keyDown(window, { key: 'Escape' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes the dialog when the backdrop is clicked while idle', async () => {
    const onClose = vi.fn()
    renderDialog(onClose)
    await userEvent.click(screen.getByTestId('regenerate-plan-backdrop'))
    expect(onClose).toHaveBeenCalledTimes(1)
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

    expect(screen.getByText('500 left')).toBeInTheDocument()

    fireEvent.change(textarea, { target: { value: 'a'.repeat(250) } })
    expect(screen.getByText('250 left')).toBeInTheDocument()

    fireEvent.change(textarea, { target: { value: 'a'.repeat(500) } })
    expect(screen.getByText('0 left')).toBeInTheDocument()
  })

  it('navigates home after successful regeneration', async () => {
    const onClose = vi.fn()
    regenerateMock.mockReturnValue({
      unwrap: () => Promise.resolve({ planId: 'plan-2', status: 'generated' }),
    })
    renderDialog(onClose)
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/'))
    expect(onClose).toHaveBeenCalledTimes(1)
    expect(onClose.mock.invocationCallOrder[0]).toBeLessThan(
      navigateMock.mock.invocationCallOrder[0],
    )
  })

  it('returns the panel with the same intent and key after failure', async () => {
    const onClose = vi.fn()
    regenerateMock
      .mockReturnValueOnce({ unwrap: () => Promise.reject(new Error('transient')) })
      .mockReturnValue({
        unwrap: () => Promise.resolve({ planId: 'plan-retry', status: 'generated' }),
      })
    renderDialog(onClose)
    const textarea = screen.getByTestId('regenerate-plan-intent')
    await userEvent.type(textarea, 'keep the first week conservative')
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not regenerate/i)
    expect(screen.getByTestId('regenerate-plan-intent')).toHaveValue(
      'keep the first week conservative',
    )
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    const [firstPayload] = regenerateMock.mock.calls[0] as [{ idempotencyKey: string }]
    const [secondPayload] = regenerateMock.mock.calls[1] as [{ idempotencyKey: string }]
    expect(firstPayload).toEqual(expect.objectContaining({ idempotencyKey: expect.any(String) }))
    expect(secondPayload).toEqual(expect.objectContaining({ idempotencyKey: expect.any(String) }))
    expect(secondPayload.idempotencyKey).toBe(firstPayload.idempotencyKey)
  })

  it('keeps the generic alert for a 400 intent-length response', async () => {
    regenerateMock.mockReturnValue({
      unwrap: () => Promise.reject({ status: 400 }),
    })
    renderDialog(vi.fn())
    await userEvent.click(screen.getByTestId('regenerate-plan-submit'))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'We could not regenerate your plan. Please try again in a moment.',
    )
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
