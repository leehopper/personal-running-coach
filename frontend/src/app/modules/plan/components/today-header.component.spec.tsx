import { describe, expect, it } from 'vitest'
import { renderInBothThemes } from '~/modules/common/test-utils/render-in-both-themes'
import { TodayHeader } from './today-header.component'

describe('TodayHeader', () => {
  it('shows the wordmark, a 2px rule, and WEEK N OF M — PHASE in both themes', () => {
    const { dark, light } = renderInBothThemes(
      <TodayHeader weekNumber={3} totalWeeks={12} phaseLabel="Base" />,
    )

    for (const result of [dark, light]) {
      expect(result.getByTestId('today-header')).toBeInTheDocument()
      expect(result.getByRole('img', { name: 'Split' })).toBeInTheDocument()
      expect(result.getByTestId('today-header').textContent).toMatch(/week 3 of 12.*base/i)
      // Token-only proof: zero raw colour literals anywhere in the rendered HTML.
      expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
    }

    // Structural parity: the same testid set renders in both modes.
    const testidsIn = (container: HTMLElement) =>
      [...container.querySelectorAll('[data-testid]')]
        .map((el) => el.getAttribute('data-testid'))
        .sort()
    expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
  })

  it('renders WEEK {N} alone, with no "OF … —" suffix, when plan.macro is null', () => {
    const { getByTestId, queryByText } = renderInBothThemes(
      <TodayHeader weekNumber={1} totalWeeks={null} phaseLabel={null} />,
    ).dark
    const header = getByTestId('today-header')
    expect(header.textContent).toMatch(/week 1/i)
    expect(queryByText(/of/i)).toBeNull()
  })

  it('renders the 2px rule as a sibling div (not nested inside SectionRule chrome)', () => {
    const { container } = renderInBothThemes(
      <TodayHeader weekNumber={1} totalWeeks={4} phaseLabel="Build" />,
    ).dark
    expect(container.querySelector('.bg-rule')).toBeInTheDocument()
  })
})
