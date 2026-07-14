import { describe, expect, it } from 'vitest'
import { renderInBothThemes } from './render-in-both-themes'

/** Reads `documentElement`'s theme class at render time — a live probe, not a snapshot. */
const ThemeProbe = () => (
  <span data-testid="theme-probe">
    {document.documentElement.classList.contains('light') ? 'light' : 'dark'}
  </span>
)

describe('renderInBothThemes', () => {
  it('renders once with the default (dark) class state and once with .light added', () => {
    const { dark, light } = renderInBothThemes(<ThemeProbe />)
    expect(dark.getByTestId('theme-probe')).toHaveTextContent('dark')
    expect(light.getByTestId('theme-probe')).toHaveTextContent('light')
  })

  it('both renders remain mounted and independently queryable afterward', () => {
    const { dark, light } = renderInBothThemes(<div data-testid="probe">hi</div>)
    expect(dark.getByTestId('probe')).toBeInTheDocument()
    expect(light.getByTestId('probe')).toBeInTheDocument()
  })

  it('restores documentElement.classList to its prior state afterward (no cross-test leakage)', () => {
    document.documentElement.classList.add('dark')
    const originalClassName = document.documentElement.className

    renderInBothThemes(<ThemeProbe />)

    expect(document.documentElement.className).toBe(originalClassName)
    expect(document.documentElement.classList.contains('light')).toBe(false)

    document.documentElement.classList.remove('dark')
  })

  it('restores a prior .light state rather than always clearing it', () => {
    document.documentElement.classList.add('light')

    renderInBothThemes(<ThemeProbe />)

    expect(document.documentElement.classList.contains('light')).toBe(true)

    document.documentElement.classList.remove('light')
  })
})
