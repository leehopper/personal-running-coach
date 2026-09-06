# Slice 6 PR-B round 1: orchestrator runs (host, 2026-09-06)

Both round-1 lenses (`round1/mutation.json`, `round1/conformance.json`) marked the e2e replays NOT_RUNNABLE_IN_SANDBOX (`listen EPERM ::1:5173`) and asked for them here. The host gates they cite as measured fact are in `build-orchestrator-runs.md` (vitest 1058/1058, check-contrast 50/50, codegen 0, build clean, eslint 0, prettier clean, Playwright 14 passed with the two pre-existing `main` failures).

## E2E mutation replays (from the finished round-1 mutation worktree at 8aa14029, host API on https://localhost:5001, Vite started by Playwright per spec)

Each replay edits one file, runs the single spec, records the outcome, and restores the file from HEAD.

```text
MUTATION revert exact password locator in e2e/auth.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION revert exact password locator in e2e/conversation-streaming.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION revert exact password locator in e2e/onboarding.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION revert exact password locator in e2e/plan-render.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION revert exact password locator in e2e/regenerate-plan.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION revert exact password locator in e2e/shell-navigation.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION revert exact password locator in e2e/workout-logging.spec.ts -> Error: locator.fill: Error: strict mode violation: getByLabel('Password') resolved to 2 elements: 1 failed 
MUTATION remove the poster tagline -> Error: expect(locator).toBeVisible() failed Error: element(s) not found 1 failed 
RESTORED: 1 dirty paths
```

All eight replays are RED: every reverted `getByLabel('Password')` fails with the strict-mode violation the spec predicted (the toggle's `Show password` name is the second match), and removing the tagline fails the realigned auth journey. The mutation ledger's eight NOT_RUNNABLE_IN_SANDBOX entries are therefore verified red.

## Disposition of the lens findings

- Mutation 1 (e2e replay): closed by the replays above.
- Mutation 2, 3, 4 (no direct test for the register frame class, the toggle geometry, the primary geometry): fix round F3, F4, F5.
- Mutation 5 (Prettier on evidence files): dropped; evidence markdown, JSON and briefs are outside the Prettier gate.
- Mutation 6 and conformance 1, 2 (stale stage report): closed by the orchestrator addendum on `.stage-report.md` and `build-report.md`.
- Sol red-team 5 and 10 (link hit target and focus ring; form rhythm classes): fix round F1, F2.

## Fix-round host runs (clone at 194df59a, the round-1 fix commit)

```text
=== npm run test
 Test Files  88 passed (88)
      Tests  1065 passed (1065)
=== eslint touched
eslint rc=0
=== prettier
All matched files use Prettier code style!
=== build
- Adjust chunk size limit for this warning via build.chunkSizeWarningLimit.
=== codegen
codegen rc=0
1) [chromium] › e2e/plan-render.spec.ts:242:1 › register → land on / → plan renders → reload → identical content + no vdot in DOM 
2) [chromium] › e2e/workout-logging.spec.ts:192:1 › register → log a minimum + a rich workout → both appear in week-grouped history 
Error: expect(locator).toBeVisible() failed
2 failed
14 passed (4.9s)
```

Playwright: 14 passed, the same two pre-existing `main` failures (`plan-render.spec.ts:242`, `workout-logging.spec.ts:192`), no PR-B surface involved. The seven new fix-round assertions (F1 to F5) are in the 1065.
