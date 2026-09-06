## Files

CHANGED: 15 requested files, including auth pages/forms, shell, text field, auth E2E, and six exact password-selector realignments.

NEW:

- `frontend/src/app/modules/auth/components/auth-poster-header.component.tsx`
- `frontend/src/app/modules/auth/components/auth-poster-header.component.spec.tsx`
- `frontend/src/app/modules/auth/components/password-visibility-toggle.component.tsx`
- `frontend/src/app/modules/auth/components/password-visibility-toggle.component.spec.tsx`

Testing-strategy audit added dual-theme toggle coverage and explicit direct-child/wrapper assertions.

## Gates

- PASS `npm run build`; last output: `- Adjust build.chunkSizeWarningLimit.`
- PASS login Vitest; `Tests 18 passed (18)`.
- PASS register Vitest; `Tests 17 passed (17)`.
- PASS poster-header Vitest; `Tests 4 passed (4)`.
- PASS password-toggle Vitest; `Tests 3 passed (3)`.
- PASS ESLint on all changed TypeScript files; no output, exit 0.
- PASS Prettier on all changed files; `All matched files use Prettier code style!`
- PASS `npm run codegen:check`; last output: `src/app/api/generated/zod/workout-logs/workout-logs.ts 3ms`.
- PASS backend build; last output: `Time Elapsed 00:00:08.00`.
- PASS `git diff --check`; no output, exit 0.
- DEVIATION `dotnet format RunCoach.slnx --no-restore --verify-no-changes`; timed out with no output.

## Acceptance table

| Criterion | Result |
|---|---|
| DU-5.1 poster header | PASS - MECHANIZED at `auth-poster-header.component.tsx:3` |
| DU-5.2 login heading | PASS - MECHANIZED at `login.page.spec.tsx:90` |
| DU-5.3 register heading | PASS - MECHANIZED at `register.page.spec.tsx:106` |
| DU-5.4 fields and autocomplete | PASS - MECHANIZED at `auth-text-field.component.tsx:57` |
| DU-5.5 visibility toggle | PASS - MECHANIZED at `password-visibility-toggle.component.tsx:14` |
| DU-5.6 page visibility behavior | PASS - MECHANIZED at `login.page.spec.tsx:140` and `register.page.spec.tsx:149` |
| DU-5.7 exact password helpers | PASS - MECHANIZED at `login.page.spec.tsx:61` and `register.page.spec.tsx:70` |
| DU-5.8 OAuth reserves | PASS - MECHANIZED at `auth-form-shell.component.tsx:64` |
| DU-5.9 primary and pending copy | PASS - MECHANIZED at `login-form.component.tsx:23` and `register-form.component.tsx:22` |
| DU-5.10 auth links | PASS - MECHANIZED at `login.page.tsx:71` and `register.page.tsx:85` |
| DU-5.11 register helper | PASS - MECHANIZED at `register-form.component.tsx:39` |
| DU-5.12 preserved auth flows | PASS - MECHANIZED, all 35 page tests pass |
| DU-5.13 auth E2E poster and eye checks | DEVIATION - INSPECTED at `frontend/e2e/auth.spec.ts:91`; host E2E was not runnable |
| DU-6.1 codegen drift | PASS - MECHANIZED via `npm run codegen:check` |
| DU-6.2 lint | PASS - MECHANIZED via ESLint |
| DU-6.3 E2E realignment | DEVIATION - INSPECTED exact selectors at `frontend/e2e/auth.spec.ts:93` and the six listed fixtures; host E2E was not runnable |

## Mutations

The test-first run was red: `Test Files 4 failed (4)` and `Tests 12 failed | 22 passed (34)`.

- `renders the Split wordmark name`: remove `aria-label="Split"`.
- `renders the poster rule`: remove `bg-rule`.
- `renders the tagline`: remove the tagline paragraph.
- `keeps parity in both themes` for the header: add a raw hex class.
- `shows the password action when hidden`: omit `aria-pressed`.
- `flips the pressed state and accessible name`: make `onToggle` a no-op.
- `keeps parity in both themes` for the toggle: add a raw hex class.
- Login poster heading test: remove `AuthPosterHeader` or make the heading visible.
- Login parity test: add a raw hex class.
- Login field test: change `current-password` to another autocomplete value.
- Login visibility test: stop flipping the input type.
- Login OAuth test: add a child to the reserve.
- Login link test: replace `Create account \u2192` with old copy.
- Login pending test: replace `Signing in\u2026` with three periods.
- Register poster heading test: pass `headingVisuallyHidden`.
- Register parity test: add a raw hex class.
- Register field test: change `new-password` to another autocomplete value.
- Register visibility test: mutate autocomplete when visibility changes.
- Register OAuth test: add an OAuth child.
- Register link test: replace `Sign in \u2192` with old copy.
- Register pending test: replace `Creating account\u2026` with three periods.

## Deviations

- `docs/plans/split-ui-cycle/slice-6-evidence/recon/adjudication.md` and `recon/design-extract.md` were absent. The spec was used as the authoritative fallback.
- `npm run e2e` was not run because the host stack is unavailable. The orchestrator must run it.
- `npm run check-contrast` was not run due the sandbox restriction. The orchestrator must run it.
- `npm run test` was not run because the sandbox cannot open the `tsx` IPC pipe. The orchestrator must run it.
- `dotnet format RunCoach.slnx --no-restore --verify-no-changes` timed out in the sandbox. The orchestrator should rerun it.
- No backend tests were added or changed. No backend test command applies.

## Open questions

None.

STAGE COMPLETE

Codex session ID: 01a076e4-fbe3-7d13-baf7-fd513bb20f50
Resume in Codex: codex resume 01a076e4-fbe3-7d13-baf7-fd513bb20f50

## Orchestrator addendum (2026-09-06, after the round-1 lenses)

The commit 8aa14029 carries 22 paths, not the 19 the report lists: the 19 frontend paths above plus three orchestrator-added evidence files (`docs/plans/split-ui-cycle/slice-6-evidence/pr-b/build-brief.txt`, `build-vars.json`, `build-report.md`), which the recipe commits with the build. A later docs commit added `build-orchestrator-runs.md`.

Host results supersede the E2E and full-suite deviations above (recorded in `build-orchestrator-runs.md`): `npm run test` 1058/1058; `npm run check-contrast` 50/50 (PR-B adds no pair); `npm run codegen:check` exit 0; `npm run build` clean; ESLint exit 0 and Prettier clean on every touched source and e2e file; Playwright 14 passed, 2 failed, both the journeys already red on `main` before this slice (`plan-render.spec.ts:242`, `workout-logging.spec.ts:192`), outside PR-B surfaces. DU-5.13 and DU-6.3 are therefore PASS on the host, not deviations. `dotnet format` is not a PR-B gate: no C# file changed.

