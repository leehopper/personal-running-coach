Implemented Slice 6 PR-A within the permitted file list.

## Files

Changed:

- `frontend/src/app/modules/settings/pages/settings.page.tsx`
- `frontend/src/app/modules/settings/pages/settings-page.spec.tsx`
- `frontend/src/app/modules/settings/components/theme-toggle.component.tsx`
- `frontend/src/app/modules/settings/components/theme-toggle.component.spec.tsx`
- `frontend/src/app/modules/settings/components/units-toggle.component.tsx`
- `frontend/src/app/modules/settings/components/units-toggle.component.spec.tsx`
- `frontend/src/app/modules/settings/components/regenerate-plan-dialog.component.tsx`
- `frontend/src/app/modules/settings/components/regenerate-plan-dialog.component.spec.tsx`
- `frontend/src/app/modules/auth/hooks/auth.hooks.ts`
- `frontend/src/app/modules/auth/components/require-auth.component.spec.tsx`
- `frontend/e2e/regenerate-plan.spec.ts`
- `frontend/vite.config.ts`
- `frontend/tsconfig.node.json`
- `frontend/src/vite-env.d.ts`
- `frontend/package.json`
- `frontend/package-lock.json`
- `frontend/scripts/check-contrast.ts`
- `frontend/scripts/__tests__/check-contrast.spec.ts`

New:

- `frontend/src/app/modules/auth/hooks/auth.hooks.spec.tsx`
- `frontend/e2e/sign-out.spec.ts`

## Gates

- `npm version 0.9.0 --no-git-tag-version`
  - Last output: `v0.9.0`

- `npm run build`
  - PASS. Last output: `- Adjust chunk size warning limit (or use code-splitting) to reduce the warning.`
  - Build completed successfully with 0 errors.
  - `rg -l '0\.9\.0' dist` found the injected version in the built bundle.

- Focused Vitest command covering all changed and added unit specs
  - PASS. Last relevant output: `Tests 57 passed (57)`

- `npx eslint` on all changed TypeScript files
  - PASS. Last output: no output, exit code 0.

- `npx prettier --check` on all changed files
  - PASS. Last output: `All matched files use Prettier code style!`

- `git diff --check`
  - PASS. Last output: no output, exit code 0.

- Backend `dotnet build RunCoach.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --disable-build-servers`
  - PASS. Last output: `Build succeeded.`

- Backend `dotnet format RunCoach.slnx --no-restore --verify-no-changes`
  - DEVIATION. Last output: `System.TimeoutException: The operation has timed out.`

- `npx vitest run scripts/__tests__/check-contrast.spec.ts`
  - DEVIATION. 59 tests passed and 2 subprocess tests failed because `tsx` could not create its IPC pipe.

## Acceptance table

### DU-1

- PASS, MECHANIZED: Settings frame, title, four sections, order, and dual-theme coverage in `settings.page.tsx:30-69` and `settings-page.spec.tsx:149`.
- PASS, MECHANIZED: Current-plan formatting and original `dateTime` in `settings.page.tsx:132-169` and `settings-page.spec.tsx:167`.
- PASS, MECHANIZED: Null macro handling in `settings-page.spec.tsx:175`.
- PASS, MECHANIZED: Blank goal omission in `settings-page.spec.tsx:183`.
- PASS, MECHANIZED: Invalid date fallback in `settings-page.spec.tsx:192`.
- PASS, MECHANIZED: Clay outline action, warning, and active contrast class in `settings.page.tsx:43-54` and `settings-page.spec.tsx:201`.
- PASS, MECHANIZED: Loading and error states preserved in `settings.page.tsx:115-129`.
- PASS, INSPECTED: Previous-plan and placeholder UI are absent from `settings.page.tsx`.

### DU-2

- PASS, MECHANIZED: Replacement dialog copy, textarea, max length, testids, and cancel action in `regenerate-plan-dialog.component.tsx:137-195`.
- PASS, MECHANIZED: Counter strings use `500 left`, `250 left`, and `0 left`.
- PASS, MECHANIZED: Trimmed intent and empty-intent omission remain covered by the focused dialog suite.
- PASS, MECHANIZED: Idempotency key remains stable across retries.
- PASS, MECHANIZED: Building surface replaces the panel while loading in `regenerate-plan-dialog.component.tsx:105-110` and `regenerate-plan-dialog.component.spec.tsx:103`.
- PASS, MECHANIZED: Success closes and navigates to `/` in `regenerate-plan-dialog.component.tsx:87-95`.
- PASS, MECHANIZED: Failure preserves intent and key.
- PASS, MECHANIZED: Idle Cancel, Escape, and backdrop behavior remain covered.
- PASS, MECHANIZED: Loading dismissal is blocked in `regenerate-plan-dialog.component.tsx:71-81` and `regenerate-plan-dialog.component.spec.tsx:131`.
- PASS, MECHANIZED: Generic 400 alert behavior remains covered.
- DEVIATION: Playwright validation of `frontend/e2e/regenerate-plan.spec.ts` was not runnable in this sandbox.

### DU-3

- PASS, MECHANIZED: Theme option order in `theme-toggle.component.spec.tsx:63`.
- PASS, MECHANIZED: Units option order in `units-toggle.component.spec.tsx:54`.
- PASS, MECHANIZED: Account email rendering in `settings.page.tsx:68-85`.
- PASS, MECHANIZED: Logout POST-first sequence in `auth.hooks.ts:39-51` and `auth.hooks.spec.tsx:116`.
- PASS, MECHANIZED: Local RTK Query purge and refetch in `auth.hooks.spec.tsx:137`.
- PASS, MECHANIZED: Exact logout broadcast in `auth.hooks.spec.tsx:160`.
- PASS, MECHANIZED: Receiver cleanup order in `auth.hooks.ts:110-119` and `auth.hooks.spec.tsx:176`.
- PASS, MECHANIZED: Rejected logout reporting and cleanup in `auth.hooks.spec.tsx:192`.
- PASS, MECHANIZED: Live require-auth redirect after `loggedOut()` in `require-auth.component.spec.tsx:88`.
- DEVIATION: Playwright validation of `sign-out.spec.ts` was not runnable in this sandbox.

### DU-4

- PASS, MECHANIZED: Version footer literal and dual-theme test in `settings.page.tsx:92-98` and `settings-page.spec.tsx:237`.
- PASS, MECHANIZED: Vite version define in `vite.config.ts:211-216`.
- PASS, MECHANIZED: JSON module support in `tsconfig.node.json:12`.
- PASS, MECHANIZED: Version declaration in `src/vite-env.d.ts:4`.
- PASS, MECHANIZED: Package versions are `0.9.0`.
- PASS, MECHANIZED: Both required contrast pairs exist in `scripts/check-contrast.ts:107-114`.
- PASS, MECHANIZED: Contrast count assertion is updated to 27 pairs and 54 results in `scripts/__tests__/check-contrast.spec.ts:456`.
- DEVIATION: The full `npm run check-contrast` subprocess gate could not run because `tsx` cannot open its IPC pipe here.
- PASS, MECHANIZED: Regenerate action includes `active:text-secondary-foreground` in `settings.page.tsx:47`.

### DU-5

- DEVIATION, INSPECTED: DU-5 is PR-B-owned auth poster, login, register, and password-toggle work. Those files are explicitly outside this PR-A file list and were not modified.

### DU-6

- PASS, MECHANIZED: `npm run build` ran code generation and produced no generated-file diff.
- PASS, MECHANIZED: ESLint passed on all changed TypeScript files.
- DEVIATION: Full Playwright validation requires the running host stack.

## Mutations

- `settings-page.spec.tsx:149`: remove `settings-account-section`.
- `settings-page.spec.tsx:167`: replace stable date formatting with `toLocaleString()`.
- `settings-page.spec.tsx:175`: render undefined macro fields.
- `settings-page.spec.tsx:183`: join an empty goal description.
- `settings-page.spec.tsx:192`: return an empty string for invalid dates.
- `settings-page.spec.tsx:201`: remove `border-clay-text`.
- `settings-page.spec.tsx:237`: remove the Vite define or hardcode the wrong version.
- `theme-toggle.component.spec.tsx:63`: swap Dark and Light options.
- `units-toggle.component.spec.tsx:54`: send a string instead of numeric units.
- `regenerate-plan-dialog.component.spec.tsx:103`: render the panel during loading.
- `regenerate-plan-dialog.component.spec.tsx:131`: call `onClose` on Escape while loading.
- `auth.hooks.spec.tsx:116`: dispatch `loggedOut()` before awaiting logout.
- `auth.hooks.spec.tsx:137`: omit `resetApiState()`.
- `auth.hooks.spec.tsx:160`: change the channel or message.
- `auth.hooks.spec.tsx:176`: remove receiver reset.
- `auth.hooks.spec.tsx:192`: move cleanup out of `finally`.
- `require-auth.component.spec.tsx:88`: snapshot auth state instead of subscribing.
- `check-contrast.spec.ts:456`: remove one contrast pair or retain the old count.
- `regenerate-plan.spec.ts:357`: add an explicit `page.goto('/')`.
- `sign-out.spec.ts:75`: remove the sign-out handler.
- `sign-out.spec.ts:89`: remove the logout broadcast.

## Deviations

The orchestrator must run:

```text
cd frontend && npm run e2e
cd frontend && npm run check-contrast
cd frontend && npm run test
cd backend && dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class <FullName>
cd frontend && npm run codegen:check
```

Reasons:

- E2E requires the running application stack.
- `check-contrast` and the full frontend suite use `tsx`, which cannot create its IPC pipe in this sandbox.
- Backend test hosts cannot create their named pipe.
- No backend tests were added or changed, so no specific backend test class applies.
- Codegen was exercised through the build, but the standalone orchestrator gate was not run.

## Open questions

None.

STAGE COMPLETE

Codex session ID: 01a076e5-225f-7451-b2b7-d56a72ac28db
Resume in Codex: codex resume 01a076e5-225f-7451-b2b7-d56a72ac28db
