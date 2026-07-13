import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatBand, StatCell } from './stat-band.component'

describe('StatBand + StatCell', () => {
  it('renders one cell per child with its value and label', () => {
    render(
      <StatBand>
        <StatCell value="9.2 KM" label="Distance" />
        <StatCell value="4:00–4:30/km" label="Pace" />
        <StatCell value="5" label="Reps" />
      </StatBand>,
    )
    const cells = screen.getAllByTestId('stat-cell')
    expect(cells).toHaveLength(3)
    expect(cells[0]).toHaveTextContent('9.2 KM')
    expect(cells[0]).toHaveTextContent('Distance')
    expect(cells[1]).toHaveTextContent('4:00–4:30/km')
  })

  it('separates cells with hairline dividers', () => {
    render(
      <StatBand>
        <StatCell value="9.2 KM" label="Distance" />
        <StatCell value="41:00" label="Time" />
      </StatBand>,
    )
    expect(screen.getByTestId('stat-band')).toHaveClass('divide-x', 'divide-border')
  })

  it('gives the numeral a condensed style so a pace range does not wrap mid-value', () => {
    render(<StatCell value="4:00–4:30/km" label="Pace" />)
    const cell = screen.getByTestId('stat-cell')
    expect(cell.querySelector('.t-numeral')).toHaveTextContent('4:00–4:30/km')
  })

  describe('variant="hero"', () => {
    it('renders the value at condensed 700/30px, not the default .t-numeral role', () => {
      render(<StatCell variant="hero" value="9.0" label="Kilometers" />)
      const cell = screen.getByTestId('stat-cell')
      expect(cell.querySelector('.t-numeral')).toBeNull()
      const value = cell.firstElementChild
      expect(value).toHaveClass('font-condensed', 'text-[30px]', 'font-bold')
      expect(value).toHaveTextContent('9.0')
    })

    it('renders the label mono 500/9.5px/+0.1em/uppercase via text-muted-foreground, not the default .t-data-label role and not the decorative --alp-faint token (builder decision, FIX 3: a unit label is essential text)', () => {
      render(<StatCell variant="hero" value="9.0" label="Kilometers" />)
      const cell = screen.getByTestId('stat-cell')
      expect(cell.querySelector('.t-data-label')).toBeNull()
      const label = cell.lastElementChild
      expect(label).toHaveClass(
        'font-mono',
        'text-[9.5px]',
        'font-medium',
        'uppercase',
        'tracking-[0.1em]',
      )
      expect(label).toHaveClass('text-muted-foreground')
      expect(label).not.toHaveClass('text-[color:var(--alp-faint)]')
      expect(label).toHaveTextContent('Kilometers')
    })

    it("leaves the default variant byte-identical to today's .t-numeral/.t-data-label styling", () => {
      render(<StatCell value="9.0" label="Kilometers" />)
      const cell = screen.getByTestId('stat-cell')
      expect(cell.querySelector('.t-numeral')).not.toBeNull()
      expect(cell.querySelector('.t-data-label')).not.toBeNull()
    })
  })
})
