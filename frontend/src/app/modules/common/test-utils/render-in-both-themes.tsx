// Shared dual-theme RTL helper (Slice 2 §5's dual-theme verification
// contract). No prior `test-utils` convention existed in this repo before
// this slice — new component specs across PR-B/C/D reuse this one helper
// rather than each hand-rolling a theme toggle.
//
// `index.css`'s two-tier token layer resolves every semantic Tailwind class
// (`bg-card`, `text-clay-text`, …) through a CSS custom property that
// `:root` (dark, default) and `.light` (override) both define under the
// SAME name — only the primitive value differs. So rendering a component
// once with the default (dark) `documentElement` class state and once with
// `.light` added, and asserting the same DOM shape and zero raw colour
// literals both times, proves the component is theme-correct BY
// CONSTRUCTION — jsdom's unreliable CSS-custom-property resolution (see the
// repo's `check-contrast` jsdom-noise note) never needs to enter the
// picture; this helper never asserts computed style, only DOM shape and
// literal colour text.

import type { ReactElement } from 'react'
import { render, type RenderResult } from '@testing-library/react'

/** The two renders produced by {@link renderInBothThemes}. */
export interface ThemeRenderResult {
  /** Rendered with `documentElement` in its default (dark) class state. */
  dark: RenderResult
  /** Rendered with `.light` added to `documentElement`. */
  light: RenderResult
}

/**
 * Renders `ui` twice — once under the default (dark) theme, once with
 * `.light` added to `document.documentElement` — and restores
 * `documentElement`'s original class state before returning, so this helper
 * never leaks theme state across tests. Both renders remain mounted (RTL's
 * own per-test `afterEach(cleanup)` unmounts them at the end of the current
 * test), so callers can assert against `dark`/`light` independently — e.g.
 * comparing testid sets for structural parity, or scanning
 * `container.innerHTML` for raw colour literals in each mode.
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
  root.classList.remove('light')
  const dark = render(ui, { container: darkContainer, baseElement: darkContainer })

  const lightContainer = document.body.appendChild(document.createElement('div'))
  root.classList.add('light')
  const light = render(ui, { container: lightContainer, baseElement: lightContainer })

  root.className = originalClassName

  return { dark, light }
}
