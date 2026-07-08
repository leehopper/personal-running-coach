# Slice 0 Design: Alpine Foundation

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: `docs/design/split-alpine/HANDOFF.md` §§ 1–3, 6–8 + design sheets 4a/4b/5g. **Research gate: R-086** (self-hosted fonts) — integrate the artifact before spec-writing.

## Purpose

Swap the visual substrate once — palette, theme polarity, typography, shared primitives, contrast gate, brand assets — so every later slice restyles *onto* a finished foundation instead of each re-deciding it. After this slice the app keeps its current layouts but renders entirely in Alpine.

## Locked design decisions

- **D1 — Token swap in place.** The two-tier architecture (primitive → semantic → single `@theme inline` block) survives verbatim; only the primitive ramp changes (Catppuccin → Alpine, handoff § 2 tables) plus the semantic remap and two net-new slots `--positive` (moss) and `--rule` (bone/ink). `--warning` already exists — remap to amber. Current tiers: `frontend/src/index.css` (primitive ≈ 39–113, semantic ≈ 120–228, `@theme inline` ≈ 234–276). **Geometry rides the same layer:** the § 2 radius ramp (4 / 6 / 8 / 10 / 999; nothing over 12 inside screens) as radius tokens, the rule law (2px `--rule` section openers, 1px `--border` data dividers, flat surfaces / rare elevation), and the spacing rhythm (4-base scale, 22px screen gutter, 20–22px section gap) as shared utilities — encoded once here, consumed by every later slice.
- **D2 — Dark becomes `:root` default; light is `.light`** (handoff § 2). Inverts today's light-default/`.dark` pattern end-to-end: primitive-tier blocks, the `@custom-variant dark` line, `theme-provider.tsx` apply/resolve/fallback, and the `index.html` no-flash IIFE default. "System" still follows `prefers-color-scheme`. The theme e2e and the provider drift-assertion spec pin the current polarity — update them in the same PR.
- **D3 — Typography roles are tokens + shared classes, not per-page improvisation.** Three families (`--font-condensed`, `--font-body`, `--font-mono`) + the § 3 role table encoded once (CSS utility classes and/or cva variants — spec decides the mechanism). Rules that are law: numbers always Barlow Condensed; labels always IBM Plex Mono; `tabular-nums` on mono data columns; big numerals `white-space: nowrap`; all-caps via `text-transform`, source copy sentence case.
- **D4 — Fonts self-hosted, per R-086.** Packaging (`@fontsource` vs manual woff2 + subsetting), weight strategy (static 500–800 condensed / 400–600 body + mono vs variable), and FOUT/preload strategy come from the artifact. No Google Fonts CDN at runtime.
- **D5 — check-contrast pair set swapped to Alpine.** Today: 14 flat 4.5/3.0 pairs over `:root`/`.dark` (`frontend/scripts/check-contrast.ts` ≈ 67–131). Swap the pair list to the Alpine commitments (bone/bg 14.2:1, muted/bg 6.6:1, ink-on-clay 6.9:1, clay-text-on-bg 4.6:1 — which clears the standard 4.5:1 normal-text gate outright), add a `--positive`-on-surface pair, register `--alp-faint` decorative-only (exempt like `--border`), and follow the new selector polarity (`:root` + `.light`). The handoff's "clay text only ≥12px semibold" constraint is a **usage rule, not a pair ratio** — the pair checker has no size/weight axis and this slice does not build a usage scanner; the rule is documented in `frontend/CLAUDE.md` § Styling and enforced at review. **The pair-list swap must ride the same PR as the token swap** — check-contrast runs in pre-commit/CI, so tokens and pairs cannot change in separate commits without breaking the gate.
- **D6 — Shared primitives land here.** Restyle existing shadcn primitives via tokens + cva variants (button, input, label, form, badge, dialog, collapsible, radio-group, sonner) — not rewrites — plus a restyled switch/`Toggle` (handoff § 7: replaces checkboxes in onboarding). Net-new shared components: `SegmentedControl` (used by completion/units/theme), `SectionRule` (2px rule + condensed label + right slot), `MonoLabel`, `StatBand`/`StatCell`, `Wordmark`, and the **plan-building surface** (`BUILDING YOUR PLAN`: full surface, clay indeterminate-honest progress, mono line — handoff § 6/sheet 4b) that Slice 5 (onboarding submit) and Slice 6 (regenerate) adopt without rebuilding. State law from § 6: primary pressed `#B0532A` + scale 0.98, disabled 35%, focus ring clay border + 3px `rgba(208,106,59,0.22)`, error = danger border + mono `role="alert"` message, focus-visible 2px clay outline / 2px offset, hit targets ≥44px, motion 150–200ms transform/opacity with `motion-reduce` pairing on every animation.
- **D7 — Brand assets.** `Wordmark` (SPLIT + clay `/`, Barlow Condensed 800, letter-spacing 0.03–0.04em, no gap before the slash, `aria-label="Split"`); favicon + app-icon set per design 5g (italic clay slash on ink, optical center nudged ~3% up, maskable-safe center 60%, 16/32/60+ sizes). SPLIT renders now — user-ratified 2026-07-07; rename risk is contained here.
- **D8 — Sonner restyle via CSS vars only** (existing theming mechanism in `components/ui/sonner.tsx`): success = moss check square + mono message; error = danger left edge + RETRY affordance.

## Functional requirements

- Every existing screen renders on Alpine tokens in both modes with zero layout recomposition.
- Theme toggle (DARK/LIGHT/SYSTEM) behaves exactly as today with inverted polarity; no flash on load in either mode.
- The role-table classes/variants exist and are consumed by the restyled primitives; later slices only apply them.
- `Wordmark` + favicon/app icons shipped and mounted (`index.html` head, manifest if present).
- The plan-building surface and the restyled switch/`Toggle` exist as shared components (consumed by Slices 5/6 without rebuilding).
- `check-contrast` gates the Alpine pairs in pre-commit + CI; the clay-text ≥12px-semibold usage rule is documented in `frontend/CLAUDE.md` § Styling.

## Quality requirements

- Full Vitest + Playwright suites green; the theme specs updated, not deleted.
- No component keeps a hardcoded Catppuccin-era color utility; token-resolution failures are build/gate failures, not silent fallbacks.
- Reduced-motion (DEC-063) pairing on every animated primitive.
- Trademark rule untouched (no user-facing "VDOT" anywhere — pre-existing, re-verified).

## Scope: In

Token tiers (color + geometry/spacing) + polarity inversion; fonts + role system; primitive restyles + the net-new shared components (including the plan-building surface); contrast-gate pair swap; brand assets; sonner restyle; theme-related test realignment.

## Scope: Out (deferred)

Any screen recomposition (Slices 2–6); TabBar/shell (Slice 1); skeletons + query-failure surfaces (surfaces own theirs; Slice 7 audits — the plan-building surface is the one full-surface state built here, per D6); `motion/react` (DEC-063 stands).

## PR sketch

1. **PR-A** — Alpine tokens (color + geometry) + semantic remap + polarity inversion + check-contrast pair swap (+ theme test updates). The contrast pair list cannot split out — the pre-commit gate must stay green per-commit.
2. **PR-B** — fonts (per R-086) + typography role system.
3. **PR-C** — primitive restyles + `SegmentedControl`/`SectionRule`/`MonoLabel`/`StatBand`/`Wordmark`/plan-building surface + favicon/app icon + sonner.

Exact cut lands in the spec; keep each PR reviewable.

## References

- `docs/design/split-alpine/HANDOFF.md` §§ 1–3, 6, 8; design sheets 4a (daylight mapping), 4b (states), 5g (icon).
- R-086 artifact (fonts) once landed.
- DEC-070 (current token architecture), DEC-063 (motion), DEC-089 (this cycle's decisions).
- `frontend/CLAUDE.md` § Styling — update it in this slice to describe the Alpine layer.
