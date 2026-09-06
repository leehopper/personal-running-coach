# Slice 6 PR-A build: orchestrator runs (host, 2026-09-06)

Run from the orchestrating session against the Settings clone at 4de63051 (the build commit). The sandbox cannot run these; the review lenses treat them as measured fact.

## Frontend gates

```text
=== npm run test
⎯⎯⎯⎯⎯⎯⎯ Failed Tests 1 ⎯⎯⎯⎯⎯⎯⎯
 FAIL  scripts/__tests__/check-contrast.spec.ts > check-contrast script (integration) > exits 0 and reports all pairs passing against the committed tokens
 Test Files  1 failed | 86 passed (87)
      Tests  1 failed | 1052 passed (1053)
=== check-contrast
check-contrast: all 54 pairs pass WCAG thresholds.
=== eslint touched
eslint rc=0
=== prettier
All matched files use Prettier code style!
=== codegen:check
codegen rc=0
=== npm run build
- Adjust chunk size limit for this warning via build.chunkSizeWarningLimit.
dist/assets/index-TSk3WVEQ.js:3
PRA-HOST-GATES DONE
```

The one vitest failure is `check-contrast.spec.ts` > `exits 0 and reports all pairs passing against the committed tokens`: the script prints the RESULT count (`all 54 pairs pass WCAG thresholds`, 27 pairs x 2 modes) while the spec text locked the string as `all 27 pairs pass`, so the build wrote the wrong literal. The host `npm run check-contrast` itself passes 54/54. This is a spec wording error, recorded as fix F1 for round 1. `dist/assets/index-*.js:3` is the count of `0.9.0` occurrences in the built bundle (the version define reached the build).

## Playwright e2e (host stack: Compose Postgres + Redis on Colima, host API from the main tree at 87718f5f on https://localhost:5001, Vite started by Playwright from this clone)

```text
1) [chromium] › e2e/plan-render.spec.ts:242:1 › register → land on / → plan renders → reload → identical content + no vdot in DOM
2) [chromium] › e2e/workout-logging.spec.ts:192:1 › register → log a minimum + a rich workout → both appear in week-grouped history
Error: expect(locator).toBeVisible() failed
Error: strict mode violation: getByText('8.0 km') resolved to 2 elements:
2 failed
16 passed (7.0s)
```

The two failures are the journeys already recorded as red on `main` before this slice (cycle plan, 2026-09-05 row): `plan-render.spec.ts:242` and `workout-logging.spec.ts:192`. Neither touches a PR-A surface. The two new `sign-out.spec.ts` journeys (Settings sign-out clears the session; sign-out broadcasts to a second tab) and the realigned `regenerate-plan.spec.ts` journey (building overlay visible during the delayed POST, then `/` with Plan B) passed.
