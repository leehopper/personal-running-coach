import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { BuildingPlanSurface } from './building-plan-surface.component'

describe('BuildingPlanSurface', () => {
  it('renders the BUILDING YOUR PLAN heading', () => {
    render(<BuildingPlanSurface />)
    expect(screen.getByText('BUILDING YOUR PLAN')).toBeInTheDocument()
  })

  it('announces itself as a live status region', () => {
    render(<BuildingPlanSurface />)
    const status = screen.getByRole('status')
    expect(status).toHaveAttribute('aria-live', 'polite')
  })

  it('renders a default mono status line when none is supplied', () => {
    render(<BuildingPlanSurface />)
    expect(screen.getByText('The coach drafts 12 weeks in about 30 seconds.')).toBeInTheDocument()
  })

  it('renders a caller-supplied mono status line', () => {
    render(<BuildingPlanSurface statusLine="Reworking your plan from the log book." />)
    expect(screen.getByText('Reworking your plan from the log book.')).toBeInTheDocument()
    expect(
      screen.queryByText('The coach drafts 12 weeks in about 30 seconds.'),
    ).not.toBeInTheDocument()
  })

  it('hides the indeterminate progress bar from assistive tech', () => {
    render(<BuildingPlanSurface />)
    const status = screen.getByRole('status')
    const decorative = status.querySelector('[aria-hidden="true"]')
    expect(decorative).not.toBeNull()
  })
})
