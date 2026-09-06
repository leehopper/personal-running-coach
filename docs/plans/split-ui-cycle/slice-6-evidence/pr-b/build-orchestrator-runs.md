# Slice 6 PR-B build: orchestrator runs (host, 2026-09-06)

Run from the orchestrating session against the Auth clone at 8aa14029 (the build commit). The sandbox cannot run these; the review lenses treat them as measured fact.

## Frontend gates

```text
=== npm run test
 Test Files  88 passed (88)
      Tests  1058 passed (1058)
=== check-contrast
check-contrast: all 50 pairs pass WCAG thresholds.
=== eslint touched
=== codegen:check
codegen rc=0
=== npm run build
- Use build.rolldownOptions.output.codeSplitting to improve chunking: https://rolldown.rs/reference/OutputOptions.codeSplitting
- Adjust chunk size limit for this warning via build.chunkSizeWarningLimit.
PRB-HOST-GATES DONE
npx eslint <the 19 touched files> -> exit 0 (no output)
npx prettier --check <touched> -> All matched files use Prettier code style!
```

check-contrast stays at 50 pairs: PR-B adds no token pair (the two muted pairs ride PR-A).

## Playwright e2e (host stack: Compose Postgres + Redis on Colima, host API from the main tree at 87718f5f on https://localhost:5001, Vite started by Playwright from this clone)

```text
Error: expect(locator).toBeVisible() failed
Error: strict mode violation: getByText('8.0 km') resolved to 2 elements:
2 failed
[chromium] › e2e/plan-render.spec.ts:242:1 › register → land on / → plan renders → reload → identical content + no vdot in DOM 
[chromium] › e2e/workout-logging.spec.ts:192:1 › register → log a minimum + a rich workout → both appear in week-grouped history 
14 passed (6.9s)
```

The two failures are the journeys already recorded as red on `main` before this slice (cycle plan, 2026-09-05 row): `plan-render.spec.ts:242` compares Today header innerHTML across a reload; `workout-logging.spec.ts:192` hits a strict-mode violation because `getByText(8.0 km)` matches both the ledger row and the week aggregate. Neither touches a PR-B surface. The realigned `auth.spec.ts` journey (poster header, tagline, eye toggle, exact password lookup, cookie assertions) and the six one-line exact-selector journeys passed.
