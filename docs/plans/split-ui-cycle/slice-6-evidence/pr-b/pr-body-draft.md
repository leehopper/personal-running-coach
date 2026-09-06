**Slice 6 (Settings & Auth) PR-B (`feat/slice-6-auth`) recomposes sign-in and register as the SPLIT poster and adds password visibility. Its sibling is Slice 6 PR-A (`feat/slice-6-settings`), an independent Settings and sign-out PR; both branch from `main`, and either may merge first. The shared `frontend/e2e/regenerate-plan.spec.ts` password line is byte-identical in both PRs.** (`docs/specs/slice-6-settings-auth/pr-strategy.md`)

Spec: `docs/specs/slice-6-settings-auth/spec.md` sections 1.A, 1.C, 3 PR-B, 4.2, and 9 (gitignored working-tree artifact). Committed evidence: `docs/plans/split-ui-cycle/slice-6-evidence/pr-b/`.

## What's here

- Both auth pages remove `Card` and use `mx-auto flex min-h-dvh w-full max-w-md flex-col justify-center gap-[26px] bg-background px-[26px] pb-10` for the centered Alpine `main`. (`frontend/src/app/pages/login/login.page.tsx`, `frontend/src/app/pages/register/register.page.tsx`)
- The poster header uses `Wordmark size="poster"`, a hidden `h-0.5 w-16 bg-rule` rule, and the tagline `The plan adapts. You do the work.` (`frontend/src/app/modules/auth/components/auth-poster-header.component.tsx`)
- Fields retain the `Email` and `Password` labels, 48px inputs, `FormItem` gap `1.5`, and form rhythm `gap-3.5`. (`frontend/src/app/modules/auth/components/auth-text-field.component.tsx`, `frontend/src/app/modules/auth/components/auth-form-shell.component.tsx`)
- `PasswordVisibilityToggle` is ghost icon control with `Show password` and `Hide password` names. It sits in a relative wrapper around `FormControl`, while the native input remains its direct child. (`frontend/src/app/modules/auth/components/password-visibility-toggle.component.tsx`, `frontend/src/app/modules/auth/components/auth-text-field.component.tsx`)
- Visibility changes only the input type. Login keeps `current-password`, register keeps `new-password`, and both retain `email` autocomplete. (`frontend/src/app/modules/auth/components/auth-text-field.component.tsx`, `frontend/src/app/pages/login/login-form.component.tsx`, `frontend/src/app/pages/register/register-form.component.tsx`)
- Login keeps a visually hidden `Sign in` h1. Register shows `Start here` as the visible heading. (`frontend/src/app/modules/auth/components/auth-form-shell.component.tsx`, `frontend/src/app/pages/login/login-form.component.tsx`, `frontend/src/app/pages/register/register-form.component.tsx`)
- Primary buttons retain their submit and pending copy with `h-[52px]`. Each page has an empty `auth-oauth-reserve` with `min-h-[52px]`. (`frontend/src/app/modules/auth/components/auth-form-shell.component.tsx`, `frontend/src/app/pages/login/login-form.component.tsx`, `frontend/src/app/pages/register/register.page.tsx`)
- Secondary links target `/register` and `/login`, include `Create account \u2192` and `Sign in \u2192`, and use `hit-target-44`. Their ring is `focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/[0.22]`. (`frontend/src/app/pages/login/login.page.tsx`, `frontend/src/app/pages/register/register.page.tsx`)
- Register adds the mono helper: `12 characters or more, with an uppercase letter, a lowercase letter, a digit, and a symbol.` (`frontend/src/app/pages/register/register-form.component.tsx`)
- Unit helpers and seven E2E files use exact `Password` lookups. Exact matching avoids strict-mode ambiguity from the toggle's `Show password` accessible name. (`docs/specs/slice-6-settings-auth/spec.md`, `frontend/e2e/auth.spec.ts`, `frontend/e2e/shell-navigation.spec.ts`, `frontend/e2e/onboarding.spec.ts`, `frontend/e2e/plan-render.spec.ts`, `frontend/e2e/workout-logging.spec.ts`, `frontend/e2e/conversation-streaming.spec.ts`, `frontend/e2e/regenerate-plan.spec.ts`)
- Existing schemas, `parseProblem` error mapping, `form-alert`, register-then-login chaining, navigation, and cookie assertions remain. (`frontend/src/app/modules/auth/schemas/auth.schema.ts`, `frontend/src/app/api/generated/zod/auth/auth.ts`, `frontend/src/app/modules/auth/helpers/problem-details.helpers.ts`, `frontend/src/app/pages/login/login-form.component.tsx`, `frontend/src/app/pages/register/register-form.component.tsx`, `frontend/e2e/auth.spec.ts`)

## Decisions and deviations

- Register has no artboard. Its mirrored poster, `Start here` heading, helper, and OAuth reserve follow the orchestrator ruling. (`docs/plans/split-ui-cycle/slice-6-evidence/redteam/adjudication.md`, `docs/specs/slice-6-settings-auth/spec.md`)
- The OAuth reserve is empty and reserves one primary-button height, 52px. (`docs/specs/slice-6-settings-auth/spec.md`)
- The eye toggle keeps the icon button's 44px target while its visual icon remains small. (`docs/specs/slice-6-settings-auth/spec.md`)
- The sign-in h1 is visually hidden while remaining the page heading. (`frontend/src/app/pages/login/login-form.component.tsx`)
- Four `sonarjs/no-hardcoded-passwords` suppressions follow the existing two-file precedent in `shell-navigation.spec.ts` and `regenerate-plan.spec.ts`. (`docs/specs/slice-6-settings-auth/spec.md`, `frontend/eslint.config.js`)

## Review trail

- The two-family spec red-team found ambiguous password queries in unit and E2E specs, missing link hit targets and focus rings, and missing form rhythm assertions. (`docs/plans/split-ui-cycle/slice-6-evidence/redteam/adjudication.md`)
- Round 1 logged 33 mutation entries and 39 conformance checks. The mutation lens reported six findings, and conformance reported two. (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/round1/mutation.json`, `docs/plans/split-ui-cycle/slice-6-evidence/pr-b/round1/conformance.json`)
- Fixes F1 to F5 added link target and ring classes, form rhythm, poster-frame, toggle-geometry, and primary-geometry assertions. (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/round1/fix-list.txt`, `docs/plans/split-ui-cycle/slice-6-evidence/pr-b/round1/fix-report.md`)
- Eight host E2E mutation replays were all RED. Seven reverted password selectors hit strict mode, and tagline removal failed its visibility assertion. (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/round1/orchestrator-runs.md`)
- No verify report is present under `docs/plans/split-ui-cycle/slice-6-evidence/pr-b/round2/`, so no verify-lens result is claimed.

## Green

- Host `npm run test` passed 1058/1058. `npm run check-contrast` passed 50/50, and PR-B adds no contrast pair. `npm run codegen:check` exited 0. (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/build-orchestrator-runs.md`)
- Host `npm run build` was clean. ESLint exited 0, and Prettier was clean on every touched source and E2E file. (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/build-orchestrator-runs.md`)
- Playwright recorded 14 passed. `plan-render.spec.ts:242` and `workout-logging.spec.ts:192` remain the two journeys already red on `main`. (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/build-orchestrator-runs.md`)
