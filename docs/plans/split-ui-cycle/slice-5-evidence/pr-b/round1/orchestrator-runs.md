# Slice 5 PR-B round 1: orchestrator runs

Both round-1 lenses (mutation ledger, spec conformance) ran in detached worktrees at 6b50123b (the build
commit). The frontend mutation probes were MECHANIZED by the mutation lens itself (`npx vitest run` is
available in the sandbox): 24 of 29 observed RED, 4 observed GREEN (the coverage gaps that became F2-F5), and
1 NOT_RUNNABLE_IN_SANDBOX (the e2e request-body probe; covered by the orchestrator's Playwright run).

Every command the lenses listed under `orchestrator_runs` was already run from the orchestrating session
before the review and is recorded in `../build-orchestrator-runs.md`:

- `npm run test`: 86 files, 1035 passed
- `npm run check-contrast`: all 50 pairs pass
- `npm run codegen:check`: exit 0
- `npm run e2e`: 14 passed, 2 failed (both reproduce from `main`'s frontend; documented there)
- `dotnet build RunCoach.slnx --no-restore`: 0 warnings, 0 errors (no C# changed; `dotnet format` not applicable)
- `dotnet test --solution RunCoach.slnx --no-build`: not rerun for PR-B (no backend file in the diff); the
  PR-A head this branch stacks on passed 2055/2055 in Replay (PR-A `build-orchestrator-runs.md`)

Evidence hygiene fixed by the orchestrator after the conformance lens: ANSI escapes and non-ASCII marks that
leaked into `build-orchestrator-runs.md` from copied terminal output were scrubbed; the stage report gained
the reconciliation addendum for the four orchestrator-added evidence files.

## Fix round 1: orchestrator runs on the fixed tree (before the fix commit)

```text
== npm run build ==
src/app/api/generated/zod/client-errors/client-errors.ts 2ms
? built in 212ms
== npm run test ==
 Test Files  86 passed (86)
      Tests  1037 passed (1037)
== eslint touched ==
eslint clean
== prettier touched ==
All matched files use Prettier code style!
== check-contrast ==
check-contrast: all 50 pairs pass WCAG thresholds.
== codegen:check ==
rc=0
PRB-FIX-GATES DONE

      1 [chromium]   e2e/onboarding.spec.ts:115:1   register -> fill the form once -> single submit -> navigate to / (2.2s)
  2 failed
  14 passed (6.1s)
```

Playwright on the fixed tree: 14 passed, the same 2 pre-existing failures; the onboarding journey passes.
