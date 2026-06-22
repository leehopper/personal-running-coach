# Slice 4A Coaching-Voice Re-tune — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-tune every LLM-produced coaching voice from cheery/validating to gruff-direct across the three active prompts, enforced by deterministic eval guards, shipped as a series of small stacked PRs (one test-scaffold PR, one doc PR, one per prompt) followed by subjective tuning rounds.

**Architecture:** Prompt prose lives in versioned YAML (`coaching-system.v1`, `adaptation.v1`, `onboarding-v1`) that the model mirrors; the persona doc (`coaching-persona.md`) is the source of truth those prompts implement. There is **no runtime scrubber** — enforcement is a prompt STYLE rule plus a deterministic test-side guard over the recorded eval fixtures (mirroring the Slice 3B F2 `TrademarkProseGuard`). Every prompt edit busts the DEC-074 hash manifest and the M.E.AI response-cache, so each prompt PR re-records only its own affected fixtures.

**Tech Stack:** .NET 10 / C# 14, xUnit v3 (MTP runner), FluentAssertions, M.E.AI.Evaluation (cached Sonnet + Haiku judge), YamlDotNet, bash maintenance scripts (`rerecord-eval-cache.sh`, `check-prompt-hashes.sh`).

## Global Constraints

- **Trademark:** every prose surface uses "Daniels-Gilbert zones" / "pace-zone index" — never the trademarked four-letter term. Existing trademark guards (`TrademarkProseGuard`, `OnboardingPromptTests`, `AdaptationTrademarkEvalTests`, `ContextAssemblerTests`) must keep passing.
- **Safety is untouchable:** all SAFETY / medical / crisis / injury / overtraining rules, the anti-toxic-culture clichés ("no days off", "pain is temporary", "push through"), and the body/weight/shape/food-labeling NEVER lines stay **verbatim** in every prompt and in the persona doc. Existing safety evals (`SafetyBoundaryEvalTests`, `SafetyRubricEvaluatorTests`) must keep passing.
- **Onboarding structure is load-bearing:** `OnboardingPromptTests` asserts the `data_handling` directive sits at the end of the system block, the three `SECTION_NAME` wrappers + nonce comment, the six documented topics, and the Daniels-Gilbert vocabulary. The rewrite must preserve all of these. The system block stays byte-stable across the six topics (Pattern-B prefix cache).
- **Prompt edits never travel alone:** a changed `Prompts/*.yaml` busts `.prompt-hashes.sha256` (DEC-074). The `check-prompt-hashes` lefthook hook and the `EvalTestBase` static-ctor backstop both fail until the manifest is regenerated and the affected fixtures re-recorded. Regenerate the manifest and re-record in the **same commit** as the prompt edit.
- **Re-record needs a funded key** in the **test** project's user-secrets (`runcoach-api-tests`), not `runcoach-api`. Per-prompt PRs re-record only their own eval class(es) (targeted recipe) so fixture diffs stay scoped.
- **Test invocation (MTP):** build with `dotnet build RunCoach.slnx --no-restore`, then run the test binary directly: `backend/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests -class <FQN>` (class filter) or `-trait "Category=Eval"`. Positional paths and VSTest `--filter` are rejected on MTP.
- **One type per file**; test files mirror `src` structure; Arrange/Act/Assert markers; `expected`/`actual` prefixes.
- **Git:** branch per PR off the prior (stacked); never commit to `main`; commit subject lowercase after the `type(scope):` prefix; end every commit body with the `Co-Authored-By:` / `Claude-Session:` trailers.

---

## PR Breakout

Small, reviewable PRs. PR1–PR2 are independent; PR3–PR5 are **stacked** because they all touch `.prompt-hashes.sha256` and `tests/eval-cache/` (rebase each onto the prior after it merges, per the stacked-PR rebase pattern). Tuning rounds come after.

| PR | Task(s) | Contents | Size / review focus | Depends on |
|---|---|---|---|---|
| **PR0** | Task 0 | Record the 4A/4B/4C split + advance Status in `cycle-plan.md` + `ROADMAP.md` | Tiny doc — **done in the 2026-06-17 planning PR**; a fresh session starts at PR1 | — |
| **PR1** | Tasks 1–2 | `VoiceProseGuard` + unit tests; `VoiceRubrics.Restraint` + unit tests. Test-only, no prompt/fixture changes → green on its own | Small, pure TDD | — |
| **PR2** | Task 3 | `coaching-persona.md` gruff-direct rewrite | Small, doc-only (the register decision in prose) | — |
| **PR3** | Task 4 | `onboarding-v1` rewrite + regenerate DEC-074 manifest (**no fixture re-record, no funded key** — no onboarding eval exists; see Task 4 correction note) | Small: prose + manifest only | PR1, PR2 |
| **PR4** | Task 5 | `coaching-system.v1` rewrite (plan-gen + conversation) + manifest + re-record plan-gen & safety-boundary fixtures + wire guard into plan-gen eval | Medium | PR3 |
| **PR5** | Task 6 | `adaptation.v1` RATIONALE rewrite + manifest + re-record adaptation fixtures + wire guard **and** advisory restraint rubric into the adaptation eval | Medium | PR4 |
| **tuning** | — | Prompt-only follow-up PRs, each re-recording its affected fixtures, until the register reads right to the builder | Small each | PR3–PR5 |

Why this keeps PRs small: the only unavoidably-coupled unit is *(one prompt + its manifest line + its fixtures + its guard wiring)*. Splitting by prompt means each PR's fixture diff is scoped to a single surface, and the test scaffolding (PR1) and the subjective doc (PR2) are reviewed on their own terms first.

---

## Task 0: Record the 4A/4B/4C split (PR0)

> **Done in the 2026-06-17 Slice 4A planning PR.** The Status blocks already point at "Slice 4A active, next = PR1", so a fresh `/catchup` session starts at Task 1. Steps retained for the record.

**Files:**
- Modify: `ROADMAP.md` (Status block — Active slice + Next step)
- Modify: `docs/plans/mvp-0-cycle/cycle-plan.md` (Status § Active Slice + Next Step + a Captured-During-Cycle disposition note)

- [ ] **Step 1: Update the cycle plan Status**

In `docs/plans/mvp-0-cycle/cycle-plan.md`, change the **Active Slice** line to note Slice 4 is decomposed into 4A (voice re-tune, active — design `slice-4a-voice-retune.md`, plan `docs/superpowers/plans/2026-06-17-slice-4a-voice-retune.md`), 4B (conversation core, `slice-4-conversation.md`), 4C (onboarding redesign, not yet written). Set **Next Step** to "Begin Slice 4A — voice re-tune (PR0–PR5 then tuning rounds)".

- [ ] **Step 2: Mirror into ROADMAP**

In `ROADMAP.md`, update the **Active slice** and **Next step** lines to match (the Status block is mirrored between the two files).

- [ ] **Step 3: Commit**

```bash
git add ROADMAP.md docs/plans/mvp-0-cycle/cycle-plan.md
git commit -m "docs(slice-4a): record slice 4 decomposition and advance status"
```

---

## Task 1: Deterministic `VoiceProseGuard` + unit tests (PR1)

**Files:**
- Create: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceProseGuard.cs`
- Test: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceProseGuardTests.cs`

**Interfaces:**
- Produces: `internal static class VoiceProseGuard` with `string? FindViolation(string value)` (null when clean) and `void AssertClean(string label, object output)` (walks every JSON string leaf of a serialized output; FluentAssertions failure on any violation). `internal static readonly string[] BannedPhrases`.
- Consumes: `RunCoach.Api.Modules.Coaching.ClaudeCoachingLlm.StructuredOutputSerializerOptions` (same serializer `TrademarkProseGuard` uses).

- [ ] **Step 1: Write the failing test**

`VoiceProseGuardTests.cs`:

```csharp
using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

public sealed class VoiceProseGuardTests
{
    [Fact]
    public void FindViolation_CleanGruffProse_ReturnsNull()
    {
        // Arrange
        var actual = VoiceProseGuard.FindViolation(
            "Cut Sunday to 9 km. Legs were flat from the first km. Build back to 14 next week.");

        // Assert
        actual.Should().BeNull();
    }

    [Theory]
    [InlineData("You ran hard — too hard.")] // em dash U+2014
    [InlineData("Easy run, 8–10 km.")]        // en dash U+2013
    public void FindViolation_Dash_ReturnsViolation(string value)
    {
        // Assert
        VoiceProseGuard.FindViolation(value).Should().NotBeNull();
    }

    [Fact]
    public void FindViolation_Exclamation_ReturnsViolation()
    {
        VoiceProseGuard.FindViolation("Strong work today!").Should().NotBeNull();
    }

    [Theory]
    [InlineData("Love it. Let us lock the goal.")]
    [InlineData("Great foundation to build on.")]
    [InlineData("That is AMAZING progress.")]
    public void FindViolation_SycophancyPhrase_ReturnsViolation(string value)
    {
        VoiceProseGuard.FindViolation(value).Should().NotBeNull();
    }

    [Fact]
    public void AssertClean_NestedObjectWithBannedPhrase_Throws()
    {
        // Arrange
        var output = new { rationale = "Love that target.", nested = new { note = "fine" } };

        // Act
        var act = () => VoiceProseGuard.AssertClean("test", output);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void AssertClean_CleanObject_DoesNotThrow()
    {
        // Arrange
        var output = new { rationale = "Held the week flat. Volume rebuilds Monday." };

        // Act
        var act = () => VoiceProseGuard.AssertClean("test", output);

        // Assert
        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet build RunCoach.slnx --no-restore` then
`backend/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests -class RunCoach.Api.Tests.Modules.Coaching.Eval.VoiceProseGuardTests`
Expected: build error / FAIL — `VoiceProseGuard` does not exist.

- [ ] **Step 3: Write the guard**

`VoiceProseGuard.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Deterministic eval-side voice/style guard (Slice 4A). Serializes an LLM
/// output and asserts no prose field carries a banned style "tell": an em or
/// en dash, an exclamation mark, or a sycophancy/validation phrase. Mirrors
/// <see cref="TrademarkProseGuard"/> — it scans decoded string leaves via
/// <see cref="JsonNode"/> rather than the serialized JSON text. There is no
/// runtime scrubber for these tells, so a fresh fixture that trips this guard
/// means the prompt regressed against the gruff-direct register: tighten the
/// prompt and re-record. ISO dates use U+002D hyphen-minus and are NOT matched.
/// </summary>
internal static class VoiceProseGuard
{
    // U+2014 EM DASH, U+2013 EN DASH. Plain hyphen-minus (U+002D) is allowed.
    private static readonly char[] BannedDashes = ['—', '–'];

    // Sycophancy / filler-enthusiasm tells flagged on the 2026-06-13 live pass.
    // Case-insensitive substring match. Extended during the tuning rounds.
    internal static readonly string[] BannedPhrases =
    [
        "love it",
        "love that",
        "great foundation",
        "great job",
        "well done",
        "amazing",
        "awesome",
        "fantastic",
        "wonderful",
        "so proud",
    ];

    /// <summary>
    /// Returns a description of the first style violation in <paramref name="value"/>,
    /// or <see langword="null"/> when the string is clean.
    /// </summary>
    internal static string? FindViolation(string value)
    {
        if (value.IndexOfAny(BannedDashes) >= 0)
        {
            return "contains an em/en dash";
        }

        if (value.Contains('!'))
        {
            return "contains an exclamation mark";
        }

        foreach (var phrase in BannedPhrases)
        {
            if (value.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return $"contains the banned phrase '{phrase}'";
            }
        }

        return null;
    }

    /// <summary>
    /// Asserts that every string field reachable from <paramref name="output"/> is
    /// free of banned style tells (Slice 4A gruff-direct register).
    /// </summary>
    internal static void AssertClean(string label, object output)
    {
        var node = JsonSerializer.SerializeToNode(output, ClaudeCoachingLlm.StructuredOutputSerializerOptions)
            ?? throw new InvalidOperationException($"Failed to serialize '{label}' to JsonNode.");
        AssertNodeClean(label, node);
    }

    private static void AssertNodeClean(string label, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (_, value) in obj)
                {
                    AssertNodeClean(label, value);
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    AssertNodeClean(label, item);
                }

                break;
            case JsonValue v when v.TryGetValue<string>(out var s):
                FindViolation(s).Should().BeNull(
                    because: $"every prose field of '{label}' must match the Slice 4A gruff-direct style "
                        + $"(offending value: '{s}')");
                break;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: rebuild, then the same `-class` command from Step 2.
Expected: PASS (all `VoiceProseGuardTests`).

- [ ] **Step 5: Commit**

```bash
git add backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceProseGuard.cs \
        backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceProseGuardTests.cs
git commit -m "test(slice-4a): add deterministic voice-prose guard"
```

---

## Task 2: `VoiceRubrics.Restraint` advisory rubric + unit tests (PR1)

**Files:**
- Create: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceRubrics.cs`
- Test: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceRubricsTests.cs`

**Interfaces:**
- Produces: `public static class VoiceRubrics` with `SafetyRubricCriteria[] Restraint { get; }`.
- Consumes: `SafetyRubricCriteria(string Name, string Description)` and `SafetyRubricEvaluator.BuildJudgePrompt(string)` (internal; visible via `InternalsVisibleTo`).

- [ ] **Step 1: Write the failing test**

`VoiceRubricsTests.cs`:

```csharp
using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

public sealed class VoiceRubricsTests
{
    [Fact]
    public void Restraint_HasTheExpectedCriteria()
    {
        // Assert
        VoiceRubrics.Restraint.Select(c => c.Name).Should().BeEquivalentTo(
            "direct_register", "no_validation_opener", "no_filler_enthusiasm",
            "keeps_rationale", "offers_forward_path");
    }

    [Fact]
    public void BuildJudgePrompt_IncludesEveryRestraintCriterion()
    {
        // Arrange
        var evaluator = new SafetyRubricEvaluator("voice restraint check", VoiceRubrics.Restraint);

        // Act
        var prompt = evaluator.BuildJudgePrompt("Cut Sunday to 9 km. Volume rebuilds Monday.");

        // Assert
        foreach (var criterion in VoiceRubrics.Restraint)
        {
            prompt.Should().Contain(criterion.Name);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: rebuild, then `... -class RunCoach.Api.Tests.Modules.Coaching.Eval.VoiceRubricsTests`
Expected: FAIL — `VoiceRubrics` does not exist.

- [ ] **Step 3: Write the rubric**

`VoiceRubrics.cs`:

```csharp
namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Advisory LLM-as-judge rubric (Slice 4A) for the gruff-direct register. Reuses
/// the <see cref="SafetyRubricEvaluator"/> harness. Scored and recorded for the
/// builder to read during the tuning rounds; NOT a hard CI gate (the deterministic
/// <see cref="VoiceProseGuard"/> is the gate). Promote to a threshold gate once the
/// scores are calibrated against builder-approved output.
/// </summary>
public static class VoiceRubrics
{
    /// <summary>Gets the gruff-direct restraint rubric.</summary>
    public static SafetyRubricCriteria[] Restraint { get; } =
    [
        new("direct_register", "Is the response blunt and direct with short sentences, rather than warm, gushing, or chatty?"),
        new("no_validation_opener", "Does the response avoid opening with praise or emotional validation (no 'Love it', 'Great foundation', 'that takes honesty to acknowledge')?"),
        new("no_filler_enthusiasm", "Does the response avoid filler enthusiasm, exclamation marks, and sycophancy?"),
        new("keeps_rationale", "Does the response still give the physiological or training reason for any recommendation?"),
        new("offers_forward_path", "When it constrains or cuts load, does it still show the path forward?"),
    ];
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: rebuild, then the `-class VoiceRubricsTests` command.
Expected: PASS.

- [ ] **Step 5: Commit + open PR1**

```bash
git add backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceRubrics.cs \
        backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/VoiceRubricsTests.cs
git commit -m "test(slice-4a): add advisory voice-restraint judge rubric"
```

Open PR1 with Tasks 1–2.

---

## Task 3: Rewrite `coaching-persona.md` (PR2)

**Files:**
- Modify: `docs/planning/coaching-persona.md`

No automated test — this is the source-of-truth doc the prompts implement. Reviewed as prose. The exact gruff-direct wording is the author's craft; the structural edits below are mandatory.

- [ ] **Step 1: Flip the register**

- **Core Principle** + **Persona Calibration**: retire the "80/20 warmth-to-directness" dial; state the register is directness-led — warmth shown through competence and straight talk, not enthusiasm, praise, or emotional validation.
- **Three-Layer Communication Architecture**: make OARS *Affirmation* a non-mandatory move (keep Open question / Reflection / Summary as available moves, not a per-response requirement).

- [ ] **Step 2: Cut the warmth mandates**

- **"Always Do" Rules**: remove "Use process praise" and "Acknowledge feelings before correcting behavior". Keep "provide rationales", "offer at least one choice", "show the path forward".
- Allow pointed factual accountability (naming a missed session as data) — as accountability, never shaming.

- [ ] **Step 3: Add a STYLE section**

Add a "Style" subsection: no em dashes; no exclamation marks; no filler enthusiasm or sycophancy; short sentences.

- [ ] **Step 4: Confirm the keeps**

Verify untouched, verbatim: all SAFETY rules, the "Never Do" toxic-culture clichés, the body/weight/shape/food-labeling lines, the Crisis Response Protocol, the Population-Specific Communication Guidelines.

- [ ] **Step 5: Commit + open PR2**

```bash
git add docs/planning/coaching-persona.md
git commit -m "docs(slice-4a): re-tune coaching persona to gruff-direct register"
```

Open PR2 with Task 3.

---

## Task 4: Rewrite `onboarding-v1` + regenerate manifest (PR3) — shipped #207, 2026-06-22

> **CORRECTION (discovered at build time, 2026-06-22): no onboarding eval exists.** This task was authored assuming a recorded onboarding eval to wire the guard into and re-record (Steps 3–4). There is none: no test under `tests/…/Eval/` exercises `onboarding-v1` (the suite is plan-gen / safety-boundary / logged-workout / adaptation only), and there are no onboarding fixtures among the 20 sonnet + 7 haiku recorded scenarios. So PR3 reduced to **(prompt rewrite + manifest regen)** — no funded key, no fixture re-record. Editing `onboarding-v1.yaml` only busts the DEC-074 hash manifest (the `EvalTestBase` static-ctor backstop + the `check-prompt-hashes` lefthook hook require it regenerated); it changes no existing scenario's cache key (those derive from `coaching-system.v1` / `adaptation.v1`). Steps 3–4 below are struck through and the onboarding voice eval is tracked as its own follow-up in the cycle plan's *Captured During Cycle* table (2026-06-22 row). PR4/PR5 are unaffected — they target evals that *do* exist.

> Stacked on PR1 + PR2. The prompt edit and the regenerated manifest land in one commit (the `check-prompt-hashes` lefthook hook + the eval static-ctor backstop block a partial commit).

**Files:**
- Modify: `backend/src/RunCoach.Api/Prompts/onboarding-v1.yaml`
- Modify: `backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256` (regenerated)
- ~~Modify: onboarding eval test (add the guard assertion)~~ — **N/A: no onboarding eval exists** (deferred follow-up).
- ~~Modify (generated): `backend/tests/eval-cache/` (onboarding scenarios)~~ — **N/A: no onboarding fixtures.**
- Possibly modify: any unit test asserting removed phrasings (see Step 1 — **none found**; the `OnboardingPromptTests` structural gate pins no removed prose).

- [ ] **Step 1: Find tests pinned to the old phrasings**

Run: `grep -rniE '80/20|process praise|acknowledge feelings|warm, curious' backend/tests --include='*.cs'`
For each hit that asserts the **onboarding** prompt contains a removed phrase, update or remove that assertion. (Most hits are `"coaching-v1"` version-label strings — leave those.)

- [ ] **Step 2: Rewrite the onboarding system prompt**

In `onboarding-v1.yaml` `static_system_prompt`: flip the opening register from "warm... 80/20 balance of warmth to directness" to gruff-direct; in REPLY GUIDELINES delete "Acknowledge feelings before correcting behavior; never lecture"; add a STYLE block (no em dashes, no exclamation, no opening affirmations, short sentences). Rewrite the whole block **em-dash-free**. **Preserve** (OnboardingPromptTests gate): the six-topic TOPIC SCHEMA, AMBIGUITY RULES, NUMERICAL BOUNDS, the PATTERN-B INVARIANT, the `data_handling` directive at the end of the system block, the Daniels-Gilbert vocabulary line, and the entire `context_template` with its three `SECTION_NAME` wrappers + nonce comment. Keep the SAFETY block and the body/weight/food line verbatim.

- [x] ~~**Step 3: Wire the guard into the onboarding eval**~~ — **N/A (no onboarding eval).** Deferred as the cycle-plan follow-up: a future `OnboardingVoiceEvalTests` (new onboarding-context plumbing + cached Sonnet turn + `VoiceProseGuard.AssertClean` + advisory `VoiceRubrics.Restraint`) once a funded key is in hand.

- [x] **Step 4 (was: re-record onboarding fixtures): regenerate the DEC-074 manifest only**

No fixture re-record — no eval produces onboarding fixtures. Only the manifest is regenerated (mandatory: the prompt edit busts the hash):
```bash
bash backend/tests/scripts/check-prompt-hashes.sh --write   # regen manifest (DEC-074); only the onboarding-v1.yaml line changes
bash backend/tests/scripts/check-prompt-hashes.sh           # confirms "in sync"
```

- [x] **Step 5: Replay-verify**

Run: `EVAL_CACHE_MODE=Replay backend/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests -trait "Category=Eval"`
Result (2026-06-22): **90/90, 0 skipped** — the DEC-074 manifest backstop passes and every existing fixture still replays (no cache key changed). Trademark + safety evals green.

- [x] **Step 6: Full suite green + commit + open PR3**

Run the structural gate + full suite.
Result (2026-06-22): `OnboardingPromptTests` 10/10; full suite **1801/1801, 0 failed** (Colima + `EVAL_CACHE_MODE=Replay`).

```bash
git add backend/src/RunCoach.Api/Prompts/onboarding-v1.yaml \
        backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256
git commit -m "feat(slice-4a): re-tune onboarding prompt to gruff-direct"
```

Opened PR3 (#207) stacked on PR2. (The eval-cache + `Modules/Coaching/` paths from the original `git add` are dropped — nothing changed there.)

---

## Task 5: Rewrite `coaching-system.v1` + re-record + wire guard (PR4)

> Stacked on PR3. Covers both plan generation and conversation (shared prompt).

**Files:**
- Modify: `backend/src/RunCoach.Api/Prompts/coaching-system.v1.yaml`
- Modify: `backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256`
- Modify: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanGenerationEvalTests.cs` (add guard assertion)
- Modify (generated): `backend/tests/eval-cache/` (plan-gen + safety-boundary scenarios)
- Possibly modify: unit tests asserting removed phrasings

- [ ] **Step 1: Rewrite the system prompt**

In `coaching-system.v1.yaml` `static_system_prompt`: flip L13 "80/20 balance of warmth to directness" to directness-led; in ALWAYS delete "Acknowledge feelings before correcting behavior" and "Use process praise — connect success to effort, consistency, and decisions"; make OARS Affirmation non-mandatory in the COMMUNICATION FRAMEWORK Layer-1 line; add a STYLE block (no em dashes, no exclamation, no opening affirmations, short sentences). Rewrite **em-dash-free**. **Keep verbatim**: the entire NEVER list (incl. body/weight/food + toxic-culture lines), all SAFETY RULES, the DETERMINISTIC GUARDRAILS, the VOCABULARY RULES, the DATA HANDLING directive, and the `context_template`. Keep the MI spine (rationales, offer a choice, show the path forward).

- [ ] **Step 2: Update pinned unit tests**

Run the Step-1 grep from Task 4 again; update any assertion that pins the **coaching-system** prompt to a removed phrase (e.g., in `ContextAssemblerTests` / `ClaudeCoachingLlmTests` if present). Leave trademark/safety assertions intact.

- [ ] **Step 3: Wire the guard into the plan-gen eval**

In `PlanGenerationEvalTests.cs`, after each recorded plan output, add `VoiceProseGuard.AssertClean("plan-gen-<scenario>", output);` alongside the existing checks.

- [ ] **Step 4: Regenerate manifest + re-record plan-gen & safety-boundary fixtures (funded key)**

```bash
bash backend/tests/scripts/check-prompt-hashes.sh --write
git rm -r backend/tests/eval-cache/<plan-gen + safety-boundary scenario dirs>
dotnet build RunCoach.slnx --no-restore
EVAL_CACHE_MODE=Record backend/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests -class RunCoach.Api.Tests.Modules.Coaching.Eval.PlanGenerationEvalTests
EVAL_CACHE_MODE=Record backend/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests -class RunCoach.Api.Tests.Modules.Coaching.Eval.SafetyBoundaryEvalTests
# patch new entry.json TTLs
```
(`coaching-system.v1` drives the safety-boundary scenarios too, so they re-record here.)

- [ ] **Step 5: Replay-verify + full suite + commit + open PR4**

Run Replay (`-trait "Category=Eval"`) then the full binary. Expected: PASS — plan-gen guard green; safety evals still pass.

```bash
git add backend/src/RunCoach.Api/Prompts/coaching-system.v1.yaml \
        backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256 \
        backend/tests/eval-cache/ \
        backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/PlanGenerationEvalTests.cs
git commit -m "feat(slice-4a): re-tune coaching-system prompt to gruff-direct + guard"
```

Open PR4 stacked on PR3.

---

## Task 6: Rewrite `adaptation.v1` + re-record + wire guard & rubric (PR5)

> Stacked on PR4.

**Files:**
- Modify: `backend/src/RunCoach.Api/Prompts/adaptation.v1.yaml`
- Modify: `backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256`
- Modify: `backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/Adaptation/AdaptationRestructureEvalTests.cs`
- Modify (generated): `backend/tests/eval-cache/` (adaptation scenarios: `adaptation.restructure.{lee,priya}` + `.judge`)

- [ ] **Step 1: Rewrite the RATIONALE shape + voice rule**

In `adaptation.v1.yaml` `static_system_prompt` RATIONALE section: **drop step-1 "Validate what happened"**; set the shape to *name the data pattern → state the change → the physiological why → the path forward*. Flip the "warm and direct, an 80/20 balance" voice rule to gruff-direct; add the STYLE rules (no em dashes, no exclamation). Rewrite **em-dash-free**. **Leave untouched**: the STRUCTURED OUTPUT CONTRACT, the CURRENT-WEEK CONSISTENCY block, GATE-BEFORE-INCREASE, the PROGRAMMING GUARDRAILS, the NUMERICAL BOUNDS, and the `data_handling` directive (the Slice 3B F4 logic).

- [ ] **Step 2: Wire the guard + advisory rubric into the adaptation eval**

In `AdaptationRestructureEvalTests.cs`:
- The existing `TrademarkProseGuard.AssertClean(...)` call already runs on `output`; add `VoiceProseGuard.AssertClean($"adaptation-restructure-{profileName}", output);` next to it.
- Add an **advisory** restraint judge: build a second `SafetyRubricEvaluator($"...", VoiceRubrics.Restraint)`, call `JudgeRationaleAsync($"adaptation.restraint.{profileName}.judge", restraintEvaluator, output.Rationale, ct)`, and record the verdict via `WriteEvalResultAsync` — **do not** add a hard `Should()` assertion on its score (advisory per the design; the existing communication-judge `RationaleRubric` keeps its hard assertion).

- [ ] **Step 3: Regenerate manifest + re-record adaptation fixtures (funded key)**

```bash
bash backend/tests/scripts/check-prompt-hashes.sh --write
git rm -r backend/tests/eval-cache/<adaptation scenario dirs>   # adaptation.restructure.* + the new restraint judge
dotnet build RunCoach.slnx --no-restore
EVAL_CACHE_MODE=Record backend/tests/RunCoach.Api.Tests/bin/Debug/net10.0/RunCoach.Api.Tests -class RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation.AdaptationRestructureEvalTests
# patch new entry.json TTLs (incl. the new adaptation.restraint.{lee,priya}.judge entries)
```

- [ ] **Step 4: Replay-verify + full suite + commit + open PR5**

Run Replay (`-trait "Category=Eval"`) then the full binary. Expected: PASS — adaptation passes both guards, the communication judge still scores 1.0, the advisory restraint verdict is recorded, F4 consistency + ramp constraints still pass.

```bash
git add backend/src/RunCoach.Api/Prompts/adaptation.v1.yaml \
        backend/src/RunCoach.Api/Prompts/.prompt-hashes.sha256 \
        backend/tests/eval-cache/ \
        backend/tests/RunCoach.Api.Tests/Modules/Coaching/Eval/Adaptation/AdaptationRestructureEvalTests.cs
git commit -m "feat(slice-4a): re-tune adaptation rationale to gruff-direct + guard"
```

Open PR5 stacked on PR4.

---

## After PR5: tuning rounds

The deterministic register is now in place and gated. The subjective "reads right to the builder" work is iterative:

- Each round: edit one prompt's prose → `check-prompt-hashes.sh --write` → targeted re-record of that prompt's eval class(es) → Replay-verify → builder reads live output against a fresh account (funded key, host-run stack).
- Extend `VoiceProseGuard.BannedPhrases` and `VoiceRubrics.Restraint` as new tells surface.
- Each round is its own small prompt-only PR. Stop when the gruff-direct register lands.

---

## Self-Review

**Spec coverage** (against `slice-4a-voice-retune.md`): D1 persona doc → Task 3. D2 three prompts → Tasks 4/5/6 (each: dial flip, mandate deletes, STYLE block, safety verbatim, em-dash-free; adaptation RATIONALE reshape in Task 6). D3 deterministic guards (hard) → Tasks 1 + wiring in 4/5/6; advisory Haiku rubric → Tasks 2 + wiring in 6. D4 structural-then-tune → PR breakout + tuning section. D5 manifest + re-record → Steps in Tasks 4/5/6. DoD (trademark + safety evals still pass) → asserted in each Replay step + Global Constraints. Split-recording → Task 0.

**Placeholder scan:** the `<...scenario dirs>` / `<OnboardingEvalFQN>` markers are deliberate — the exact fixture sub-paths and the onboarding eval class name are resolved at implementation time by inspecting `tests/eval-cache/` and the onboarding eval file; every other step is concrete. No "add error handling" / "write tests for the above" placeholders.

**Type consistency:** `VoiceProseGuard.AssertClean(string, object)` / `FindViolation(string)` and `VoiceRubrics.Restraint` (a `SafetyRubricCriteria[]`) are used with those exact signatures everywhere they appear (Tasks 4/5/6 wiring). `SafetyRubricEvaluator(scenarioDescription, criteria)` + `JudgeAsync`/`BuildJudgePrompt` match the existing harness read from source.
