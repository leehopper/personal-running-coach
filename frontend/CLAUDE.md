# Frontend — React 19 + TypeScript

> **Trademark rule — VDOT.** All user-visible strings (component text, page copy, form labels, tooltips, error messages, placeholder text, toast notifications) must use "Daniels-Gilbert zones" or "pace-zone index" — **not** "VDOT". The VDOT mark is enforced by The Run SMART Project LLC (Runalyze precedent). This is a hard rule for the frontend because every string on this tier is user-facing by definition. There is no carve-out for internal identifiers on the frontend — TypeScript type names, variables, and props that might flow into logs or telemetry should also avoid the term. See root `CLAUDE.md` and `NOTICE` for full context.

## Stack

See root CLAUDE.md for full tech stack. Additionally: eslint-plugin-sonarjs (build-time analysis), Zod v4.

## Quality Pipeline (DEC-043)

See root `CLAUDE.md` for the full five-layer pipeline. Frontend-specific notes: CodeQL uses `build-mode: none` with the combined `javascript-typescript` identifier. SonarQube Cloud ingests LCOV coverage from `npm run test -- --coverage`. Build-time `eslint-plugin-sonarjs` remains the compile-time hard gate; SonarQube Cloud is advisory dashboard only.

## Build & Dev Commands

Run from `frontend/`:

- `npm run dev` — start Vite dev server (port 5173)
- `npm run build` — TypeScript check + production build
- `npm run test` — run Vitest suite
- `npm run test:watch` — Vitest in watch mode
- `npm run test:coverage` — Vitest with coverage report
- `npm run lint` — ESLint check
- `npm run format` — Prettier auto-format

## Module-First Organization

```
frontend/
  src/
    app/
      pages/                              # Route-level components
        {page-name}/
          {page-name}.page.tsx
          {page-name}.page.css
      modules/
        app/                              # Core application shell
          app.component.tsx
          app.store.ts
          providers/
        common/                           # Shared utilities, components, hooks
          components/
          hooks/
          utils/
          models/
        {feature}/                        # Feature modules
          {feature}.api.ts                # RTK Query endpoints
          {feature}.slice.ts              # Redux slice (if needed)
          {feature}.helpers.ts            # Business logic / utilities
          models/
            {entity}.model.ts             # Types, DTOs, enums
          schemas/
            {entity}.schema.ts            # Zod validation schemas
          {component}/
            {component}.component.tsx
            {component}.component.css
            {component}.hooks.ts
            {component}.helpers.ts
            {component}.component.spec.tsx
```

## File Naming

Pattern: `{name}.{type}.{extension}`

| Suffix | Purpose |
|--------|---------|
| `.page.tsx` | Route-level components |
| `.component.tsx` | Reusable UI components |
| `.component.css` | Component-scoped styles |
| `.helpers.ts` | Utility functions, business logic |
| `.api.ts` | RTK Query endpoint definitions |
| `.slice.ts` | Redux state slices |
| `.hooks.ts` | Custom React hooks |
| `.model.ts` | Type definitions, DTOs, enums |
| `.schema.ts` | Zod validation schemas |
| `.spec.tsx` | Test files |

## Component Standards

- **Arrow functions** for component definitions
- **Props interface** named `{ComponentName}Props`, destructured as parameters (each on its own line)
- **Named exports** (except route components which use default export)
- **Composition over render functions** — always extract JSX into separate components, never use `renderX()` helpers
- Extract components when: >20-30 lines, repeated patterns, independent state, or isolated testability needed
- **Reusability over duplication** — one component with optional props/config, not multiple similar components

## TypeScript

- **Strict mode** enabled
- **No `any`** — use proper interfaces or union types
- **Type imports:** `import { type Foo }` or `import type { Foo }`
- **Nullish coalescing** (`??`) over logical OR (`||`) for defaults
- Explicit null checks instead of relying on implicit truthiness for `0`, `''`, or `false`

## State Management

- **Router state** for URL-derived state (React Router v7)
- **Local state** (`useState`, `useReducer`) for component-scoped state
- **Redux** for truly global client state only (auth, UI preferences, active conversation)
- **RTK Query** for all HTTP interactions — `tagTypes`, `providesTags`/`invalidatesTags` for cache
- Export auto-generated hooks from API files
- Keep Redux slices minimal — avoid unnecessary global state

## Forms

- **React Hook Form** for form state management
- **Zod** for schema-based validation with inferred TypeScript types
- `Controller` wrapper for component library integration
- `mode: 'onChange'` for real-time validation feedback
- Schemas in `schemas/` directory within each feature module
- Disable submit buttons when form is invalid or submitting

## Styling

- **Tailwind CSS** (v4, CSS-first) utility classes for styling
- **shadcn/ui** is installed — `new-york` style, copy-pasted Radix-primitive
  sources under `src/components/ui/`, configured via `components.json` and
  the `cn()` helper in `src/lib/utils.ts`. There is no `tailwind.config.ts`;
  Tailwind v4 is CSS-first.
- Avoid inline styles — prefer Tailwind classes
- Mobile-first responsive design with Tailwind breakpoints

### Design tokens & theming (DEC-089)

- All themed colour flows through **semantic tokens**, never hardcoded
  colour utilities (`bg-slate-*`, `text-red-*`, `bg-white`, etc.). New UI
  uses `bg-background`, `text-foreground`, `text-muted-foreground`,
  `bg-card`, `bg-primary`, `border`, `bg-destructive`, `text-positive`,
  `border-rule`, `text-clay-text`, and friends.
- `src/index.css` carries a **two-tier token layer**: a primitive Alpine
  tier (`--alp-*`) under shadcn/ui's semantic tier, joined by one
  `@theme inline` block. The accent is Alpine **clay** (`#D06A3B`).
- **Dark is the default polarity.** `:root` carries the Alpine dark
  primitive ramp (bg `#10140F`, bone `#EDE8DB`, …) and the semantic
  mappings; `.light` overrides every primitive with the light ramp and
  re-declares the semantic mappings. Primitive token *names* are
  mode-invariant — only their values swap. This inverts the pre-Alpine
  (Catppuccin) polarity, where `:root` was the light default; the `.dark`/
  `.light` class-toggle mechanism and the `dark:` utility variant are
  unchanged (see `theme-provider.tsx`'s header comment for the full
  mechanism note).
- Five project-owned semantic slots beyond shadcn's defaults: `--positive`
  (moss — done/success state), `--rule` (the 2px section-rule colour, holds
  bone/ink per mode — see `SectionRule`), `--clay-text` (clay used as text,
  not a fill), `--clay-pressed` (the pressed/active clay fill under
  `--primary-foreground` text — see § Alpine components below), and
  `--input-fill` (the sunken resting fill of form controls, distinct from
  `--input`, the border). Clay-as-text clears AA at **any** size (measured
  ~5.14:1 dark / ~5.17:1 light against `--background`; contrast ratio does
  not vary with weight or size). **Design guideline: prefer `--clay-text` at
  ≥12px semibold** — a conservative legibility preference for the warm
  mid-tone, not a contrast requirement, so it is not a hard rule: small mono
  clay labels (e.g. `MonoLabel`'s `clay` tone at `.t-data-label`'s 10px/500
  weight — used for metadata like a coach-turn `COACH · HH:MM` line) are an
  accepted, AA-passing exception; keep them. `--alp-faint` is
  decorative-only (fails AA by design) and must never carry essential text.
- **Geometry:** a radius ramp is exposed as `@theme` tokens — `rounded-xs`
  (4px, day cells/tags), `rounded-sm` (6px, chips), `rounded-md` (8px,
  buttons/inputs/cards-lite), `rounded-lg` (10px, turn cards), `rounded-full`
  (999px, pills). Nothing should exceed 12px inside a screen. Rule law:
  section openers are `2px solid var(--rule)`; data dividers are
  `1px solid var(--border)`; surfaces stay flat, elevation is rare.
- **Spacing rhythm:** Tailwind's default 4px base scale, a 22px screen
  gutter, and a 20–22px section gap. `--gutter: 22px` is exposed as a
  custom property; a `.screen-gutter` utility (`padding-inline:
  var(--gutter)`) is available in `@layer components` for screens that want
  the horizontal rhythm without repeating an arbitrary-value class.
- **Dark mode** is class-based — the `.dark`/`.light` class on
  `documentElement`. `ThemeProvider` (`src/components/theme-provider.tsx`)
  owns that class and the `light | dark | system` choice; consume it via
  the `useTheme()` hook from `src/components/theme-context.ts`. A no-flash
  script in `index.html` sets the initial class before first paint,
  falling back to **dark** (not light) if `localStorage` throws. The
  Settings page surfaces a 3-state toggle.
- A `check-contrast` script gates the token set against WCAG AA in
  pre-commit and CI — every semantic foreground/background pair must clear
  the ratio. Do not commit a token change that fails it. `--input` IS gated
  (3:1 UI-component rule): it is the resting boundary of an empty form field
  and maps to a dedicated `--alp-input-border` primitive, distinct from the
  fainter `--alp-hairline` divider tone behind `--border`. Exempt (WCAG
  1.4.11 decorative / non-text): `--border` (pure divider), `--warning`
  (supplementary severity accent), and `--alp-faint`.

### Typography (DR-6)

- Three self-hosted families, no CDN: **Barlow Condensed** (`--font-condensed`
  — numbers, display/screen titles, section labels, buttons), **Barlow**
  (`--font-body`, aliased from `--font-sans`), and **IBM Plex Mono**
  (`--font-mono` — labels/eyebrows/data values). Granular per-weight, latin-
  subset `@fontsource/*` imports in `src/index.css` (no
  `@fontsource-variable/*` — those packages don't exist for these families);
  a build-time Vite plugin (`vite.config.ts`'s `fontFallbackFaces`, built on
  `fontaine`'s metric-computation utilities) generates metric-matched
  `"… fallback"` faces appended to each `--font-*` stack.
- **Rules as law:** numbers are always `--font-condensed`; labels/data are
  always `--font-mono`; source copy stays sentence case — `uppercase` is a
  presentation concern applied via CSS, never baked into stored strings.
- The HANDOFF §3 role table is encoded as shared utility classes in
  `src/index.css`'s typography `@layer components` block: `.t-display`,
  `.t-screen-title`, `.t-section-label`, `.t-numeral` (condensed numerals,
  `white-space: nowrap`, not `tabular-nums` — Barlow Condensed's `tnum`
  feature is commonly stripped from Google-Fonts-derived files),
  `.t-body`, `.t-row-title`, `.t-data-label` / `.t-data-value` (mono,
  `font-variant-numeric: tabular-nums`), `.t-button`. Later slices restyle
  primitives onto these; new UI should reach for them over ad-hoc
  font-size/weight utilities.

### Alpine components (Slice 0)

- **State law**, applied to every interactive Alpine primitive (button,
  input/textarea, checkbox, radio-group, segmented-control, switch, dialog
  close, badge, sonner's RETRY action): pressed = darken + `active:scale-
  [0.98]` (the primary/clay-filled treatment darkens to `--clay-pressed`;
  outline/secondary-style controls darken to `--secondary`); disabled =
  `disabled:opacity-35`; focus-visible = `focus-visible:border-ring
  focus-visible:ring-[3px] focus-visible:ring-ring/[0.22]` (the one
  canonical ring — do not reintroduce a plain `outline-2` focus treatment);
  error = `aria-invalid:border-destructive aria-invalid:ring-destructive/
  [0.22]` with no `dark:` override (a single ratio holds both modes); hit
  targets ≥44px via a `relative` + `before:absolute before:inset-[-14px]
  before:content-['']` expansion around a visually smaller control (see
  `radio-group.tsx` and `checkbox.tsx`); every animation pairs a
  `motion-reduce:` variant (DEC-063).
- **Net-new shared components** — shadcn-style primitives under
  `src/components/ui/`, app-composed ones under
  `src/app/modules/common/components/{name}/`:
  - `Switch` (`components/ui/switch.tsx`) — radix Switch; replaces
    onboarding's checkboxes.
  - `SegmentedControl` / `SegmentedControlItem`
    (`components/ui/segmented-control.tsx`) — radix RadioGroup-based
    mutually-exclusive picker (completion / units / theme).
  - `SectionRule`, `MonoLabel`, `StatBand` / `StatCell`, `Wordmark`,
    `BuildingPlanSurface` — each under its own
    `common/components/{name}/{name}.component.tsx` with a co-located spec.
    `BuildingPlanSurface`'s progress indicator is genuinely indeterminate
    (a partial-width bar travels the track via the `animate-indeterminate`
    utility in `index.css`, not a fixed-position opacity pulse), so it
    never reads as a stalled percentage.
  - `Wordmark` still has no mount point: Slice 1's `ShellLayout`/`TabBar`
    shipped without one (no header chrome in that slice's scope). It
    remains unmounted, to be picked up by a later slice's screen work.
- Two of the five project-owned tokens above exist specifically to back
  this layer: `--clay-pressed` (retuned to `#C56438` so
  `--primary-foreground` clears AA against it — the handoff's literal
  `#B0532A` measured only ~3.65:1) and `--input-fill` (gated for
  placeholder/value legibility, not just boundary perceptibility like
  `--input`). Both carry dedicated check-contrast pairs.

### Animation baseline (DEC-063)

Tailwind utility classes are the animation baseline for the current frontend surface. **Do not add `motion`, `motion/react`, or `framer-motion`** to `package.json` until a slice introduces a use case Tailwind cannot cover cleanly.

- State-change tweens — `transition-colors duration-200 ease-out` (or the appropriate `transition-{property}` variant).
- Loading shimmers / placeholders — `animate-pulse`, `animate-spin`.
- Radix-driven enter/exit (Dialog, Popover, etc.) — `data-[state=open]:animate-in data-[state=closed]:animate-out` from `tw-animate-css` (a direct dependency imported in `src/index.css`; the Tailwind v4 successor to `tailwindcss-animate`). Do not wrap Radix primitives in `AnimatePresence`.
- Reduced-motion contract (WCAG 2.3.3) — pair every animation with the `motion-reduce:` variant (e.g. `motion-reduce:transition-none`, `motion-reduce:animate-none`). Tailwind handles `prefers-reduced-motion: reduce` at the CSS level; no JS hook is needed for the current surface.

`motion/react` adoption is deferred until either (a) Slice 4's streaming chat UI lands token-by-token rendering or animated typing indicators that need spring physics, or (b) a gesture-driven surface (drag-to-dismiss, swipe-back) enters scope. When either trigger fires, adopt `motion@^12` slice-wide alongside `useReducedMotion()` parity — no piecemeal pre-adoption. See `docs/decisions/decision-log.md` § DEC-063 for the full rationale and revisit triggers.

## Custom Hooks

- Options interface: `Use{HookName}Options`
- Return type interface: `Use{HookName}Return`
- Export both interfaces
- **Explicit return type annotation** on the hook function

## Import Order

1. React and framework imports
2. Third-party library imports
3. Path alias imports (`~/modules/*`)
4. Parent directory imports (`../`)
5. Same directory imports (`./`)
6. CSS/style imports

Use `~/` path alias for cross-module imports. Avoid `../` to top-level directories.

## Naming

- **DTOs:** `Dto` suffix (`UserDto`, `WorkoutDto`)
- **Event handlers:** name for intent (`createProduct`), not event (`handleClick`)
- **Booleans:** verb prefix (`isLoading`, `hasError`, `canSubmit`)
- **Identifiers:** descriptive (`productId`, `workoutId`), never generic `id`

## Dev-Only Code

Code under `frontend/src/dev-only/` is tree-shaken in production builds; gate every consumer with `import.meta.env.DEV`.

## Code Quality

- Components under 100 lines — extract when larger
- Prefer `.map` / `.filter` / `.reduce` over imperative loops
- Prefer pure functions and immutability
- `useMemo` / `useCallback` only for genuinely expensive operations — not by default
- `React.memo()` selectively for components that re-render frequently with unchanged props
- Proper `key` props in lists — **never array index as key**
- Code splitting with dynamic imports for large features

## Accessibility

- Semantic HTML elements
- Proper ARIA attributes
- Keyboard navigation support
- Good color contrast ratios

## Security

- Sanitize any HTML before rendering — no sanitizer is wired yet; add one (e.g. DOMPurify) when the first HTML-rendering surface lands
- Environment variables for configuration — no secrets in client bundle
- Never commit API keys or credentials

## Testing

- **Vitest + React Testing Library**
- Co-located `.spec.tsx` files alongside components
- Focus on logic-heavy code: helpers, hooks, reducers
- Test components in isolation where possible

## Post-Change

See root CLAUDE.md checklist.
