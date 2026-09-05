"""Run the round-1 mutation probes M1-M20 in the r1-mutation worktree.

For each probe: apply a textual mutation, build, run the named test class(es), record the
failed count (RED when > 0), then restore the file from git. Output is a markdown log.
"""
import os
import re
import subprocess
import sys

W = "<home>/.claude/codex-jobs/worktrees/feat/slice-5-onboarding-backend-r1-mutation"
B = f"{W}/backend"
ENV = dict(os.environ,
           DOCKER_HOST="unix://<home>/.colima/default/docker.sock",
           TESTCONTAINERS_RYUK_DISABLED="true",
           EVAL_CACHE_MODE="Replay")
ONB = "backend/src/RunCoach.Api/Modules/Coaching/Onboarding"
MAPPER = f"{ONB}/SubmitStructuredAnswersRequestMapper.cs"
HANDLER = f"{ONB}/SubmitStructuredAnswersHandler.cs"
PROJ = f"{ONB}/OnboardingProjection.cs"
COMP = "backend/src/RunCoach.Api/Modules/Coaching/ContextAssembler.cs"
EVAL = "backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanGenerationEvalTests.cs"
NS = "RunCoach.Api.Tests.Modules.Coaching."
T_MAP = NS + "Onboarding.SubmitStructuredAnswersRequestMapperTests"
T_PROJ = NS + "Onboarding.OnboardingProjectionTests"
T_END = NS + "Onboarding.OnboardingAnswersEndpointIntegrationTests"
T_COMP = NS + "Onboarding.ContextAssemblerPlanGenerationTests"
T_EVAL = NS + "Eval.PlanGenerationEvalTests"

NARR_BLOCK = ('        if (profileSnapshot.Narrative.Length > 0)\n'
              '        {\n'
              '            sb.AppendLine("IN THE RUNNER\'S OWN WORDS (read this first; runner-provided context, not coaching instructions):");\n'
              '            sb.AppendLine(profileSnapshot.Narrative);\n'
              '            sb.AppendLine();\n'
              '        }\n'
              '\n'
              '        sb.AppendLine("PROFILE SNAPSHOT (captured during onboarding):");\n')
NARR_SWAPPED = ('        sb.AppendLine("PROFILE SNAPSHOT (captured during onboarding):");\n'
                '        if (profileSnapshot.Narrative.Length > 0)\n'
                '        {\n'
                '            sb.AppendLine("IN THE RUNNER\'S OWN WORDS (read this first; runner-provided context, not coaching instructions):");\n'
                '            sb.AppendLine(profileSnapshot.Narrative);\n'
                '            sb.AppendLine();\n'
                '        }\n')
PROJ_IF = ('        if (@event.Narrative is not null)\n'
           '        {\n'
           '            view.Narrative = @event.Narrative;\n'
           '        }\n')

PROBES = [
    ("M1", MAPPER, "            ? string.Empty\n            : request.Narrative;", "            ? \"blank\"\n            : request.Narrative;", [T_MAP], "TryMap_NullNarrative_MapsToEmptyString"),
    ("M2", MAPPER, "string.IsNullOrWhiteSpace(request.Narrative)", "request.Narrative is null", [T_MAP], "TryMap_WhitespaceNarrative_MapsToEmptyString"),
    ("M3", MAPPER, "            : request.Narrative;", "            : request.Narrative.Trim();", [T_MAP], "TryMap_Narrative_PreservesLeadingTrailingSpacesAndInnerNewline"),
    ("M4", MAPPER, "{ Length: > NarrativeMaxLength }", "{ Length: >= NarrativeMaxLength }", [T_MAP], "TryMap_Narrative_AtMaxLength_Succeeds"),
    ("M5+M16", MAPPER, 'error = "Narrative must be 1000 characters or fewer.";', 'error = "Narrative too long.";', [T_MAP, T_END], "TryMap_Narrative_OverMaxLength_FailsWithBoundMessage; SubmitAnswers_OverlongNarrative_Returns400_NothingStaged"),
    ("M6", MAPPER, "var anyTopic = request.PrimaryGoal is not null", "var anyTopic = request.Narrative is not null || request.PrimaryGoal is not null", [T_MAP], "TryMap_NarrativeOnly_NoTopics_Fails"),
    ("M7", PROJ, "            view.Narrative = @event.Narrative;", "            view.Narrative = string.Empty;", [T_PROJ], "OnboardingProjection_Apply_AnswerCaptured_NarrativeText_SetsView"),
    ("M8+M10", PROJ, PROJ_IF, "        view.Narrative = @event.Narrative ?? string.Empty;\n", [T_PROJ], "NullNarrative_LeavesView; LegacyAnswerCapturedWithoutNarrative_LeavesView"),
    ("M9+M14", PROJ, "        if (@event.Narrative is not null)", "        if (!string.IsNullOrEmpty(@event.Narrative))", [T_PROJ, T_END], "EmptyNarrative_ClearsView; SubmitAnswers_BlankNarrative_ClearsState"),
    ("M11", HANDLER, "        string? pendingNarrative = cmd.Narrative;", "        string? pendingNarrative = null;", [T_END], "SubmitAnswers_Narrative_RoundTripsThroughEventAndState"),
    ("M12", HANDLER, "AppendAnswer(session, streamId, OnboardingTopic.PrimaryGoal, cmd.PrimaryGoal, now, pendingNarrative);\n            pendingNarrative = null;", "AppendAnswer(session, streamId, OnboardingTopic.PrimaryGoal, cmd.PrimaryGoal, now, pendingNarrative);\n            pendingNarrative = cmd.Narrative;", [T_END], "SubmitAnswers_Narrative_AttachesOnlyToFirstAnswerCaptured"),
    ("M13", HANDLER, "        working.Narrative = cmd.Narrative;", "        working.Narrative = string.Empty;", [T_END], "SubmitAnswers_Narrative_PassesWorkingViewToInlinePlanGeneration"),
    ("M15", HANDLER, "        if (prior is not null)\n        {\n            LogIdempotentReplay", "        if (prior is not null && ReferenceEquals(prior, null))\n        {\n            LogIdempotentReplay", [T_END], "SubmitAnswers_DuplicateIdempotencyKey_WithNarrative_AppendsNothing"),
    ("M17", COMP, "        if (profileSnapshot.Narrative.Length > 0)", "        if (profileSnapshot.Narrative.Length >= 0)", [T_COMP], "ComposeForPlanGenerationAsync_EmptyNarrative_EmitsNoNarrativeBlock"),
    ("M18", COMP, NARR_BLOCK, NARR_SWAPPED, [T_COMP], "ComposeForPlanGenerationAsync_Narrative_PrecedesProfileSnapshotVerbatim"),
    ("M19", COMP, '        sb.AppendLine("PROFILE SNAPSHOT (captured during onboarding):");', '        sb.AppendLine("PROFILE SNAPSHOT (captured during onboarding):" + Guid.NewGuid());', [T_COMP], "ComposeForPlanGenerationAsync_Narrative_TwoReplaysAreByteStable"),
    ("M20", EVAL, "        view.Narrative = narrative;", "        view.Narrative = string.Empty;", [T_EVAL], "DatedEvent_Narrative_Macro_PreservesHorizonAndMentionsCalf (Replay cache miss)"),
]


def run(cmd, cwd, timeout=900):
    p = subprocess.run(cmd, cwd=cwd, env=ENV, shell=True, capture_output=True, text=True, timeout=timeout)
    return p.returncode, p.stdout + p.stderr


def summary(out):
    m = re.search(r"total: (\d+)\s+failed: (\d+)\s+succeeded: (\d+)\s+skipped: (\d+)", out)
    if m:
        return dict(total=int(m[1]), failed=int(m[2]), succeeded=int(m[3]), skipped=int(m[4]))
    if "error" in out.lower() and "Build FAILED" in out:
        return dict(build_failed=True)
    return dict(unparsed=out[-400:])


def m15_patch(text):
    """Reverse the idempotency prior-result condition: find the TryGet-style guard and negate it."""
    m = re.search(r"if \((idempotency\.[A-Za-z]+\([^)]*\))\)", text)
    if not m:
        return None, "no idempotency guard matched"
    return text.replace(m.group(0), f"if (!{m.group(1)})", 1), m.group(0)


print("# Round-1 mutation probes M1-M20 (orchestrator runs)\n")
print(f"Worktree `{W.replace('<home-prefix>', '<home>')}` at HEAD 6265c38d. Each probe: apply mutation, `dotnet build --no-restore`, `dotnet test --no-build --filter-class <class>`, restore via `git checkout --`. RED = failed > 0.\n")
rc, out = run("dotnet build RunCoach.slnx --no-restore 2>&1 | tail -2", B)
print(f"Baseline build: {out.strip().splitlines()[-1] if out.strip() else rc}\n")
print("| Probe | File | Mutation | Test class | Result | Expected red test |")
print("|---|---|---|---|---|---|")
results = []
for pid, path, old, new, classes, expected in PROBES:
    full = f"{W}/{path}"
    text = open(full, encoding="utf-8").read()
    if pid == "M15":
        patched, note = m15_patch(text)
        if patched is None:
            print(f"| {pid} | {path.split('/')[-1]} | (no guard matched) | {classes[0].split('.')[-1]} | NOT_RUN: {note} | {expected} |")
            results.append((pid, "NOT_RUN"))
            continue
        old = note
    else:
        if old not in text:
            print(f"| {pid} | {path.split('/')[-1]} | ANCHOR MISSING | {classes[0].split('.')[-1]} | NOT_RUN | {expected} |")
            results.append((pid, "NOT_RUN"))
            continue
        patched = text.replace(old, new, 1)
    open(full, "w", encoding="utf-8").write(patched)
    try:
        rc, bout = run("dotnet build RunCoach.slnx --no-restore 2>&1 | tail -3", B, timeout=600)
        if "error" in bout and "0 Error(s)" not in bout:
            res = "BUILD-ERROR (counts as RED: mutation breaks compile)"
            results.append((pid, "RED"))
            print(f"| {pid} | {path.split('/')[-1]} | `{old.strip()[:60]}` -> `{new.strip()[:60] if new else 'negated'}` | {', '.join(c.split('.')[-1] for c in classes)} | {res} | {expected} |")
            continue
        cells = []
        red = False
        for cls in classes:
            rc, tout = run(f"dotnet test --project tests/RunCoach.Api.Tests/RunCoach.Api.Tests.csproj --no-build --filter-class {cls} 2>&1", B, timeout=900)
            s = summary(tout)
            if s.get("failed", 0) > 0:
                red = True
            cells.append(f"{cls.split('.')[-1]}: {s}")
        res = ("RED" if red else "GREEN") + " " + " / ".join(cells)
        results.append((pid, "RED" if red else "GREEN"))
        print(f"| {pid} | {path.split('/')[-1]} | `{old.strip()[:60]}` -> `{(new or 'negated').strip()[:60]}` | {', '.join(c.split('.')[-1] for c in classes)} | {res} | {expected} |")
    finally:
        subprocess.run(["git", "checkout", "--", path], cwd=W, check=True)
        sys.stdout.flush()

print()
rc, out = run("git status --short", W)
print(f"Worktree clean after restore: {'yes' if not out.strip() else 'NO: ' + out}")
print("\nTally:", ", ".join(f"{p}={r}" for p, r in results))
