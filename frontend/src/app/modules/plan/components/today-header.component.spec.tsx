import { describe, expect, it } from 'vitest'
import {
  expectDualThemeParity,
  renderInBothThemes,
} from '~/modules/common/test-utils/render-in-both-themes'
import { TodayHeader } from './today-header.component'

describe('TodayHeader', () => {
  it('shows the wordmark, a 2px rule, and WEEK N OF M — PHASE in both themes', () => {
    const result = renderInBothThemes(
      <TodayHeader weekNumber={3} totalWeeks={12} phaseLabel="Base" />,
    )

    for (const themed of [result.dark, result.light]) {
      expect(themed.getByRole('img', { name: 'Split' })).toBeInTheDocument()
      expect(themed.getByTestId('today-header').textContent).toMatch(/week 3 of 12.*base/i)
    }

    expectDualThemeParity(result, 'today-header')
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
