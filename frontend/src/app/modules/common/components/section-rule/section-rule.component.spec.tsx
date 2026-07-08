import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SectionRule } from './section-rule.component'

describe('SectionRule', () => {
  it('renders the label inside an h2 by default', () => {
    render(<SectionRule label="The Week" />)
    const heading = screen.getByRole('heading', { level: 2, name: 'The Week' })
    expect(heading).toBeInTheDocument()
  })

  it('renders the label inside the requested heading level', () => {
    render(<SectionRule label="Appearance" as="h3" />)
    expect(screen.getByRole('heading', { level: 3, name: 'Appearance' })).toBeInTheDocument()
  })

  it('applies the 2px rule border to the opener', () => {
    render(<SectionRule label="The Block" />)
    expect(screen.getByTestId('section-rule')).toHaveClass('border-t-2', 'border-rule')
  })

  it('omits the right slot when no children are passed', () => {
    render(<SectionRule label="Units" />)
    expect(screen.queryByTestId('section-rule-slot')).not.toBeInTheDocument()
  })

  it('renders an optional right slot beside the label', () => {
    render(
      <SectionRule label="The Block">
        <span>10K — OCT 3</span>
      </SectionRule>,
    )
    expect(screen.getByTestId('section-rule-slot')).toHaveTextContent('10K — OCT 3')
  })
})
