## Files

Changed: all 16 requested existing files.

New:

- `backend/tests/RunCoach.Api.Tests/Infrastructure/RecordingPlanGenerationService.cs`

No generated files, prompts, migrations, snapshots, or caches changed.

## Gates

- PASS `dotnet build RunCoach.slnx --no-restore`  
  Last line: `Time Elapsed: 00:00:04.84`

- PASS changed-file `dotnet format ... --no-restore --verify-no-changes`  
  Last output: no output.

- PASS `bash backend/tests/scripts/check-prompt-hashes.sh`  
  Last line: `Prompt-hash manifest is in sync (DEC-074).`

- PASS `npm run build`  
  Build completed: `built in 530ms`

- PASS `npx vitest run src/app/modules/onboarding src/app/api`  
  Last line: `Duration 2.31s`

- PASS `npx eslint src/app/modules/onboarding/models/onboarding.model.ts`  
  Last output: no output.

- PASS `npx prettier --check src/app/modules/onboarding/models/onboarding.model.ts`  
  Last line: `All matched files use Prettier code style!`

- DEVIATION `npm run test`  
  `Test Files 3 failed, 83 passed (86)` due sandbox `tsx` IPC pipe and missing `localStorage`.

- DEVIATION `npm run check-contrast`  
  Failed because `tsx` cannot create its IPC pipe: `Error: listen EPERM`.

## Acceptance table

| Criterion | Result | Evidence |
|---|---|---|
| Unit 1 | PASS, build-verified | INSPECTED narrative plumbing at `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/SubmitStructuredAnswersHandler.cs:113-176`; tests at `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Onboarding/OnboardingAnswersEndpointIntegrationTests.cs:144-222`. |
| Unit 2 | PASS, build-verified | INSPECTED mapper bound and normalization at `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/SubmitStructuredAnswersRequestMapper.cs:24-82`; tests at `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Onboarding/SubmitStructuredAnswersRequestMapperTests.cs:148-246`. |
| Unit 3 | PASS, build-verified | INSPECTED projection tri-state behavior at `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/OnboardingProjection.cs:88-92`; composer empty branch at `backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs:617-624`. |
| Unit 4 | PASS, build-verified | INSPECTED deterministic composition tests at `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Onboarding/ContextAssemblerPlanGenerationTests.cs:152-209`. |
| Unit 5 | DEVIATION | INSPECTED eval scenario and Replay guard at `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanGenerationEvalTests.cs:552-626`; fixture recording and Replay were not run. |
| Unit 6 | DEVIATION | INSPECTED nullable DTO shapes at `backend/src/RunCoach.Api/Modules/Coaching/Onboarding/Models/SubmitStructuredAnswersRequestDto.cs:21-30` and `OnboardingStateDto.cs:30-46`; Release OpenAPI emission and codegen are orchestrator-owned. |

## Mutations

All new tests were written before production edits and are expected to be RED against the original gap. Backend test execution was not runnable in this sandbox.

- INSPECTED `SubmitStructuredAnswersRequestMapperTests.cs:148`: remove blank normalization and null no longer maps to empty.
- INSPECTED `SubmitStructuredAnswersRequestMapperTests.cs:165`: normalize only null and whitespace remains non-empty.
- INSPECTED `SubmitStructuredAnswersRequestMapperTests.cs:182`: trim the narrative and exact spaces/newline preservation fails.
- INSPECTED `SubmitStructuredAnswersRequestMapperTests.cs:200`: change `>` to `>=` and the 1000-character case fails.
- INSPECTED `SubmitStructuredAnswersRequestMapperTests.cs:218`: remove the bound or alter the message and the overlong case fails.
- INSPECTED `SubmitStructuredAnswersRequestMapperTests.cs:235`: include narrative in `anyTopic` and the narrative-only guard fails.
- INSPECTED `OnboardingProjectionTests.cs:120`: remove narrative assignment and text projection fails.
- INSPECTED `OnboardingProjectionTests.cs:143`: assign null to the view and legacy preservation fails.
- INSPECTED `OnboardingProjectionTests.cs:166`: ignore empty narrative and clearing fails.
- INSPECTED `OnboardingProjectionTests.cs:190`: deserialize missing narrative as empty and legacy replay fails.
- INSPECTED `OnboardingAnswersEndpointIntegrationTests.cs:144`: omit event or state mapping and round-trip fails.
- INSPECTED `OnboardingAnswersEndpointIntegrationTests.cs:172`: duplicate narrative on later events and first-event-only fails.
- INSPECTED `OnboardingAnswersEndpointIntegrationTests.cs:198`: remove `working.Narrative` assignment and inline generation capture fails.
- INSPECTED `OnboardingAnswersEndpointIntegrationTests.cs:226`: preserve the existing narrative for blank input and clear-state fails.
- INSPECTED `OnboardingAnswersEndpointIntegrationTests.cs:273`: bypass idempotency short-circuit and event count changes.
- INSPECTED `OnboardingAnswersEndpointIntegrationTests.cs:302`: remove mapper bound and HTTP 400 assertion fails.
- INSPECTED `ContextAssemblerPlanGenerationTests.cs:132`: always emit the narrative block and empty-input test fails.
- INSPECTED `ContextAssemblerPlanGenerationTests.cs:152`: trim or move the narrative block and ordering test fails.
- INSPECTED `ContextAssemblerPlanGenerationTests.cs:182`: introduce nondeterministic composition and replay stability fails.
- INSPECTED `PlanGenerationEvalTests.cs:552`: remove narrative assignment before composition and the prompt-prefix assertion fails.

## Deviations

Backend tests were not run. The sandbox test host cannot create its named pipe and reports `SocketException 13`.

The orchestrator should run from `backend/`:

```text
dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Onboarding.SubmitStructuredAnswersRequestMapperTests
dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Onboarding.OnboardingProjectionTests
dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Onboarding.OnboardingAnswersEndpointIntegrationTests
dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Onboarding.ContextAssemblerPlanGenerationTests
dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class RunCoach.Api.Tests.Modules.Coaching.Eval.PlanGenerationEvalTests
ANTHROPIC_API_KEY=<funded-key> EVAL_CACHE_MODE=Record dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-method RunCoach.Api.Tests.Modules.Coaching.Eval.PlanGenerationEvalTests.DatedEvent_Narrative_Macro_PreservesHorizonAndMentionsCalf
EVAL_CACHE_MODE=Replay dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-method RunCoach.Api.Tests.Modules.Coaching.Eval.PlanGenerationEvalTests.DatedEvent_Narrative_Macro_PreservesHorizonAndMentionsCalf
EVAL_CACHE_MODE=Replay dotnet test --solution RunCoach.slnx --no-build
```

The orchestrator must also run Release OpenAPI emission, `npm run codegen`, `npm run codegen:check`, and the specified `jq` Swagger inspection. It must record the new eval fixture and patch its expiration.

The orchestrator-owned `backend/REVIEW.md` additive-nullable Marten exception amendment remains outstanding.

## Open questions

None.

STAGE COMPLETE

Codex session ID: 01a072ba-9414-7f40-b06e-9fe446a88ac0
Resume in Codex: codex resume 01a072ba-9414-7f40-b06e-9fe446a88ac0
