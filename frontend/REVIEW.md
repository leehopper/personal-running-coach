# Review Configuration — frontend

## Skip

- "coverage/**"

## Rules

### TypeScript strictness

- CRITICAL: Never use 'any' type. Use 'unknown' with runtime type narrowing,
  proper generics, or specific union types. 'any' defeats TypeScript's type
  system entirely and propagates silently through the codebase.
- No 'as' assertions to silence type errors. Use discriminated unions with type
  guards, or 'satisfies' for validation. 'as' is acceptable only for
  well-understood narrowing (as const, as HTMLElement after querySelector with
  null check).
- Use 'import type' for type-only imports. Required by verbatimModuleSyntax
  and Vite for correct tree-shaking. Use 'import { type Foo }' for mixed
  imports.
- No TypeScript enum. Use string literal unions (type Status = 'active' |
  'inactive') or 'as const' objects. Enums generate runtime code, are
  incompatible with erasableSyntaxOnly, and don't interoperate cleanly with
  string values.

### React 19 patterns

- CRITICAL: No forwardRef in new code. Use ref as a regular prop with
  ComponentProps<'element'>. forwardRef is deprecated in React 19 and adds
  unnecessary HOC overhead.
- No useEffect for initial data fetching when Suspense or RTK Query is viable.
  useEffect fetch patterns require manual loading/error/race-condition
  handling. useEffect remains correct for subscriptions, DOM effects, and
  timers.
- Never call hooks conditionally or inside loops. This violates React's Rules
  of Hooks and causes unpredictable state behavior.
- Every useEffect must have a cleanup function that releases event listeners,
  subscriptions, and timers. Missing cleanup causes memory leaks that compound
  across navigation.
- Flag missing useEffect dependency arrays. Empty arrays ([]) are acceptable
  only when the effect genuinely runs once on mount. Missing dependencies
  create stale closure bugs.

### Component composition

- CRITICAL: Never use renderX() helper functions for rendering. Extract to
  separate named components. Render functions bypass React's reconciliation
  and prevent proper memoization.
- Arrow functions for all component definitions. Named exports, not default
  exports. Props interface named {ComponentName}Props defined inline above
  the component.
- Extract components exceeding ~100 lines. Props drilling beyond 2 levels
  signals need for Context or RTK state.

### RTK Query

- CRITICAL: Every mutation endpoint must declare invalidatesTags. Missing tag
  invalidation causes stale cache data after mutations — users see "my changes
  didn't save." Use specific { type, id } tags for updates/deletes and
  { type, id: 'LIST' } for creates.
- API middleware must be in the store. Flag configureStore missing
  .concat(api.middleware). Without it, RTKQ caching, invalidation, and
  polling silently fail. Also require setupListeners(store.dispatch).
- Don't duplicate RTKQ cache in Redux slices. Flag extraReducers copying
  RTKQ-fetched data into separate slices. Let RTKQ own server state; use
  slices only for truly global client state (auth session, UI preferences).

### Zod v4

- CRITICAL: z.record() requires two arguments in v4. Single-argument form
  is a compile-time error. Use z.partialRecord() for optional keys.
- Audit .default() + .optional() in object schemas carefully. In v4, defaults
  are applied even for optional fields — z.string().default("x").optional()
  parsing {} produces { a: "x" }, which may break React Hook Form behavior.
- Define Zod schemas as the single source of truth. Infer TypeScript types
  with z.infer<typeof Schema> rather than defining interfaces separately.
  Dual definitions drift apart silently.
- Use top-level format schemas: z.email(), z.uuid(), z.url() instead of
  z.string().email(). Old method forms are deprecated and prevent
  tree-shaking.

### React Hook Form

- CRITICAL: No unscoped watch(). const values = watch() re-renders the entire
  component on every field change. Use useWatch({ name: 'specificField' }) for
  targeted subscriptions.
- Use field.id as key in useFieldArray, never index. Using index causes field
  state loss on reorder/remove.
- Provide complete defaultValues in useForm() matching the full Zod schema
  shape. Missing defaults cause undefined fields, triggering
  uncontrolled-to-controlled warnings and breaking reset().

### Tailwind v4 and shadcn/ui

- CRITICAL: No legacy Tailwind v3 directives. @tailwind base/components/
  utilities must not appear. Use @import "tailwindcss" exclusively. No
  tailwind.config.js — use @theme in CSS.
- Never break Radix accessibility primitives in shadcn/ui components. Don't
  remove asChild patterns, ARIA attributes, keyboard handlers, or focus
  trapping. All interactive elements must retain visible focus indicators.
- Use semantic theme variables (bg-primary, text-muted-foreground), never
  hardcoded colors (bg-blue-500). Custom colors must be defined in :root +
  .dark scopes.
- CRITICAL: Icon-only buttons require sr-only text or aria-label. Interactive
  elements must have visible focus styles. Form inputs require associated
  labels. Color combinations must meet WCAG 2.1 AA contrast ratios.

### Trademark: VDOT

- CRITICAL: Flag any appearance of "VDOT" in any frontend source file. This
  includes JSX text, component props, form labels, tooltip text, error
  messages, page copy, shadcn/ui string props, TypeScript type names,
  variable names, test-file strings, and telemetry field names. Use
  "Daniels-Gilbert zones" or "pace-zone index" instead. The VDOT mark is
  enforced by The Run SMART Project LLC (Runalyze precedent).
- The frontend has no internal-code carve-out — every string on this tier
  either renders to a user or is logged/telemetered and becomes user-facing
  by proxy. Apply the rule uniformly.
- When generating new pages, components, or forms that reference fitness
  metrics, default to "Daniels-Gilbert zones" or "pace-zone index" language
  from the start. Do not require a fix-up review pass to remove VDOT.

### Testing (Vitest + RTL)

- Use screen.getByRole/getByLabelText over getByTestId. RTL queries follow
  accessibility priority: getByRole -> getByLabelText -> getByText ->
  getByTestId (last resort). Overusing getByTestId means you're not testing
  accessibility.
- Use userEvent instead of fireEvent. userEvent simulates realistic user
  interactions. fireEvent dispatches bare DOM events and misses interaction
  side effects.
- Wrap only async operations in waitFor. Use findBy*queries (built-in getBy*
  - waitFor) for elements that appear asynchronously.

### Tool authority partitioning (DEC-043)

- When reviewing CI changes, check the one-authority-per-signal mapping:
  CodeQL = first-party SAST, Codecov = coverage via Cobertura, SonarQube
  Cloud = dashboard via LCOV, dependency-review-action = license + CVE
  gate. Reject any PR that adds a second tool owning the same signal.

### Snyk/Codacy proposal gate (DEC-043)

- Reject any proposal to add Snyk or Codacy unless at least one of the
  explicit reconsider-triggers in ROADMAP § Deferred Items has fired.
  See DEC-043 in docs/decisions/decision-log.md.

## Ignore

# 2026-03-25: Zod fluent API chains are intentionally long

- simplification:"optional chaining" for Zod schema definitions

# 2026-03-25: RTK Query API slices are inherently large files

- conventions:"file length" for RTK Query API slice definitions
