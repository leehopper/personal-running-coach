#!/usr/bin/env -S npx tsx
/**
 * check-contrast — WCAG 2.x contrast gate for the design-token layer.
 *
 * DEC-089's two-tier token system maps every shadcn semantic slot
 * (--foreground, --primary, …) to an Alpine primitive (--alp-*). This
 * script parses BOTH tiers out of src/index.css — the primitive tier
 * (raw --alp-* values) and the semantic tier (--foreground: var(--alp-*),
 * …) — resolves each semantic slot through its own var() reference to the
 * per-mode primitive value, computes the WCAG 2.x contrast ratio, and
 * exits non-zero if any pair falls below its threshold:
 *
 *   - text-role pairs        ≥ 4.5:1  (WCAG SC 1.4.3 AA, normal text)
 *   - non-text UI pairs      ≥ 3.0:1  (WCAG SC 1.4.11, UI components)
 *
 * Dark is the DEFAULT mode (`:root`) and light is the override (`.light`);
 * both ramps are checked and labelled by mode ('dark' for :root, 'light'
 * for .light — see runChecks). The PAIRS matrix below names the SEMANTIC
 * slots to assert; following the committed var() mapping (rather than
 * hard-coding the primitive) means both failure modes are caught: a
 * primitive-value regression AND a re-pointed semantic mapping (e.g.
 * --muted-foreground swapped to a lower-contrast primitive). If a new
 * semantic pair must be guarded, add its slot names here.
 *
 * EXEMPT (never asserted, by design — WCAG 1.4.11 decorative / non-text):
 *   --border      — a pure divider between rows/sections.
 *   --warning     — a supplementary severity accent (the severity is always
 *     also conveyed by content and structure, never by colour alone).
 *   --alp-faint   — a decorative-only label tint that fails AA by design and
 *     must never carry essential text; documented, not machine-checked.
 *   --clay-marker — the Slice 2 "current" marker (AD-9): border/fill only,
 *     never text (THE WEEK's today-cell outline, THE BLOCK's current-week
 *     cell, the coach digest's accent-indent border). Text usages of clay
 *     use --clay-text instead, which IS gated above. Same exemption posture
 *     as --border.
 *   --surface-dim — the Slice 2 THE BLOCK "distant" (far-future week) cell
 *     fill (AD-10): fill-only, never text — the phase-span label row sits
 *     below the cell grid, never layered over a cell's fill. Same
 *     exemption posture as --border/--clay-marker.
 *
 * NOT exempt — --input IS asserted (3:1 UI-component rule): it is a
 *   form-control boundary and the only cue that an empty resting field
 *   exists (the field fill --alp-input is a near-invisible ~1:1 against the
 *   page bg by design), which is exactly WCAG 1.4.11's in-scope case. It is
 *   backed by a dedicated --alp-input-border primitive (distinct from the
 *   fainter --alp-hairline divider tone), tuned to just clear 3:1 vs the
 *   page background in each mode.
 *
 * ALSO asserted — two text-on-fill pairs beyond the shadcn defaults:
 *   --clay-pressed  — the primary button's / segmented-control's pressed
 *     fill. Both consumers render --primary-foreground text over it while
 *     pressed, so it is held to the same 4.5:1 as --primary, not treated as
 *     a decorative press state.
 *   --input-fill    — the sunken resting fill of form controls (distinct
 *     from --input, the border). Value and placeholder copy render
 *     directly on it, so both --foreground and --muted-foreground are
 *     checked against it at 4.5:1.
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import nodeProcess from 'node:process'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const DEFAULT_CSS_PATH = resolve(scriptDir, '../src/index.css')

/** sRGB colour in 0-255 channels. */
export interface Rgb {
  r: number
  g: number
  b: number
}

/** The only two legal WCAG 2.x thresholds: AA normal text and non-text UI. */
export type WcagThreshold = 4.5 | 3

/**
 * A semantic foreground/background pair to assert. `fg` and `bg` are
 * SEMANTIC slot names (e.g. `muted-foreground`, `muted`); each is resolved
 * through its committed `var(--alp-*)` mapping to a primitive value per mode.
 */
export interface Pair {
  /** Human-readable label for failure messages, e.g. "--foreground on --background". */
  label: string
  /** Foreground semantic slot name (without leading --). */
  fg: string
  /** Background semantic slot name (without leading --). */
  bg: string
  /** 4.5 for text roles, 3.0 for non-text UI roles. */
  threshold: WcagThreshold
}

/**
 * The semantic pairs to assert, named by their shadcn SEMANTIC slot
 * (`--foreground`, `--muted`, …). Each slot is resolved through its
 * committed `var(--alp-*)` mapping in index.css to a primitive value, per
 * mode — so a re-pointed mapping is followed, not silently ignored. The
 * mappings are mode-invariant; only the primitive values differ between
 * dark (:root, default) and light (.light, override), so each pair is
 * checked against both.
 */
export const PAIRS: readonly Pair[] = [
  // Text-role pairs — WCAG AA normal text, 4.5:1.
  { label: '--foreground on --background', fg: 'foreground', bg: 'background', threshold: 4.5 },
  { label: '--card-foreground on --card', fg: 'card-foreground', bg: 'card', threshold: 4.5 },
  {
    label: '--popover-foreground on --popover',
    fg: 'popover-foreground',
    bg: 'popover',
    threshold: 4.5,
  },
  {
    label: '--secondary-foreground on --secondary',
    fg: 'secondary-foreground',
    bg: 'secondary',
    threshold: 4.5,
  },
  { label: '--muted-foreground on --muted', fg: 'muted-foreground', bg: 'muted', threshold: 4.5 },
  {
    label: '--accent-foreground on --accent',
    fg: 'accent-foreground',
    bg: 'accent',
    threshold: 4.5,
  },
  {
    label: '--primary-foreground on --primary',
    fg: 'primary-foreground',
    bg: 'primary',
    threshold: 4.5,
  },
  {
    label: '--destructive-foreground on --destructive',
    fg: 'destructive-foreground',
    bg: 'destructive',
    threshold: 4.5,
  },
  {
    label: '--sidebar-foreground on --sidebar',
    fg: 'sidebar-foreground',
    bg: 'sidebar',
    threshold: 4.5,
  },
  {
    label: '--sidebar-accent-foreground on --sidebar-accent',
    fg: 'sidebar-accent-foreground',
    bg: 'sidebar-accent',
    threshold: 4.5,
  },
  {
    label: '--sidebar-primary-foreground on --sidebar-primary',
    fg: 'sidebar-primary-foreground',
    bg: 'sidebar-primary',
    threshold: 4.5,
  },
  // Net-new Alpine project slots — WCAG AA normal text, 4.5:1.
  { label: '--positive on --card', fg: 'positive', bg: 'card', threshold: 4.5 },
  { label: '--clay-text on --background', fg: 'clay-text', bg: 'background', threshold: 4.5 },
  // Pressed/active clay fill — a text-on-fill pair (not decorative): the
  // primary button and segmented-control render on-clay text over this fill
  // while pressed, so it is held to the same 4.5:1 as --primary above.
  {
    label: '--primary-foreground on --clay-pressed',
    fg: 'primary-foreground',
    bg: 'clay-pressed',
    threshold: 4.5,
  },
  // Sunken form-control fill — text/value and placeholder legibility.
  // Distinct from the --input border pair below (3:1, boundary
  // perceptibility): this asserts the copy rendered ON the fill.
  { label: '--foreground on --input-fill', fg: 'foreground', bg: 'input-fill', threshold: 4.5 },
  {
    label: '--muted-foreground on --input-fill',
    fg: 'muted-foreground',
    bg: 'input-fill',
    threshold: 4.5,
  },
  // Non-text UI pairs — WCAG SC 1.4.11, 3:1. --ring and --input are checked
  // against --background, the surface they border. --input is the resting
  // boundary of an empty form field (see the header comment) and must stay
  // perceptible. --border is a pure decorative divider and is not asserted.
  { label: '--ring on --background', fg: 'ring', bg: 'background', threshold: 3.0 },
  { label: '--input on --background', fg: 'input', bg: 'background', threshold: 3.0 },
  {
    label: '--sidebar-ring on --sidebar',
    fg: 'sidebar-ring',
    bg: 'sidebar',
    threshold: 3.0,
  },
]

/** Parse `#rrggbb` (3- or 6-digit) into an Rgb. */
export function parseHex(hex: string): Rgb {
  const h = hex.replace('#', '')
  // Length must be 3 or 6 and every character a hex digit. The explicit
  // character test matters because `parseInt` stops at the first invalid
  // digit (`parseInt('1z', 16) === 1`), so a partially-invalid string like
  // `#1z3456` would otherwise parse to a silently wrong colour.
  if ((h.length !== 3 && h.length !== 6) || !/^[0-9a-fA-F]+$/.test(h)) {
    throw new Error(`Unsupported hex colour format: ${hex}`)
  }
  const full =
    h.length === 3
      ? h
          .split('')
          .map((c) => c + c)
          .join('')
      : h
  return {
    r: parseInt(full.slice(0, 2), 16),
    g: parseInt(full.slice(2, 4), 16),
    b: parseInt(full.slice(4, 6), 16),
  }
}

/**
 * Convert an `oklch(L C H)` colour to sRGB 0-255.
 * Pipeline: OKLCH → OKLab → linear sRGB → gamma-encoded sRGB.
 * Matrices per the CSS Color 4 spec / Björn Ottosson's OKLab reference.
 */
export function parseOklch(value: string): Rgb {
  const match = /oklch\(\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\)/i.exec(value)
  if (!match) {
    throw new Error(`Unrecognised oklch() value: ${value}`)
  }
  const lightness = parseFloat(match[1])
  const chroma = parseFloat(match[2])
  const hueDeg = parseFloat(match[3])
  const hueRad = (hueDeg * Math.PI) / 180
  const aLab = chroma * Math.cos(hueRad)
  const bLab = chroma * Math.sin(hueRad)

  // OKLab → LMS' (cube roots) → LMS.
  const lp = lightness + 0.3963377774 * aLab + 0.2158037573 * bLab
  const mp = lightness - 0.1055613458 * aLab - 0.0638541728 * bLab
  const sp = lightness - 0.0894841775 * aLab - 1.291485548 * bLab
  const l = lp * lp * lp
  const m = mp * mp * mp
  const s = sp * sp * sp

  // LMS → linear sRGB.
  const rLin = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s
  const gLin = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s
  const bLin = -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s

  // Linear → gamma-encoded sRGB, clamped to gamut.
  const encode = (c: number): number => {
    const clamped = Math.min(Math.max(c, 0), 1)
    const v = clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055
    return Math.round(v * 255)
  }
  return { r: encode(rLin), g: encode(gLin), b: encode(bLin) }
}

/** Parse a committed CSS colour token value (hex or oklch) into Rgb. */
export function parseColor(value: string): Rgb {
  const trimmed = value.trim()
  if (trimmed.startsWith('#')) {
    return parseHex(trimmed)
  }
  if (trimmed.toLowerCase().startsWith('oklch')) {
    return parseOklch(trimmed)
  }
  throw new Error(`Unsupported colour format: ${value}`)
}

/** WCAG relative luminance of an sRGB colour (WCAG 2.x SC 1.4.3). */
export function relativeLuminance({ r, g, b }: Rgb): number {
  const channel = (v: number): number => {
    const srgb = v / 255
    return srgb <= 0.04045 ? srgb / 12.92 : Math.pow((srgb + 0.055) / 1.055, 2.4)
  }
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)
}

/** WCAG contrast ratio between two colours: (L1 + 0.05) / (L2 + 0.05). */
export function contrastRatio(a: Rgb, b: Rgb): number {
  const la = relativeLuminance(a)
  const lb = relativeLuminance(b)
  const lighter = Math.max(la, lb)
  const darker = Math.min(la, lb)
  return (lighter + 0.05) / (darker + 0.05)
}

/**
 * Strip CSS block comments. index.css comments mention selector names and
 * contain `:` and `;` characters that would otherwise confuse the literal
 * block- and declaration-scanning below.
 */
export function stripComments(css: string): string {
  let out = ''
  let cursor = 0
  for (;;) {
    const start = css.indexOf('/*', cursor)
    if (start === -1) {
      return out + css.slice(cursor)
    }
    out += css.slice(cursor, start)
    const end = css.indexOf('*/', start + 2)
    if (end === -1) {
      return out
    }
    cursor = end + 2
  }
}

/**
 * Find the body text of EVERY CSS rule whose `selector` is followed
 * (whitespace only) by an opening brace. Literal scanning, not a regex —
 * a dynamic `new RegExp` from a selector is a ReDoS surface, and
 * index.css mentions the selector text inside `@custom-variant` where it
 * is *not* brace-adjacent (so it is skipped). index.css declares each
 * selector twice — a primitive block and a semantic block — and both are
 * returned; neither tier has nested braces, so the next `}` closes it.
 */
export function findBlockBodies(css: string, selector: string): string[] {
  const bodies: string[] = []
  for (let at = css.indexOf(selector); at !== -1; at = css.indexOf(selector, at + 1)) {
    let cursor = at + selector.length
    while (cursor < css.length && /\s/.test(css[cursor])) {
      cursor++
    }
    if (css[cursor] !== '{') {
      continue
    }
    const close = css.indexOf('}', cursor)
    if (close === -1) {
      break
    }
    bodies.push(css.slice(cursor + 1, close))
  }
  return bodies
}

/** Find the first brace-adjacent block body for `selector`, erroring if none. */
export function findBlockBody(css: string, selector: string): string {
  const [first] = findBlockBodies(css, selector)
  if (first === undefined) {
    throw new Error(`Could not find a "${selector}" block in index.css`)
  }
  return first
}

/**
 * Parse one `--prop: value` declaration. The property is the text after
 * the FIRST `--` up to the first following `:`; the value is the
 * remainder. The first `--` (not the last) is the property name: a
 * semantic value like `var(--alp-bone)` contains its own `--`, and
 * `lastIndexOf` would mistake that for the property. Pure string indexing —
 * no regex, no backtracking. Returns `null` for a segment that is not a
 * custom-property declaration.
 */
export function parseDeclaration(segment: string): readonly [string, string] | null {
  const dashes = segment.indexOf('--')
  if (dashes === -1) {
    return null
  }
  const colon = segment.indexOf(':', dashes)
  if (colon === -1) {
    return null
  }
  const name = segment.slice(dashes + 2, colon).trim()
  const value = segment.slice(colon + 1).trim()
  return name.length > 0 && value.length > 0 ? [name, value] : null
}

/**
 * Build the custom-property table for one mode by merging EVERY matching
 * block. `selector` is `:root` (dark, default) or `.light` (light,
 * override). index.css declares both a primitive block (`--alp-*: #hex`)
 * and a semantic block (`--foreground: var(--alp-bone)`) for each; merging
 * them lets a semantic slot be resolved to its primitive value via var()
 * indirection, so a re-pointed mapping is reflected rather than silently
 * ignored.
 */
export function extractTokens(css: string, selector: string): Map<string, string> {
  const bodies = findBlockBodies(css, selector)
  if (bodies.length === 0) {
    throw new Error(`Could not find a "${selector}" block in index.css`)
  }
  const tokens = new Map<string, string>()
  for (const body of bodies) {
    for (const segment of body.split(';')) {
      const decl = parseDeclaration(segment)
      if (decl) {
        const [name, value] = decl
        tokens.set(name, value)
      }
    }
  }
  return tokens
}

/**
 * Resolve a custom-property name to an Rgb for `mode`, following CSS
 * `var(--other)` indirection (the semantic tier points at primitive
 * tokens). Errors on a missing token or a var() reference cycle.
 */
export function resolveToken(tokens: Map<string, string>, name: string, mode: string): Rgb {
  const seen = new Set<string>()
  let current = name
  for (;;) {
    const value = tokens.get(current)
    if (value === undefined) {
      throw new Error(`Token --${current} not found in the ${mode} tier`)
    }
    const ref = /^var\(\s*--([\w-]+)\s*\)$/.exec(value.trim())
    if (!ref) {
      return parseColor(value)
    }
    if (seen.has(current)) {
      throw new Error(`Cyclic var() reference resolving --${name} in the ${mode} tier`)
    }
    seen.add(current)
    current = ref[1]
  }
}

export interface Result {
  mode: string
  label: string
  ratio: number
  threshold: number
  passed: boolean
}

/** Check every pair for one mode and return the results. */
export function checkMode(
  tokens: Map<string, string>,
  mode: string,
  pairs: readonly Pair[],
): Result[] {
  return pairs.map((pair) => {
    const fg = resolveToken(tokens, pair.fg, mode)
    const bg = resolveToken(tokens, pair.bg, mode)
    const ratio = contrastRatio(fg, bg)
    return {
      mode,
      label: pair.label,
      ratio,
      threshold: pair.threshold,
      passed: ratio >= pair.threshold,
    }
  })
}

/**
 * Run the gate against CSS source text: strip comments, extract the merged
 * token table for each mode, and check every pair against both. Pure — no
 * I/O and no `exit`, so it is unit-testable.
 */
export function runChecks(css: string): Result[] {
  const source = stripComments(css)
  // Each pair is checked against both the :root (dark, default) and .light
  // (light, override) token tables; the semantic mappings are
  // mode-invariant, the primitive values they resolve to are not.
  return [
    ...checkMode(extractTokens(source, ':root'), 'dark', PAIRS),
    ...checkMode(extractTokens(source, '.light'), 'light', PAIRS),
  ]
}

/** Render one result as a `[PASS|FAIL] mode label: ratio (need threshold)` line. */
function formatResult(result: Result): string {
  const status = result.passed ? 'PASS' : 'FAIL'
  return `[${status}] ${result.mode.padEnd(5)} ${result.label}: ${result.ratio.toFixed(2)}:1 (need ${result.threshold.toFixed(1)}:1)`
}

function main(): void {
  // Optional argv[2] overrides the token file (used by integration tests);
  // CI, Lefthook, and `npm run check-contrast` pass no argument.
  const cssPath =
    nodeProcess.argv[2] !== undefined ? resolve(nodeProcess.argv[2]) : DEFAULT_CSS_PATH
  let css: string
  try {
    css = readFileSync(cssPath, 'utf8')
  } catch {
    nodeProcess.stderr.write(`check-contrast: could not read ${cssPath}\n`)
    nodeProcess.exit(1)
  }

  const results = runChecks(css)

  let failures = 0
  for (const result of results) {
    const stream = result.passed ? nodeProcess.stdout : nodeProcess.stderr
    if (!result.passed) {
      failures++
    }
    stream.write(`${formatResult(result)}\n`)
  }

  if (failures > 0) {
    nodeProcess.stderr.write(`\ncheck-contrast: ${failures} pair(s) below threshold.\n`)
    nodeProcess.exit(1)
  }
  nodeProcess.stdout.write(`\ncheck-contrast: all ${results.length} pairs pass WCAG thresholds.\n`)
}

// Run the gate only on direct invocation (`tsx scripts/check-contrast.ts`),
// not when the module is imported by a unit test.
if (nodeProcess.argv[1] === fileURLToPath(import.meta.url)) {
  main()
}
