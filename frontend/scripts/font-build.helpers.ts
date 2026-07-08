/**
 * Pure, testable data + matchers for the self-hosted-font build wiring in
 * `vite.config.ts`. Extracted here so a co-located vitest spec can guard the
 * two brittle constants — the @fontsource output-filename patterns and the
 * per-family fallback specs — against silent drift (e.g. a future Vite or
 * @fontsource release changing the emitted asset filename scheme), catching
 * it in CI instead of only via manual `dist/` inspection.
 *
 * Nothing here touches the filesystem or fontaine; it is all synchronous
 * string/regex logic and plain records so the spec runs in the node vitest
 * environment with no build step.
 */

/** A self-hosted primary family paired with the system font whose metrics
 * approximate it, used to synthesise a metric-matched `@font-face` fallback. */
export interface FontFallbackSpec {
  /** The `font-family` name of the real self-hosted webfont (e.g. "Barlow"). */
  family: string
  /** A locally-installed system font used as the metric-matched fallback. */
  systemFallback: string
}

/**
 * One entry per self-hosted family. The `systemFallback` names ("Arial" for
 * the sans families, "Courier New" for the monospace one) are deliberate, not
 * arbitrary: the metric-matching step looks each fallback up in fontaine's
 * bundled subset of @capsizecss/metrics, and system-generic tokens like
 * "system-ui" / "ui-monospace" / "Menlo" have no entry there (they'd silently
 * yield no fallback face). Arial (sans) and Courier New (monospace) DO have
 * entries and are universally present via `local()` on every desktop/mobile
 * OS this app targets.
 */
export const FONT_FALLBACK_SPECS: readonly FontFallbackSpec[] = [
  { family: 'Barlow', systemFallback: 'Arial' },
  { family: 'Barlow Condensed', systemFallback: 'Arial' },
  { family: 'IBM Plex Mono', systemFallback: 'Courier New' },
]

/**
 * The exactly-three above-the-fold weights to `<link rel="preload">`: the
 * body weight (Barlow 400), the primary heading/numeral weight (Barlow
 * Condensed 700), and the data-column weight (IBM Plex Mono 400). Keyed by the
 * hashed filename Vite emits for each @fontsource woff2, verified against a
 * real production build: the scheme is
 * `<family>-latin-<weight>-normal-<hash>.woff2` — the content hash is
 * HYPHEN-separated from the descriptive name (not dot-separated just before
 * the extension), so each pattern anchors on `-<hash>.woff2$`.
 */
export const PRELOAD_FONT_PATTERNS: readonly RegExp[] = [
  /barlow-latin-400-normal-[\w-]+\.woff2$/,
  /barlow-condensed-latin-700-normal-[\w-]+\.woff2$/,
  /ibm-plex-mono-latin-400-normal-[\w-]+\.woff2$/,
]

/**
 * Resolves each `PRELOAD_FONT_PATTERNS` entry to the single build-emitted
 * filename it matches, preserving pattern order. Throws (rather than skipping)
 * if any pattern has no match — a missing preload weight is a silent LCP
 * regression, and the @fontsource/Vite filename scheme drifting is exactly the
 * failure this guard exists to surface loudly at build time.
 *
 * @param fileNames the emitted bundle filenames (e.g. `Object.keys(bundle)`)
 * @returns the matched filenames, one per pattern, in `PRELOAD_FONT_PATTERNS` order
 */
export function matchPreloadFiles(fileNames: readonly string[]): string[] {
  const matched = PRELOAD_FONT_PATTERNS.map((pattern) =>
    fileNames.find((fileName) => pattern.test(fileName)),
  )
  const missingPatterns = PRELOAD_FONT_PATTERNS.filter((_, i) => matched[i] === undefined)
  if (missingPatterns.length > 0) {
    throw new Error(
      `preload fonts: expected a hashed build asset matching each of ` +
        `${String(PRELOAD_FONT_PATTERNS.length)} preload font patterns, but ` +
        `${String(missingPatterns.length)} had no match ` +
        `(${missingPatterns.map((pattern) => pattern.source).join(', ')}). The ` +
        `@fontsource output filename scheme may have changed — update ` +
        `PRELOAD_FONT_PATTERNS in scripts/font-build.helpers.ts.`,
    )
  }
  return matched as string[]
}
