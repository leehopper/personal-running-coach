// @vitest-environment node
import { describe, expect, it } from 'vitest'
import {
  FONT_FALLBACK_SPECS,
  PRELOAD_FONT_PATTERNS,
  matchPreloadFiles,
} from '../font-build.helpers'

// Representative hashed filenames in the exact scheme Vite emits for the
// three preloaded weights: `<family>-latin-<weight>-normal-<hash>.woff2`.
// Hashes intentionally vary in shape (alnum, mixed-case, hyphen, underscore)
// to prove the `[\w-]+` hash segment is matched, not a fixed-length token.
const HASHED = {
  barlow400: 'assets/barlow-latin-400-normal-abc123.woff2',
  barlowCondensed700: 'assets/barlow-condensed-latin-700-normal-XYZ789.woff2',
  ibmPlexMono400: 'assets/ibm-plex-mono-latin-400-normal-x_y-9Z.woff2',
} as const

describe('PRELOAD_FONT_PATTERNS', () => {
  it('has exactly three patterns (the above-the-fold weights)', () => {
    expect(PRELOAD_FONT_PATTERNS).toHaveLength(3)
  })

  it('each pattern matches its representative hashed filename', () => {
    const [barlow, barlowCondensed, ibmPlexMono] = PRELOAD_FONT_PATTERNS
    expect(barlow.test(HASHED.barlow400)).toBe(true)
    expect(barlowCondensed.test(HASHED.barlowCondensed700)).toBe(true)
    expect(ibmPlexMono.test(HASHED.ibmPlexMono400)).toBe(true)
  })

  it('the Barlow 400 pattern rejects the wrong weight (500)', () => {
    const [barlow] = PRELOAD_FONT_PATTERNS
    expect(barlow.test('assets/barlow-latin-500-normal-abc123.woff2')).toBe(false)
  })

  it('the Barlow 400 pattern rejects the wrong family (Barlow Condensed)', () => {
    // Barlow Condensed 400 must NOT be captured by the Barlow (non-condensed)
    // pattern — this is the exact "Barlow" ⊂ "Barlow Condensed" collision the
    // fallback-face wiring already had to defend against elsewhere.
    const [barlow] = PRELOAD_FONT_PATTERNS
    expect(barlow.test('assets/barlow-condensed-latin-400-normal-abc123.woff2')).toBe(false)
  })

  it('the IBM Plex Mono pattern rejects a non-woff2 extension', () => {
    const [, , ibmPlexMono] = PRELOAD_FONT_PATTERNS
    expect(ibmPlexMono.test('assets/ibm-plex-mono-latin-400-normal-abc123.woff')).toBe(false)
  })
})

describe('matchPreloadFiles', () => {
  it('returns the three matched filenames in pattern order', () => {
    // Deliberately shuffled + padded with unrelated assets to prove order
    // comes from PRELOAD_FONT_PATTERNS, not from input order.
    const fileNames = [
      'assets/index-abc.css',
      HASHED.ibmPlexMono400,
      'assets/barlow-latin-500-normal-def.woff2',
      HASHED.barlow400,
      'assets/index-abc.js',
      HASHED.barlowCondensed700,
    ]
    expect(matchPreloadFiles(fileNames)).toEqual([
      HASHED.barlow400,
      HASHED.barlowCondensed700,
      HASHED.ibmPlexMono400,
    ])
  })

  it('throws when a required weight is missing from the build output', () => {
    const fileNames = [HASHED.barlow400, HASHED.barlowCondensed700] // no IBM Plex Mono
    expect(() => matchPreloadFiles(fileNames)).toThrow(/ibm-plex-mono-latin-400-normal/)
  })
})

describe('FONT_FALLBACK_SPECS', () => {
  it('covers exactly the three self-hosted families', () => {
    expect(FONT_FALLBACK_SPECS.map((spec) => spec.family)).toEqual([
      'Barlow',
      'Barlow Condensed',
      'IBM Plex Mono',
    ])
  })

  it('pairs sans families with Arial and the mono family with Courier New', () => {
    const byFamily = Object.fromEntries(
      FONT_FALLBACK_SPECS.map((spec) => [spec.family, spec.systemFallback]),
    )
    expect(byFamily['Barlow']).toBe('Arial')
    expect(byFamily['Barlow Condensed']).toBe('Arial')
    expect(byFamily['IBM Plex Mono']).toBe('Courier New')
  })
})
