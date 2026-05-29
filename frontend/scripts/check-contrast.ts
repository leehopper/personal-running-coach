#!/usr/bin/env -S npx tsx
/**
 * check-contrast — WCAG 2.x contrast gate for the design-token layer.
 *
 * DEC-070's two-tier token system maps every shadcn semantic slot
 * (--foreground, --primary, …) to a Catppuccin primitive (--ctp-*). This
 * script parses the committed primitive values out of src/index.css,
 * resolves each semantic foreground/background pair through the mapping,
 * computes the WCAG 2.x contrast ratio, and exits non-zero if any pair
 * falls below its threshold:
 *
 *   - text-role pairs        ≥ 4.5:1  (WCAG SC 1.4.3 AA, normal text)
 *   - non-text UI pairs      ≥ 3.0:1  (WCAG SC 1.4.11, UI components)
 *
 * Both light (Latte, :root) and dark (Mocha, .dark) ramps are checked.
 * The semantic-pair matrix below is hand-encoded from index.css; if a new
 * semantic token is added there, add it here too. Parsing the primitive
 * *values* (rather than hard-coding ratios) means a token-value
 * regression — the failure mode this gate exists to catch — is detected.
 */

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import nodeProcess from 'node:process'

const scriptDir = dirname(fileURLToPath(import.meta.url))
const cssPath = resolve(scriptDir, '../src/index.css')

/** sRGB colour in 0-255 channels. */
export interface Rgb {
  r: number
  g: number
  b: number
}

/**
 * A semantic foreground/background pair to assert. `fg` and `bg` are
 * primitive token names (--ctp-*); the values are resolved per mode.
 */
export interface Pair {
  /** Human-readable label for failure messages, e.g. "--foreground on --background". */
  label: string
  /** Foreground primitive token name (without leading --). */
  fg: string
  /** Background primitive token name (without leading --). */
  bg: string
  /** 4.5 for text roles, 3.0 for non-text UI roles. */
  threshold: number
}

/**
 * The semantic matrix from src/index.css. Each entry names the primitive
 * (--ctp-*) token pair behind a shadcn semantic foreground/background slot.
 * Mode-invariant: the same primitive names hold for Latte and Mocha — only
 * their *values* differ between modes, so each pair is checked twice (once
 * against the :root table, once against the .dark table).
 */
export const PAIRS: readonly Pair[] = [
  // Text-role pairs — WCAG AA normal text, 4.5:1.
  { label: '--foreground on --background', fg: 'ctp-text', bg: 'ctp-base', threshold: 4.5 },
  { label: '--card-foreground on --card', fg: 'ctp-text', bg: 'ctp-base', threshold: 4.5 },
  { label: '--popover-foreground on --popover', fg: 'ctp-text', bg: 'ctp-base', threshold: 4.5 },
  {
    label: '--secondary-foreground on --secondary',
    fg: 'ctp-text',
    bg: 'ctp-surface0',
    threshold: 4.5,
  },
  { label: '--muted-foreground on --muted', fg: 'ctp-subtext1', bg: 'ctp-mantle', threshold: 4.5 },
  { label: '--accent-foreground on --accent', fg: 'ctp-text', bg: 'ctp-surface0', threshold: 4.5 },
  {
    label: '--primary-foreground on --primary',
    fg: 'ctp-accent-on',
    bg: 'ctp-accent-fill',
    threshold: 4.5,
  },
  {
    label: '--destructive-foreground on --destructive',
    fg: 'ctp-destructive-on',
    bg: 'ctp-red',
    threshold: 4.5,
  },
  {
    label: '--sidebar-foreground on --sidebar',
    fg: 'ctp-text',
    bg: 'ctp-mantle',
    threshold: 4.5,
  },
  {
    label: '--sidebar-accent-foreground on --sidebar-accent',
    fg: 'ctp-text',
    bg: 'ctp-surface0',
    threshold: 4.5,
  },
  {
    label: '--sidebar-primary-foreground on --sidebar-primary',
    fg: 'ctp-accent-on',
    bg: 'ctp-accent-fill',
    threshold: 4.5,
  },
  // Non-text UI pairs — WCAG SC 1.4.11, 3:1. --ring and --input are
  // checked against --background, the surface they border. --border is a
  // decorative divider (1.4.11 exempt) and is intentionally not asserted.
  { label: '--ring on --background', fg: 'ctp-accent-fill', bg: 'ctp-base', threshold: 3.0 },
  { label: '--input on --background', fg: 'ctp-overlay2', bg: 'ctp-base', threshold: 3.0 },
  {
    label: '--sidebar-ring on --sidebar',
    fg: 'ctp-accent-fill',
    bg: 'ctp-mantle',
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
 * Find the body text of the first CSS rule whose `selector` is followed
 * (whitespace only) by an opening brace. Literal scanning, not a regex —
 * a dynamic `new RegExp` from a selector is a ReDoS surface, and
 * index.css mentions the selector text inside `@custom-variant` where it
 * is *not* brace-adjacent. The primitive tier has no nested braces, so
 * the next `}` closes it.
 */
export function findBlockBody(css: string, selector: string): string {
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
    return css.slice(cursor + 1, close)
  }
  throw new Error(`Could not find a "${selector}" block in index.css`)
}

/**
 * Parse one `--prop: value` declaration. The property is the text after
 * the last `--` up to the first following `:`; the value is the
 * remainder. Pure string indexing — no regex, no backtracking. Returns
 * `null` for a segment that is not a custom-property declaration.
 */
export function parseDeclaration(segment: string): readonly [string, string] | null {
  const dashes = segment.lastIndexOf('--')
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
 * Extract the primitive token table for one mode from index.css. `selector`
 * is the CSS rule that opens the primitive block: `:root` for Latte,
 * `.dark` for Mocha. Only the first matching block is read — index.css
 * declares the primitive tier once per mode, before the semantic tier.
 */
export function extractTokens(css: string, selector: string): Map<string, string> {
  const tokens = new Map<string, string>()
  for (const segment of findBlockBody(css, selector).split(';')) {
    const decl = parseDeclaration(segment)
    if (decl) {
      tokens.set(decl[0], decl[1])
    }
  }
  return tokens
}

/** Resolve a primitive token name to an Rgb, erroring if absent. */
export function resolveToken(tokens: Map<string, string>, name: string, mode: string): Rgb {
  const value = tokens.get(name)
  if (value === undefined) {
    throw new Error(`Token --${name} not found in the ${mode} primitive tier`)
  }
  return parseColor(value)
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

function main(): void {
  let css: string
  try {
    css = readFileSync(cssPath, 'utf8')
  } catch {
    nodeProcess.stderr.write(`check-contrast: could not read ${cssPath}\n`)
    nodeProcess.exit(1)
    return
  }

  const source = stripComments(css)
  const lightTokens = extractTokens(source, ':root')
  const darkTokens = extractTokens(source, '.dark')

  // Every pair is mode-invariant: the same primitive matrix runs against
  // both the :root (Latte) and .dark (Mocha) token tables.
  const results = [
    ...checkMode(lightTokens, 'light', PAIRS),
    ...checkMode(darkTokens, 'dark', PAIRS),
  ]

  let failures = 0
  for (const result of results) {
    const status = result.passed ? 'PASS' : 'FAIL'
    const line = `[${status}] ${result.mode.padEnd(5)} ${result.label}: ${result.ratio.toFixed(2)}:1 (need ${result.threshold.toFixed(1)}:1)`
    const stream = result.passed ? nodeProcess.stdout : nodeProcess.stderr
    if (!result.passed) {
      failures++
    }
    stream.write(`${line}\n`)
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
