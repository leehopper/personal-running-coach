import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { App } from './app.component'

describe('App', () => {
  it('mounts and renders the RequireAuth loading fallback on first render', () => {
    render(<App />)
    // Initial auth slice status === 'unknown' → RequireAuth shows the
    // loading fallback. Full routing coverage lives in T03.3.
    expect(screen.getByRole('status')).toBeInTheDocument()
  })
})
