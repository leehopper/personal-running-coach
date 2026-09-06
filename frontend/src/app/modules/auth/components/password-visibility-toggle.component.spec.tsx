import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import userEvent from '@testing-library/user-event'
import { render, screen } from '@testing-library/react'

import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { PasswordVisibilityToggle } from './password-visibility-toggle.component'

const ToggleHarness = () => {
  const [isVisible, setIsVisible] = useState(false)

  return (
    <PasswordVisibilityToggle
      isVisible={isVisible}
      onToggle={() => {
        setIsVisible((current) => !current)
      }}
    />
  )
}

describe('PasswordVisibilityToggle', () => {
  it('shows the password action when hidden', () => {
    render(<PasswordVisibilityToggle isVisible={false} onToggle={() => {}} />)
    const button = screen.getByTestId('password-visibility-toggle')

    expect(button).toHaveAttribute('type', 'button')
    expect(button).toHaveAttribute('aria-label', 'Show password')
    expect(button).toHaveAttribute('aria-pressed', 'false')
    expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  })

  it('flips the pressed state and accessible name', async () => {
    const user = userEvent.setup()
    render(<ToggleHarness />)
    const button = screen.getByTestId('password-visibility-toggle')

    await user.click(button)

    expect(button).toHaveAttribute('aria-label', 'Hide password')
    expect(button).toHaveAttribute('aria-pressed', 'true')
    expect(button.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  })

  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('keeps parity in both themes', () => {
    const result = renderInBothThemes(
      <PasswordVisibilityToggle isVisible={false} onToggle={() => {}} />,
    )

    expectDualThemeParity(result, 'password-visibility-toggle')
  })
})
