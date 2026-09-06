# Slice 6 PR-A round 1: orchestrator runs (host, 2026-09-06)

Both round-1 lenses (`round1/mutation.json`, `round1/conformance.json`) marked the e2e replays NOT_RUNNABLE_IN_SANDBOX (`listen EPERM ::1:5173`) and asked for them here. The host gates they cite as measured fact are in `build-orchestrator-runs.md` (vitest 1052/1053 with the F1 literal as the one red, check-contrast 54/54, codegen 0, build clean with 0.9.0 in the bundle, eslint 0, prettier clean, Playwright 16 passed with the two pre-existing `main` failures).

## E2E mutation replays (from the finished round-1 mutation worktree at 4de63051, host API on https://localhost:5001, Vite started by Playwright per spec)

Each replay applies one production mutation, runs the single spec, records the outcome, and restores the file from HEAD.

```text
MUTATION remove the SIGN OUT click handler -> Error: expect(page).toHaveURL(expected) failed Error: expect(page).toHaveURL(expected) failed 2 failed 
MUTATION remove postLogoutBroadcast() from the sender -> Error: expect(page).toHaveURL(expected) failed 1 failed 1 passed (7.1s) 
MUTATION render the panel instead of the building overlay while loading -> Error: expect(locator).toBeVisible() failed Error: element(s) not found 1 failed 
RESTORED: 0 tracked dirty paths
```

All three replays are RED with the expected shape: removing the SIGN OUT click handler fails both sign-out journeys; removing `postLogoutBroadcast()` fails only the second-tab journey (the first tab still signs out locally); rendering the panel instead of the building overlay fails the regenerate journey's building assertion. The ledger's three NOT_RUNNABLE_IN_SANDBOX entries are therefore verified red.

## Disposition of the lens findings

- Mutation 1 and conformance 1 (contrast literal): fix F1. Conformance 2 and mutation 3 (panel gap-3): F3. Mutation 2 (Current plan eyebrow missing): F4.
- Mutation 4 to 10 (untested order and contracts; placeholder bytes): F5 to F11.
- Conformance 3 and the stage-report claims: closed by the orchestrator addendum on `.stage-report.md` and `build-report.md` (F12).
- Sol red-team 4 (dark-mode clay outline): F2.
- The three e2e replays: closed above (F13).
