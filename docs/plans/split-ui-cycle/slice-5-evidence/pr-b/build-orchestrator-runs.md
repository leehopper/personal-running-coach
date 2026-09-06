# Slice 5 PR-B build stage: orchestrator runs (2026-09-05)

Host-side gates the sandbox could not run, executed from the orchestrating session against the PR-B clone after the luna build stage.

## Frontend gates

```text
== npm run build ==
src/app/api/generated/zod/client-errors/client-errors.ts 2ms

transforming...[ok] 2441 modules transformed.
[ok] built in 289ms
== npm run test ==
 Test Files  86 passed (86)
      Tests  1035 passed (1035)
== npm run lint (full eslint) ==

? 7 problems (4 errors, 3 warnings)

lint rc=0
== npm run check-contrast ==

check-contrast: all 50 pairs pass WCAG thresholds.
== prettier check on touched ==
Checking formatting...
All matched files use Prettier code style!
== codegen:check ==
codegen:check rc=0
PRB-GATES DONE
```

`npm run lint` (full `eslint .`) reports 4 errors and 3 warnings, all in files this PR does not touch (`e2e/auth.spec.ts`, `login.page.spec.tsx`, `register.page.spec.tsx`: `sonarjs/no-hardcoded-passwords` on test fixtures; `badge.tsx`, `button.tsx`, `form.tsx`: `react-refresh/only-export-components`). The same problems are reported on `main`; CI does not run `npm run lint`. Per-file eslint on every touched file is clean (implementer gate and the round-1 lenses).

## Playwright e2e (host stack: Compose Postgres + Redis on Colima, host API from this clone on https://localhost:5001, Vite started by Playwright)

First run failed on every test with `browserType.launch: Executable doesn't exist` (the Playwright bump needs Chromium headless shell v1234); `npx playwright install chromium` fixed it. Rerun:

```text
  [fail]   6 [chromium] ? e2e/plan-render.spec.ts:242:1 ? register -> land on / -> plan renders -> reload -> identical content + no vdot in DOM (2.5s)
  [fail]  16 [chromium] ? e2e/workout-logging.spec.ts:192:1 ? register -> log a minimum + a rich workout -> both appear in week-grouped history (2.6s)
  2 failed
  14 passed (8.0s)
```

The onboarding journey (`e2e/onboarding.spec.ts`, realigned in this PR) passes. The two failures are pre-existing on `main`: the same two specs fail identically when run from `main`'s frontend against the same API (`plan-render.spec.ts:242` compares Today's header innerHTML across a reload; `workout-logging.spec.ts:192` hits a Playwright strict-mode violation because `getByText('8.0 km')` matches both the Log Book ledger row and the week aggregate). Both are frontend-only assertions on surfaces this PR does not touch; captured as a cycle-plan follow-up for Slice 7's e2e consolidation.

## Backend build (recorded for the round-1 conformance lens; no C# changed in PR-B)

```text
dotnet build RunCoach.slnx --no-restore -> 0 Warning(s), 0 Error(s)
```
