# R-086: Self-hosted web fonts for a Vite + React 19 + Tailwind v4 SPA (Barlow Condensed / Barlow / IBM Plex Mono)

## Context

The SPLIT / Alpine UI redesign cycle (`docs/plans/split-ui-cycle/cycle-plan.md`, Slice 0) replaces the current OS-native font stacks with three Google-family typefaces that must be **self-hosted** in production per the design handoff (`docs/design/split-alpine/HANDOFF.md` § 3): Barlow Condensed (weights 500–800, display + numerals), Barlow (400–600, body), IBM Plex Mono (400–600, labels + data). The repo is a Vite 7 / React 19 / TypeScript SPA with Tailwind CSS v4 (CSS-first `@theme` config, single `index.css`, two-tier token architecture per DEC-070), no font packages installed today, and a hard rule against runtime third-party requests for fonts (privacy + determinism). The design is data-dense: condensed numerals with `white-space: nowrap` and mono data columns with `font-variant-numeric: tabular-nums`, so metric stability and weight fidelity matter.

## Research Question

What is the 2026 best-practice way to self-host these three families in this stack? Sub-questions:

1. **Packaging:** `@fontsource/*` (static per-weight) vs `@fontsource-variable/*` vs manually vendored woff2 + hand-written `@font-face`. Which is best for Vite asset hashing, tree-shaking unused weights, and long-term maintenance? Are variable versions of Barlow / Barlow Condensed / IBM Plex Mono available and faithful (weight axis coverage 400–800; condensed is a separate family, not a width axis of Barlow — confirm)?
2. **Weight/subset strategy:** which concrete weight files are needed for the role table (Condensed 500/600/700/800, Barlow 400/500/600, Plex Mono 400/500/600); latin subset only vs latin-ext; per-file size budget and total payload estimate.
3. **Loading strategy:** `font-display` choice (swap vs optional vs fallback) for an app (not content site) where FOUT on numerals is jarring; preload strategy in a Vite SPA (which files, how to emit `<link rel="preload">` with hashed asset URLs); interaction with the existing no-flash dark-mode script.
4. **CSS integration:** wiring `--font-condensed` / `--font-body` / `--font-mono` into Tailwind v4's `@theme` (font-family tokens + utilities); metric-compatible fallback stacks (and whether `size-adjust`/`ascent-override` fallback metrics are worth it here); `font-variant-numeric: tabular-nums` support caveats in these families.
5. **Licensing/attribution:** OFL obligations for redistribution in an OSS repo (license files in-repo? NOTICE entries?).

## Why It Matters

Slice 0 is the cycle's foundation slice — every later slice renders on these fonts. A wrong packaging choice ripples through Vite build output, CI size, and every `@font-face` touchpoint; the repo's Research Protocol requires an artifact before adopting a library/pattern not already in use.

## Deliverables

- Concrete recommendation with rationale (packaging, exact package names + version pins or vendoring procedure, weight/subset list, `font-display` + preload plan, Tailwind v4 wiring snippet shape).
- Alternatives considered and why rejected.
- Gotchas: Vite hashed-URL preload, FOUT on tabular numerals, condensed-family fallback behavior, OFL compliance steps.
- Version pins and browser-support notes (Safari included — the app is used on iOS Safari).
