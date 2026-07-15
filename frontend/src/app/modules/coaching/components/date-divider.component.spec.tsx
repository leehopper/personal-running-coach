import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { DateDivider } from './date-divider.component'

describe('DateDivider', () => {
  it('renders the label flanked by two hairlines', () => {
    render(<DateDivider label="TUE JUN 30" />)

    const root = screen.getByTestId('date-divider')
    expect(screen.getByText('TUE JUN 30')).toBeInTheDocument()
    expect(screen.getByText('TUE JUN 30')).toHaveClass('text-[var(--alp-faint)]')
    const hairlines = root.querySelectorAll('.bg-border')
    expect(hairlines).toHaveLength(2)
  })

  it('renders a TODAY-prefixed label verbatim', () => {
    render(<DateDivider label="TODAY — WED JUL 8" />)
    expect(screen.getByText('TODAY — WED JUL 8')).toBeInTheDocument()
  })

  // The assertions live inside the shared expectDualThemeParity helper —
  // sonarjs's static check can't see through the function call.
  // eslint-disable-next-line sonarjs/assertions-in-tests
  it('holds dual-theme structural parity with no raw hex colour literals', () => {
    const result = renderInBothThemes(<DateDivider label="TUE JUN 30" />)
    expectDualThemeParity(result, 'date-divider')
  })
})
