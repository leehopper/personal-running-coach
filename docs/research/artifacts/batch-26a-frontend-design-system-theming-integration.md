# R-075 — Frontend Design-System Theming Integration

**Artifact:** `docs/research/artifacts/batch-26a-frontend-design-system-theming-integration.md`
**Date:** 2026-05-18
**Status:** Draft for ADR input
**Scope:** shadcn/ui × Tailwind CSS v4 × Catppuccin-derived hybrid tokens × class-based dark mode on React 19.2 + Vite 8 SPA (TypeScript-strict).

---

## 1. TL;DR

1. Adopt the **shadcn/ui Tailwind-v4 canonical layout**: a single `src/index.css` with `@import "tailwindcss";` → `@import "tw-animate-css";` → `@custom-variant dark (&:is(.dark *));` → two CSS layers of variables (`:root` light / `.dark` dark) → one `@theme inline` block that re-exposes the variables as Tailwind utility tokens. No `tailwind.config.ts`. (Source: ui.shadcn.com/docs/theming.)
2. **Two-tier tokens**: a *primitive* tier `--ctp-*` carrying raw Catppuccin (Latte for `:root`, Mocha for `.dark`) sits below a *semantic* tier (`--background`, `--foreground`, `--primary`, …) that shadcn primitives consume. Dark mode swaps the *primitive* tier; semantic mappings remain stable.
3. **Catppuccin source of truth**: paste hex values from `catppuccin/palette` (palette.json v1.7.1) into the primitive tier as static CSS variables. Do **not** depend on `@catppuccin/tailwindcss` for the foundation — it ships its own `ctp-*` utility classes at the Tailwind layer rather than mapping into shadcn's semantic slots. **Fallback**: `@catppuccin/tailwindcss@1.0.0` (release tag dated "26 Jul [2025] 00:47 · github-actions" on github.com/catppuccin/tailwindcss/releases, release notes: "support Tailwind v4 (#22) — Thank you to @unseen-ninja who did majority of the work on supporting Tailwind v4") if/when accent utilities are desired.
4. **Dark-mode mechanism**: shadcn's documented Vite recipe — `ThemeProvider` (~40 LOC, `useState` + `localStorage` + `prefers-color-scheme`) toggling `.dark` on `documentElement`, plus an **inline no-flash script** in `index.html`. For MVP-0 single-user app: ship `defaultTheme="system"` plus a small in-app toggle in Settings; do not gate the foundation on OS-only.
5. **The contrast rule is encoded in the tokens, not a linter**: the only tokens mapped to text roles (`--foreground`, `--card-foreground`, `--primary-foreground`, `--muted-foreground`, `--popover-foreground`) point at Catppuccin `text` or `subtext1`. `--muted-foreground` maps to `subtext1` in Latte because Latte's `subtext0`-on-`base` is 4.37:1 (AA-normal fail by 0.13). In Mocha, headroom is wide enough that `subtext0` is also safe. Misuse is caught by review against the named primitive tier, plus an optional `stylelint-declaration-strict-value` rule restricting `color` to `var(--*-foreground)` / `var(--ctp-text)` / `var(--ctp-subtext1)`.
6. **Accent**: pick the Catppuccin accent family (project's pick — green/teal/etc.) and ship a single **statically committed OKLCH-derived shade** per mode for `--primary`. Hue is held constant across modes; lightness is the only lever. `--primary-foreground` is `crust` in dark mode (≥12:1 with Mocha green) and a near-white `oklch(0.985 0 0)` in light mode. **Default**: static committed OKLCH. **Fallback**: `color-mix(in oklch, …)` at runtime.
7. **Animation baseline (DEC-063 reconciliation)**: `tw-animate-css` is a CSS-utility import only — zero JS. Per ui.shadcn.com/docs/tailwind-v4 ("March 19, 2025 — Deprecate tailwindcss-animate"): "We've deprecated tailwindcss-animate in favor of tw-animate-css. New projects will have tw-animate-css installed by default." It is **fully consistent with DEC-063**. The `data-[state=open]:animate-in` patterns carry `motion-reduce:` cleanly because they are class-state, not JS animations. Establish a project rule: every shadcn primitive that ships `animate-in/animate-out` classes gets a sibling `motion-reduce:animate-none motion-reduce:transition-none` on the same element.
8. **Component-install set (MVP-0 + Slice 2b)**: `button input label form card collapsible dialog sonner badge radio-group scroll-area`. Six distinct `@radix-ui/react-*` packages + `sonner`. Gzipped runtime delta ~32–35 kB (Dialog's `react-remove-scroll` dominates).
9. **Migration shape**: shadcn `Form/FormField/FormControl/FormMessage` is a thin wrapper over `react-hook-form`'s `FormProvider` + `Controller` + a context that wires `aria-*` ids to error/description. The existing `useForm({ resolver: zodResolver(schema) })` + `form.handleSubmit(onSubmit)` is preserved verbatim — only the JSX wrapping changes. Per-component effort: 15–25 minutes per form, **zero** validation-behaviour change, **zero** request-wire-shape change.
10. **Typography & spacing**: keep Tailwind's default spacing/type/radius scales. Only changes: set `--font-sans` in `@theme` to a system stack; leave `--radius` at shadcn's default `0.625rem`. That's it.

---

## 2. Defaults + Fallbacks Table

| Decision | Default | Fallback | Rationale |
|---|---|---|---|
| Init command | `npx shadcn@latest init` | `npx shadcn@canary init` | `@latest` has been stable for v4 since Feb 2025; canary kept as escape hatch. |
| Style | `new-york` (auto-selected by CLI) | `default` (deprecated) | Per ui.shadcn.com/docs/changelog/2025-02-tailwind-v4: "We're deprecating the default style. New projects will use new-york. HSL colors are now converted to OKLCH." |
| Color format | OKLCH | HSL | shadcn switched defaults to OKLCH in Feb 2025; HSL still works. |
| Tailwind config | CSS-first (`@theme` in `index.css`, no `tailwind.config.ts`) | `tailwind.config.ts` via `@config` | v4 is CSS-first; the JS config is escape-hatch only. |
| Catppuccin integration | Hex pasted into primitive tier in `index.css` | `@catppuccin/tailwindcss@^1.0.0` | Two-tier control beats utility-class theming for shadcn semantics. |
| Dark-mode mechanism | shadcn's `ThemeProvider` (Vite docs) + inline no-flash script | `useTheme` hook only (no Context) — see shadcn-ui/ui #2080 sketch | Provider is documented, typed, ~30 LOC. |
| Dark-mode default | `defaultTheme="system"` with toggle visible in Settings | `defaultTheme="dark"` and no toggle | MVP-0 still benefits from OS-follow + manual override. |
| `--dark` variant selector | `@custom-variant dark (&:is(.dark *));` | `@custom-variant dark (&:where(.dark, .dark *));` | shadcn's canonical pattern; both work. |
| Animation lib | `tw-animate-css` (CSS-only) | `tailwindcss-animate` (deprecated in v4) | shadcn deprecated `tailwindcss-animate` Mar 19, 2025. |
| Form lib | RHF 7 + Zod 4 wired through shadcn `Form` | New `<Controller>`+`<Field>` form pattern documented at ui.shadcn.com/docs/forms/react-hook-form | Existing project already uses RHF+Zod. |
| Accent derivation | Static committed OKLCH value | Runtime `color-mix(in oklch, …)` | Committed value is reviewable, grep-able, and CI-assertable. |
| Vite React plugin | `@vitejs/plugin-react@^6` | `@vitejs/plugin-react@^5` | Per vite.dev/blog/announcing-vite8 (Mar 12, 2026): "Alongside Vite 8, we are releasing @vitejs/plugin-react v6. The plugin uses Oxc for React Refresh transform. Babel is no longer a dependency and the installation size is smaller." v5 still works on Vite 8. |
| Contrast enforcement | Named-token convention + code-review checklist + optional Stylelint `declaration-strict-value` | CI script that computes WCAG ratios for each semantic pair from committed primitives | Linting can't see CSS-variable downstream values directly. |

---

## 3. Sub-question answers

### 3.1 shadcn/ui on Tailwind v4 — what `npx shadcn@latest init` produces (2026)

The shadcn CLI (latest, post-2.x) on a Vite + React 19 + Tailwind v4 project produces:

- A `components.json` with `tailwind.config: ""` (empty — v4 is CSS-first), `style: "new-york"`, `tailwind.css: "src/index.css"`, `tailwind.baseColor: "neutral"` (one of `neutral | stone | zinc | mauve | olive | mist | taupe`), `cssVariables: true`, `iconLibrary: "lucide"`, and the standard `aliases` block. (Source: ui.shadcn.com/docs/components-json; ui.shadcn.com/docs/installation/manual.)
- A `src/lib/utils.ts` exporting `cn` (`twMerge(clsx(inputs))`). (Source: ui.shadcn.com/docs/installation/manual.)
- A rewritten `src/index.css` containing `@import "tailwindcss"; @import "tw-animate-css"; @custom-variant dark (&:is(.dark *));` and the full default neutral theme scaffold inside `:root` + `.dark` plus a single `@theme inline` block. (Source: ui.shadcn.com/docs/theming, "Default Theme CSS" reference.)
- Devdep additions: `class-variance-authority`, `clsx`, `tailwind-merge`, `lucide-react`, `tw-animate-css`. Per ui.shadcn.com/docs/tailwind-v4 ("March 19, 2025 — Deprecate tailwindcss-animate"): "We've deprecated tailwindcss-animate in favor of tw-animate-css. New projects will have tw-animate-css installed by default."
- **No** `forwardRef` in primitive sources — every primitive renders a `data-slot="…"` attribute and accepts `React.ComponentProps<typeof Primitive>`. (Source: ui.shadcn.com/docs/tailwind-v4: "We've removed the forwardRefs and adjusted the types. Every primitive now has a data-slot attribute for styling.")
- **No** generated `tailwind.config.ts` — `tailwind.config` in `components.json` is left empty for v4. (Source: ui.shadcn.com/docs/components-json: "For Tailwind CSS v4, leave this blank.")

**Known breakage / sharp edges on Vite 8 + React 19.2 + TS 5.9-strict:**
- `tsconfig.json` *and* `tsconfig.app.json` both need a `paths` entry for `@/*`; Vite splits the TS config and shadcn checks both. (Verified pattern in shadcn-ui/ui issue #6784.)
- `@vitejs/plugin-react@^6` (Mar 2026, alongside Vite 8) drops Babel and uses Oxc for the React Refresh transform; v5 still works on Vite 8. Choose v6 unless you specifically need the Babel pipeline for React Compiler.
- The shadcn-docs Vite `ThemeProvider` has two minor TS-strict warnings around `localStorage.getItem(...) as Theme` (should be `as Theme | null`) and a `context === undefined` check (context is non-undefined by initialisation). Easy patch — see shadcn-ui/ui PR #6413 and the local copy below in §4.3.

### 3.2 Token architecture — layering Catppuccin under shadcn's semantic tokens

shadcn's canonical v4 layout, recapped: variables live in plain `:root` / `.dark` blocks (no `@layer base`), values stay as raw OKLCH/hex/HSL strings, and a single `@theme inline { --color-foo: var(--foo); … }` block re-exposes them as Tailwind utilities. (Source: ui.shadcn.com/docs/theming, "Default Theme CSS" reference.)

`@theme inline` is **important**: without `inline`, Tailwind tries to resolve the variable at build time and freezes the value (so `.dark` never wins); with `inline`, the utility class compiles to `color: var(--foo)` and the cascade does the work at runtime. (Source: tailwindcss.com `@theme` reference; reiterated by shadcn theming docs.)

**Recommended two-tier layering** — keep it as a single `src/index.css` with three labelled sections (file shown in §4.1):
1. **Primitive tier** in `:root` (Catppuccin Latte) + `.dark` (Catppuccin Mocha). Variables prefixed `--ctp-*`.
2. **Semantic tier** in `:root` (and explicitly re-declared in `.dark` for self-documentation). Variables: `--background`, `--foreground`, `--primary`, `--card`, `--card-foreground`, `--popover`, `--popover-foreground`, `--primary-foreground`, `--secondary`, `--secondary-foreground`, `--muted`, `--muted-foreground`, `--accent`, `--accent-foreground`, `--destructive`, `--destructive-foreground`, `--border`, `--input`, `--ring`, `--radius`. Each maps to a `--ctp-*` primitive.
3. **`@theme inline`** block that exposes the semantic tier as `--color-*` (Tailwind reads `--color-foo` to generate `bg-foo` / `text-foo` utilities).

The semantic tier is intentionally redeclared in `.dark` to be **explicit**: a reader can see the slot mapping in one place and only the primitive references differ. The mappings themselves are mode-invariant.

### 3.3 Catppuccin integration mechanism

`@catppuccin/tailwindcss@1.0.0` (release tag dated "26 Jul [2025] 00:47 · github-actions" on github.com/catppuccin/tailwindcss/releases, release notes: "support Tailwind v4 (#22) — Thank you to @unseen-ninja who did majority of the work on supporting Tailwind v4") supports Tailwind v4 via `@import "@catppuccin/tailwindcss/mocha.css"` and prefixes every color with `ctp-*` so utilities look like `bg-ctp-base text-ctp-text`. It also exposes `.latte/.frappe/.macchiato/.mocha` selector classes for forcing flavours. (Source: github.com/catppuccin/tailwindcss README.)

**Recommendation — DEFAULT**: do **not** use `@catppuccin/tailwindcss` for R-075's primitive tier. The package's value-add is the full 26-color palette as ergonomic Tailwind utilities (`bg-ctp-mauve hover:bg-ctp-mauve-400`), which is the *opposite* of what we want — we want a **closed** primitive tier in our CSS file that maps into shadcn's semantic slots and is auditable in one place. Paste the 12 neutral-ramp hex codes + one accent hex directly. The values are stable (palette v1.7.1 has not changed the neutral steps since Catppuccin v0.2.0 per catppuccin/catppuccin release notes).

**Recommendation — FALLBACK**: install `@catppuccin/tailwindcss@^1.0.0` once we want non-text accents (chart colors, semantic status colors). The package and our inline tokens are not mutually exclusive.

The `@catppuccin/palette@^1.7.1` npm package is also available if we want a build-time TypeScript-typed source of hex codes (`flavors.latte.colors.text.hex`), useful for a future codegen step. (Source: npmjs.com/package/@catppuccin/palette.)

### 3.4 Dark-mode mechanism

shadcn ships an exact Vite recipe at ui.shadcn.com/docs/dark-mode/vite. It uses:
- `Theme = "dark" | "light" | "system"`,
- `useState<Theme>(() => (localStorage.getItem(storageKey) as Theme) || defaultTheme)`,
- a `useEffect` that removes both classes from `<html>` and adds either the explicit theme or the system-matched one, plus
- a `value.setTheme` that writes to `localStorage` and `setTheme`.

**Class-toggle vs media-query-only**: media-query-only (`@media (prefers-color-scheme: dark)`) is simpler but cannot honour an in-app override. shadcn's class-based approach is required because `@custom-variant dark (&:is(.dark *))` keys off the class. (Source: shadcn theming docs.)

**Recommendation for MVP-0**: ship the provider with `defaultTheme="system"` and include a small Settings-page toggle (3-state: Light / Dark / System). The toggle is ~10 LOC; not shipping it means a user whose OS disagrees with their preference cannot fix it. The contract is the same whether MVP-0 has one user or one thousand.

**No-flash inline script**: shadcn's published provider does not ship a no-flash script (it runs the toggle in `useEffect`, which fires after first paint). For Vite + React 19, the canonical FOUC fix is an inline `<script>` in `index.html` *before* `<div id="root">`. Pattern in §4.4 below; the structure matches the "toggle inline in head to avoid FOUC" pattern documented for Tailwind dark-mode setups (openreplay.com, Astro-Tailwind tutorials, and adapted to Vite's `index.html`).

**Interaction with two-tier tokens**: the `.dark` block re-points the **primitive tier** (`--ctp-base`, `--ctp-text`, etc.) from Latte hexes to Mocha hexes. The semantic tier (`--background: var(--ctp-base)`, etc.) is identical between modes. This is the design property that makes the foundation closed: future re-themes (e.g., a high-contrast mode) only need a new primitive block, never a touch to component code.

### 3.5 Contrast rule — making it enforceable, not just documented

**The hard data** (WCAG 2.x relative-luminance method; full table in §6 below):

For **Latte (light) text on neutral surfaces**:
- `text on base`: **7.06** — AA + AAA ✅ (borderline AAA)
- `text on mantle`: **6.57** — AA ✅, AAA ❌
- `subtext1 on base`: **5.54** — AA ✅
- `subtext0 on base`: **4.37** — **AA-normal FAIL** (by 0.13)
- `overlay2 on base`: 3.50 — large/UI only
- `overlay1/overlay0 on base`: 2.83 / 2.30 — unsafe

For **Mocha (dark)** the contrast headroom is much wider; even `overlay2 on base` clears AA-normal (5.81). `overlay1 on base` is 4.44 — large-text only.

**Conclusion**: the rule "text only maps to `text` or `subtext1`, never `overlay*`" is correct and **necessary** for Latte. In Latte, `subtext0` is also a tertiary-text fail risk — it is borderline (4.37) and should be flagged as **large text or non-text only**. Adopt the stricter form: in Latte, `--muted-foreground` maps to `subtext1`; in Mocha, `--muted-foreground` may map to `subtext0` if needed for design contrast.

**Mechanical enforcement options** (best to worst):
1. **Token-naming + review** (default). The primitive tier names (`--ctp-overlay1`) make misuse obvious in code review. Components never use `--ctp-*` directly — only `--foreground` / `--muted-foreground` / etc. Catch: a developer can still write `color: var(--ctp-overlay1)` in component CSS.
2. **`stylelint-declaration-strict-value`** — restrict the `color` and `background-color` declarations in component CSS to `var(--*)` and reject raw hex. Stops the broad class of regression.
3. **`stylelint-plugin-defensive-css`** + a custom rule that bans `var(--ctp-overlay*)` outside of the primitive/semantic sections of `index.css`. Effectively closes the gap.
4. **Build-time contrast assertion**: a small script (`scripts/check-contrast.ts`) that imports `@catppuccin/palette`, computes WCAG ratios for each `(--*-foreground, --*)` pair in both modes, and exits non-zero if any falls below 4.5 (3.0 for `--ring`/borders). Run in CI; treats the JSON of tokens as machine-readable. **Recommended addition** for the MVP-0 milestone.

For WCAG-AA regression prevention more broadly, 2026 design-token setups (GitLab Pajamas, Atlassian, Kong design tokens) rely on: (a) a single source-of-truth tokens file, (b) named "approved" foreground/background pairs in design tokens (the `-foreground` shadcn convention is exactly this), (c) automated contrast in CI. None of these tools auto-derive contrast from CSS variables — that's what the contrast-assertion script is for.

### 3.6 The accent — deriving one contrast-safe solid-fill shade

The contrast probes (§6 below): **Catppuccin Latte `green` (#40a02b)** has only 3.35:1 against white and 2.53:1 against Latte `crust` — **fails AA-normal in both directions**. Catppuccin Mocha `green` (#a6e3a1) has 11.03:1 against Mocha `base` and 12.62:1 against Mocha `crust` — comfortably AAA. This asymmetry is the core problem for any Catppuccin-derived accent.

**Recommended derivation method (default)**: **static OKLCH lightness adjustment, committed**. Steps:
1. Pick the accent family (project decision — let's call it `accent`).
2. Take the Mocha accent hex → that becomes `--primary` in `.dark`. Pair with `--primary-foreground = var(--ctp-crust)` (dark text on light accent) — verified AA/AAA.
3. Take the Latte accent hex → it will likely **not** pass AA. Convert to OKLCH and **reduce L** until the pair against white reaches ≥4.5. For Catppuccin Latte `green` (#40a02b ≈ `oklch(0.62 0.17 142)`), lowering L to ~0.48 gives ~`oklch(0.48 0.17 142)` ≈ `#2f7a1f` with a contrast of ~5.0 on white. Commit that hex as `--primary` in `:root`; pair with `--primary-foreground = oklch(0.985 0 0)` (near-white).
4. The hue is held constant across modes (`h` channel of OKLCH); only L moves.

**Why static-committed beats build-time computed**: the value can be code-reviewed, tested with WebAIM/Pa11y, and is one git-grep away from any future re-theme. `color-mix(in oklch, var(--ctp-green) 80%, black)` works in modern browsers but introduces a runtime moving target that's hard to assert against. Reserve `color-mix` as the fallback if the project ever wants multiple accent families behind a single token name.

**Should `--primary` keep the same hue across modes?** Yes — same hue (h), different lightness (L). Saturation (C) often needs a small bump in dark mode to keep perceived chroma equal. Per the LogRocket and arXiv 2025/2026 OKLCH work, holding hue stable while moving L per mode is the canonical pattern.

**Map to shadcn slots**:
```css
:root { --primary: oklch(0.48 0.17 142); --primary-foreground: oklch(0.985 0 0); }
.dark { --primary: #a6e3a1;              --primary-foreground: var(--ctp-crust); }
```

### 3.7 Migration shape — hand-rolled RHF+Zod → shadcn `Form`

shadcn's `Form` is `react-hook-form`'s `FormProvider`. `FormField` is a `Controller` wrapper that pushes the field name into context. `FormItem` provides ids for label/description/message wiring (so `aria-describedby` resolves correctly). `FormControl` is a `Slot` that splices `aria-invalid` and `aria-describedby` onto the inner input. `FormMessage` reads `error?.message` from the field state and renders it (or `null`). (Source: shadcn-ui/ui — `apps/www/registry/new-york-v4/ui/form.tsx`, also at v3.shadcn.com/docs/components/form.)

**The crucial property**: shadcn `Form` adds **zero** validation or submission logic. `useForm({ resolver: zodResolver(schema) })` and `form.handleSubmit(onSubmit)` are unchanged. The request wire shape is unchanged. The Zod schema is unchanged. The change is purely the JSX wrapping.

**Per-surface effort estimate (existing surfaces, this codebase)**:

| Surface | Fields | Effort | Notes |
|---|---|---|---|
| Login | email, password | 15 min | trivial |
| Register | email, password, confirm | 20 min | confirm wiring is identical |
| Onboarding turn-input ×5 | varies (number/text/select) | 30 min each | reuse a `<FormField>` row helper |
| Onboarding chat | n/a (no form here; message bubbles only) | 1–2 h | wrap bubbles in `Card`/`ScrollArea` |
| Settings | preferences toggles | 30–45 min | introduce `RadioGroup` for theme select |
| Home | n/a (display only) | 1 h | `Card` + `Button` wrap |

Total foundation migration: **~6–8 hours** of focused work, including the `index.css` rewrite and the no-flash script. Validation behaviour and the request wire shape are byte-identical.

### 3.8 Component set & Radix footprint

Install: **`button input label form card collapsible dialog sonner badge radio-group scroll-area`** (11 components). This covers the existing surfaces and the Slice 2b near-term needs (form + collapsible "more details" + card + list-via-ScrollArea).

Per-component Radix dependency footprint and bundle delta (per npm + bundlephobia, May 2026): see §5 below.

**Note on `class-variance-authority` + `clsx` + `tailwind-merge`**: these are shadcn's hard deps and total ~3 kB gzipped together. Already included in the §5 deltas.

### 3.9 Reduced-motion / DEC-063 reconciliation

`tw-animate-css` is a **CSS-only** import (`@import "tw-animate-css"` in `index.css`) that defines a set of utility classes (`animate-in`, `fade-in-0`, `zoom-in-95`, `slide-in-from-top-2`, plus parameter classes like `duration-150`). No JavaScript runtime; no animation engine; nothing competing with Tailwind's own `motion-*` variants. (Source: github.com/Wombosvideo/tw-animate-css README: "This package is a replacement for tailwindcss-animate. It embraces the new CSS-first architecture, providing a pure CSS solution for adding animation capabilities to your Tailwind CSS project without relying on the legacy JavaScript plugin system.") This is fully consistent with **DEC-063**'s "Tailwind-only animation baseline" — `tw-animate-css` is the v4 evolution of `tailwindcss-animate`, written in pure CSS.

**The `motion-reduce:` parity pattern**: Tailwind's `motion-reduce:` variant compiles to `@media (prefers-reduced-motion: reduce)`. The canonical shadcn pattern uses `data-[state=open]:animate-in` to gate animations on the Radix `data-state` attribute. To carry `motion-reduce:` parity, add a sibling class:

```tsx
<DialogContent className={cn(
  "data-[state=open]:animate-in data-[state=closed]:animate-out",
  "fade-in-0 zoom-in-95 fade-out-0 zoom-out-95",
  "data-[state=open]:duration-200 data-[state=closed]:duration-150",
  // motion-reduce parity: disable the keyframes entirely
  "motion-reduce:animate-none motion-reduce:transition-none"
)}>
```

For shadcn primitives that ship with `animate-in` baked into the source (Dialog, Popover, Sheet, DropdownMenu, etc.), patch each at install time to add the `motion-reduce:` suffix. Centralise the pattern in a `cn`-able constant (`MOTION_REDUCE_OFF`) used everywhere.

A **stricter variant** (per the Tailwind GitHub discussion #18596) is to override `@custom-variant motion-reduce` to additionally honour a `[data-reduce-motion]` attribute on `<html>`, letting a user opt out of motion in-app independent of OS preference. Defer this until DEC-063 is revisited.

### 3.10 Typography & spacing

Tailwind v4's `@theme` exposes spacing/type/radius via variables (`--spacing-*`, `--text-*`, `--radius-*`). shadcn's default `--radius` is `0.625rem`, exposed as `--radius-sm/md/lg/xl` derivations. (Source: ui.shadcn.com/docs/theming "Radius Scale".)

**Recommendation**: change exactly one thing — set `--font-sans` and `--font-mono` to system stacks in `@theme`. Keep `--radius` at the shadcn default unless the project explicitly dislikes the rounding. Do not change Tailwind's default spacing or type scales; they are well-calibrated and changing them ripples through every shadcn primitive.

### 3.11 Reference precedents

**Recommended precedent**: `arthur404dev/tailwindcss-shadcn-catppuccin-theme` (github.com/arthur404dev/tailwindcss-shadcn-catppuccin-theme) — a plugin that maps Catppuccin Latte (`:root`) and Mocha (`.dark`) hex values into shadcn's semantic CSS variables. It is the closest public example of the exact integration we are recommending, but as a plugin rather than as inline tokens. Read it for the shape of the mapping; do not depend on it — we want our primitive tier inline and code-reviewable, not behind a plugin.

Secondary references for cross-checking:
- **`shadcn/app-tailwind-v4`** (github.com/shadcn/app-tailwind-v4) — official shadcn v4 example application.
- **`catppuccin/tailwindcss@v1.0.0`** (github.com/catppuccin/tailwindcss) — official Tailwind v4 Catppuccin integration; demonstrates the `.latte/.mocha` flavour-class pattern that we mirror with `.dark`.
- **shadcn-ui/ui discussion #13211** (`tailwindlabs/tailwindcss#13211`) — foundational thread that produced the `@theme inline` + `@custom-variant dark` mapping pattern now in the shadcn docs.

---

## 4. Concrete artifacts

### 4.1 Full recommended `src/index.css`

```css
/* src/index.css
 * R-075 — shadcn/ui × Tailwind v4 × Catppuccin-derived hybrid tokens.
 * Order matters: tailwind first, then animate, then variant, then variables,
 * then @theme inline mapping last.
 */
@import "tailwindcss";
@import "tw-animate-css";

@custom-variant dark (&:is(.dark *));

/* ------------------------------------------------------------------ */
/* 1. PRIMITIVE TIER — raw Catppuccin Latte/Mocha neutral ramps.       */
/*    Values: catppuccin/palette palette.json v1.7.1 (verified 2026-05).*/
/*    NEVER reference these from components directly. Use the semantic */
/*    tier below.                                                       */
/* ------------------------------------------------------------------ */
:root {
  /* Catppuccin LATTE neutrals */
  --ctp-text:       #4c4f69;
  --ctp-subtext1:   #5c5f77;
  --ctp-subtext0:   #6c6f85;
  --ctp-overlay2:   #7c7f93;
  --ctp-overlay1:   #8c8fa1;
  --ctp-overlay0:   #9ca0b0;
  --ctp-surface2:   #acb0be;
  --ctp-surface1:   #bcc0cc;
  --ctp-surface0:   #ccd0da;
  --ctp-base:       #eff1f5;
  --ctp-mantle:     #e6e9ef;
  --ctp-crust:      #dce0e8;
  /* Accent — Latte: contrast-derived dark green (h held; L reduced).
   * Source: Catppuccin Latte green = #40a02b → oklch(0.62 0.17 142).
   * Derived: oklch(0.48 0.17 142) ≈ #2f7a1f for AA-normal on white. */
  --ctp-accent:           oklch(0.48 0.17 142);
  --ctp-accent-fg:        oklch(0.985 0 0); /* near-white */
}

.dark {
  /* Catppuccin MOCHA neutrals */
  --ctp-text:       #cdd6f4;
  --ctp-subtext1:   #bac2de;
  --ctp-subtext0:   #a6adc8;
  --ctp-overlay2:   #9399b2;
  --ctp-overlay1:   #7f849c;
  --ctp-overlay0:   #6c7086;
  --ctp-surface2:   #585b70;
  --ctp-surface1:   #45475a;
  --ctp-surface0:   #313244;
  --ctp-base:       #1e1e2e;
  --ctp-mantle:     #181825;
  --ctp-crust:      #11111b;
  /* Accent — Mocha: native Catppuccin green; foreground is dark crust. */
  --ctp-accent:           #a6e3a1;
  --ctp-accent-fg:        var(--ctp-crust);
}

/* ------------------------------------------------------------------ */
/* 2. SEMANTIC TIER — shadcn's named tokens. These are what components */
/*    consume. Identical mappings in both modes; the swap happens at   */
/*    the primitive tier.                                              */
/* ------------------------------------------------------------------ */
:root {
  --radius:                 0.625rem;

  --background:             var(--ctp-base);
  --foreground:             var(--ctp-text);

  --card:                   var(--ctp-mantle);
  --card-foreground:        var(--ctp-text);

  --popover:                var(--ctp-mantle);
  --popover-foreground:     var(--ctp-text);

  --primary:                var(--ctp-accent);
  --primary-foreground:     var(--ctp-accent-fg);

  --secondary:              var(--ctp-surface0);
  --secondary-foreground:   var(--ctp-text);

  --muted:                  var(--ctp-surface0);
  --muted-foreground:       var(--ctp-subtext1); /* CONTRAST RULE: never overlay* */

  --accent:                 var(--ctp-surface1);
  --accent-foreground:      var(--ctp-text);

  --destructive:            oklch(0.577 0.245 27.325);
  --destructive-foreground: oklch(0.985 0 0);

  --border:                 var(--ctp-surface1);
  --input:                  var(--ctp-surface1);
  --ring:                   var(--ctp-accent);
}

.dark {
  /* Semantic mapping is identical to :root; only the primitives differ.
   * Re-declared for explicitness — single place to read the slot map. */
  --background:             var(--ctp-base);
  --foreground:             var(--ctp-text);
  --card:                   var(--ctp-mantle);
  --card-foreground:        var(--ctp-text);
  --popover:                var(--ctp-mantle);
  --popover-foreground:     var(--ctp-text);
  --primary:                var(--ctp-accent);
  --primary-foreground:     var(--ctp-accent-fg);
  --secondary:              var(--ctp-surface0);
  --secondary-foreground:   var(--ctp-text);
  --muted:                  var(--ctp-surface0);
  --muted-foreground:       var(--ctp-subtext1);
  --accent:                 var(--ctp-surface1);
  --accent-foreground:      var(--ctp-text);
  --destructive:            oklch(0.577 0.245 27.325);
  --destructive-foreground: oklch(0.985 0 0);
  --border:                 var(--ctp-surface1);
  --input:                  var(--ctp-surface1);
  --ring:                   var(--ctp-accent);
}

/* ------------------------------------------------------------------ */
/* 3. TAILWIND THEME — exposes the semantic tokens as utility classes  */
/*    (bg-background, text-foreground, …). `inline` is mandatory so    */
/*    the variables resolve at runtime (not build time).                */
/* ------------------------------------------------------------------ */
@theme inline {
  --font-sans: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont,
               "Segoe UI", Roboto, "Helvetica Neue", Arial,
               "Apple Color Emoji", "Segoe UI Emoji";
  --font-mono: ui-monospace, SFMono-Regular, Menlo, Monaco, "Cascadia Mono",
               "Roboto Mono", monospace;

  --radius-sm: calc(var(--radius) - 4px);
  --radius-md: calc(var(--radius) - 2px);
  --radius-lg: var(--radius);
  --radius-xl: calc(var(--radius) + 4px);

  --color-background:             var(--background);
  --color-foreground:             var(--foreground);
  --color-card:                   var(--card);
  --color-card-foreground:        var(--card-foreground);
  --color-popover:                var(--popover);
  --color-popover-foreground:     var(--popover-foreground);
  --color-primary:                var(--primary);
  --color-primary-foreground:     var(--primary-foreground);
  --color-secondary:              var(--secondary);
  --color-secondary-foreground:   var(--secondary-foreground);
  --color-muted:                  var(--muted);
  --color-muted-foreground:       var(--muted-foreground);
  --color-accent:                 var(--accent);
  --color-accent-foreground:      var(--accent-foreground);
  --color-destructive:            var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-border:                 var(--border);
  --color-input:                  var(--input);
  --color-ring:                   var(--ring);
}

/* ------------------------------------------------------------------ */
/* 4. Body defaults                                                     */
/* ------------------------------------------------------------------ */
@layer base {
  * { border-color: var(--border); }
  body { background-color: var(--background); color: var(--foreground); }
}
```

### 4.2 shadcn init command sequence & resulting `components.json`

```bash
# Prereqs (already met in this project):
# - React 19.2.6, Vite 8.0.11, Tailwind v4.2.2, TS 5.9.3
# - tsconfig.json AND tsconfig.app.json both have:
#     "paths": { "@/*": ["./src/*"] }
# - vite.config.ts has @ alias to ./src and the @tailwindcss/vite plugin.

# 1. Initialize shadcn. Prompts: style → new-york; baseColor → neutral;
#    cssVariables → yes; iconLibrary → lucide.
npx shadcn@latest init

# 2. Replace src/index.css with the file in §4.1 above
#    (the CLI's scaffold is a starting point; ours is the canonical one).

# 3. Add the components.
npx shadcn@latest add button input label form card collapsible dialog sonner badge radio-group scroll-area
```

Resulting `components.json`:
```json
{
  "$schema": "https://ui.shadcn.com/schema.json",
  "style": "new-york",
  "rsc": false,
  "tsx": true,
  "tailwind": {
    "config": "",
    "css": "src/index.css",
    "baseColor": "neutral",
    "cssVariables": true,
    "prefix": ""
  },
  "aliases": {
    "components": "@/components",
    "utils": "@/lib/utils",
    "ui": "@/components/ui",
    "lib": "@/lib",
    "hooks": "@/hooks"
  },
  "iconLibrary": "lucide"
}
```

### 4.3 `src/components/theme-provider.tsx`

```tsx
import { createContext, useContext, useEffect, useState } from "react";

type Theme = "dark" | "light" | "system";

type ThemeProviderProps = {
  children: React.ReactNode;
  defaultTheme?: Theme;
  storageKey?: string;
};

type ThemeProviderState = {
  theme: Theme;
  setTheme: (theme: Theme) => void;
};

const ThemeProviderContext = createContext<ThemeProviderState | undefined>(
  undefined,
);

export function ThemeProvider({
  children,
  defaultTheme = "system",
  storageKey = "vite-ui-theme",
}: ThemeProviderProps) {
  const [theme, setTheme] = useState<Theme>(
    () => (localStorage.getItem(storageKey) as Theme | null) ?? defaultTheme,
  );

  useEffect(() => {
    const root = window.document.documentElement;
    root.classList.remove("light", "dark");
    const next =
      theme === "system"
        ? window.matchMedia("(prefers-color-scheme: dark)").matches
          ? "dark"
          : "light"
        : theme;
    root.classList.add(next);
  }, [theme]);

  const value: ThemeProviderState = {
    theme,
    setTheme: (t) => {
      localStorage.setItem(storageKey, t);
      setTheme(t);
    },
  };

  return (
    <ThemeProviderContext.Provider value={value}>
      {children}
    </ThemeProviderContext.Provider>
  );
}

export const useTheme = (): ThemeProviderState => {
  const ctx = useContext(ThemeProviderContext);
  if (!ctx) throw new Error("useTheme must be used within a ThemeProvider");
  return ctx;
};
```

### 4.4 No-flash inline script for `index.html`

Place in `<head>`, *before* `<script type="module" src="/src/main.tsx">`:

```html
<script>
  (function () {
    try {
      var k = "vite-ui-theme";
      var t = localStorage.getItem(k);
      var sysDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
      var dark = t === "dark" || ((t === null || t === "system") && sysDark);
      document.documentElement.classList.add(dark ? "dark" : "light");
    } catch (e) { /* localStorage may throw in private mode; default to light */ }
  })();
</script>
```

### 4.5 Migrated form (before → after)

**Before** (hand-rolled login form):
```tsx
const { register, handleSubmit, formState: { errors } } =
  useForm<LoginInput>({ resolver: zodResolver(loginSchema) });

return (
  <form onSubmit={handleSubmit(onLogin)} className="space-y-4">
    <label htmlFor="email" className="block text-slate-700">Email</label>
    <input
      id="email"
      {...register("email")}
      className="border border-slate-300 rounded px-3 py-2 w-full"
    />
    {errors.email && (
      <p className="border border-red-200 bg-red-50 text-red-800 rounded px-2 py-1 text-sm">
        {errors.email.message}
      </p>
    )}
    {/* …password field similar… */}
    <button className="bg-slate-900 text-white px-4 py-2 rounded">Sign in</button>
  </form>
);
```

**After** (shadcn-wrapped — same `useForm`, same `zodResolver`, same `onLogin`):
```tsx
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage }
  from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";

const form = useForm<LoginInput>({ resolver: zodResolver(loginSchema) });

return (
  <Form {...form}>
    <form onSubmit={form.handleSubmit(onLogin)} className="space-y-4">
      <FormField
        control={form.control}
        name="email"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Email</FormLabel>
            <FormControl>
              <Input type="email" autoComplete="email" {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />
      {/* password field identical shape */}
      <Button type="submit">Sign in</Button>
    </form>
  </Form>
);
```

Behaviour deltas: **zero**. Same schema, same resolver, same submit handler, same request shape. The only visible difference is consistent token-driven styling and the automatic `aria-describedby` / `aria-invalid` wiring that shadcn `FormItem` + `FormControl` inject.

---

## 5. Component-install list with Radix footprint & bundle delta

| Component | Radix dep(s) | Other deps | gzipped delta (approx) |
|---|---|---|---|
| `button` | `@radix-ui/react-slot` | `class-variance-authority` | ~3 kB |
| `input` | — | — | ~0.5 kB |
| `label` | `@radix-ui/react-label` | — | ~1.5 kB |
| `form` | `@radix-ui/react-label`, `@radix-ui/react-slot` | `react-hook-form` (existing) | ~2 kB net |
| `card` | — | — | ~0.5 kB |
| `collapsible` | `@radix-ui/react-collapsible` | — | ~4 kB |
| `dialog` | `@radix-ui/react-dialog` | `react-remove-scroll`, `aria-hidden` (transitive) | ~12–14 kB |
| `sonner` | — (not Radix) | `sonner` | ~5 kB |
| `badge` | — | `class-variance-authority` | ~0.5 kB |
| `radio-group` | `@radix-ui/react-radio-group` | — | ~3 kB |
| `scroll-area` | `@radix-ui/react-scroll-area` | — | ~5 kB |
| **Total** | **6 distinct Radix packages** | + sonner | **~32–35 kB gzipped** |

Estimates from npmjs.com + bundlephobia + shared-dep deduplication. **Verify with `pnpm dlx vite-bundle-visualizer` after install** — these are package-level numbers and reflect the deduplication shadcn assumes. The single biggest line item is `react-remove-scroll` pulled by Dialog. Most of this is hot-path code that would be re-implemented (slot/portal/focus-trap/scroll-lock) in any DIY equivalent — Radix Primitives' value proposition.

---

## 6. Contrast-ratio table for the Catppuccin Latte/Mocha text-on-surface pairs

WCAG 2.x relative-luminance method (Source: W3C, Understanding SC 1.4.3; formula `(L1+0.05)/(L2+0.05)`). AA-normal ≥ 4.5, AA-large/UI ≥ 3.0, AAA-normal ≥ 7.0.

### Latte (light)

| Pair | Ratio | AA-large | AA-normal | AAA-normal |
|---|---:|:---:|:---:|:---:|
| text on base | **7.06** | ✅ | ✅ | ✅ (borderline) |
| text on mantle | 6.57 | ✅ | ✅ | ❌ |
| text on crust | 6.04 | ✅ | ✅ | ❌ |
| text on surface0 | 5.18 | ✅ | ✅ | ❌ |
| subtext1 on base | 5.54 | ✅ | ✅ | ❌ |
| subtext1 on mantle | 5.15 | ✅ | ✅ | ❌ |
| subtext0 on base | 4.37 | ✅ | ❌ | ❌ |
| overlay2 on base | 3.50 | ✅ | ❌ | ❌ |
| overlay1 on base | 2.83 | ❌ | ❌ | ❌ |
| overlay0 on base | 2.30 | ❌ | ❌ | ❌ |

### Mocha (dark)

| Pair | Ratio | AA-large | AA-normal | AAA-normal |
|---|---:|:---:|:---:|:---:|
| text on base | 11.34 | ✅ | ✅ | ✅ |
| text on mantle | 12.14 | ✅ | ✅ | ✅ |
| text on crust | 12.97 | ✅ | ✅ | ✅ |
| text on surface0 | 8.69 | ✅ | ✅ | ✅ |
| subtext1 on base | 9.26 | ✅ | ✅ | ✅ |
| subtext1 on mantle | 9.91 | ✅ | ✅ | ✅ |
| subtext0 on base | 7.37 | ✅ | ✅ | ✅ |
| overlay2 on base | 5.81 | ✅ | ✅ | ❌ |
| overlay1 on base | 4.44 | ✅ | ❌ | ❌ |
| overlay0 on base | 3.36 | ✅ | ❌ | ❌ |

### Accent probes (Catppuccin green; method generalises to any accent family)

| Pair | Ratio | Verdict |
|---|---:|---|
| #ffffff on Latte green (#40a02b) | 3.35 | AA-large only |
| Latte crust on Latte green | 2.53 | fails all |
| Mocha green (#a6e3a1) on Mocha base | 11.03 | AAA |
| Mocha crust on Mocha green | 12.62 | AAA |

**Safe-ramp summary**:
- **Normal text (AA 4.5:1)**: Latte → `text`, `subtext1` (on `base`/`mantle`). Mocha → `text`, `subtext1`, `subtext0`, `overlay2`.
- **Large text / non-text UI (AA 3:1)**: Latte → `subtext0`, `overlay2`. Mocha → `overlay1`, `overlay0`.
- **Unsafe**: Latte → `overlay1`, `overlay0` (do not use for any meaningful content).

Caveat: Latte `text` on `base` lands at 7.06 — clears AAA by 0.06. Different sRGB pipelines or rounding can put it just under 7.0; treat as "essentially AAA" rather than guaranteed.

---

## 7. Reference precedent

**Recommended single precedent**: **`arthur404dev/tailwindcss-shadcn-catppuccin-theme`** — github.com/arthur404dev/tailwindcss-shadcn-catppuccin-theme. A small open-source plugin that maps Catppuccin Latte (`:root`) and Mocha (`.dark`) hex values into shadcn's semantic CSS variables. It is the closest public example of the exact integration we are recommending. Read the source for the mapping shape; do **not** add it as a dependency (we want our primitive tier inline and code-reviewable, not behind a plugin).

Secondary references:
- **`shadcn/app-tailwind-v4`** (github.com/shadcn/app-tailwind-v4) — official shadcn v4 example application.
- **`catppuccin/tailwindcss@v1.0.0`** (github.com/catppuccin/tailwindcss) — official Tailwind v4 Catppuccin integration; demonstrates the `.latte/.mocha` flavour-class pattern that we mirror with `.dark`.

---

## 8. Open follow-ups (for the implementing slice)

1. **Pick the accent family** (green, teal, peach, blue, …). Then compute its OKLCH-reduced Latte shade and commit both hexes. R-075 specifies only the method.
2. **Decide on `motion-reduce` strictness**: shipped utility (`MOTION_REDUCE_OFF` constant applied per-primitive) vs. a global `@custom-variant motion-reduce` override that also reads a `data-reduce-motion` attribute on `<html>`. DEC-063 currently leaves this open.
3. **CI contrast assertion script**: write `scripts/check-contrast.ts` that imports `@catppuccin/palette` + the committed accent hex and asserts each semantic foreground/background pair clears 4.5:1 (3:1 for `--ring`/borders). Wire into the lint/test pipeline.
4. **`stylelint-declaration-strict-value`** adoption: agree on the property list to restrict (`color`, `background-color`, `border-color`) and the `var(--*)` allowlist. Out of scope for R-075.
5. **Token introspection page**: a `/dev/theme-debug` route (dev-only) that renders every shadcn semantic token name with its current resolved value in both modes. Cheap insurance.
6. **`@vitejs/plugin-react` v5 vs v6**: Vite 8 supports both; v6 drops Babel (per the Vite 8 release blog quoted in §2). Pick during the foundation PR — the project's current `react-compiler` posture (none) means v6 is the simpler choice.
7. **Sidebar tokens**: shadcn ships `--sidebar-*` tokens by default (for the `Sidebar` primitive). We are not installing `Sidebar` yet; defer until the slice that needs it. If the CLI scaffolds them anyway, leave them in but unused.

---

## Caveats

- **Bundle-delta numbers are estimates.** Per-package gzipped sizes are taken from npmjs.com / bundlephobia at the time of writing; deduplication of shared Radix runtimes is assumed. The right number after install is whatever `vite-bundle-visualizer` reports for the production build.
- **Latte `text`-on-`base` is borderline AAA (7.06).** Treat the foundation as AA-compliant, not AAA. If AAA matters, swap the body-text role to a darker primitive (`text` is already the darkest neutral in Latte; no further darkening is available without leaving the Catppuccin palette).
- **The OKLCH-derived Latte accent (`oklch(0.48 0.17 142)` ≈ `#2f7a1f`) is illustrative.** The actual committed value depends on the project's accent-family pick; the method is held but the exact L value will shift slightly with hue.
- **`@catppuccin/palette` does not ship with verified palette.json commit SHA fingerprinting.** We treat the values in §4.1 as authoritative for v1.7.1 (`main` branch as of 2026-05-18); if the upstream palette ever shifts the neutral steps, the primitive tier must be re-synced.
- **shadcn's documented Vite `ThemeProvider` does not include a no-flash inline script.** Ours is added separately in §4.4; this is a project addition, not a copy from shadcn docs.
- **`@vitejs/plugin-react@^6` was released alongside Vite 8 in March 2026.** Verify the exact version pinning at the time of foundation PR — if v6 is still very new (<60 days), keep v5 as a safety net.