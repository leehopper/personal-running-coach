# RunCoach frontend audit: versions, review rules, and anti-patterns

**Every pinned dependency is either current or semver-compatible with the latest release, with one exception: `vite: ^8.0.1` likely resolves to a patch that doesn't yet exist.** The broader ecosystem picture matters more than the versions themselves — React Compiler 1.0 is stable and should be enabled, Vite 8's Rolldown bundler delivers 10–30× faster production builds, Zod v4 and Tailwind v4 are major rewrites with numerous renamed APIs, and TypeScript 6.0 just shipped on March 17. This audit covers all 20+ libraries plus 70+ actionable code review rules for REVIEW.md, organized by severity.

---

## Version audit: one fix needed, two upgrades to plan

| Library | Pinned | Latest Stable | Status | Action |
|---|---|---|---|---|
| react | ^19.2.4 | 19.2.4 | ✅ Current | None |
| react-dom | ^19.2.4 | 19.2.4 | ✅ Current | None |
| react-router-dom | ^7.13.1 | 7.13.2 | ✅ Compatible | Auto-resolves. Consider importing from `react-router` directly (v7 unified package) |
| @reduxjs/toolkit | ^2.11.2 | 2.11.2 | ✅ Current | None |
| react-redux | ^9.2.0 | 9.2.0 | ✅ Current | None |
| tailwindcss | ^4.2.2 | 4.2.2 | ✅ Current | None |
| @tailwindcss/vite | ^4.2.2 | 4.2.2 | ✅ Current | None |
| zod | ^4.3.6 | 4.3.6 | ✅ Current | None |
| react-hook-form | ^7.71.2 | 7.72.0 | ✅ Compatible | Auto-resolves |
| @hookform/resolvers | ^5.2.2 | 5.2.2 | ✅ Current | None |
| typescript | ~5.9.3 | 5.9.3 (6.0.2 released Mar 17) | ✅ Intentional | **Plan TS 6.0 upgrade** — final JS-based release before Go-native TS 7.0 |
| **vite** | **^8.0.1** | **8.0.0** (or 8.0.2) | **⚠️ Fix** | **Change to `^8.0.0`** — 8.0.1 may not exist on npm |
| vitest | ^4.1.0 | 4.1.1 | ✅ Compatible | Auto-resolves |
| @vitest/coverage-v8 | ^4.1.0 | 4.1.1 | ✅ Compatible | Auto-resolves |
| @testing-library/react | ^16.3.2 | 16.3.2 | ✅ Current | None |
| @testing-library/jest-dom | ^6.9.1 | 6.9.1 | ✅ Current | None |
| eslint | ^9.39.4 | 9.39.4 (10.1.0 released Feb 6) | ✅ Current on v9 | **Plan ESLint 10 migration** when typescript-eslint supports it |
| typescript-eslint | ^8.57.1 | 8.57.2 | ✅ Compatible | Auto-resolves |
| eslint-plugin-react-hooks | ^7.0.1 | 7.0.1 | ✅ Current | None |
| eslint-plugin-react-refresh | ^0.5.2 | 0.5.2 | ✅ Current | None |
| eslint-plugin-sonarjs | ^4.0.2 | 4.0.2 | ✅ Current | None |
| prettier | ^3.8.1 | 3.8.1 | ✅ Current | Prettier 4.0 in alpha — wait for stable |
| jsdom | ^29.0.1 | ❓ Unconfirmed | ⚠️ Verify | Run `npm view jsdom version` — could not confirm 29.x exists via registry |
| **Planned: shadcn/ui** | — | CLI v4 (Mar 2026) | 📋 Ready | Install with `npx shadcn@latest init` — full Tailwind v4 + React 19 support |
| **Planned: Playwright** | — | ~1.58.x | 📋 Ready | Install `@playwright/test` — includes Chrome for Testing and AI agent mode |

**Three immediate actions.** First, fix `vite` to `^8.0.0`. Second, verify `jsdom` version with `npm view jsdom version`. Third, upgrade `@vitejs/plugin-react` to v6 (uses Oxc instead of Babel, smaller and faster).

**Two planned upgrades.** TypeScript 6.0 shipped March 17, 2026 — the final JavaScript-based compiler before TS 7.0 (Go-native "Corsa"). ESLint 10.0 shipped February 6, 2026 with a new config search algorithm and requires Node 20.19+. Both are safe to defer until the ecosystem stabilizes but should be on the roadmap.

**Libraries to add.** Install `eslint-plugin-jsx-a11y` for accessibility linting — it catches inaccessible JSX at lint time and is standard for production React apps. Install `prettier-plugin-tailwindcss` for consistent utility class ordering. Consider `@zod/mini` for frontend-only validation where bundle size matters (~1.9 KB vs full Zod). Enable **React Compiler** via `babel-plugin-react-compiler` — it's stable since October 2025, battle-tested at Meta, and eliminates manual memoization.

---

## React 19 review rules: Actions replace boilerplate, Compiler replaces memo

React 19.2.4 introduces a paradigm shift. **Actions** (`useActionState`, form `action` prop, `useTransition` with async functions) replace manual pending/error state management. The `use()` hook reads promises and context conditionally. `ref` is now a regular prop, making `forwardRef` unnecessary. The **React Compiler 1.0** (stable since October 7, 2025) auto-memoizes components at build time, making manual `useMemo`/`useCallback`/`React.memo` largely redundant for new code.

| ID | Severity | Rule |
|---|---|---|
| R-01 | 🔴 Critical | **No `forwardRef` in new code.** Use `ref` as a regular prop: `function MyInput({ ref, ...props }: ComponentProps<'input'>)`. `forwardRef` is deprecated in React 19 and adds unnecessary HOC overhead. |
| R-02 | 🔴 Critical | **No `useEffect` for initial data fetching when Suspense is viable.** Use `use()` with a cached promise + `<Suspense>` + `<ErrorBoundary>`, or use RTK Query / TanStack Query which integrate with Suspense. `useEffect` fetch patterns require ~40 lines of manual loading/error/race-condition handling. Exception: `useEffect` remains correct for subscriptions, DOM effects, and timers. |
| R-03 | 🟠 High | **No `<Context.Provider>`.** Use `<MyContext value={...}>` directly — the `.Provider` suffix will be deprecated. |
| R-04 | 🟠 High | **No `useFormState` from react-dom.** Import `useActionState` from `react` instead — renamed and moved in React 19. |
| R-05 | 🟠 High | **No manual isPending/isLoading state for form submissions.** Use `useActionState` which returns `[state, submitAction, isPending]`, or pass async functions to `<form action={...}>`. React 19 Actions handle pending state, error handling, and form reset automatically. |
| R-06 | 🟡 Medium | **No default `useMemo`/`useCallback`/`React.memo` when React Compiler is enabled.** The compiler auto-memoizes more precisely than manual optimization, including conditional paths that `useMemo` cannot reach. Use manual memoization only as an escape hatch for measured performance issues. |
| R-07 | 🟡 Medium | **Ref callbacks must use block body syntax.** React 19 interprets return values from ref callbacks as cleanup functions. `ref={c => instance = c}` (implicit return) causes TypeScript errors. Use `ref={c => { instance = c }}`. |
| R-08 | 🟡 Medium | **Never create promises inside render passed to `use()`.** Promises must be created outside the component — in route loaders, parent components, or Suspense-compatible libraries. Uncached render-created promises cause undefined behavior. |
| R-09 | 🟡 Medium | **Use `useOptimistic` for instant mutation feedback.** Replaces manual optimistic update patterns with automatic revert-on-error semantics. `const [optimisticValue, setOptimistic] = useOptimistic(serverValue)`. |
| R-10 | 🔵 Low | **Prefer native `<title>`, `<meta>`, `<link>` in components.** React 19 automatically hoists these to `<head>`. No `useEffect` for `document.title` manipulation; reserve react-helmet only for advanced route-based overriding. |

---

## TypeScript 5.9 review rules: stricter defaults, erasable syntax

TypeScript 5.9 introduced `import defer` for lazy module evaluation, a prescriptive new `tsc --init` output that enables `noUncheckedIndexedAccess` and `exactOptionalPropertyTypes` by default, and expanded hover information in editors. With TS 6.0 freshly released as the final JS-based compiler, the project's `~5.9.3` pin is correct for stability — but the tsconfig should adopt the stricter defaults that 5.9 recommends.

**Recommended tsconfig additions** beyond `strict: true`: enable `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, `verbatimModuleSyntax`, `erasableSyntaxOnly` (disallows enums and namespace declarations), and `noUncheckedSideEffectImports`. These align with the direction TypeScript is heading for TS 7.0 compatibility.

| ID | Severity | Rule |
|---|---|---|
| TS-01 | 🔴 Critical | **No `any` type.** Use `unknown` for truly unknown types with type guards, proper generics, or specific union types. `any` disables type checking and propagates silently through the system. |
| TS-02 | 🟠 High | **No `as` assertions to silence errors.** Use discriminated unions with type guards, or `satisfies` for validation. `as` is acceptable only for well-understood narrowing (`as const`, `as HTMLElement` after `querySelector` with null check). |
| TS-03 | 🟠 High | **Use `import type` for type-only imports.** Enforced by `verbatimModuleSyntax`. Use `import { type Foo }` for mixed imports or `import type { Foo }` for type-only modules. Required by Vite for correct tree-shaking. |
| TS-04 | 🟠 High | **No TypeScript `enum`.** Use string literal unions (`type Status = 'active' | 'inactive'`) or`as const` objects. Enums generate runtime code, are incompatible with `erasableSyntaxOnly`, and don't interoperate cleanly with string values. |
| TS-05 | 🟡 Medium | **Use `satisfies` for config objects and constants.** Type annotations widen types and lose literal inference. `const routes = { ... } satisfies Record<string, RouteConfig>` validates structure while preserving autocomplete-friendly literal types. |
| TS-06 | 🟡 Medium | **Handle `undefined` from index access.** With `noUncheckedIndexedAccess` enabled, `arr[0]` returns `T | undefined`. Check for`undefined`, use`.at()`, or non-null assert only when bounds are guaranteed by preceding logic. |
| TS-07 | 🟡 Medium | **Use `ComponentProps<'element'>` for HTML prop extension.** `interface MyInputProps extends ComponentProps<'input'> { label: string }` automatically includes `ref` (React 19), all HTML attributes, and stays in sync with React's type definitions. Prefer this over manual `HTMLAttributes` typing. |
| TS-08 | 🔵 Low | **Distinguish missing vs. explicitly-undefined props.** With `exactOptionalPropertyTypes`, `x?: string` means the property can be absent but cannot be set to `undefined`. If `undefined` should be accepted, type it as `x?: string | undefined`. |

---

## Vite 8 review rules: Rolldown replaces everything

Vite 8.0 (March 12, 2026) is the most significant architectural change since Vite 2. **Rolldown**, a unified Rust-based bundler, replaces both esbuild (dev) and Rollup (production). Linear reported build times dropping from **46 seconds to 6 seconds**. The `@vitejs/plugin-react` v6 switches from Babel to **Oxc** for React Refresh transforms, dramatically reducing install size.

| ID | Severity | Rule |
|---|---|---|
| V-01 | 🔴 Critical | **Migrate `build.rollupOptions` to `build.rolldownOptions`.** The Rollup-named key is deprecated in Vite 8 and will be removed. Same for `worker.rollupOptions` → `worker.rolldownOptions` and `optimizeDeps.esbuildOptions` → `optimizeDeps.rolldownOptions`. |
| V-02 | 🔴 Critical | **Require Node.js ≥ 20.19 or ≥ 22.12.** Vite 8 requires these versions for `require(esm)` support. CI/CD pipelines and Docker images must specify this floor. |
| V-03 | 🟠 High | **Upgrade `@vitejs/plugin-react` to v6.** v6 drops the Babel dependency and uses Oxc for React Refresh transforms. For React Compiler, use the new `reactCompilerPreset` helper with `@rolldown/plugin-babel` instead of adding full Babel. |
| V-04 | 🟠 High | **Review `build.target` against actual browser support needs.** Vite 8 defaults to Chrome 111, Firefox 114, Safari 16.4. Remove manual targets that duplicate these defaults; set explicit targets only if older browser support is required. |
| V-05 | 🟡 Medium | **Enable `server.forwardConsole` for dev.** Forwards browser console output to the terminal — especially valuable when developing with AI coding agents or headless workflows. |

---

## Vitest 4 and testing review rules: coverage config is breaking

Vitest 4 ships aligned with Vite 8 and brings a **rewritten pool architecture** (Tinypool removed), **stable browser mode** with visual regression testing, and a critical coverage configuration change: **`coverage.all` was removed**, meaning uncovered files no longer appear in reports unless you explicitly define `coverage.include`. The testing rules below cover Vitest, React Testing Library, and Playwright.

| ID | Severity | Rule |
|---|---|---|
| T-01 | 🔴 Critical | **Define `coverage.include` explicitly in Vitest config.** `coverage.all` and `coverage.extensions` were removed in v4. Without `coverage.include: ['src/**/*.{ts,tsx}']`, only files imported during tests appear in reports — giving a misleadingly high coverage percentage. |
| T-02 | 🔴 Critical | **Use web-first assertions in Playwright, never `waitForTimeout`.** Replace `page.waitForTimeout(5000)` with `await expect(locator).toBeVisible()`. Hard-coded timeouts are the primary cause of flaky E2E tests. |
| T-03 | 🔴 Critical | **Playwright tests must be fully isolated.** Each test gets its own browser context (Playwright's default). Never rely on test execution order or shared state between tests. Use `storageState` for auth reuse, not inter-test dependencies. |
| T-04 | 🟠 High | **Use `screen.getByRole`/`getByLabelText` over `getByTestId`.** RTL queries should follow accessibility priority: `getByRole` → `getByLabelText` → `getByText` → `getByTestId` (last resort). Overusing `getByTestId` means you're not testing accessibility and won't catch missing ARIA roles/labels. |
| T-05 | 🟠 High | **Use `userEvent` instead of `fireEvent`.** `@testing-library/user-event` simulates realistic user interactions (typing dispatches keydown/keypress/input/keyup, clicking handles focus). `fireEvent` dispatches bare DOM events and misses interaction side effects. |
| T-06 | 🟠 High | **Wrap only async operations in `waitFor`.** Don't wrap synchronous queries in `waitFor` — it adds unnecessary retries and obscures test intent. Use `findBy*` queries (built-in `getBy*` + `waitFor`) for elements that appear asynchronously. |
| T-07 | 🟠 High | **Update Vitest pool configuration.** Replace deprecated `singleThread`/`singleFork` with `maxWorkers: 1, isolate: false`. Replace env var `VITEST_MAX_THREADS` with `VITEST_MAX_WORKERS`. Replace `poolMatchGlobs` with the `projects` config. |
| T-08 | 🟡 Medium | **Use `screen` object, not render destructuring.** Always `screen.getByRole(...)` instead of `const { getByRole } = render(...)`. Eliminates destructuring maintenance and is the current RTL recommended pattern. |
| T-09 | 🟡 Medium | **Use Page Object Model for Playwright suites.** Encapsulate page-specific locators and actions in dedicated classes. Each POM class represents one page or major component, receives `Page` via constructor, and separates locators from actions from assertions. |
| T-10 | 🔵 Low | **Prefer API-driven setup in Playwright over UI-driven setup.** Use `request.post()` to create test data and `page.route()` to mock third-party APIs rather than navigating through UI flows for test setup. Dramatically reduces test runtime and flakiness. |

**Coverage expectations for a new SPA.** Target **80% line coverage** for business logic (hooks, utilities, state management), **70% for components** (focus on user interaction paths, not snapshot coverage), and **critical user flows covered by E2E** (auth, core coach interactions, form submissions). Co-located `.spec.tsx` files should be required for every component and hook; E2E tests for every user-facing page route.

---

## Tailwind CSS v4 review rules: CSS-first everything

Tailwind v4 (January 2025) replaced `tailwind.config.js` with **CSS-first configuration** via `@theme` directives, moved from `@tailwind` directives to `@import "tailwindcss"`, adopted **OKLCH colors** for wider gamut, and built a new **Rust-based engine** that's 100×+ faster on incremental builds. Many v3 class names were renamed, default behaviors changed (border is now `currentColor`, ring width is now 1px), and the `@utility` directive replaced `@layer components` for custom classes that need variant support.

| ID | Severity | Rule |
|---|---|---|
| TW-01 | 🔴 Critical | **No legacy Tailwind v3 directives.** `@tailwind base`, `@tailwind components`, `@tailwind utilities` must not appear. Use `@import "tailwindcss"` exclusively. No `tailwind.config.js` in new projects — use `@theme` in CSS. Remove `postcss-import` and `autoprefixer` (built into v4). |
| TW-02 | 🔴 Critical | **Use canonical v4 class names.** `bg-gradient-to-*` → `bg-linear-to-*`, `flex-shrink-0` → `shrink-0`, `flex-grow` → `grow`, `overflow-ellipsis` → `text-ellipsis`. CSS variable shorthand uses parentheses: `bg-(--my-var)` not `bg-[--my-var]`. Named values required: `shadow-sm` not bare `shadow`, `rounded-sm` not bare `rounded`. |
| TW-03 | 🟠 High | **Extract repeated arbitrary values to `@theme` tokens.** One-off `bg-[#ff6b35]` is acceptable; two or more occurrences of the same arbitrary value must become a design token in `@theme { --color-brand: oklch(...) }` and used as `bg-brand`. |
| TW-04 | 🟠 High | **Use `@utility` not `@layer components` for custom classes.** Custom classes in `@layer components { .my-btn { ... } }` cannot use Tailwind variants (`hover:`, `md:`). Use `@utility my-btn { ... }` for anything that should participate in the variant system. Minimize `@apply` usage. |
| TW-05 | 🟠 High | **Every custom color needs a dark mode variant.** Define colors in both `:root` and `.dark` scopes, then map via `@theme inline`. Semantic naming required: `bg-primary` not `bg-blue-500`. Be aware that `hover:` is now wrapped in `@media (hover: hover)` — test on touch devices. |
| TW-06 | 🟡 Medium | **Prefer shorthand and consolidated utilities.** `size-5` over `w-5 h-5`. `inset-0` over `top-0 right-0 bottom-0 left-0`. `gap-*` over `space-x-*`/`space-y-*` in flex/grid. Use container queries (`@sm:`, `@md:` on `@container` parents) for component-level responsiveness. |
| TW-07 | 🔴 Critical | **Accessibility in utility classes.** Icon-only buttons require `sr-only` text or `aria-label`. Interactive elements must have visible focus styles (`focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2`). Form inputs require associated labels. Color combinations must meet WCAG 2.1 AA contrast ratios. |
| TW-08 | 🟡 Medium | **Install and configure `prettier-plugin-tailwindcss`.** Enforces consistent class ordering across the codebase. Configure `tailwindFunctions: ['cva', 'cn']` to also sort classes inside utility functions. |

---

## shadcn/ui review rules: own the code, preserve the accessibility

shadcn/ui is not a library — it's a component system where you **own the source code** entirely. Components are built on **Radix UI primitives** (headless, accessible) styled with Tailwind. The March 2026 CLI v4 ships with full Tailwind v4 and React 19 support: `forwardRef` removed in favor of `ComponentProps`, OKLCH colors, `data-slot` attributes on all primitives, and `tw-animate-css` replacing `tailwindcss-animate`.

| ID | Severity | Rule |
|---|---|---|
| SC-01 | 🔴 Critical | **Never break Radix accessibility primitives.** Don't remove `asChild` patterns, ARIA attributes, keyboard handlers, or focus trapping from Radix-based components. If component structure is modified, require keyboard and screen reader testing. All interactive elements must retain visible focus indicators. |
| SC-02 | 🟠 High | **Use semantic theme variables, never hardcoded colors.** Reference `bg-primary`, `text-muted-foreground`, `border-border` — never raw `bg-blue-500`. Custom colors must be defined in `:root` + `.dark` scopes and mapped via `@theme inline`. Follow the `background`/`foreground` naming convention. |
| SC-03 | 🟠 High | **Use React 19 patterns in component definitions.** No `forwardRef` — use `ComponentProps<typeof Primitive>` directly. Include `data-slot` attribute on all primitives. Use `tw-animate-css` not `tailwindcss-animate`. |
| SC-04 | 🟠 High | **Form components must use shadcn/ui Form primitives with RHF.** Use `<Form>` (wraps FormProvider), `<FormField>` (wraps Controller), `<FormControl>`, `<FormMessage>`, `<FormDescription>`. These auto-generate `id`, `aria-describedby`, and `aria-invalid` attributes. Don't mix controlled and uncontrolled patterns. |
| SC-05 | 🟡 Medium | **Maintain 3-tier component architecture.** Raw `ui/` components (shadcn defaults) → `primitives/` (lightly customized wrappers) → `blocks/` (product-level compositions). Don't import raw `ui/` components directly in feature code — create abstraction layers for product-specific behavior. Use `cn()` for all conditional classes and `cva` for variant management. |

---

## Zod v4 review rules: renamed APIs and subtle behavior changes

Zod v4 (July 2025) is a ground-up rewrite delivering **14× faster parsing**, **57% smaller bundle**, and tree-shakable architecture. The migration from v3 involves dozens of renamed APIs and several behavioral changes that silently compile but produce different results — particularly `.default()` inside optional object fields and stricter UUID validation.

| ID | Severity | Rule |
|---|---|---|
| Z-01 | 🔴 Critical | **`z.record()` requires two arguments.** `z.record(z.string())` → `z.record(z.string(), z.string())`. Single-argument form is a compile-time error in v4. Records with enum keys are now exhaustive. Use `z.partialRecord()` for optional keys. |
| Z-02 | 🔴 Critical | **Audit `.default()` + `.optional()` in object schemas.** In v4, defaults are applied even for optional fields: `z.object({ a: z.string().default("tuna").optional() })` parsing `{}` produces `{ a: "tuna" }` (v3 produced `{}`). Verify this matches intended form behavior, especially with React Hook Form. |
| Z-03 | 🔴 Critical | **Fix `ZodType` generics for v4.** `ZodTypeAny` is eliminated — use `ZodType`. The three-parameter form `ZodType<Output, Def, Input>` → two-parameter `ZodType<Output, Input>`. Generic helpers like `useZodForm<T extends z.ZodType>` need explicit constraints: `<T extends z.ZodType<any, any>>`. |
| Z-04 | 🟠 High | **Use top-level format schemas.** `z.string().email()` → `z.email()`. Same for `z.uuid()`, `z.url()`, `z.base64()`, `z.nanoid()`. Old method forms are deprecated and prevent tree-shaking. |
| Z-05 | 🟠 High | **Use `error` parameter instead of `message`.** `{ message: "..." }` is deprecated → use `{ error: "..." }`. `invalid_type_error` and `required_error` are removed. Use error callback with `issue.input === undefined` check for required vs. invalid type differentiation. |
| Z-06 | 🟠 High | **Use `.extend()` or spread instead of `.merge()`.** `.merge()` is deprecated. Use `schemaA.extend(schemaB.shape)` or `z.object({ ...A.shape, ...B.shape })` for better TypeScript inference performance. |
| Z-07 | 🟡 Medium | **Use `z.guid()` for non-strict UUID validation.** `z.uuid()` in v4 enforces RFC 9562/4122 variant bits — stricter than v3. Use `z.guid()` when validating UUIDs that may not be fully compliant (test data, legacy systems). |
| Z-08 | 🟡 Medium | **Use `z.treeifyError()` for error formatting.** `.format()` and `.flatten()` on `ZodError` are deprecated. Access issues via `.issues` not `.errors`. |

---

## Redux Toolkit and RTK Query review rules: tags are non-negotiable

RTK 2.11.2 is current and stable. RTK Query's value proposition for RunCoach is clear: it handles API caching for coach interactions, workout plans, and user data without a separate data-fetching library. The most common RTK Query bugs in production stem from **missing tag invalidation on mutations** and **copying RTKQ cache into separate Redux slices**.

| ID | Severity | Rule |
|---|---|---|
| RQ-01 | 🔴 Critical | **Every mutation must declare `invalidatesTags`.** Missing tag invalidation causes stale cache data after mutations. Use specific `{ type: 'Workout', id: arg.id }` tags for updates/deletes, and `{ type: 'Workout', id: 'LIST' }` for creates. |
| RQ-02 | 🔴 Critical | **API middleware must be in the store.** Flag `configureStore` missing `.concat(api.middleware)`. Without it, RTKQ caching, invalidation, and polling silently fail. Also require `setupListeners(store.dispatch)` for `refetchOnFocus`/`refetchOnReconnect`. |
| RQ-03 | 🔴 Critical | **Optimistic updates must include rollback.** Any `onQueryStarted` with `api.util.updateQueryData` must wrap `await queryFulfilled` in `try/catch` and call `patchResult.undo()` on error. Optimistic data without rollback corrupts the UI on failure. |
| RQ-04 | 🟠 High | **Use specific tag IDs, not broad invalidation.** Flag mutations invalidating bare `['Workout']` — this refetches ALL Workout queries. Prefer `[{ type: 'Workout', id: arg.id }]` for targeted cache invalidation. |
| RQ-05 | 🟠 High | **Use `selectFromResult` for partial data needs.** Flag components destructuring large query results when only a subset is used. `selectFromResult` prevents re-renders when unrelated parts of the RTKQ cache update. |
| RQ-06 | 🟠 High | **Don't duplicate RTKQ cache in Redux slices.** Flag `extraReducers` that copy RTKQ-fetched data into separate slices. This creates stale data bugs. Let RTKQ own server-state; use slices only for truly global client state (auth session, UI preferences, cross-module coordination). |
| RQ-07 | 🟡 Medium | **Set `keepUnusedDataFor` intentionally per endpoint.** Default is 60 seconds. Set shorter for frequently-changing data (AI coach responses), longer for static reference data (exercise catalogs). Flag API definitions with no explicit cache lifetime configuration. |
| RQ-08 | 🟡 Medium | **Don't chain mutations sharing the same tags.** `.then(() => triggerMutation())` where both mutations invalidate the same tags creates timing issues. Use `Promise.all()` for parallel mutations instead. |

---

## React Hook Form review rules: watch() is the silent performance killer

React Hook Form v7.72.0 continues its uncontrolled-first philosophy. The most impactful recent additions are `FormStateSubscribe` for granular re-render control and memoized `FormProvider` context values. The most common performance issue in RHF codebases is **unscoped `watch()` calls** that trigger full-component re-renders on every keystroke.

| ID | Severity | Rule |
|---|---|---|
| HF-01 | 🔴 Critical | **No unscoped `watch()`.** `const values = watch()` re-renders the entire component on every field change. Use `useWatch({ name: 'specificField' })` for targeted subscriptions, or `<FormStateSubscribe>` for granular control. |
| HF-02 | 🔴 Critical | **Use `field.id` as key in `useFieldArray`.** Flag `fields.map((field, index) => <div key={index}>)` — using `index` causes field state loss on reorder/remove. Must be `<div key={field.id}>`. |
| HF-03 | 🟠 High | **Use `Controller` for controlled components.** Flag direct `register()` on components that don't expose native `ref` (shadcn Select, Checkbox, DatePicker, rich text editors). Use `<Controller>` or `useController()` to properly connect them. This is required for all shadcn/ui form components. |
| HF-04 | 🟠 High | **Provide complete `defaultValues` in `useForm()`.** Missing defaults cause fields to start as `undefined`, triggering uncontrolled→controlled warnings and breaking `reset()`. Always define `defaultValues` matching the full Zod schema shape. |
| HF-05 | 🟠 High | **Include ARIA attributes on all form fields.** Minimum: `aria-invalid={!!fieldState.error}`, `aria-describedby` linking to error and help text elements, `role="alert"` on error containers. shadcn/ui's `<FormControl>` handles this automatically — flag any custom form components that bypass it. |
| HF-06 | 🟠 High | **Use `FormProvider` for nested form components.** Flag prop-drilling of `register`, `control`, `errors` through multiple layers. Wrap form in `<FormProvider {...form}>` and use `useFormContext()` in children. shadcn/ui's `<Form>` is already a FormProvider. |
| HF-07 | 🟡 Medium | **Use `replace()` not `setValue()` for field arrays.** Recent RHF versions changed behavior: `setValue('arrayField', newArray)` no longer directly updates field array state. Use `replace()` from `useFieldArray` instead. |
| HF-08 | 🟡 Medium | **Update generic Zod form helpers for v4.** Flag `useZodForm<T extends z.ZodType>` or `z.ZodTypeAny` usage — Zod v4's changed generics break these patterns. Fix: `<T extends z.ZodType<any, any>>` with explicit type annotations. |

---

## ESLint v9 flat config rules: three plugins to add

ESLint 9's flat config (`eslint.config.js`) with `defineConfig` from `eslint/config` is the current standard. The project's plugin set is solid but missing three important additions: **`eslint-plugin-jsx-a11y`** (accessibility), **`eslint-plugin-testing-library`** (RTL anti-patterns), and **`eslint-plugin-simple-import-sort`** (import organization).

| ID | Severity | Rule |
|---|---|---|
| E-01 | 🔴 Critical | **Use `defineConfig` and `globalIgnores` from `eslint/config`.** Provides type safety, auto-flattening, and `extends` support. Use `globalIgnores(['dist/', 'node_modules/'])` instead of bare `{ ignores: [...] }` objects. |
| E-02 | 🔴 Critical | **Configure `rules-of-hooks` as error.** The `eslint-plugin-react-hooks` `rules-of-hooks` rule must be `"error"`, never `"warn"`. Hook ordering violations cause runtime crashes, not just suboptimal code. |
| E-03 | 🟠 High | **Use `tseslint.config()` helper with strict presets.** Use `tseslint.configs.strict` or `recommendedTypeChecked` — not just `recommended`. Always scope TS rules to `files: ['**/*.ts', '**/*.tsx']`. Enable `no-floating-promises` and `consistent-type-imports` as errors. |
| E-04 | 🟠 High | **Add `eslint-plugin-jsx-a11y`.** Catches inaccessible JSX at lint time. Essential rules: `alt-text`, `anchor-is-valid`, `click-events-have-key-events`, `label-has-associated-control`. Use `recommended` flat config preset. |
| E-05 | 🟡 Medium | **Disable React rules obsoleted by TS and React 17+.** Turn off `react/react-in-jsx-scope`, `react/prop-types`, `react/display-name`. Apply `reactPlugin.configs.flat['jsx-runtime']` to handle this in bulk. |
| E-06 | 🟡 Medium | **Add `eslint-plugin-testing-library` for test files.** Scope to `files: ['**/*.spec.tsx', '**/*.test.tsx']`. Catches RTL anti-patterns like `getByTestId` overuse, `container.querySelector`, and improper `waitFor` usage automatically. |
| E-07 | 🔵 Low | **Add import sorting.** Install `eslint-plugin-simple-import-sort` for consistent import grouping (builtin → external → internal → sibling). Simpler to configure than `eslint-plugin-import` in flat config, and pairs well with the `~/` path alias convention. |

---

## Conclusion: what to do first

The RunCoach frontend stack is well-chosen and nearly fully current. **Three actions deserve immediate attention**: fix the Vite version pin to `^8.0.0`, enable React Compiler via `babel-plugin-react-compiler` (free performance wins and eliminates manual memoization debates), and add `eslint-plugin-jsx-a11y` (accessibility enforcement from day one is far cheaper than retrofitting).

The biggest review-rule themes across all libraries converge on four principles. First, **React 19 Actions and `use()` replace manual state management boilerplate** — flag any `useState` + `useEffect` fetch patterns or manual `isPending` tracking. Second, **Zod v4's behavioral changes are subtle and dangerous** — the `.default()` + `.optional()` interaction and stricter UUID validation will silently change data shapes in forms. Third, **RTK Query tag invalidation is the #1 source of production cache bugs** — every mutation needs explicit tags, and RTKQ cache should never be duplicated into Redux slices. Fourth, **accessibility is non-negotiable** — between shadcn/ui's Radix primitives, `jsx-a11y` linting, and proper ARIA attributes on RHF forms, the tooling exists to catch issues before they reach users.

For the planned TypeScript 6.0 and ESLint 10 migrations, wait 2–3 months for ecosystem stabilization. Monitor Biome v2 as a potential future replacement for ESLint + Prettier, and keep an eye on `@zod/mini` for bundle-size optimization once the component library grows.
