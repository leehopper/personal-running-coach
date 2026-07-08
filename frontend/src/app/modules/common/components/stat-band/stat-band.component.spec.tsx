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
})
