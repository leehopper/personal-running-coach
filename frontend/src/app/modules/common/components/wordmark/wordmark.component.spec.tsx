import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Wordmark } from './wordmark.component'

describe('Wordmark', () => {
  it('renders the SPLIT text and the trailing slash', () => {
    render(<Wordmark />)
    expect(screen.getByText('SPLIT')).toBeInTheDocument()
    expect(screen.getByText('/')).toBeInTheDocument()
  })

  it('exposes a single accessible name of "Split"', () => {
    render(<Wordmark />)
    expect(screen.getByRole('img', { name: 'Split' })).toBeInTheDocument()
  })

  it('hides the inner text runs from assistive tech so the name is announced once', () => {
    render(<Wordmark />)
    const wordmark = screen.getByRole('img', { name: 'Split' })
    const hiddenRuns = wordmark.querySelectorAll('[aria-hidden="true"]')
    expect(hiddenRuns).toHaveLength(2)
  })

  it('defaults to the header size', () => {
    render(<Wordmark />)
    expect(screen.getByRole('img', { name: 'Split' })).toHaveAttribute('data-size', 'header')
  })

  it('accepts a poster size for the auth screen', () => {
    render(<Wordmark size="poster" />)
    expect(screen.getByRole('img', { name: 'Split' })).toHaveAttribute('data-size', 'poster')
  })
})
