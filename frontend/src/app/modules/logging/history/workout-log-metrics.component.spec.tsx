import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { WorkoutLogMetrics } from './workout-log-metrics.component'

describe('WorkoutLogMetrics', () => {
  it('renders a sparse definition list with only the metrics that are present', () => {
    render(<WorkoutLogMetrics metrics={{ hrAvg: 142, rpe: 7 }} />)

    expect(screen.getByText('Avg HR')).toBeInTheDocument()
    expect(screen.getByText('142 bpm')).toBeInTheDocument()
    expect(screen.getByText('RPE')).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()

    // Absent metrics produce no row at all (sparse, DEC-075).
    expect(screen.queryByText('Cadence')).toBeNull()
    expect(screen.queryByText('Power')).toBeNull()
  })

  it('renders free-text metrics (weather/terrain) without a unit suffix', () => {
    render(<WorkoutLogMetrics metrics={{ weather: 'Cold rain', terrain: 'Trail' }} />)
    expect(screen.getByText('Weather')).toBeInTheDocument()
    expect(screen.getByText('Cold rain')).toBeInTheDocument()
    expect(screen.getByText('Terrain')).toBeInTheDocument()
    expect(screen.getByText('Trail')).toBeInTheDocument()
  })

  it('ignores keys that are not canonical metric keys', () => {
    render(<WorkoutLogMetrics metrics={{ hrAvg: 150, bogusKey: 99 }} />)
    expect(screen.getByText('Avg HR')).toBeInTheDocument()
    expect(screen.queryByText('99')).toBeNull()
  })

  it('renders nothing when there are no present metrics', () => {
    const { container } = render(<WorkoutLogMetrics metrics={{ hrAvg: null, rpe: undefined }} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when metrics is null or undefined', () => {
    const { container, rerender } = render(<WorkoutLogMetrics metrics={null} />)
    expect(container).toBeEmptyDOMElement()
    rerender(<WorkoutLogMetrics metrics={undefined} />)
    expect(container).toBeEmptyDOMElement()
  })
})
