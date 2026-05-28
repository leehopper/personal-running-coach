# Slice 2a Design: Frontend Visual Foundation

> **Design doc — produced from the 2026-05-18 brainstorm. Not a specification and not an implementation plan.** Captures the locked design decisions and the "what" of the foundation; the "how" is written as a spec in a fresh session once the pending research artifact (R-075) lands. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`. Sibling: `slice-2-logging.md` (sub-project 2b).

## Why 2a exists

Slice 2 (Workout Logging) is the first slice where frontend polish matters — a log form, a today's-workout card, a history list. The frontend has no deliberate visual design to build them on: `index.css` is bare (`@import 'tailwindcss';`), colours are hardcoded ad hoc per component (`bg-slate-900`, `border-red-200`, …), there is no dark mode, and shadcn/ui — named in the stack — was never actually installed. 2a establishes that foundation once so 2b and every later slice consume it instead of each re-deciding. It is cross-cutting, so it ships before 2b.

## Purpose

Give RunCoach a coherent, accessible visual foundation: a semantic design-token layer with a real palette, light and dark modes, an installed component library, and the existing surfaces migrated onto it — with no surface left half-styled.

## Locked design decisions (2026-05-18 brainstorm)

**Palette — the "hybrid".** Catppuccin's Latte (light) and Mocha (dark) *neutral* ramps supply the surface, border, and text colours; the light↔dark pairing comes free because Catppuccin designed the flavours as a matched set. Two corrections sit on top:

- *Contrast rule.* Any text role maps only to the high-contrast ramp steps (`text`, `subtext1`); the lighter `overlay*` greys are for non-text use only (dividers, disabled fills, icon strokes). This is a constraint on the token mapping, not a new colour — Catppuccin's softer greys fail WCAG AA as text, and RunCoach is metric- and text-dense.
- *One project-owned accent.* Catppuccin's accent colours are tuned as coloured text/tint, not as solid button fills with white text (its green button fails AA). The foundation adds exactly one derived shade — the chosen accent darkened by formula until white-on-accent clears AA 4.5:1 — used for primary actions.

The custom layer is therefore deliberately tiny: one contrast rule plus one formula-derived shade. It is a closed, enumerable set of token definitions, not an open design effort — the foundation is scoped so it cannot drift into one.

**Component library — shadcn/ui.** Adopt shadcn/ui properly: `components.json`, the `cn()` utility, Radix dependencies, and the primitives the existing and near-term surfaces need. This is already the prescribed choice — R-065 (`batch-21a-onboarding-chat-ux-react19.md`) and `10a-frontend-latest-practices.md` both establish shadcn/ui as the component library, and the `CLAUDE.md` / REVIEW configs already name it. The decision was made and never executed; 2a executes the install that the 2026-05-06 cycle-plan captured item deferred. (R-065 also recommended `motion/react`; DEC-063 later overrode that with a Tailwind-only animation baseline. The shadcn/ui prescription was never overridden.)

**Typography & spacing.** Tailwind's built-in default type, spacing, and radius scales, used as-is, plus a system font stack. Nothing bespoke.

**Light and dark.** Both modes ship — the Catppuccin Latte/Mocha pair makes dark essentially free once the token layer is built for it.

## Functional requirements

When 2a is complete:

- A semantic design-token layer exists — surface, border, text, muted, accent, and state roles — with light and dark values, consumed by Tailwind utilities and shadcn primitives.
- shadcn/ui is installed and configured, with the primitives the existing and near-term surfaces need.
- The existing surfaces (login, register, the five onboarding turn-inputs, the onboarding chat, settings, home) render on the token layer and shadcn primitives — no surface still uses hardcoded colour utilities.
- Light and dark modes both render correctly across every migrated surface.
- The frontend stack documentation (root and `frontend/CLAUDE.md`) is reconciled with reality — shadcn/ui is now actually present.

## Quality requirements

- Every text/background token pair is verified against WCAG AA — a machine-checkable contrast result, not a judgement call.
- The accent's primary-action pairing (fill + foreground) clears AA in both modes.
- Existing component tests and Playwright E2E still pass after migration; the wire-level behaviour of migrated forms is unchanged — shadcn's `Form` wraps the existing React Hook Form + Zod usage, it does not replace it.
- The reduced-motion contract (DEC-063) is preserved on any shadcn primitive that animates.

## Scope: In

- The semantic design-token layer (the hybrid palette: primitive Catppuccin values + the semantic mapping + light/dark).
- shadcn/ui install plus the primitives the existing and near-term surfaces require.
- Migration of all existing surfaces onto the tokens + shadcn primitives.
- Light/dark wiring.
- Reconciling the stack documentation.

## Scope: Out (deferred)

- Calendar component / date-heavy views — picked when a slice needs one.
- Chat-UI library — Slice 4 territory (R-065 already flags `assistant-ui` for the streaming open-conversation panel).
- Animation library — DEC-063 holds; Tailwind-only baseline stands.
- Custom iconography, illustration, or brand-identity work — `lucide-react` (shadcn's icon set) only.
- Any Slice 2b feature UI (log form, today's-workout card, history list) — built on this foundation, in 2b.
- Per-route error UI / `createBrowserRouter` data-router migration — deferred by DEC-068.

## Locked architecture (R-075 → DEC-070)

R-075 (`docs/research/artifacts/batch-26a-frontend-design-system-theming-integration.md`, landed 2026-05-18) resolved the theming-integration question; **DEC-070** locks it:

- **Single `src/index.css`**, shadcn's Tailwind-v4 canonical order: `@import "tailwindcss"` → `@import "tw-animate-css"` → `@custom-variant dark (&:is(.dark *))` → primitive tier → semantic tier → one `@theme inline` block. No `tailwind.config.ts`.
- **Two-tier tokens.** A primitive tier (`--ctp-*`, Catppuccin Latte in `:root` / Mocha in `.dark`, hex pasted from `catppuccin/palette` v1.7.1) sits below shadcn's semantic tokens (`--background`, `--primary`, …). Dark mode swaps only the primitive tier; the semantic mappings are mode-invariant.
- **Dark mode** via a ~40-LOC `ThemeProvider` toggling `.dark` on `documentElement` + an inline no-flash script in `index.html`; `defaultTheme="system"` plus a 3-state toggle on the Settings page.
- **Contrast rule is encoded in the token mappings** — text-role tokens point only at `text` / `subtext1` (`--muted-foreground` = `subtext1` in Latte, since `subtext0`-on-`base` is 4.37:1, an AA-normal fail). An optional CI contrast-assertion script backs it.
- **Accent** = one statically-committed OKLCH shade per mode (hue held, lightness moved); `--primary-foreground` is `crust` in dark, near-white in light.
- **Component set:** `button input label form card collapsible dialog sonner badge radio-group scroll-area` (~32–35 kB gz, six Radix packages).
- **shadcn `Form`** wraps the existing React Hook Form + Zod usage verbatim — zero validation or wire-shape change.
- **`tw-animate-css`** (CSS-only) is the animation import — consistent with DEC-063; every animated primitive carries `motion-reduce:animate-none motion-reduce:transition-none`.

## Open items for the spec / implementation

R-075 is integrated as DEC-070 — the token architecture and dark-mode mechanism are locked above. What remains for the 2a spec / implementation:

- **Accent family.** Pick one Catppuccin accent (green / teal / …) and commit both the native Mocha hex and the OKLCH-lightness-reduced Latte hex. DEC-070 / R-075 lock the derivation *method*; the family is the pick.
- **`motion-reduce` strictness.** A per-primitive `MOTION_REDUCE_OFF` utility constant vs a global `@custom-variant motion-reduce` override that also honours a `data-reduce-motion` attribute on `<html>`. DEC-063 leaves this open; resolve at spec time.
- **CI contrast-assertion script.** `scripts/check-contrast.ts` computing WCAG ratios for each semantic foreground/background pair — recommended; scope into the spec.
- **`stylelint-declaration-strict-value`.** Restrict `color` / `background-color` / `border-color` to `var(--*)`; agree the allowlist.
- **`@vitejs/plugin-react` v5 → v6.** v6 (Babel-free, Oxc) is the simpler choice given no React Compiler use; confirm at the foundation PR.
- **Dev-only `/dev/theme-debug` route** rendering every semantic token in both modes — cheap insurance, optional.

## References to consult

- `docs/research/artifacts/batch-21a-onboarding-chat-ux-react19.md` (R-065) — the shadcn/ui prescription and the shared chat-primitive set.
- `docs/research/artifacts/10a-frontend-latest-practices.md` — the React 19 + Tailwind v4 + shadcn conventions baseline.
- The Catppuccin style guide — the palette source.

## How this feeds the spec

1. R-075 is integrated — DEC-070 locks the token architecture + dark-mode mechanism; the "Locked architecture" section above carries the summary. ✓ (done 2026-05-18)
2. A spec is written fresh, from this doc plus the artifact.
3. The user reviews before implementation.
4. Implementation runs in small, tight MRs (sketch: shadcn bootstrap + the token layer first, then surfaces migrated a couple at a time — exact cut lands in the spec/plan).

## Relationship to the cycle plan

The cycle plan's Status section carries the 2a/2b decomposition; this doc is the 2a detail. If they conflict, the cycle plan wins — update this doc to match.
