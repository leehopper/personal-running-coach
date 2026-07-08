import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { TabBar } from './tab-bar.component'

// `NavLink` needs router context; wrap in a minimal route table so
// `useLocation`-driven active-state matching behaves exactly as it would
// nested under `ShellLayout` in the real app.
const renderTabBarAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="*" element={<TabBar />} />
      </Routes>
    </MemoryRouter>,
  )

describe('TabBar', () => {
  it('renders a Primary nav landmark', () => {
    renderTabBarAt('/')
    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument()
  })

  it('renders five interactive targets with accessible names', () => {
    renderTabBarAt('/')
    expect(screen.getByRole('link', { name: /today/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /coach/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Log a workout' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /log book/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /settings/i })).toBeInTheDocument()
  })

  it('marks TODAY as the current page when on /', () => {
    renderTabBarAt('/')
    expect(screen.getByTestId('tab-today')).toHaveAttribute('aria-current', 'page')
    expect(screen.getByTestId('tab-settings')).not.toHaveAttribute('aria-current')
  })

  it('marks SETTINGS as the current page when on /settings', () => {
    renderTabBarAt('/settings')
    expect(screen.getByTestId('tab-settings')).toHaveAttribute('aria-current', 'page')
    expect(screen.getByTestId('tab-today')).not.toHaveAttribute('aria-current')
  })

  it('marks COACH as the current page when on /coach', () => {
    renderTabBarAt('/coach')
    expect(screen.getByTestId('tab-coach')).toHaveAttribute('aria-current', 'page')
  })

  it('marks LOG BOOK as the current page when on /history', () => {
    renderTabBarAt('/history')
    expect(screen.getByTestId('tab-history')).toHaveAttribute('aria-current', 'page')
  })

  it('does not mark TODAY current on unrelated shell routes (exact match on "/")', () => {
    renderTabBarAt('/history')
    expect(screen.getByTestId('tab-today')).not.toHaveAttribute('aria-current')
  })

  it('exposes the expected new data-testids', () => {
    renderTabBarAt('/')
    expect(screen.getByTestId('tab-bar')).toBeInTheDocument()
    expect(screen.getByTestId('tab-today')).toBeInTheDocument()
    expect(screen.getByTestId('tab-coach')).toBeInTheDocument()
    expect(screen.getByTestId('tab-log')).toBeInTheDocument()
    expect(screen.getByTestId('tab-history')).toBeInTheDocument()
    expect(screen.getByTestId('tab-settings')).toBeInTheDocument()
  })

  it('points each target at its route', () => {
    renderTabBarAt('/')
    expect(screen.getByTestId('tab-today')).toHaveAttribute('href', '/')
    expect(screen.getByTestId('tab-coach')).toHaveAttribute('href', '/coach')
    expect(screen.getByTestId('tab-log')).toHaveAttribute('href', '/log')
    expect(screen.getByTestId('tab-history')).toHaveAttribute('href', '/history')
    expect(screen.getByTestId('tab-settings')).toHaveAttribute('href', '/settings')
  })
})
