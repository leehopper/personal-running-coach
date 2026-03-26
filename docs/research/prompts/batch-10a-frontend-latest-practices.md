# Research Prompt: Batch 10a — R-021

# Frontend Library Versions and Code Review Best Practices (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: Validate current frontend library versions and establish code review rules for a React 19 + TypeScript SPA

Context: I'm building a React SPA frontend for an AI-powered running coach application (RunCoach). The frontend is early-stage — scaffolding is complete with module-first architecture, but no feature modules are implemented yet. I need to (1) confirm we're on the latest stable versions of all libraries, and (2) establish comprehensive code review rules for an AI reviewer (REVIEW.md) that catches real problems in PRs.

Current pinned versions (from package.json, `^` semver ranges):

**Dependencies:**

- react: ^19.2.4, react-dom: ^19.2.4
- react-router-dom: ^7.13.1
- @reduxjs/toolkit: ^2.11.2, react-redux: ^9.2.0
- tailwindcss: ^4.2.2, @tailwindcss/vite: ^4.2.2
- zod: ^4.3.6
- react-hook-form: ^7.71.2, @hookform/resolvers: ^5.2.2

**Dev Dependencies:**

- typescript: ~5.9.3
- vite: ^8.0.1
- vitest: ^4.1.0, @vitest/coverage-v8: ^4.1.0
- @testing-library/react: ^16.3.2, @testing-library/jest-dom: ^6.9.1
- eslint: ^9.39.4, typescript-eslint: ^8.57.1
- eslint-plugin-react-hooks: ^7.0.1, eslint-plugin-react-refresh: ^0.5.2, eslint-plugin-sonarjs: ^4.0.2
- prettier: ^3.8.1
- jsdom: ^29.0.1

**Not yet installed but planned:** shadcn/ui components (Radix primitives), Playwright (E2E testing)

Established conventions (from frontend/CLAUDE.md):

- Module-first organization: `src/app/modules/{feature}/`
- File naming: `{name}.{type}.{extension}` (e.g., `user-profile.component.tsx`)
- Arrow functions for all components, named exports
- Component composition over render functions (never `renderX()` helpers)
- RTK Query for all HTTP, Redux for truly global state only, React Hook Form + Zod for forms
- TypeScript strict mode, no `any`, type imports with `import { type Foo }`
- `~/` path alias for cross-module imports
- Vitest + React Testing Library, co-located `.spec.tsx` files

What I need to learn:

### 1. Version Validation

For each library listed above:

- What is the actual latest stable version as of today?
- Are we on it, behind, or using a version that doesn't exist yet?
- If behind: what changed? Any breaking changes or important new features we should adopt?
- If any library has had a major version bump (e.g., Vite 7→8, Zod 3→4, Tailwind 3→4): what are the key migration notes and new patterns?

### 2. React 19 Specific Review Rules

- What are the new features in React 19 that change best practices? (Server Components, Actions, `use()` hook, `useFormStatus`, `useOptimistic`, `ref` as prop, etc.)
- Which React 18 patterns are now anti-patterns in React 19?
- What should a code reviewer flag in React 19 code? (e.g., unnecessary `forwardRef` wrappers, manual `useTransition` where Actions suffice, `useEffect` for data fetching where `use()` is better)
- React Compiler (React Forget) — is it stable? Should we enable it? Does it change memoization guidance?

### 3. TypeScript 5.9 Review Rules

- What are the latest TypeScript 5.9 features relevant to React development?
- Any new strict mode options we should enable?
- Anti-patterns that newer TypeScript catches or enables better alternatives for?
- `satisfies` operator patterns, `const` type parameters, decorator patterns — what's review-worthy?

### 4. Vite 8 + Vitest 4 Review Rules

- What changed in Vite 8? New configuration patterns, deprecated options, performance improvements?
- Vitest 4 — any new testing patterns, configuration changes, or breaking changes from v3?
- Testing anti-patterns to flag: improper async handling, missing cleanup, snapshot overuse, test isolation failures

### 5. Tailwind CSS v4 + shadcn/ui Review Rules

- Tailwind v4 is a major rewrite (CSS-first config, no `tailwind.config.js`). What are the new patterns?
- What Tailwind v3 patterns are now anti-patterns in v4?
- shadcn/ui best practices: when to customize vs use as-is, accessibility patterns, theming approach
- Common Tailwind mistakes a reviewer should catch: redundant utilities, responsive breakpoint ordering, dark mode handling

### 6. Zod v4 Review Rules

- Zod v4 was a major rewrite from v3. What are the breaking changes and new patterns?
- New features: `z.interface()`, `z.template()`, tree-shaking improvements, better error messages?
- Migration pitfalls from v3 patterns that still compile but are suboptimal in v4?
- Integration with React Hook Form — any changes in how resolvers work with v4?

### 7. Redux Toolkit + RTK Query Review Rules

- Latest RTK patterns and anti-patterns for 2026
- RTK Query: proper cache invalidation, optimistic updates, error handling, prefetching
- Common mistakes: over-caching, missing tag invalidation, incorrect mutation lifecycle handling
- When to use RTK Query vs local state vs React Router loaders

### 8. React Hook Form v7 Review Rules

- Latest patterns for form composition, nested forms, dynamic fields
- Integration patterns with Zod v4 and shadcn/ui components
- Anti-patterns: excessive re-renders from watch(), improper Controller usage, missing unregister cleanup
- Accessibility: proper ARIA attributes, error announcement, focus management

### 9. ESLint v9 Flat Config Review Rules

- ESLint v9 uses flat config (eslint.config.js). What are the best practices?
- Which rules from eslint-plugin-react-hooks, typescript-eslint, and sonarjs are most valuable?
- Custom rules worth adding? Missing plugins we should install?

### 10. Testing Best Practices (Vitest + RTL + Playwright)

- What should a code reviewer check in test code?
- React Testing Library anti-patterns: `getByTestId` overuse, not using `screen`, improper `waitFor`, testing implementation details
- Vitest patterns: proper mocking, timer handling, async assertions
- When to require tests: what coverage expectations are realistic for a new SPA?
- Playwright E2E: when to add E2E tests, page object patterns, test isolation

Output I need:

- A version audit table: library → our version → latest version → action needed
- For each library/area: 5-10 specific, actionable code review rules suitable for REVIEW.md (natural language, AI-reviewable)
- Anti-patterns to flag with severity (critical / high / medium / low)
- Any libraries we should add or replace (e.g., is there something better than react-router-dom v7 now?)
- Links to official migration guides and changelogs where relevant
