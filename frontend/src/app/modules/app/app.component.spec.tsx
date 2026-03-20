import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { App } from './app.component'

describe('App', () => {
  it('renders without crashing', () => {
    render(<App />)
    expect(screen.getByText('RunCoach')).toBeInTheDocument()
  })
})
