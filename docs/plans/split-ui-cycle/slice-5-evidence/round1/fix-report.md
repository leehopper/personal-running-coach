## Fixes

- F1: Removed the narrative Replay skip. The committed fixture now reaches ReplayGuard on cache miss. `PlanGenerationEvalTests.cs:547-553`. INSPECTED.
- F2: Added the Marten System.Text.Json legacy-document regression test. A sentinel initializer would fail the empty assertion. `OnboardingProjectionTests.cs:95-112`. INSPECTED.
- F3: Restored the cacheable-prefix XML summary and added the narrative behavior sentence. `ContextAssembler.cs:606-616`. VERIFIED BY INSPECTION.
- F4: No action required. Orchestrator addenda are present at `.stage-report.md:106` and `build-report.md:106`. VERIFIED BY INSPECTION.
- F5: No action required per adjudication. The narrowed ASCII checklist is recorded at `spec.md:432`. VERIFIED BY INSPECTION.
- F6: Accepted orchestrator-measured results at `build-orchestrator-runs.md:57`, `:99`, and `:120`. No action required.

## Gates

- PASS `TMPDIR=... dotnet build RunCoach.slnx --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --disable-build-servers`  
  Last line: `Time Elapsed: 00:00:04.44`
- PASS changed-file `git diff --check`  
  Last line: `Changed-file diff check passed`
- NOT RUNNABLE `TMPDIR=... dotnet format RunCoach.slnx --include ... --no-restore --verify-no-changes`  
  Last output: `System.Net.Sockets.SocketException (13): Permission denied`

## Deviations

Backend tests were not run because the sandbox test host cannot create its named pipe.

The orchestrator should run:

- `dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Eval.PlanGenerationEvalTests`
- `dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Onboarding.OnboardingProjectionTests`

## Open questions

None.

FIX ROUND COMPLETE

Codex session ID: 01a072df-4a5d-7462-8428-6ce5db8b0789
Resume in Codex: codex resume 01a072df-4a5d-7462-8428-6ce5db8b0789
