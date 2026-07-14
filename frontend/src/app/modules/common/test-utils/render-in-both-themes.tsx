// Shared dual-theme RTL helper. No prior `test-utils` convention existed in
// this repo before this file — component specs across the plan/coaching
// modules reuse this one helper rather than each hand-rolling a theme
// toggle.
//
// The app's two-tier token layer resolves every semantic Tailwind class
// (`bg-card`, `text-clay-text`, …) through a CSS custom property that
// `:root` (dark, default) and `.light` (override) both define under the
// SAME name — only the primitive value differs. So rendering a component
// once with the default (dark) `documentElement` class state and once with
// `.light` added, and asserting the same DOM shape and zero raw colour
// literals both times, proves the component is theme-correct BY
// CONSTRUCTION — jsdom's unreliable CSS-custom-property resolution never
// needs to enter the picture; this helper never asserts computed style,
// only DOM shape and literal colour text.

import type { ReactElement } from 'react'
import { render, type RenderResult } from '@testing-library/react'
import { afterEach, expect } from 'vitest'

/** The two renders produced by {@link renderInBothThemes}. */
export interface ThemeRenderResult {
  /** Rendered with `documentElement` in its default (dark) class state. */
  dark: RenderResult
  /** Rendered with `.light` added to `documentElement`. */
  light: RenderResult
}

// Each `renderInBothThemes` call appends two containers directly to
// `document.body` (see below for why). RTL's own per-test
// `afterEach(cleanup)` unmounts the React trees mounted inside them but does
// NOT remove a caller-supplied `container` from the DOM, so without this
// tracking set every dual-theme test would leak two orphaned `div`s into
// `document.body` for the rest of the test run.
const mountedContainers = new Set<HTMLElement>()

afterEach(() => {
  mountedContainers.forEach((container) => container.remove())
  mountedContainers.clear()
})

/**
 * Renders `ui` twice — once under the default (dark) theme, once with
 * `.light` added to `document.documentElement` — and restores
 * `documentElement`'s original class state before returning, so this helper
 * never leaks theme state across tests. Both renders remain mounted for the
 * rest of the current test (cleaned up automatically afterward — see the
 * module-level `afterEach` above), so callers can assert against
 * `dark`/`light` independently — e.g. comparing testid sets for structural
 * parity, or scanning `container.innerHTML` for raw colour literals in each
 * mode.
 *
 * Each render gets its OWN container, and `baseElement` is pinned to that
 * same container — RTL's bound queries (`getByTestId`, …) otherwise default
 * to searching the whole `document.body`, which would see BOTH trees at
 * once and throw "found multiple elements" the instant a shared testid
 * (e.g. the component's own root testid) appears in both renders.
 */
export const renderInBothThemes = (ui: ReactElement): ThemeRenderResult => {
  const root = document.documentElement
  const originalClassName = root.className

  const darkContainer = document.body.appendChild(document.createElement('div'))
  mountedContainers.add(darkContainer)
  root.classList.remove('light')
  const dark = render(ui, { container: darkContainer, baseElement: darkContainer })

  const lightContainer = document.body.appendChild(document.createElement('div'))
  mountedContainers.add(lightContainer)
  root.classList.add('light')
  const light = render(ui, { container: lightContainer, baseElement: lightContainer })

  root.className = originalClassName

  return { dark, light }
}

/** Sorted `data-testid` values under `container` — for structural-parity comparisons between two renders. */
export const testidsIn = (container: HTMLElement): string[] =>
  [...container.querySelectorAll('[data-testid]')]
    .map((el) => el.getAttribute('data-testid'))
    .filter((testid): testid is string => testid !== null)
    .sort((a, b) => a.localeCompare(b))

/**
 * Asserts the standard dual-theme-parity contract for a {@link ThemeRenderResult}:
 * both renders expose `testId`, neither render's HTML contains a raw hex
 * colour literal (every colour must flow through a token, never a
 * hardcoded value), and both renders expose the identical set of
 * `data-testid` values (structural parity — the same DOM shape rendered
 * under each theme).
 */
export const expectDualThemeParity = ({ dark, light }: ThemeRenderResult, testId: string): void => {
  for (const result of [dark, light]) {
    expect(result.getByTestId(testId)).toBeInTheDocument()
    expect(result.container.innerHTML).not.toMatch(/#[0-9a-fA-F]{3,8}\b/)
  }
  expect(testidsIn(dark.container)).toEqual(testidsIn(light.container))
}
