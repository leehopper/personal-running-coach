**Slice 5 PR-A is the backend root of the SPLIT / Alpine onboarding redesign; PR-B follows with the frontend recomposition and consumes this regenerated contract.** (`docs/specs/slice-5-onboarding/spec.md`)

Spec: `docs/specs/slice-5-onboarding/spec.md` section 3 PR-A is a gitignored working-tree artifact. The committed evidence is under `docs/plans/split-ui-cycle/slice-5-evidence/`.

## What's here

- `narrative` is a top-level nullable field on both onboarding DTOs, absent from both Swagger `required` arrays. Release OpenAPI, generated RTK and Zod clients, and hand-written onboarding models carry this shape. (`backend/openapi/swagger.json`, `frontend/src/app/api/generated/rtk/api.ts`, `frontend/src/app/api/generated/zod/onboarding/onboarding.ts`, `frontend/src/app/modules/onboarding/models/onboarding.model.ts`)
- The mapper normalizes null and whitespace-only input to `string.Empty`, preserves nonblank bytes, accepts 1000 characters, and rejects 1001 with the exact message. (`backend/src/RunCoach.Api/Modules/Coaching/Onboarding/SubmitStructuredAnswersRequestMapper.cs`, `docs/specs/slice-5-onboarding/spec.md`)
- `AnswerCaptured.Narrative` attaches only to the first event in a submission. Null means no narrative information, empty means clear, and text means the submitted narrative. This preserves legacy replay and avoids a new event type or repeated payloads. (`backend/src/RunCoach.Api/Modules/Coaching/Onboarding/SubmitStructuredAnswersHandler.cs`, `docs/specs/slice-5-onboarding/spec.md`)
- The handler copies the canonical narrative to the working view before inline plan generation, because generation reads that copy. (`backend/src/RunCoach.Api/Modules/Coaching/Onboarding/SubmitStructuredAnswersHandler.cs`)
- The state DTO maps an empty view narrative to JSON null. (`backend/src/RunCoach.Api/Modules/Coaching/Onboarding/OnboardingController.cs`, `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/Models/OnboardingStateDto.cs`)
- The C# composer adds the three-line `IN THE RUNNER'S OWN WORDS` block before `PROFILE SNAPSHOT`, using the narrative verbatim. Empty input adds zero bytes, so existing fixtures remain byte-identical and need no re-record. (`backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs`, `docs/decisions/decision-log.md` DEC-089 D7)
- The new `plan.dated-event-narrative.macro` eval was recorded with a funded key and passed on its first sample. Release OpenAPI emission then regenerated the clients, while PR-A updated the hand-written models. (`backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanGenerationEvalTests.cs`, `docs/plans/split-ui-cycle/slice-5-evidence/build-orchestrator-runs.md`)

## Decision record

- DEC-089 D7 records that C# composition makes `check-prompt-hashes.sh --write` a verified no-op. The eval consequence is one new fixture, not existing-fixture re-recording. (`docs/decisions/decision-log.md`, `docs/plans/split-ui-cycle/slice-5-evidence/build-orchestrator-runs.md`)
- `backend/REVIEW.md` now records the additive-nullable Marten exception. The precedent is DEC-091 and the Slice 2 `PlanGenerated` capture, where old JSON hydrates null without an upcaster. (`backend/REVIEW.md`, `docs/decisions/decision-log.md`, `docs/plans/split-ui-cycle/cycle-plan.md`)
- Four Captured During Cycle rows cover C1 through C4: D7 manifest and event-rule handling, 12-week copy, narrative bounds, and generic 422 behavior. (`docs/plans/split-ui-cycle/cycle-plan.md`)

## Review trail

- Six read-only recon lenses informed the locked backend decisions. The two-family spec red-team returned 3 blockers, 5 majors, and 5 minors for the first lens. It returned 4 blockers, 8 majors, and 4 minors for the second; findings 1 and 12 were rejected. (`docs/plans/split-ui-cycle/slice-5-evidence/recon/adjudication.md`, `docs/plans/split-ui-cycle/slice-5-evidence/redteam/adjudication.md`)
- Round 1 used mutation and conformance lenses. M1-M20 all went red, the missing-fixture M21 probe failed Replay, and the legacy-view M22 probe failed its hydration test. Thus M1-M22 were red. (`docs/plans/split-ui-cycle/slice-5-evidence/round1/orchestrator-runs.md`, `docs/plans/split-ui-cycle/slice-5-evidence/round1/mutation.json`)
- One fix round delivered F1 through F3: missing-fixture Replay now fails, legacy view hydration is tested, and the composer cache-prefix documentation is restored. (`docs/plans/split-ui-cycle/slice-5-evidence/round1/fix-list.txt`, `docs/plans/split-ui-cycle/slice-5-evidence/round1/fix-report.md`)
- The maintainer's own code-gauntlet remains the cross-family pass still to come. (`docs/decisions/decision-log.md` DEC-092)

## Green

- Release build: 0 warnings, 0 errors, and 12.60 seconds. Debug build: 0 errors and 0.61 seconds. Fix-round build: 0 warnings, 0 errors, and 4.44 seconds. (`docs/plans/split-ui-cycle/slice-5-evidence/build-orchestrator-runs.md`, `docs/plans/split-ui-cycle/slice-5-evidence/round1/fix-report.md`)
- Focused backend classes passed 40/40, 41/41, 10/10, and 20/20. After the fixture and fix round, eval Replay passed 7/7. Targeted Record passed 1/1, targeted Replay passed 1/1, and full Replay passed 2054/2054 with zero skips. (`docs/plans/split-ui-cycle/slice-5-evidence/build-orchestrator-runs.md`, `docs/plans/split-ui-cycle/slice-5-evidence/round1/orchestrator-runs.md`)
- Frontend tests passed 1022/1022 across 86 files. Contrast passed 50/50 pairs, `codegen:check` exited 0, and the Swagger oracle returned `true`. The F2 regression class passed 42/42. (`docs/plans/split-ui-cycle/slice-5-evidence/build-orchestrator-runs.md`, `docs/plans/split-ui-cycle/slice-5-evidence/round1/orchestrator-runs.md`)

## Notes for review

- The `ROADMAP.md` and cycle-plan Status edits ride with this PR. (`ROADMAP.md`, `docs/plans/split-ui-cycle/cycle-plan.md`)
- The narrative remains unsanitized like existing nuance fields, by DEC-089 D7 decision. (`docs/decisions/decision-log.md`, `docs/specs/slice-5-onboarding/spec.md`)
- The frontend form does not send the field until PR-B. PR-A lands the contract and backend path only. (`docs/specs/slice-5-onboarding/spec.md`, `docs/plans/split-ui-cycle/slice-5-evidence/build-report.md`)
