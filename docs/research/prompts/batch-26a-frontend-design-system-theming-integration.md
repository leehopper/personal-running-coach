# Research Prompt: Batch 26a — R-075

# Frontend Design-System Theming Integration — shadcn/ui × Tailwind CSS v4 × a Catppuccin-Derived Hybrid Token Layer with Light/Dark (Vite + React 19 SPA, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a React 19.2.x + Vite 8 + Tailwind CSS v4 SPA with TypeScript-strict that is adopting **shadcn/ui** as its component library, what is the canonical 2026 pattern for wiring a custom **semantic design-token layer** whose values come from **Catppuccin's Latte (light) and Mocha (dark) neutral ramps plus one project-owned accent**, where a **contrast rule** constrains text tokens to high-contrast ramp steps and **dark mode** switches the whole token set?

The component-library *choice* is already settled (shadcn/ui, per prior research R-065) — this prompt is **not** re-litigating it. The open question is the *theming and wiring*: how shadcn/ui's CSS-variable theming, Tailwind v4's CSS-first `@theme` configuration, the Catppuccin-derived hybrid tokens, and the dark-mode mechanism compose into one coherent foundation.

Pick a default approach plus one fallback wherever a choice is genuinely contested. Recommend exact package names and versions known to work together on React 19.2 + Vite 8 + Tailwind v4.2 + TS-strict.

### Sub-questions the artifact must answer

1. **shadcn/ui on Tailwind v4 — current 2026 state.** What does `npx shadcn@latest init` produce on a Tailwind v4 + Vite + React 19 + TS-strict project? Cover: the `components.json` shape; whether a `tailwind.config.ts` is still emitted or everything is CSS-first via `@theme`; the `cn()` utility + `clsx` + `tailwind-merge`; the `tw-animate-css` replacement for `tailwindcss-animate`; the OKLCH-based default theme shadcn now ships. Flag any known breakage on Vite 8 / React 19.2 / TypeScript 5.9-strict.

2. **Token architecture — layering a palette under shadcn's semantic tokens.** shadcn defines semantic CSS variables (`--background`, `--foreground`, `--primary`, `--primary-foreground`, `--muted`, `--muted-foreground`, `--border`, `--ring`, `--card`, `--destructive`, …) in `:root` and `.dark`. What is the 2026 best practice for sitting a *primitive* palette (Catppuccin's named colours — `base`, `mantle`, `crust`, `surface0/1/2`, `overlay0/1/2`, `subtext0/1`, `text`, and accents) underneath shadcn's *semantic* tokens — a two-tier (primitive → semantic) token file? Show how it is organized in `index.css` and how the semantic layer is exposed to Tailwind utilities (`@theme` vs `@theme inline`). Recommend the exact file structure.

3. **Catppuccin integration mechanism.** Is there an official `@catppuccin/tailwindcss` or `@catppuccin/palette` package that supports Tailwind v4's `@theme`, or should the hex values be pasted directly into the primitive tier? Recommend, with the maintenance tradeoff.

4. **Dark-mode mechanism.** shadcn's convention is a `.dark` class plus Tailwind v4's `@custom-variant dark`. For a Vite SPA (no Next.js, no `next-themes`), what is the 2026-canonical small theme controller — a tiny provider + `localStorage` + a `prefers-color-scheme` initial value, with a no-flash inline script? Compare class-toggle vs media-query-only. For an MVP-0 single-user app, recommend whether to ship an in-app toggle or follow the OS setting. Specify how the chosen mechanism interacts with the two-tier token file — does the `.dark` block re-point the *primitive* tier (swap Latte→Mocha values) or the *semantic* tier?

5. **The contrast rule — making it enforceable, not just documented.** The foundation's rule is "text tokens only map to high-contrast ramp steps (`text`, `subtext1`); never `overlay*`." Can this be machine-checked — a stylelint plugin, a build-time contrast assertion, a token-naming scheme that makes misuse obvious in review? What do 2026 design-token setups do to keep WCAG AA from regressing silently? Also: report the actual contrast ratios for the candidate Catppuccin Latte/Mocha text-on-surface pairs and state explicitly which ramp steps are safe for normal text vs large text vs non-text.

6. **The accent — deriving one contrast-safe solid-fill shade.** The foundation adds exactly one derived accent shade for primary-action fills. Given a chosen base accent, what is the recommended derivation *method* — OKLCH lightness adjustment vs a hand-picked darker value; build-time computed vs a static committed value? Show how to map it to shadcn's `--primary` / `--primary-foreground` so both the fill and its foreground clear WCAG AA in *both* light and dark modes. Should `--primary` keep the same hue across modes or shift? (The accent *family* — green, teal, etc. — is the project's pick from candidates; research only needs the method.)

7. **Migration shape — hand-rolled components → shadcn primitives.** The existing surfaces are hand-rolled: e.g. a login form using `bg-slate-900`, `border-slate-300`, `border-red-200 bg-red-50 text-red-800`, and native `<input>`/`<button>`. shadcn's `Form` component composes React Hook Form + Zod — and this project already uses RHF 7 + Zod 4 directly. Show how shadcn `Form` / `FormField` / `FormControl` / `FormMessage` wraps an existing `useForm` *without* changing validation behaviour or the request wire shape. Give a per-component effort estimate.

8. **Component set.** Which shadcn primitives should be installed for the existing surfaces (login, register, five onboarding turn-inputs, onboarding chat, settings, home) and the near-term Slice 2b needs (a form, a collapsible "more details" affordance, a card, a list)? Consider `Button`, `Input`, `Label`, `Form`, `Card`, `Collapsible`, `Dialog`, `Sonner` (toast), `Badge`, `RadioGroup`, `ScrollArea`. Report the Radix dependency footprint and the gzipped bundle delta.

9. **Reduced-motion / DEC-063 reconciliation.** shadcn primitives (Dialog, Popover, etc.) ship enter/exit animations expecting `tw-animate-css` (Tailwind v4's successor to `tailwindcss-animate`). The project's DEC-063 mandates a Tailwind-only animation baseline with a `motion-reduce:` parity contract and explicitly defers `motion`/`motion/react`. Confirm `tw-animate-css` is consistent with DEC-063 (it is CSS utility classes, not a JS animation library) and show how the `data-[state=open]:animate-in` pattern carries the `motion-reduce:` variant.

10. **Typography & spacing.** How to wire a system font stack via Tailwind v4 `@theme` `--font-*`; whether to keep Tailwind's default type / spacing / radius scales or adjust the shadcn-exposed `--radius` token. Recommend the minimal set of changes.

11. **Reference precedents.** Identify 2026 open-source React 19 + Vite + Tailwind v4 + shadcn/ui projects that ship a *custom* (non-default) token theme with class-based dark mode — ideally one using a Catppuccin-derived or otherwise externally-sourced palette. Link them.

## Context

I'm planning **sub-project 2a (Frontend Visual Foundation)** of Slice 2 of the MVP-0 cycle for RunCoach, an AI running coach. 2a establishes the design foundation every later frontend slice builds on. Design doc: `docs/plans/mvp-0-cycle/slice-2a-frontend-foundation.md`.

**Current state (verified 2026-05-18):**

- `frontend/package.json` — React 19.2.6, React Router 7.15.0, TypeScript 5.9.3, Vite 8.0.11, Tailwind CSS v4.2.2 via `@tailwindcss/vite` 4.2.4, React Hook Form 7.75, Zod 4.4. No `@radix-ui/*`, no `class-variance-authority`, no `clsx`, no `tailwind-merge`, no `lucide-react`, no `tailwindcss-animate` / `tw-animate-css`.
- `frontend/src/index.css` — the entire file is `@import 'tailwindcss';`. No `@theme`, no tokens, no `@custom-variant`.
- **shadcn/ui is not installed.** No `components.json`, no `src/components/ui/`, no `cn()` utility. Despite `frontend/CLAUDE.md` listing shadcn/ui in the stack, every component is hand-rolled with raw Tailwind utility classes.
- Colours are hardcoded ad hoc per component (`bg-slate-900`, `border-slate-300`, `text-slate-500`, `border-red-200 bg-red-50 text-red-800`, …). No semantic tokens, no dark mode.
- Existing surfaces: login, register, five onboarding turn-input components, an onboarding chat surface (message bubbles + transcript scroller, hand-rolled), a settings page, a home page. Forms use React Hook Form + Zod directly with native `<input>`/`<button>`.
- **Locked palette decision (the "hybrid").** Catppuccin Latte (light) + Mocha (dark) *neutral* ramps; a contrast rule (text roles map only to `text` / `subtext1`, never `overlay*`); plus exactly one formula-derived accent shade for solid primary-action fills.
- **DEC-063 holds** — Tailwind-only animation baseline; `motion` / `motion/react` deferred until a streaming-chat or gesture surface needs it.
- **R-065** (`docs/research/artifacts/batch-21a-onboarding-chat-ux-react19.md`) already prescribed shadcn/ui as the component library and identified a shared chat-primitive set. This prompt does not re-open the library choice — only its theming/wiring.

## Why It Matters

This is the foundation. The palette is decided and the component library is decided, but the *integration* — shadcn's CSS-variable theming × Tailwind v4's CSS-first `@theme` × the Catppuccin hybrid tokens × the dark-mode mechanism — has real, non-obvious choices. Getting the token architecture wrong means re-theming every component later, after surfaces have multiplied across Slices 2b–4. Tailwind v4's CSS-first config and shadcn's 2025–26 v4 migration both diverge sharply from older training data, so this genuinely needs current sources rather than recalled patterns. The project owner is explicitly not a designer — the recommendation must be a closed, mechanical, enumerable setup, not an open-ended design system.

## Deliverables

- **Recommended `index.css` token-layer structure** — the primitive tier (Catppuccin values) and the semantic tier (shadcn's variables), light and dark, with the exact `@theme` / `@custom-variant` wiring.
- **The shadcn/ui init procedure on Tailwind v4** — what to run, what it generates, what to adjust for Vite 8 + React 19.2.
- **Dark-mode controller recommendation** — mechanism, the no-flash approach, in-app toggle vs OS-follow for MVP-0.
- **Contrast-rule enforcement mechanism** — how to keep WCAG AA from regressing silently, with the candidate Catppuccin pair ratios reported.
- **Accent-derivation method** — how to compute the one AA-safe fill shade and map it to `--primary` / `--primary-foreground`.
- **Component-install list** with the Radix footprint and gzipped bundle delta.
- **Migration shape** for converting a hand-rolled RHF + Zod form to shadcn `Form` without behaviour change.
- **DEC-063 reconciliation** — `tw-animate-css` consistency and the `motion-reduce:` parity pattern.
- **One reference precedent** — a real 2026 project demonstrating the recommended setup, linked.
- One **default + fallback** wherever a choice is contested.

## Out of scope

- The Slice 2b feature UI (log form, today's-workout card, history list) — built on this foundation later.
- Calendar / date-picker and chat-UI component libraries — chosen by the slices that need them.
- The accent *family* pick (green / teal / …) — the project picks from candidates; research specifies only the derivation method.
- Animation-library adoption — DEC-063 holds; do not re-propose `motion`.
- The component-library choice itself — settled by R-065.

The artifact lands at `docs/research/artifacts/batch-26a-frontend-design-system-theming-integration.md` and integrates into the Slice 2a spec plus a new decision-log entry locking the token architecture and the dark-mode mechanism.
