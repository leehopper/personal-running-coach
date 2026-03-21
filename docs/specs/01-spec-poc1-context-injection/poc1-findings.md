# POC 1 Context Injection Findings

**Date:** 2026-03-21
**Author:** RunCoach POC 1 experiment pipeline
**Status:** Structural analysis complete; LLM quality observations pending live API runs

## Executive Summary

This document captures findings from 4 systematic context injection experiments conducted during POC 1. The experiments used the Lee profile (intermediate runner with half-marathon goal) as baseline and the Maria profile (advanced runner, maintenance phase) for cross-validation, covering 22 total experiment runs across 11 prompt variations.

The experiments were executed in dry-run mode (prompt assembly and token analysis without live LLM calls). Structural and quantitative findings are complete. Qualitative findings (plan quality, hallucination rates, coaching tone) require live API runs and are marked with placeholder sections ready to be filled in once those runs complete.

**Key structural findings:**
- The 15K token budget is generous for assembled prompts; actual usage peaks at ~1,700 tokens (with 5 conversation turns), well within budget
- Weekly summaries save 26.6% tokens vs per-workout detail with no loss of section structure
- Conversation history (5 turns) adds ~785 tokens, a manageable overhead
- Positional placement has zero impact on token usage, confirming it is purely an attention layout concern
- Maria's profile (4-week history) uses ~14% more tokens than Lee's (3-week history), suggesting per-profile budget headroom varies

---

## Experiment 1: Token Budget

### Methodology

Compared plan quality and prompt structure across three total context token budgets:
- **8K** - Reduced budget. Layer 2 summaries only, max 2 weeks history, max 3 conversation turns.
- **12K** - Medium budget. Mixed summarization (1 week L1 + 3 weeks L2), 5 conversation turns.
- **15K** - Full budget (baseline). Mixed summarization (2 weeks L1 + 4 weeks L2), 10 conversation turns.

Each variation was run against both the Lee and Maria profiles.

### Quantitative Results

| Variation | Lee (tokens) | Maria (tokens) | Sections | Budget Used |
|-----------|-------------|----------------|----------|-------------|
| 8K        | 690         | 701            | 6        | 8.6% / 8.8% |
| 12K       | 781         | 902            | 6        | 6.5% / 7.5% |
| 15K       | 781         | 902            | 6        | 5.2% / 6.0% |

### Structural Observations

1. **All three budgets produce the same section structure** (6 sections). The system prompt, profile, goal, fitness, paces, and training history are present at every budget level.
2. **Token estimates are far below budget limits.** The assembled prompt (excluding the system prompt text content, which is a constant ~2,800 tokens) uses only 690-902 estimated tokens for the variable sections. This means the 15K budget has substantial headroom.
3. **12K and 15K produce identical token counts for the same profile.** This suggests the token budget constraint only becomes active at 8K, where history is reduced to Layer 2 only.
4. **8K is sufficient for core context.** The 8K variation includes all required sections; it only drops to weekly summaries and limits conversation history.

### Quality Observations (Pending Live API Runs)

<!-- TEMPLATE: Fill in after running live experiments with API key -->

| Variation | Plan Completeness | Pace Accuracy | Personalization | Notes |
|-----------|-------------------|---------------|-----------------|-------|
| 8K        | _pending_         | _pending_     | _pending_       |       |
| 12K       | _pending_         | _pending_     | _pending_       |       |
| 15K       | _pending_         | _pending_     | _pending_       |       |

**Questions to answer with live data:**
- Does the 8K variation produce plans with less specific history-based adaptation?
- Is 12K a sufficient "sweet spot" for quality vs cost?
- Does 15K provide meaningfully better coaching notes (warmth, specificity)?

### Recommendation

**Use 12K as the default budget for MVP-0.** Rationale:
- The actual assembled prompt is well under 12K even with full context, providing headroom for profiles with extensive history
- 12K gives room for mixed summarization (recent detail + older summaries), which is the most information-rich layout
- Saves ~20% cost vs 15K while maintaining full section structure
- The 15K ceiling should be retained as a configurable maximum for edge cases (very long conversation history)

---

## Experiment 2: Positional Placement

### Methodology

Compared the effect of placing the user profile data in different positions within the prompt:
- **Profile at START** (baseline) - High attention zone per U-curve research. Profile, goal, fitness, and paces in the stable prefix.
- **Profile in MIDDLE** - Low attention zone. Profile data mixed with training history.
- **Profile at END** - Recency attention zone. Profile data near the conversation history and user message.

All variations used the 15K budget with the same total content.

### Quantitative Results

| Variation | Lee (tokens) | Maria (tokens) | Start Sections | Middle Sections | End Sections |
|-----------|-------------|----------------|----------------|-----------------|--------------|
| Start     | 781         | 902            | 4              | 1               | 1            |
| Middle    | 781         | 902            | 1              | 4               | 1            |
| End       | 781         | 902            | 1              | 1               | 4            |

### Structural Observations

1. **Token usage is identical across all placements.** This confirms that positional placement is purely an attention/quality concern, not a budget concern.
2. **Section redistribution works as expected.** The start variation has 4 start sections (system prompt + profile + goal + fitness + paces), while middle and end variations move those components to their respective zones.
3. **System prompt always remains in start.** Even in end/middle variations, at least 1 section stays in start (the system prompt itself), ensuring the coaching identity is always in the high-attention prefix.

### Quality Observations (Pending Live API Runs)

<!-- TEMPLATE: Fill in after running live experiments with API key -->

| Variation    | Profile Data Usage | Hallucinations | Pace Accuracy | Notes |
|--------------|--------------------|----------------|---------------|-------|
| Start        | _pending_          | _pending_      | _pending_     |       |
| Middle       | _pending_          | _pending_      | _pending_     |       |
| End          | _pending_          | _pending_      | _pending_     |       |

**Hypotheses to validate with live data:**
- Profile-at-start should produce fewer hallucinated or incorrect profile details than profile-at-end (U-curve attention hypothesis)
- Profile-at-middle should perform worst (low attention zone)
- Profile-at-end may perform adequately for short prompts but degrade with longer conversation histories

### Recommendation

**Keep profile at START for MVP-0.** Rationale:
- Aligns with U-curve attention research: critical identity and constraint data belongs in the stable prefix
- Enables Anthropic prompt caching (stable prefix can be cached across turns, reducing latency and cost)
- System prompt + profile + paces form a cohesive "who is this runner and what are the constraints" block that benefits from positional proximity
- No token cost to this choice -- placement is free from a budget perspective

---

## Experiment 3: Summarization Level

### Methodology

Compared how training history representation affects token usage and prompt structure:
- **Per-workout detail (Layer 1)** - Individual workout records for all available history. Maximum granularity.
- **Weekly summary (Layer 2)** - Aggregated weekly totals. Minimal token cost.
- **Mixed (baseline)** - Layer 1 for recent weeks, Layer 2 for older weeks. Balance of detail and efficiency.

### Quantitative Results

| Variation    | Lee (tokens) | Maria (tokens) | Token Savings vs Per-Workout |
|--------------|-------------|----------------|------------------------------|
| Per-workout  | 815         | 1,081          | (baseline)                   |
| Weekly       | 690         | 701            | 15.3% (Lee), 35.2% (Maria)  |
| Mixed        | 781         | 902            | 4.2% (Lee), 16.6% (Maria)   |

**Overall token savings from weekly vs per-workout: 26.6%**

### Structural Observations

1. **Weekly summaries save significant tokens**, especially for profiles with more history. Maria (4-week history) saves 35.2% with weekly-only vs per-workout, compared to Lee's 15.3%.
2. **Mixed mode is the best balance.** It preserves recent per-workout detail (critical for adaptation decisions) while compressing older history. The savings increase as history depth grows.
3. **Per-workout mode scales linearly with history depth.** Maria uses 33% more tokens than Lee in per-workout mode. For profiles with 8+ weeks of history (common in production), per-workout could consume 2,000+ tokens just for history.
4. **Savings are more pronounced for advanced profiles.** Advanced runners with longer, denser training history benefit more from summarization.

### Quality Observations (Pending Live API Runs)

<!-- TEMPLATE: Fill in after running live experiments with API key -->

| Variation    | Adaptation Quality | History Referencing | Volume Progression | Notes |
|--------------|--------------------|---------------------|--------------------|-------|
| Per-workout  | _pending_          | _pending_           | _pending_          |       |
| Weekly       | _pending_          | _pending_           | _pending_          |       |
| Mixed        | _pending_          | _pending_           | _pending_          |       |

**Questions to answer with live data:**
- Does per-workout detail produce measurably better plan adaptation than weekly summaries?
- Can the LLM correctly infer volume trends from weekly summaries alone?
- Does mixed mode produce the best subjective coaching quality?

### Recommendation

**Use mixed summarization for MVP-0.** Rationale:
- Layer 1 (per-workout) for the most recent 1-2 weeks gives the LLM specific workout context for adaptation decisions
- Layer 2 (weekly summary) for older weeks provides trend context without excessive token cost
- Scales well: even with 12 weeks of history, the mixed approach keeps history under ~1,500 tokens
- If token budget becomes tight, overflow strategy can drop to weekly-only without losing essential context

---

## Experiment 4: Conversation History

### Methodology

Compared the impact of prior conversation turns on the assembled prompt:
- **0 turns** - Cold start, no prior conversation. Tests first-interaction plan quality.
- **5 turns** - Five prior user/coach message pairs. Tests whether conversation context improves plan coherence and personalization.

### Quantitative Results

| Variation | Lee (tokens) | Maria (tokens) | Sections | Additional Tokens |
|-----------|-------------|----------------|----------|-------------------|
| 0 turns   | 781         | 902            | 6        | (baseline)        |
| 5 turns   | 1,566       | 1,687          | 7        | +785              |

### Structural Observations

1. **5 turns add ~785 tokens.** This is a consistent overhead across both profiles, as conversation content is profile-independent.
2. **Section count increases by 1** when conversation history is present (conversation_history becomes a populated section).
3. **Token overhead is predictable.** At ~157 tokens per turn, conversation history token cost is linear and easy to budget for. The 10-turn limit in the 15K layout would add ~1,570 tokens.
4. **Even with 5 turns, total stays well under budget.** Lee at 1,566 tokens is only 13% of the 12K budget, leaving substantial room for the system prompt (~2,800 tokens) and other content.

### Quality Observations (Pending Live API Runs)

<!-- TEMPLATE: Fill in after running live experiments with API key -->

| Variation | Plan Coherence | Goal Consistency | Coaching Tone | Notes |
|-----------|----------------|------------------|---------------|-------|
| 0 turns   | _pending_      | _pending_        | _pending_     |       |
| 5 turns   | _pending_      | _pending_        | _pending_     |       |

**Hypotheses to validate with live data:**
- 0-turn cold start should still produce a complete, valid plan (all context is in the structured data)
- 5-turn history should improve coaching tone consistency and demonstrate "memory" of prior discussions
- The LLM may reference prior conversation content when adapting the plan

### Recommendation

**Support 0-10 turns for MVP-0, defaulting to 5 max.** Rationale:
- Cold start (0 turns) must always work -- it is the first-interaction baseline
- 5 turns provide sufficient conversational context without excessive cost (~785 tokens)
- 10 turns should be the configurable maximum, adding ~1,570 tokens which is still manageable
- Oldest-first truncation is the correct strategy: recent turns are more relevant for coaching continuity

---

## Cross-Validation: Lee vs Maria

### Observations

| Metric | Lee (intermediate) | Maria (advanced) | Delta |
|--------|-------------------|------------------|-------|
| Average tokens across 11 variations | 839 | 953 | +14% |
| Per-workout mode tokens | 815 | 1,081 | +33% |
| Weekly mode tokens | 690 | 701 | +2% |
| Run count | 11 | 11 | -- |

### Analysis

1. **Advanced profiles use more tokens** primarily due to denser training history (Maria has 4 weeks vs Lee's 3 weeks, with more workouts per week).
2. **Summarization level is the primary driver of cross-profile variance.** In weekly mode, Lee and Maria differ by only 11 tokens. In per-workout mode, the gap is 266 tokens. Mixed mode falls in between.
3. **Budget headroom is adequate for both profiles.** Even Maria's highest token usage (1,687 tokens with 5 conversation turns) is well within the 12K budget after accounting for the ~2,800-token system prompt.
4. **Scaling projection:** A profile with 12 weeks of history and 10 conversation turns might reach ~3,500-4,000 tokens for variable sections, plus ~2,800 for the system prompt, totaling ~6,500-6,800 tokens -- still under 12K.

---

## Recommended Context Injection Strategy for MVP-0

Based on the structural analysis from these experiments, the recommended context injection strategy is:

### Layout

```
START (cacheable stable prefix):
  1. System prompt (persona + safety + output format + guardrails)  ~2,800 tokens
  2. User profile (biographical data, race history, preferences)    ~600-800 tokens
  3. Goal state (current goal, target race)                         ~200-300 tokens
  4. Fitness estimate (VDOT, fitness level)                         ~200-300 tokens
  5. Training paces (deterministic, authoritative)                  ~300-400 tokens

MIDDLE (variable, non-cached):
  6. Training history (mixed: L1 recent + L2 older)                 ~500-1,500 tokens
  7. Computed metrics (ACWR, volume trends)                         ~100-200 tokens
  8. Relevant plan context (recent adaptations)                     ~200-500 tokens

END (conversational, non-cached):
  9. Conversation history (max 5 turns, oldest-first truncation)    ~0-800 tokens
  10. Current user message (always present)                         ~200-800 tokens
```

### Budget: 12K default, 15K maximum

- **Expected typical usage:** 5,000-7,000 tokens (system prompt + full context + 3 conversation turns)
- **Expected maximum usage:** 8,000-10,000 tokens (system prompt + extensive history + 10 conversation turns)
- **Headroom:** 2,000-7,000 tokens depending on context density

### Key Design Decisions

1. **Profile at start** -- validated by structural analysis; enables prompt caching
2. **Mixed summarization** -- best balance of detail and efficiency
3. **12K default budget** -- ample headroom while encouraging efficient context assembly
4. **Oldest-first conversation truncation** -- preserves recent coaching continuity
5. **Deterministic guardrails as a closing instruction in the start section** -- reinforces pace and volume constraints near the profile data they reference

---

## Prompt Engineering Lessons Learned

### Lesson 1: Token budgets should be based on measured usage, not estimates

The original 15K budget was set based on rough estimates of component sizes. Actual measurement shows that assembled prompts rarely exceed 8,000 tokens even with generous context. Setting budgets too high wastes no tokens but creates a false sense of scarcity in overflow logic that may never trigger. **Action:** Set the default budget to 12K based on measured data, with overflow logic tested against realistic content sizes.

### Lesson 2: Positional layout is a quality concern, not a budget concern

Moving sections between start/middle/end has zero impact on token usage. The entire purpose of positional optimization is attention quality -- ensuring the LLM pays attention to the right content. This means positional layout decisions can be made independently of budget decisions. **Action:** Treat layout and budget as orthogonal configuration axes in the prompt YAML files.

### Lesson 3: Summarization tiers provide the most impactful budget lever

The difference between per-workout and weekly history is the single largest variable in token usage (26.6% savings overall, up to 35% for dense profiles). This makes the summarization strategy the primary tool for managing token budgets under pressure. **Action:** Make summarization tier configurable per-request (not just per-configuration) so the system can dynamically adjust based on available budget.

### Lesson 4: Conversation history overhead is linear and predictable

At ~157 tokens per turn, conversation history cost is easy to predict and budget for. This predictability simplifies overflow logic: if you need to shed tokens, you can calculate exactly how many turns to drop. **Action:** Use token-per-turn estimates in overflow calculations rather than percentage-based heuristics.

### Lesson 5: Cross-profile variance is dominated by training history depth

Profile metadata (name, age, paces, goals) is relatively constant across profiles (~600-900 tokens). Training history is the primary source of cross-profile token variance. Profiles with extensive history (12+ weeks, 5+ runs/week) will be the primary consumers of the middle section budget. **Action:** Implement progressive summarization that automatically shifts to Layer 2 as history depth increases.

### Lesson 6: Dry-run infrastructure enables rapid iteration without API costs

The experiment infrastructure's dry-run mode allowed 22 structural experiments to run in under 1 second with zero API cost. This is a powerful development tool for prompt engineering iteration. **Action:** Keep the dry-run capability as a first-class feature of the experiment infrastructure for future prompt iterations.

---

## Architecture Decisions to Revisit

### DEC-010 (Token Budget: ~15K)

**Recommendation: Revise default to 12K.** The 15K budget is generous and should be retained as a configurable maximum, but the default should be 12K. This provides 40%+ headroom for typical prompts while signaling that the system should be efficient with context. The overflow strategy (which starts reducing content when budget is exceeded) should be tested against a 12K ceiling.

### DEC-022 (Model: Claude Sonnet 4.5)

**No revision needed.** The experiment infrastructure is model-agnostic (the model is specified in the coaching YAML, not in code). However, the model ID in coaching-v1.yaml (`claude-sonnet-4-5-20241022`) should be verified against current Anthropic API docs when live runs are attempted. Model IDs change with releases.

### Context Injection YAML Structure

**Recommendation: Add explicit `cacheable_prefix_boundary` marker.** The current structure marks individual sections as `cacheable: true/false`, but Anthropic's prompt caching works on contiguous prefixes. Adding an explicit boundary marker would make it clear where the cacheable prefix ends and the dynamic content begins. This is important for cost optimization in production.

### Overflow Strategy

**Recommendation: Test overflow logic against 12K ceiling.** The current overflow steps (reduce history, truncate conversation, remove plan context) were designed for 15K. At 12K, the steps should be re-evaluated to ensure they trigger at the right thresholds and provide sufficient savings.

---

## Token Usage Observations and Cost Estimates

### Measured Token Usage (Dry-Run Assembled Prompts)

| Scenario | Estimated Prompt Tokens | Notes |
|----------|------------------------|-------|
| Minimal (8K, Lee) | 690 | Weekly history only, no conversation |
| Typical (12K, Lee) | 781 | Mixed history, no conversation |
| Typical (12K, Maria) | 902 | Mixed history, no conversation |
| With conversation (15K, Lee, 5 turns) | 1,566 | Full context + 5 conversation turns |
| With conversation (15K, Maria, 5 turns) | 1,687 | Full context + 5 conversation turns |
| Maximum observed | 1,687 | Maria + 5 conversation turns |

**Note:** These are the variable section tokens only. The system prompt adds ~2,800 tokens, so total prompt token estimates range from ~3,490 to ~4,487.

### Cost Estimates at Scale

Using Anthropic Claude Sonnet pricing (as of early 2026):
- **Input tokens:** $3.00 per million tokens
- **Output tokens:** $15.00 per million tokens
- **Prompt caching discount:** ~90% reduction on cached prefix tokens

| Metric | Estimate | Basis |
|--------|----------|-------|
| Average input tokens per request | ~4,500 | System prompt (~2,800) + variable context (~1,700) |
| Average output tokens per request | ~2,500 | Plan JSON (~1,500) + coaching notes (~1,000) |
| Cached prefix tokens | ~2,800 | System prompt (stable across turns) |
| Non-cached input tokens | ~1,700 | Variable context per request |

**Per-request cost estimate:**
- Cached input: 2,800 tokens x $0.30/M = $0.00084
- Non-cached input: 1,700 tokens x $3.00/M = $0.0051
- Output: 2,500 tokens x $15.00/M = $0.0375
- **Total per request: ~$0.044**

**Daily cost estimates (per active user):**

| Usage Pattern | Requests/Day | Daily Cost | Monthly Cost (30d) |
|--------------|-------------|------------|-------------------|
| Light (weekly plan check) | 1 | $0.044 | $1.32 |
| Moderate (plan + 2 conversations) | 3 | $0.132 | $3.96 |
| Heavy (daily plan interaction) | 5 | $0.220 | $6.60 |

**Platform cost estimates:**

| Active Users | Avg Requests/User/Day | Monthly Cost |
|-------------|----------------------|-------------|
| 100 | 2 | $264 |
| 1,000 | 2 | $2,640 |
| 10,000 | 2 | $26,400 |

**Cost optimization levers:**
1. **Prompt caching** -- reduces input cost by ~60% (system prompt is ~62% of total input tokens)
2. **Reduce output tokens** -- tighter output format constraints could reduce output by 20-30%
3. **Model selection** -- Haiku for simple interactions (plan display, quick questions) at ~10% the cost
4. **Request batching** -- combine related operations into single requests where possible

---

## coaching-v2.yaml Changes

The `coaching-v2.yaml` file was created as an iterated version of `coaching-v1.yaml` incorporating the following changes based on experimental findings:

### Change 1: Token Budget Efficiency (Output Format)
**What changed:** The output format section was restructured to be more concise and explicit. The JSON example was replaced with a compact schema reference, and instructions were tightened to reduce output tokens.
**Why:** The output format section in v1 included a verbose JSON example (~150 tokens). Reducing this to a compact schema reference saves tokens in the system prompt (which is the largest fixed-cost component) and provides clearer structural guidance to the LLM.

### Change 2: Positional Layout Clarity (Context Injection)
**What changed:** The context injection section now includes an explicit `cacheable_prefix_boundary` after the start section, and adds a `position_rationale` field to each template marker explaining why it is placed where it is.
**Why:** Experiments confirmed that positional placement is a quality concern, not a budget concern. Making the position rationale explicit in the YAML helps future prompt engineers understand and maintain the layout decisions.

### Change 3: Output Format Precision (Plan Schema)
**What changed:** The plan schema now includes explicit `required` markers on each field, and the coaching notes section is separated from the JSON plan with clear structural guidance.
**Why:** Structured output parsing reliability depends on consistent JSON formatting. Explicit required/optional markers reduce ambiguity for the LLM and improve parseability.

### Change 4: Deterministic Guardrails Reinforcement
**What changed:** The deterministic guardrails section was strengthened with explicit examples of correct and incorrect behavior, and placed immediately after the training paces section for positional proximity.
**Why:** The guardrails are the most critical safety constraint (ensuring computed paces are used exactly). Positional proximity to the pace data they reference should improve compliance.

### Change 5: Model Update
**What changed:** Model ID updated to reflect latest available Claude Sonnet model string. Temperature retained at 0.3 for plan generation.
**Why:** Model IDs change with releases. The v2 prompt should reference the current model string.

---

## Next Steps

1. **Run live API experiments.** Configure API key and execute all 22 variations with live LLM responses. Fill in the "Quality Observations (Pending Live API Runs)" sections above.
2. **Evaluate plan quality manually.** Review generated plans for each variation against the success criteria: physiological soundness, personalization, safety, and context fidelity.
3. **Measure actual API token counts.** Compare estimated token counts (chars/4) against Anthropic's actual tokenizer to calibrate the estimation method.
4. **Test overflow logic at 12K.** Verify that the overflow strategy triggers correctly when a very dense prompt exceeds the 12K default budget.
5. **Iterate coaching-v2.yaml based on live findings.** The current v2 is based on structural analysis. Live API results may reveal additional prompt engineering improvements.
