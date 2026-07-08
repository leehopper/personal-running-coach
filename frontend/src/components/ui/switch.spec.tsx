import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Switch } from './switch'

describe('Switch', () => {
  it('renders as an accessible switch, unchecked by default', () => {
    render(<Switch aria-label="Enable dark mode" />)

    const toggle = screen.getByRole('switch', { name: 'Enable dark mode' })
    expect(toggle).toHaveAttribute('aria-checked', 'false')
  })

  it('toggles aria-checked and fires onCheckedChange when clicked (uncontrolled)', async () => {
    const user = userEvent.setup()
    const onCheckedChange = vi.fn()
    render(<Switch aria-label="Enable dark mode" onCheckedChange={onCheckedChange} />)

    const toggle = screen.getByRole('switch', { name: 'Enable dark mode' })
    await user.click(toggle)

    expect(onCheckedChange).toHaveBeenCalledWith(true)
    expect(toggle).toHaveAttribute('aria-checked', 'true')
  })

  it('reflects a controlled checked value', () => {
    render(<Switch aria-label="Enable dark mode" checked onCheckedChange={vi.fn()} />)

    expect(screen.getByRole('switch', { name: 'Enable dark mode' })).toHaveAttribute(
      'aria-checked',
      'true',
    )
  })

  it('does not respond to interaction when disabled', async () => {
    const user = userEvent.setup()
    const onCheckedChange = vi.fn()
    render(<Switch aria-label="Enable dark mode" disabled onCheckedChange={onCheckedChange} />)

    const toggle = screen.getByRole('switch', { name: 'Enable dark mode' })
    await user.click(toggle)

    expect(onCheckedChange).not.toHaveBeenCalled()
    expect(toggle).toBeDisabled()
  })
})
