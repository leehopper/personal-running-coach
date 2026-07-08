// @vitest-environment node
import { execFileSync } from 'node:child_process'
import { readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, resolve } from 'node:path'
import process from 'node:process'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  PAIRS,
  checkMode,
  contrastRatio,
  extractTokens,
  findBlockBodies,
  findBlockBody,
  parseColor,
  parseDeclaration,
  parseHex,
  parseOklch,
  relativeLuminance,
  resolveToken,
  runChecks,
  stripComments,
  type Pair,
} from '../check-contrast'

const WHITE = { r: 255, g: 255, b: 255 }
const BLACK = { r: 0, g: 0, b: 0 }

const here = dirname(fileURLToPath(import.meta.url))
const realIndexCssPath = resolve(here, '../../src/index.css')

describe('parseHex', () => {
  it('parses a 6-digit #rrggbb into channels', () => {
    expect(parseHex('#eff1f5')).toEqual({ r: 0xef, g: 0xf1, b: 0xf5 })
  })

  it('parses 6-digit hex without a leading hash', () => {
    expect(parseHex('11111b')).toEqual({ r: 0x11, g: 0x11, b: 0x1b })
  })

  it('expands a 3-digit #rgb shorthand to full channels', () => {
    expect(parseHex('#abc')).toEqual({ r: 0xaa, g: 0xbb, b: 0xcc })
  })

  it('treats #fff as opaque white', () => {
    expect(parseHex('#fff')).toEqual(WHITE)
  })

  it('throws on a 4-digit hex length', () => {
    expect(() => parseHex('#abcd')).toThrow(/Unsupported hex colour format/)
  })

  it('throws on a 5-digit hex length', () => {
    expect(() => parseHex('#abcde')).toThrow(/Unsupported hex colour format/)
  })

  it('throws on non-hex characters via the hex-digit regex', () => {
    expect(() => parseHex('#gggggg')).toThrow(/Unsupported hex colour format/)
  })

  it('throws when only some channels are non-hex', () => {
    expect(() => parseHex('#12zz56')).toThrow(/Unsupported hex colour format/)
  })
})

describe('relativeLuminance', () => {
  it('is 1 for white', () => {
    expect(relativeLuminance(WHITE)).toBeCloseTo(1, 10)
  })

  it('is 0 for black', () => {
    expect(relativeLuminance(BLACK)).toBeCloseTo(0, 10)
  })

  it('is strictly between black and white for a mid grey', () => {
    const grey = relativeLuminance({ r: 0x76, g: 0x76, b: 0x76 })
    expect(grey).toBeGreaterThan(0)
    expect(grey).toBeLessThan(1)
  })
})

describe('contrastRatio', () => {
  it('is 21:1 for white against black', () => {
    expect(contrastRatio(WHITE, BLACK)).toBeCloseTo(21, 5)
  })

  it('is 1:1 for a colour against itself', () => {
    expect(contrastRatio(WHITE, WHITE)).toBeCloseTo(1, 10)
  })

  it('is symmetric: ratio(a, b) === ratio(b, a)', () => {
    const a = { r: 0x76, g: 0x76, b: 0x76 }
    const b = WHITE
    expect(contrastRatio(a, b)).toBeCloseTo(contrastRatio(b, a), 10)
  })

  it('clears AA (4.5:1) for the canonical #767676 grey on white', () => {
    // #767676 on #ffffff is the WCAG reference "minimum AA grey": ~4.54:1.
    const ratio = contrastRatio({ r: 0x76, g: 0x76, b: 0x76 }, WHITE)
    expect(ratio).toBeGreaterThanOrEqual(4.5)
    expect(ratio).toBeCloseTo(4.54, 1)
  })
})

describe('parseOklch', () => {
  it('maps achromatic oklch(1 0 0) to white', () => {
    expect(parseOklch('oklch(1 0 0)')).toEqual(WHITE)
  })

  it('maps achromatic oklch(0 0 0) to black', () => {
    expect(parseOklch('oklch(0 0 0)')).toEqual(BLACK)
  })

  it('accepts decimal lightness/chroma/hue components', () => {
    const rgb = parseOklch('oklch(0.7 0.15 250)')
    for (const channel of [rgb.r, rgb.g, rgb.b]) {
      expect(channel).toBeGreaterThanOrEqual(0)
      expect(channel).toBeLessThanOrEqual(255)
      expect(Number.isInteger(channel)).toBe(true)
    }
  })

  it('clamps an out-of-sRGB-gamut colour to the [0,255] boundary', () => {
    // oklch(0.7 0.35 30) is well outside the sRGB gamut: the red linear
    // channel overshoots 1 while green and blue go negative. Without the
    // [0,1] gamut clamp in encode() those would round to out-of-range
    // values (r≈318, g≈-303); the clamp pins them to 255 / 0. Asserting
    // both boundaries proves the clamp fires in both directions — remove
    // it and this test fails.
    const rgb = parseOklch('oklch(0.7 0.35 30)')
    for (const channel of [rgb.r, rgb.g, rgb.b]) {
      expect(channel).toBeGreaterThanOrEqual(0)
      expect(channel).toBeLessThanOrEqual(255)
    }
    expect(Math.max(rgb.r, rgb.g, rgb.b)).toBe(255)
    expect(Math.min(rgb.r, rgb.g, rgb.b)).toBe(0)
  })

  it('throws on malformed oklch() input', () => {
    expect(() => parseOklch('oklch(not a colour)')).toThrow(/Unrecognised oklch/)
  })

  it('throws when the function name is missing', () => {
    expect(() => parseOklch('1 0 0')).toThrow(/Unrecognised oklch/)
  })
})

describe('parseColor', () => {
  it('dispatches a hex string to the hex parser', () => {
    expect(parseColor('  #ffffff  ')).toEqual(WHITE)
  })

  it('dispatches an oklch string to the oklch parser', () => {
    expect(parseColor('oklch(0 0 0)')).toEqual(BLACK)
  })

  it('throws on an unsupported colour format', () => {
    expect(() => parseColor('rgb(1, 2, 3)')).toThrow(/Unsupported colour format/)
  })
})

describe('parseDeclaration', () => {
  it('extracts the name and value of a custom property', () => {
    expect(parseDeclaration('--alp-bg: #eff1f5')).toEqual(['alp-bg', '#eff1f5'])
  })

  it('trims surrounding whitespace from name and value', () => {
    expect(parseDeclaration('   --alp-danger :   #d20f39  ')).toEqual(['alp-danger', '#d20f39'])
  })

  it('keeps a var() value intact (the property is the FIRST --, not the last)', () => {
    // The semantic tier holds values like `var(--alp-muted)`, whose own `--`
    // must not be mistaken for the property name. This is the regression the
    // first-`--` parse fixes; lastIndexOf would have returned null here.
    expect(parseDeclaration('--muted-foreground: var(--alp-muted)')).toEqual([
      'muted-foreground',
      'var(--alp-muted)',
    ])
  })

  it('returns null for a segment with no custom property', () => {
    expect(parseDeclaration('color: red')).toBeNull()
  })

  it('returns null for a segment with a property but no colon', () => {
    expect(parseDeclaration('--orphan-token')).toBeNull()
  })

  it('returns null for an empty value', () => {
    expect(parseDeclaration('--alp-bg:   ')).toBeNull()
  })
})

describe('stripComments', () => {
  it('strips a block comment but preserves the surrounding text', () => {
    // The comment carries `:` and `;` — the exact characters that would
    // otherwise confuse the literal block/declaration scanning downstream.
    const out = stripComments('a { x: 1; } /* note: has ; and : */ b { y: 2; }')
    expect(out).not.toContain('note')
    expect(out).toContain('a { x: 1; }')
    expect(out).toContain('b { y: 2; }')
  })

  it('removes multiple consecutive comments', () => {
    expect(stripComments('/* one */keep/* two *//* three */end')).toBe('keepend')
  })

  it('drops everything from an unterminated comment onward', () => {
    // Documented truncation contract (an unclosed `/*` swallows the rest):
    // the gate would rather lose trailing tokens than mis-parse them.
    expect(stripComments('visible /* unterminated rest is gone')).toBe('visible ')
  })
})

const CSS_FIXTURE = `
/* primitive tier — comment with a : and ; to confuse naive parsers */
:root {
  --alp-bg: #ffffff;
  --alp-bone: #000000;
  --alp-danger: #d20f39;
  --alp-on-danger: #ffffff;
}

.light {
  --alp-bg: #000000;
  --alp-bone: #ffffff;
  --alp-danger: #f38ba8;
  --alp-on-danger: oklch(0 0 0);
}
`

// A two-tier fixture: a primitive :root block plus a semantic :root block
// that maps slots through var(--alp-*), mirroring index.css's structure.
const TWO_TIER_FIXTURE = `
:root {
  --alp-bg: #ffffff;
  --alp-bone: #000000;
}
:root {
  --background: var(--alp-bg);
  --foreground: var(--alp-bone);
}
`

describe('findBlockBodies', () => {
  it('returns every brace-adjacent block for a selector', () => {
    const bodies = findBlockBodies(TWO_TIER_FIXTURE, ':root')
    expect(bodies).toHaveLength(2)
    expect(bodies[0]).toContain('--alp-bg: #ffffff')
    expect(bodies[1]).toContain('--background: var(--alp-bg)')
  })

  it('returns an empty array when the selector is not brace-adjacent', () => {
    // Real Tailwind v4 `@custom-variant` directive form (unchanged under
    // Alpine — DR-1): `.dark` appears but is followed by ` *))`, not `{`,
    // so it is skipped.
    expect(findBlockBodies('@custom-variant dark (&:is(.dark *));', '.dark')).toEqual([])
  })
})

describe('findBlockBody', () => {
  it('extracts the body of a :root block', () => {
    const body = findBlockBody(CSS_FIXTURE, ':root')
    expect(body).toContain('--alp-bg: #ffffff')
    expect(body).toContain('--alp-bone: #000000')
    expect(body).not.toContain('.light')
  })

  it('extracts the body of a .light block', () => {
    const body = findBlockBody(CSS_FIXTURE, '.light')
    expect(body).toContain('--alp-bg: #000000')
    expect(body).toContain('--alp-bone: #ffffff')
  })

  it('throws when the selector has no brace-adjacent block', () => {
    expect(() => findBlockBody('@custom-variant dark (&:is(.dark *));', '.dark')).toThrow(
      /Could not find a "\.dark" block/,
    )
  })
})

describe('extractTokens', () => {
  it('builds the primitive token map for :root', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    expect(tokens.get('alp-bg')).toBe('#ffffff')
    expect(tokens.get('alp-bone')).toBe('#000000')
    expect(tokens.get('alp-danger')).toBe('#d20f39')
    expect(tokens.get('alp-on-danger')).toBe('#ffffff')
  })

  it('builds a distinct map for .light', () => {
    const tokens = extractTokens(CSS_FIXTURE, '.light')
    expect(tokens.get('alp-bg')).toBe('#000000')
    expect(tokens.get('alp-on-danger')).toBe('oklch(0 0 0)')
  })

  it('merges the primitive and semantic blocks into one map', () => {
    const tokens = extractTokens(TWO_TIER_FIXTURE, ':root')
    expect(tokens.get('alp-bone')).toBe('#000000')
    expect(tokens.get('foreground')).toBe('var(--alp-bone)')
    expect(tokens.get('background')).toBe('var(--alp-bg)')
  })

  it('throws when no block matches the selector', () => {
    expect(() => extractTokens(':root { --alp-bg: #fff; }', '.light')).toThrow(
      /Could not find a "\.light" block/,
    )
  })
})

describe('resolveToken', () => {
  it('resolves a known primitive token to an Rgb', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    expect(resolveToken(tokens, 'alp-bg', 'dark')).toEqual(WHITE)
    expect(resolveToken(tokens, 'alp-bone', 'dark')).toEqual(BLACK)
  })

  it('follows a var() reference from the semantic tier to the primitive value', () => {
    const tokens = extractTokens(TWO_TIER_FIXTURE, ':root')
    expect(resolveToken(tokens, 'foreground', 'dark')).toEqual(BLACK)
    expect(resolveToken(tokens, 'background', 'dark')).toEqual(WHITE)
  })

  it('throws naming the mode when the token is absent', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    expect(() => resolveToken(tokens, 'alp-missing', 'dark')).toThrow(
      /Token --alp-missing not found in the dark tier/,
    )
  })

  it('throws when a var() reference points at a missing token', () => {
    const tokens = new Map([['slot', 'var(--alp-missing)']])
    expect(() => resolveToken(tokens, 'slot', 'dark')).toThrow(/Token --alp-missing not found/)
  })

  it('throws on a cyclic var() reference instead of looping forever', () => {
    const tokens = new Map([
      ['a', 'var(--b)'],
      ['b', 'var(--a)'],
    ])
    expect(() => resolveToken(tokens, 'a', 'dark')).toThrow(/Cyclic var\(\) reference/)
  })
})

describe('PAIRS matrix', () => {
  it('names the destructive pair by its semantic slots (F1 fix)', () => {
    const destructive = PAIRS.find((pair) =>
      pair.label.includes('--destructive-foreground on --destructive'),
    )
    expect(destructive).toBeDefined()
    expect(destructive?.fg).toBe('destructive-foreground')
    expect(destructive?.bg).toBe('destructive')
    expect(destructive?.threshold).toBe(4.5)
  })

  it('asserts the --input form-control boundary (guards a silent drop)', () => {
    // --input is the only cue an empty resting field exists (its fill is a
    // near-invisible ~1:1 against the page bg), so it must stay gated to the
    // 3:1 UI-component rule and never be quietly dropped from PAIRS again.
    const input = PAIRS.find((pair) => pair.fg === 'input')
    expect(input).toBeDefined()
    expect(input?.bg).toBe('background')
    expect(input?.threshold).toBe(3.0)
  })

  it('does not carry mode-suffixed labels — pairs are mode-invariant', () => {
    for (const pair of PAIRS) {
      expect(pair.label).not.toMatch(/\((light|dark)\)/)
    }
  })

  it('has exactly one entry per semantic slot (no DARK_OVERRIDES duplicates)', () => {
    const labels = PAIRS.map((pair) => pair.label)
    expect(new Set(labels).size).toBe(labels.length)
  })

  it('uses only AA text (4.5) or UI (3.0) thresholds', () => {
    for (const pair of PAIRS) {
      expect([3.0, 4.5]).toContain(pair.threshold)
    }
  })
})

describe('checkMode', () => {
  const PASS_PAIR: Pair = {
    label: '--text on --base',
    fg: 'alp-bone',
    bg: 'alp-bg',
    threshold: 4.5,
  }
  const FAIL_PAIR: Pair = {
    label: '--base on --base',
    fg: 'alp-bg',
    bg: 'alp-bg',
    threshold: 4.5,
  }

  it('passes a black-on-white pair with a sane 21:1 ratio', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    const [result] = checkMode(tokens, 'dark', [PASS_PAIR])
    expect(result.passed).toBe(true)
    expect(result.mode).toBe('dark')
    expect(result.threshold).toBe(4.5)
    expect(result.ratio).toBeCloseTo(21, 5)
  })

  it('fails a same-colour pair with a 1:1 ratio', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    const [result] = checkMode(tokens, 'dark', [FAIL_PAIR])
    expect(result.passed).toBe(false)
    expect(result.ratio).toBeCloseTo(1, 10)
  })

  it('resolves the same pair per mode, yielding sane ratios in both', () => {
    const dark = checkMode(extractTokens(CSS_FIXTURE, ':root'), 'dark', [PASS_PAIR])
    const light = checkMode(extractTokens(CSS_FIXTURE, '.light'), 'light', [PASS_PAIR])
    // alp-bone/alp-bg flip between modes but the contrast is mode-invariant.
    expect(dark[0].ratio).toBeCloseTo(light[0].ratio, 5)
    expect(dark[0].passed).toBe(true)
    expect(light[0].passed).toBe(true)
  })

  it('returns one result per input pair', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    const results = checkMode(tokens, 'dark', [PASS_PAIR, FAIL_PAIR])
    expect(results).toHaveLength(2)
  })

  it('detects a re-pointed semantic mapping (bug-1 regression)', () => {
    // The same semantic slot --fg, mapped to a strong vs a weak primitive.
    // Only the mapping differs; the gate must follow it through var() and
    // flip pass→fail. Under the old primitive-only design this regression
    // class was invisible.
    const primitives = ':root { --alp-strong: #595959; --alp-weak: #bdbdbd; --alp-bg: #ffffff; }'
    const pair: Pair = { label: '--fg on --bg', fg: 'fg', bg: 'bg', threshold: 4.5 }
    const passing = primitives + ':root { --fg: var(--alp-strong); --bg: var(--alp-bg); }'
    const failing = primitives + ':root { --fg: var(--alp-weak); --bg: var(--alp-bg); }'
    expect(checkMode(extractTokens(passing, ':root'), 'dark', [pair])[0].passed).toBe(true)
    expect(checkMode(extractTokens(failing, ':root'), 'dark', [pair])[0].passed).toBe(false)
  })
})

describe('runChecks (against the committed index.css)', () => {
  it('resolves every PAIRS slot in both modes without throwing', () => {
    const css = stripComments(readFileSync(realIndexCssPath, 'utf8'))
    for (const selector of [':root', '.light']) {
      const tokens = extractTokens(css, selector)
      for (const pair of PAIRS) {
        expect(() => resolveToken(tokens, pair.fg, selector)).not.toThrow()
        expect(() => resolveToken(tokens, pair.bg, selector)).not.toThrow()
      }
    }
  })

  it('reports 32 results (16 pairs × 2 modes), all passing', () => {
    const results = runChecks(readFileSync(realIndexCssPath, 'utf8'))
    expect(results).toHaveLength(32)
    for (const result of results) {
      expect(result.passed).toBe(true)
    }
  })
})

describe('check-contrast script (integration)', () => {
  const frontendRoot = resolve(here, '../..')
  const scriptRel = 'scripts/check-contrast.ts'
  const tsxBin = resolve(frontendRoot, 'node_modules/.bin/tsx')

  function runGate(cssPathArg?: string): { status: number; stdout: string; stderr: string } {
    try {
      const stdout = execFileSync(tsxBin, [scriptRel, ...(cssPathArg ? [cssPathArg] : [])], {
        cwd: frontendRoot,
        encoding: 'utf8',
        // Pipe (not inherit) the child's stderr so the deliberate token-regression
        // case's `[FAIL]` lines land in `error.stderr` for the assertions below
        // instead of leaking to the test console.
        stdio: ['ignore', 'pipe', 'pipe'],
      })
      return { status: 0, stdout, stderr: '' }
    } catch (error) {
      const e = error as { status?: number; stdout?: Buffer | string; stderr?: Buffer | string }
      return {
        status: e.status ?? 1,
        stdout: e.stdout?.toString() ?? '',
        stderr: e.stderr?.toString() ?? '',
      }
    }
  }

  it('exits 0 and reports all pairs passing against the committed tokens', () => {
    const { status, stdout } = runGate()
    expect(status).toBe(0)
    expect(stdout).toContain('all 32 pairs pass WCAG thresholds')
  }, 30000)

  it('exits non-zero and names the failing pair + ratio on stderr when a token regresses', () => {
    // Break the dark --foreground by darkening its primitive (--alp-bone)
    // towards the dark --background, so --foreground on --background drops
    // below 4.5:1. Plain string slicing (no backtracking-prone regex)
    // rewrites the FIRST `--alp-bone:` declaration's value up to its `;` —
    // the first occurrence is the :root (dark, default) primitive tier.
    const original = readFileSync(realIndexCssPath, 'utf8')
    const markerAt = original.indexOf('--alp-bone:')
    const semicolonAt = original.indexOf(';', markerAt)
    const broken = `${original.slice(0, markerAt)}--alp-bone: #141814${original.slice(semicolonAt)}`
    const tmp = resolve(tmpdir(), `check-contrast-fail-${process.pid}.css`)
    writeFileSync(tmp, broken)
    try {
      const { status, stderr } = runGate(tmp)
      expect(status).toBe(1)
      expect(stderr).toContain('below threshold')
      const failLine = stderr
        .split('\n')
        .find((line) => line.includes('[FAIL]') && line.includes('--foreground on --background'))
      expect(failLine).toBeDefined()
      expect(failLine).toContain('(need 4.5:1)')
      // The measured ratio is printed just before ':1' and must be below 4.5.
      const measuredRatio = Number.parseFloat((failLine ?? '').split('--background:')[1] ?? '')
      expect(measuredRatio).toBeGreaterThan(0)
      expect(measuredRatio).toBeLessThan(4.5)
    } finally {
      rmSync(tmp, { force: true })
    }
  }, 30000)
})
