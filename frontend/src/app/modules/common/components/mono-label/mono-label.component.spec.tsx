import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MonoLabel } from './mono-label.component'

describe('MonoLabel', () => {
  it('renders its children as a span', () => {
    render(<MonoLabel>You · 14:32</MonoLabel>)
    const label = screen.getByTestId('mono-label')
    expect(label.tagName).toBe('SPAN')
    expect(label).toHaveTextContent('You · 14:32')
  })

  it('defaults to the muted tone', () => {
    render(<MonoLabel>Distance</MonoLabel>)
    expect(screen.getByTestId('mono-label')).toHaveClass('text-muted-foreground')
  })

  it('applies the clay tone', () => {
    render(<MonoLabel tone="clay">Prescribed</MonoLabel>)
    expect(screen.getByTestId('mono-label')).toHaveClass('text-clay-text')
  })

  it('applies the positive tone', () => {
    render(<MonoLabel tone="positive">Completed</MonoLabel>)
    expect(screen.getByTestId('mono-label')).toHaveClass('text-positive')
  })

  it('always carries the mono data-label typography role', () => {
    render(<MonoLabel>Reps</MonoLabel>)
    expect(screen.getByTestId('mono-label')).toHaveClass('t-data-label')
  })
})
