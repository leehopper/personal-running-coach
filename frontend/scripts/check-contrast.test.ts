// @vitest-environment node
import { describe, expect, it } from 'vitest'
import {
  PAIRS,
  checkMode,
  contrastRatio,
  extractTokens,
  findBlockBody,
  parseColor,
  parseDeclaration,
  parseHex,
  parseOklch,
  relativeLuminance,
  resolveToken,
  type Pair,
} from './check-contrast'

const WHITE = { r: 255, g: 255, b: 255 }
const BLACK = { r: 0, g: 0, b: 0 }

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

  it('throws on non-hex characters via the NaN guard', () => {
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
    expect(parseDeclaration('--ctp-base: #eff1f5')).toEqual(['ctp-base', '#eff1f5'])
  })

  it('trims surrounding whitespace from name and value', () => {
    expect(parseDeclaration('   --ctp-red :   #d20f39  ')).toEqual(['ctp-red', '#d20f39'])
  })

  it('returns null for a segment with no custom property', () => {
    expect(parseDeclaration('color: red')).toBeNull()
  })

  it('returns null for a segment with a property but no colon', () => {
    expect(parseDeclaration('--orphan-token')).toBeNull()
  })

  it('returns null for an empty value', () => {
    expect(parseDeclaration('--ctp-base:   ')).toBeNull()
  })
})

const CSS_FIXTURE = `
/* primitive tier — comment with a : and ; to confuse naive parsers */
:root {
  --ctp-base: #ffffff;
  --ctp-text: #000000;
  --ctp-red: #d20f39;
  --ctp-destructive-on: #ffffff;
}

.dark {
  --ctp-base: #000000;
  --ctp-text: #ffffff;
  --ctp-red: #f38ba8;
  --ctp-destructive-on: oklch(0 0 0);
}
`

describe('findBlockBody', () => {
  it('extracts the body of a :root block', () => {
    const body = findBlockBody(CSS_FIXTURE, ':root')
    expect(body).toContain('--ctp-base: #ffffff')
    expect(body).toContain('--ctp-text: #000000')
    expect(body).not.toContain('.dark')
  })

  it('extracts the body of a .dark block', () => {
    const body = findBlockBody(CSS_FIXTURE, '.dark')
    expect(body).toContain('--ctp-base: #000000')
    expect(body).toContain('--ctp-text: #ffffff')
  })

  it('throws when the selector has no brace-adjacent block', () => {
    expect(() => findBlockBody('@custom-variant dark', '.dark')).toThrow(
      /Could not find a "\.dark" block/,
    )
  })
})

describe('extractTokens', () => {
  it('builds the primitive token map for :root', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    expect(tokens.get('ctp-base')).toBe('#ffffff')
    expect(tokens.get('ctp-text')).toBe('#000000')
    expect(tokens.get('ctp-red')).toBe('#d20f39')
    expect(tokens.get('ctp-destructive-on')).toBe('#ffffff')
  })

  it('builds a distinct map for .dark', () => {
    const tokens = extractTokens(CSS_FIXTURE, '.dark')
    expect(tokens.get('ctp-base')).toBe('#000000')
    expect(tokens.get('ctp-destructive-on')).toBe('oklch(0 0 0)')
  })
})

describe('resolveToken', () => {
  it('resolves a known token to an Rgb', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    expect(resolveToken(tokens, 'ctp-base', 'light')).toEqual(WHITE)
    expect(resolveToken(tokens, 'ctp-text', 'light')).toEqual(BLACK)
  })

  it('throws naming the mode when the token is absent', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    expect(() => resolveToken(tokens, 'ctp-missing', 'light')).toThrow(
      /Token --ctp-missing not found in the light primitive tier/,
    )
  })
})

describe('PAIRS matrix', () => {
  it('asserts the destructive pair against the real ctp-destructive-on token (F1 fix)', () => {
    const destructive = PAIRS.find((pair) =>
      pair.label.includes('--destructive-foreground on --destructive'),
    )
    expect(destructive).toBeDefined()
    expect(destructive?.fg).toBe('ctp-destructive-on')
    expect(destructive?.bg).toBe('ctp-red')
    expect(destructive?.threshold).toBe(4.5)
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
    fg: 'ctp-text',
    bg: 'ctp-base',
    threshold: 4.5,
  }
  const FAIL_PAIR: Pair = {
    label: '--base on --base',
    fg: 'ctp-base',
    bg: 'ctp-base',
    threshold: 4.5,
  }

  it('passes a black-on-white pair with a sane 21:1 ratio', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    const [result] = checkMode(tokens, 'light', [PASS_PAIR])
    expect(result.pass).toBe(true)
    expect(result.mode).toBe('light')
    expect(result.threshold).toBe(4.5)
    expect(result.ratio).toBeCloseTo(21, 5)
  })

  it('fails a same-colour pair with a 1:1 ratio', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    const [result] = checkMode(tokens, 'light', [FAIL_PAIR])
    expect(result.pass).toBe(false)
    expect(result.ratio).toBeCloseTo(1, 10)
  })

  it('resolves the same pair per mode, yielding sane ratios in both', () => {
    const light = checkMode(extractTokens(CSS_FIXTURE, ':root'), 'light', [PASS_PAIR])
    const dark = checkMode(extractTokens(CSS_FIXTURE, '.dark'), 'dark', [PASS_PAIR])
    // ctp-text/ctp-base flip between modes but the contrast is mode-invariant.
    expect(light[0].ratio).toBeCloseTo(dark[0].ratio, 5)
    expect(light[0].pass).toBe(true)
    expect(dark[0].pass).toBe(true)
  })

  it('returns one result per input pair', () => {
    const tokens = extractTokens(CSS_FIXTURE, ':root')
    const results = checkMode(tokens, 'light', [PASS_PAIR, FAIL_PAIR])
    expect(results).toHaveLength(2)
  })
})
