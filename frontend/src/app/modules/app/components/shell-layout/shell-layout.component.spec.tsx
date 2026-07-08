import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { ShellLayout } from './shell-layout.component'

const renderShellAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<ShellLayout />}>
          <Route path="/" element={<div data-testid="page-content">Home</div>} />
          <Route path="/settings" element={<div data-testid="page-content">Settings</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )

describe('ShellLayout', () => {
  it('renders the routed page content via Outlet', () => {
    renderShellAt('/')
    expect(screen.getByTestId('page-content')).toHaveTextContent('Home')
  })

  it('renders the TabBar alongside the page content', () => {
    renderShellAt('/')
    expect(screen.getByTestId('tab-bar')).toBeInTheDocument()
  })

  it('keeps the TabBar mounted and reflects the routed content on another shell route', () => {
    renderShellAt('/settings')
    expect(screen.getByTestId('tab-bar')).toBeInTheDocument()
    expect(screen.getByTestId('page-content')).toHaveTextContent('Settings')
  })
})
