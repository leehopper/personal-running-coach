# Frontend — React 19 + TypeScript

> **Trademark rule — VDOT.** All user-visible strings (component text, page copy, form labels, tooltips, error messages, placeholder text, toast notifications) must use "Daniels-Gilbert zones" or "pace-zone index" — **not** "VDOT". The VDOT mark is enforced by The Run SMART Project LLC (Runalyze precedent). This is a hard rule for the frontend because every string on this tier is user-facing by definition. There is no carve-out for internal identifiers on the frontend — TypeScript type names, variables, and props that might flow into logs or telemetry should also avoid the term. See root `CLAUDE.md` and `NOTICE` for full context.

## Stack

See root CLAUDE.md for full tech stack. Additionally: eslint-plugin-sonarjs (build-time analysis), Zod v4.

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
          root-layout/
          error-boundary.component.tsx
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

- **Tailwind CSS** utility classes for styling
- **shadcn/ui** components (Radix primitives) — copy-paste into project
- Avoid inline styles — prefer Tailwind classes
- Mobile-first responsive design with Tailwind breakpoints

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

- **DOMPurify** for rendering any HTML content
- Environment variables for configuration — no secrets in client bundle
- Never commit API keys or credentials

## Testing

- **Vitest + React Testing Library**
- Co-located `.spec.tsx` files alongside components
- Focus on logic-heavy code: helpers, hooks, reducers
- Test components in isolation where possible

## Post-Change

See root CLAUDE.md checklist.
