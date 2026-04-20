# Research Prompt: Batch 17c — R-053

# Multi-Turn LLM Eval Pattern — Extending M.E.AI.Evaluation for Onboarding Flows in .NET 10 (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a .NET 10 + xUnit v3 + `Microsoft.Extensions.AI.Evaluation` (M.E.AI.Evaluation) test suite that currently evaluates single-turn LLM calls with committed replay-cache fixtures, what is the current 2026 best-practice pattern for evaluating multi-turn LLM conversations — specifically a hybrid deterministic-controller + LLM-extraction onboarding flow per DEC-047 — across extraction accuracy, completion-gate behavior, prompt-cache hit rate, ambiguity handling, and end-to-end "did onboarding produce a sane `UserProfile`" assertions?

## Context

I'm preparing Slice 1 (Onboarding → Plan) of MVP-0 for an AI-powered running coach (RunCoach). The existing eval surface (DEC-036, DEC-037, DEC-039) was built for single-turn LLM calls — one prompt → one structured-output response → one assertion against a committed cache fixture. Slice 1 introduces a fundamentally different eval shape.

**The Slice 1 eval problem (per DEC-047):** Onboarding is a multi-turn flow. A static topic list controls slot order; the LLM phrases questions, handles follow-ups, and extracts structured answers per turn (`{ extracted: PartialAnswer, reply: string, confidence: number, needs_clarification: bool }`). Completion is a deterministic gate (all required fields present, all validate, no outstanding `needs_clarification` flag) with an LLM ambiguity pre-check (final `ready_for_plan` structured output). Several quality dimensions need evaluation per scenario:

- **Extraction accuracy per turn** — given a user input "I want to do a marathon in October," does the LLM extract `{ Topic: PrimaryGoal, NormalizedValue: marathon, Confidence: 0.9 }` correctly?
- **Completion-gate behavior** — across full flows, does the gate fire when (and only when) the deterministic preconditions are met?
- **Prompt-cache hit-rate as a quality metric** — turn 2 and beyond should hit the cache (~70% cost saving per R-048). Eval should fail if cache hit rate falls below threshold for a given scenario.
- **Ambiguity handling** — when the user input is genuinely ambiguous, does the LLM correctly flag `needs_clarification: true`?
- **End-to-end flow assertion** — given a full scripted user-input sequence, does the resulting `UserProfile` projection match the expected shape?
- **Safety-keyword detection** — when a user mentions "I'm pregnant" or "my back is killing me," does onboarding surface the safety-relevant context to the projection (per `batch-4b-special-populations-safety.md`)?

**Existing eval infrastructure:**

- M.E.AI.Evaluation framework with replay-mode `DiskBasedCachingChatClient` (DEC-039 post-process for committed fixtures).
- 5 existing test profiles (per `TestProfiles` in the eval suite).
- Single-turn eval pattern: `[Theory] [MemberData(...)] async Task EvalAsync(Profile profile)` calls `ICoachingLlm`, asserts via composed `IEvaluator` instances on the response.
- xUnit v3 + MTP runner with Microsoft.Extensions.AI.Evaluation.Reporting.
- LLM-as-judge already in use for plan-quality evals (Haiku as judge per DEC-038).

**Cycle plan acceptance criterion (Slice 1):** *"Eval cache extended with onboarding scenarios."* The shape of that extension is currently undefined — the cycle plan punts to the spec session.

## Research Question

**Primary:** What is the current 2026 best-practice pattern — in tooling, test architecture, assertion strategy, and cache-replay design — for evaluating multi-turn LLM conversations in a .NET 10 + M.E.AI.Evaluation stack, and how should it integrate with the project's existing committed replay-cache (DEC-039) and onboarding event-source pattern (DEC-047)?

**Sub-questions (must be actionable):**

1. **Industry pattern survey for multi-turn LLM eval in 2026.** What patterns have stabilized — full-trajectory evaluation, per-turn assertion, end-state assertion, LLM-as-judge over the full conversation, snapshot testing of conversation flows, hybrid? Which tools own these patterns: LangSmith eval, Anthropic's eval framework, OpenAI evals, Phoenix, Opik, custom builds, M.E.AI.Evaluation extensions?

2. **M.E.AI.Evaluation extension surface.** Does M.E.AI.Evaluation have multi-turn primitives in 2026, or are we writing a custom extension? If custom: what's the recommended shape — wrap `IChatClient` calls in a session-scoped trace, accumulate per-turn assertions, snapshot the final state? Document the actual API surface available.

3. **Cache-replay design for multi-turn flows.** The existing DEC-039 cache hashes a single request → caches a single response. For a multi-turn flow:
   - Cache per turn (one entry per `(scenario_id, turn_index, request_hash)`)?
   - Cache the full exchange (one entry per scenario, replays in sequence)?
   - Hash by `(prior_events, user_input)` so retries replay correctly?
   - How does cache invalidation work when a prompt changes mid-scenario (e.g., `onboarding-v1.yaml` → `onboarding-v2.yaml`)?
   - How do we avoid cache explosion as scenario count grows?

4. **Scenario definition shape.** What's the recommended way to declare a multi-turn scenario in code? Scripted user-input sequence + per-turn expectations + final-state expectation? YAML-defined scenarios vs C# `[Theory]` parameters? Show concrete example shapes from the leading tools.

5. **Cache-hit-rate as a quality assertion.** Anthropic emits `cache_creation_input_tokens` and `cache_read_input_tokens` per call. Recommended pattern: per-turn assertion `expectedCacheReadTokens > 0` after turn 1? Per-scenario assertion `cache_hit_rate >= 0.7`? Both? How do we surface this as a first-class eval dimension rather than buried in usage metadata?

6. **LLM-as-judge for full conversations.** For "did onboarding feel natural?" or "did the LLM correctly handle the ambiguous input?" — how do you structure an LLM judge that reads a full multi-turn transcript and emits a structured rubric verdict? Anthropic-compatible patterns?

7. **Deterministic + LLM-judge tiered assertions.** Some assertions are deterministic (extraction accuracy → exact match against expected `NormalizedValue`); some need a judge (response tone, naturalness). What's the tiering pattern that minimizes LLM-judge cost while keeping coverage?

8. **Snapshot testing for conversation flows.** Verify-style snapshot testing (Verify, Snapshooter for .NET) for multi-turn flows — does it work for non-deterministic assistant outputs? Or is it limited to deterministic projection state?

9. **Integration with the onboarding event-source pattern (DEC-047).** Each onboarding turn appends events (`UserTurnRecorded`, `AssistantTurnRecorded`, `AnswerCaptured`, `ClarificationRequested`, `OnboardingCompleted`). The eval can assert against the resulting event sequence rather than (or in addition to) the LLM's text output. What's the recommended shape — event-stream assertion as the primary surface?

10. **Eval-drift detection across prompt versions.** When `onboarding-v1.yaml` evolves to `v2`, existing scenarios need to flag behavior changes. What's the diff-and-alert pattern — A/B run both prompts, compare per-turn extraction accuracy, surface regressions?

11. **Statistical assertions.** LLM outputs have variance. Best-of-N sampling, pass-rate-over-N-runs, mean-with-confidence-interval — which statistical patterns does M.E.AI.Evaluation support, and which need custom code? What's the right N for MVP-0 vs CI-cost-tolerable?

12. **CI cost and runtime.** With committed cache fixtures (DEC-039), CI-cost is "near zero" but cache record runs are "real cost." For multi-turn, recording one scenario costs `N_turns × cost_per_turn`. What's a sensible scenario budget for MVP-0 (~10? ~50? ~200?), and how do we keep record runs from blowing the API budget?

13. **Integration with R-051 (LLM observability) and R-052 (Anthropic SDK).** Whichever observability tool wins, eval scenarios should produce traces too. Whichever SDK wins, the eval cache must compose with it. Confirm the pattern works across the foreseeable choices.

14. **Safety-scenario coverage.** Per `batch-4b-special-populations-safety.md`, onboarding inputs may include pregnancy, injury history, mental health flags. Eval scenarios for these need to verify: (a) the safety context lands in the projection, (b) the LLM does not propose contraindicated training, (c) no PII leaks into the event payloads where avoidable. Are there established patterns for safety-eval in 2026 (Anthropic's safety eval, OWASP-LLM-style frameworks)?

15. **Existing-eval-suite migration.** The 5 single-turn test profiles still need to work. Does the multi-turn extension coexist with them in one suite (one project, one runner) or split? If coexist, do they share a base class, a fixture, anything?

## Why It Matters

- **Slice 1 acceptance includes onboarding eval scenarios.** The shape of those scenarios is the eval contract for every slice that adds LLM behavior afterward (Slice 3 adaptation, Slice 4 conversation).
- **Without a pattern, the team writes ad-hoc multi-turn tests that don't aggregate, don't replay, and don't compose with the existing eval cache.** That dilutes the test signal and makes prompt iteration expensive.
- **Cache-hit-rate as a quality dimension is novel** — most eval patterns from 2024–2025 don't measure it because prompt caching was newer. Getting this right at the eval layer is what makes the ~70% cost saving a verified property, not an aspirational one.
- **R-051 (observability) and R-053 are interlinked.** Eval traces should land in the same observability surface as production traces — same trace-id shape, same dashboards. Settling both together is cleaner than retrofitting.
- **Safety-eval is a pre-public-release blocker.** Building eval primitives that handle safety scenarios from Slice 1 means the safety scaffolding lands incrementally rather than as a pre-public-release ramp-up.

## Deliverables

- **A concrete recommendation** for the multi-turn eval pattern, with the chosen architecture and the explicit tradeoffs.
- **A capability matrix** comparing M.E.AI.Evaluation extension vs LangSmith eval vs Phoenix vs Opik vs Anthropic eval framework vs custom build, across multi-turn support, .NET integration quality, cache-replay compatibility, LLM-as-judge support, statistical assertion primitives, snapshot patterns, and CI-cost.
- **A scenario-definition format** — concrete example showing how a single onboarding scenario is declared (scripted user inputs + per-turn assertions + final-state assertion + cache-hit assertion + safety-keyword assertion).
- **A cache-replay design** for multi-turn — hashing strategy, fixture file layout, invalidation on prompt change, growth budget.
- **Wiring sketches** — how the existing single-turn `[Theory] [MemberData]` eval pattern coexists with the new multi-turn pattern; how eval scenarios produce observability traces.
- **A scenario budget** for MVP-0 — how many onboarding scenarios, broken down by category (happy-path × topic combinations, safety-adjacent, ambiguity, prompt-cache validation, end-to-end-projection).
- **A safety-scenario sub-pattern** — distilled from `batch-4b-special-populations-safety.md` and current 2026 safety-eval frameworks.
- **An eval-drift workflow** — when `onboarding-v2.yaml` lands, the recommended workflow for A/B running both, surfacing regressions, deciding what to commit.
- **A migration plan** for the existing 5 single-turn profiles — coexist as-is, refactor into the multi-turn shape, or both.
- **An interaction note with R-051 (observability)** — eval traces flow through the same provider; same trace-id shape.
- **An interaction note with R-052 (Anthropic SDK)** — confirm the pattern works on whichever SDK wins.
- **Citations** — current M.E.AI.Evaluation docs, LangSmith eval docs, Anthropic eval framework docs (if it exists in 2026), OWASP LLM eval guidance, real-world 2025–2026 multi-turn eval case studies in .NET or general.

## Out of Scope

- The training-science domain content of onboarding scenarios — covered by `batch-2a-training-methodologies.md`; only relevant for choosing scenario inputs, not for the eval architecture.
- Choice of LLM provider — Anthropic locked.
- Choice of judge model — DEC-038 picked Haiku for judging.
- Plan-quality eval scenarios (already covered by existing single-turn pattern) — this prompt is about extending to multi-turn, not replacing.
- Adversarial / red-team eval — explicitly deferred to pre-public-release scaffolding (DEC-016, DEC-018).
- Open-conversation eval (Slice 4) — the multi-turn pattern that lands here will inform it, but Slice 4 has its own characteristics (no completion gate, free-form, no extraction).
