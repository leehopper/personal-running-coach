# Slice 5 PR-A round 1: orchestrator runs

Commands the two round-1 lenses could not run in the sandbox (`dotnet test` cannot create its named pipe there),
executed from the orchestrating session. Gate runs for the build stage itself are in `../build-orchestrator-runs.md`
(four test classes, eval Replay, funded-key Record, TTL patch, targeted and full Replay 2054/2054, `npm run test`
1022/1022, `check-contrast` 50/50, `codegen:check` exit 0, swagger `jq` oracle, DEC-089 D7 manifest no-op).

## Mutation probes M1-M20 from the mutation lens

Run in the detached worktree for the mutation lens at HEAD 6265c38d (the build commit). Each probe: apply the
mutation, `dotnet build RunCoach.slnx --no-restore`, `dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class <class>`
(Colima Docker, Ryuk disabled, Replay mode), then `git checkout --` the file. RED = failed > 0 in the named class.
Probes sharing one mutation site were run together (M5 with M16, M8 with M10, M9 with M14). Three probes needed a
second pass: the first M17 mutation (`Length >= 0`) and the first M15 mutation were rejected at build time by
SonarAnalyzer (always-true / always-false condition), and my probe runner initially misread that build error as a pass
and ran the stale binary; the runner was fixed to fail on `N Error(s)` and the three were rerun with analyzer-safe
mutations (M15 `&& Environment.TickCount64 < 0`; M17 `Length != 1`; M18 first attempt also rejected, replaced by M18b).

| Probe | File | Mutation | Test class | Result | Expected red test |
|---|---|---|---|---|---|
| M1 | SubmitStructuredAnswersRequestMapper.cs | `? string.Empty
| M2 | SubmitStructuredAnswersRequestMapper.cs | `string.IsNullOrWhiteSpace(request.Narrative)` -> `request.Narrative is null` | SubmitStructuredAnswersRequestMapperTests | RED SubmitStructuredAnswersRequestMapperTests: {'total': 40, 'failed': 1, 'succeeded': 39, 'skipped': 0} | TryMap_WhitespaceNarrative_MapsToEmptyString |
| M3 | SubmitStructuredAnswersRequestMapper.cs | `: request.Narrative;` -> `: request.Narrative.Trim();` | SubmitStructuredAnswersRequestMapperTests | RED SubmitStructuredAnswersRequestMapperTests: {'total': 40, 'failed': 1, 'succeeded': 39, 'skipped': 0} | TryMap_Narrative_PreservesLeadingTrailingSpacesAndInnerNewline |
| M4 | SubmitStructuredAnswersRequestMapper.cs | `{ Length: > NarrativeMaxLength }` -> `{ Length: >= NarrativeMaxLength }` | SubmitStructuredAnswersRequestMapperTests | RED SubmitStructuredAnswersRequestMapperTests: {'total': 40, 'failed': 1, 'succeeded': 39, 'skipped': 0} | TryMap_Narrative_AtMaxLength_Succeeds |
| M5+M16 | SubmitStructuredAnswersRequestMapper.cs | `error = "Narrative must be 1000 characters or fewer.";` -> `error = "Narrative too long.";` | SubmitStructuredAnswersRequestMapperTests, OnboardingAnswersEndpointIntegrationTests | RED SubmitStructuredAnswersRequestMapperTests: {'total': 40, 'failed': 1, 'succeeded': 39, 'skipped': 0} / OnboardingAnswersEndpointIntegrationTests: {'total': 20, 'failed': 1, 'succeeded': 19, 'skipped': 0} | TryMap_Narrative_OverMaxLength_FailsWithBoundMessage; SubmitAnswers_OverlongNarrative_Returns400_NothingStaged |
| M6 | SubmitStructuredAnswersRequestMapper.cs | `var anyTopic = request.PrimaryGoal is not null` -> `var anyTopic = request.Narrative is not null || request.Prim` | SubmitStructuredAnswersRequestMapperTests | RED SubmitStructuredAnswersRequestMapperTests: {'total': 40, 'failed': 1, 'succeeded': 39, 'skipped': 0} | TryMap_NarrativeOnly_NoTopics_Fails |
| M7 | OnboardingProjection.cs | `view.Narrative = @event.Narrative;` -> `view.Narrative = string.Empty;` | OnboardingProjectionTests | RED OnboardingProjectionTests: {'total': 41, 'failed': 1, 'succeeded': 40, 'skipped': 0} | OnboardingProjection_Apply_AnswerCaptured_NarrativeText_SetsView |
| M8+M10 | OnboardingProjection.cs | `if (@event.Narrative is not null)
| M9+M14 | OnboardingProjection.cs | `if (@event.Narrative is not null)` -> `if (!string.IsNullOrEmpty(@event.Narrative))` | OnboardingProjectionTests, OnboardingAnswersEndpointIntegrationTests | RED OnboardingProjectionTests: {'total': 41, 'failed': 1, 'succeeded': 40, 'skipped': 0} / OnboardingAnswersEndpointIntegrationTests: {'total': 20, 'failed': 1, 'succeeded': 19, 'skipped': 0} | EmptyNarrative_ClearsView; SubmitAnswers_BlankNarrative_ClearsState |
| M11 | SubmitStructuredAnswersHandler.cs | `string? pendingNarrative = cmd.Narrative;` -> `string? pendingNarrative = null;` | OnboardingAnswersEndpointIntegrationTests | RED OnboardingAnswersEndpointIntegrationTests: {'total': 20, 'failed': 4, 'succeeded': 16, 'skipped': 0} | SubmitAnswers_Narrative_RoundTripsThroughEventAndState |
| M12 | SubmitStructuredAnswersHandler.cs | `AppendAnswer(session, streamId, OnboardingTopic.PrimaryGoal,` -> `AppendAnswer(session, streamId, OnboardingTopic.PrimaryGoal,` | OnboardingAnswersEndpointIntegrationTests | RED OnboardingAnswersEndpointIntegrationTests: {'total': 20, 'failed': 2, 'succeeded': 18, 'skipped': 0} | SubmitAnswers_Narrative_AttachesOnlyToFirstAnswerCaptured |
| M13 | SubmitStructuredAnswersHandler.cs | `working.Narrative = cmd.Narrative;` -> `working.Narrative = string.Empty;` | OnboardingAnswersEndpointIntegrationTests | RED OnboardingAnswersEndpointIntegrationTests: {'total': 20, 'failed': 1, 'succeeded': 19, 'skipped': 0} | SubmitAnswers_Narrative_PassesWorkingViewToInlinePlanGeneration |
| M15 | SubmitStructuredAnswersHandler.cs | `if (prior is not null)
| M17 | ContextAssembler.cs | `if (profileSnapshot.Narrative.Length > 0)` -> `if (profileSnapshot.Narrative.Length != 1)` | ContextAssemblerPlanGenerationTests | RED ContextAssemblerPlanGenerationTests: {'total': 10, 'failed': 1, 'succeeded': 9, 'skipped': 0} | ComposeForPlanGenerationAsync_EmptyNarrative_EmitsNoNarrativeBlock |
| M18 | ContextAssembler.cs | `if (profileSnapshot.Narrative.Length > 0)
| M18b | ContextAssembler.cs | duplicate the `PROFILE SNAPSHOT (captured during onboarding):` header inside the narrative block (compile-clean ordering mutation, run by hand) | ContextAssemblerPlanGenerationTests | RED {'total': 10, 'failed': 2, 'succeeded': 8} incl. `ComposeForPlanGenerationAsync_Narrative_PrecedesProfileSnapshotVerbatim` | ComposeForPlanGenerationAsync_Narrative_PrecedesProfileSnapshotVerbatim |
| M19 | ContextAssembler.cs | `sb.AppendLine("PROFILE SNAPSHOT (captured during onboarding)` -> `sb.AppendLine("PROFILE SNAPSHOT (captured during onboarding)` | ContextAssemblerPlanGenerationTests | RED ContextAssemblerPlanGenerationTests: {'total': 10, 'failed': 3, 'succeeded': 7, 'skipped': 0} | ComposeForPlanGenerationAsync_Narrative_TwoReplaysAreByteStable |
| M20 | PlanGenerationEvalTests.cs | `view.Narrative = narrative;` -> `view.Narrative = string.Empty;` | PlanGenerationEvalTests | RED PlanGenerationEvalTests: {'total': 7, 'failed': 1, 'succeeded': 6, 'skipped': 0} | DatedEvent_Narrative_Macro_PreservesHorizonAndMentionsCalf (Replay cache miss) |

Result: every probe M1-M20 observed RED against its named test. M21 (missing fixture must fail Replay, not skip) and
M22 (legacy `OnboardingView` document hydrates `Narrative` empty) depend on fix-round items F1 and F2 and are run in
round 2.

## Worktree state

`git status --short` in the mutation worktree after the last restore: clean.

## Fix round 1: orchestrator runs (after the luna fix task, before the fix commit)

`dotnet format --verify-no-changes` on the three changed .cs files produced no diagnostics beyond the tolerated IDE1006 naming noise (pass).

```text
== dotnet build ==
    0 Warning(s)
    0 Error(s)
== dotnet format --verify-no-changes (3 changed .cs) ==
format rc=
== OnboardingProjectionTests (F2 present) ==
  total: 42
  failed: 0
  succeeded: 42
  skipped: 0
== PlanGenerationEvalTests Replay (skip removed: expect 7 succeeded, 0 skipped) ==
  total: 7
  failed: 0
  succeeded: 7
  skipped: 0
== M22: sentinel initializer on OnboardingView.Narrative -> expect RED ==
1
    0 Error(s)
failed RunCoach.Api.Tests.Modules.Coaching.Onboarding.OnboardingProjectionTests.OnboardingView_LegacyDocumentWithoutNarrative_HydratesEmpty (33ms)
  total: 42
  failed: 1
  succeeded: 41
    0 Error(s)
== M21: fixture absent -> targeted Replay must FAIL (not skip) ==
    at RunCoach.Api.Tests.Modules.Coaching.Eval.ReplayGuardChatClient.GetResponseAsync(IEnumerable`1 messages, ChatOptions options, CancellationToken cancellationToken) in <path>
  total: 1
  failed: 1
  succeeded: 0
  skipped: 0
fixture restored: 0 changes
== targeted Replay with fixture present ==
  total: 1
  failed: 0
  succeeded: 1
  skipped: 0
FIX1-RUNS DONE
```

M21: with the fixture directory moved aside, the targeted Replay FAILED through `ReplayGuardChatClient` (1 failed, 0 skipped), then passed again once restored. M22: with the `OnboardingView.Narrative` initializer replaced by a sentinel, `OnboardingView_LegacyDocumentWithoutNarrative_HydratesEmpty` went RED (1 of 42), then green after restore. Both mutations were reverted; the clone tree carries only the three fix files.
