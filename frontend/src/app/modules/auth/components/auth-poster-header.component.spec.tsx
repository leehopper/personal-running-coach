import { describe, expect, it } from 'vitest'

import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { AuthPosterHeader } from './auth-poster-header.component'

describe('AuthPosterHeader', () => {
  it('renders the Split wordmark name', () => {
    const { getByRole } = renderInBothThemes(<AuthPosterHeader />).dark

    const wordmark = getByRole('img', { name: 'Split' })

    expect(wordmark).toBeInTheDocument()
    expect(wordmark).toHaveAttribute('data-size', 'poster')
  })

  it('renders the poster rule', () => {
    const { getByTestId } = renderInBothThemes(<AuthPosterHeader />).dark
    const rule = getByTestId('auth-poster-header').querySelector('div')

    expect(rule).toHaveClass('h-0.5', 'w-16', 'bg-rule')
    expect(rule).toHaveAttribute('aria-hidden', 'true')
  })

  it('renders the tagline', () => {
    const { getByText } = renderInBothThemes(<AuthPosterHeader />).dark

    expect(getByText('The plan adapts. You do the work.')).toBeInTheDocument()
  })

  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('keeps parity in both themes', () => {
    const result = renderInBothThemes(<AuthPosterHeader />)

    expectDualThemeParity(result, 'auth-poster-header')
  })
})
