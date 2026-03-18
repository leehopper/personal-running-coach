# POC Exploration Roadmap

Rather than planning a full build, these are the experiments and proofs-of-concept that will inform the real architecture. Each POC should answer a specific question.

## MVP Staging

Two distinct milestones, not one:

- **MVP-0 (Personal validation):** Conversation + plan generation. Enough for the builder to use it on their own runs and validate that the AI produces genuinely good plans. Core loop: onboard → generate plan → see upcoming workouts.
- **MVP-1 (Friends/testers):** Adds adaptation. This is the minimum bar before handing it to anyone else, because without adaptation it's just a plan generator — the differentiator isn't visible. Core loop: onboard → generate plan → log workouts → see the plan intelligently adjust.

POCs below are ordered to support this progression: POC 1 and POC 4 feed MVP-0, POC 2 feeds MVP-1.

### Evaluation Strategy (from R-007)

R-007 research established that evaluation should start alongside POC work, not after. Begin with 15-20 manually curated test scenarios scored in a spreadsheet (safety, personalization, scope boundaries). Every POC generates test cases that feed the growing eval suite. By the time POC work completes, the eval suite should have ~50 scenarios with binary safety checks + LLM-as-judge quality scoring. See decision log DEC-016 for the full progressive roadmap.

---

## POC 1: Context Injection & Plan Quality

**Core question:** Can a single well-structured prompt with injected user context produce training plans that are genuinely good?

**Sub-questions:**
- What does the context payload need to look like?
- How does plan quality degrade as history grows and needs summarization?

**Why it matters:** This is the foundational question — everything else depends on the AI's coaching quality.

**Status:** Not started

---

## POC 2: Adaptive Replanning

**Core question:** When a user logs a workout that deviates from the plan, can the AI produce a sensible adjustment?

**Sub-questions:**
- Does it know when to absorb a deviation vs. restructure?
- How do you prevent cascading over-corrections?
- Does the 5-level escalation ladder (DEC-012) produce appropriate responses across the full range of scenarios?

**Approach:** Test with a simulated sequence of workouts with intentional deviations. Use the R-007 scenario library structure (CheckList framework: minimum functionality tests, invariance tests, directional expectation tests). Specifically test multi-turn escalation patterns (e.g., gradually increasing fatigue signals across 4+ turns).

**Eval approach:** Binary safety pass/fail on every adaptation response + LLM-as-judge for training accuracy and communication quality. Run each scenario 3-5 times at production temperature to catch stochastic failures.

**Status:** Not started

---

## POC 3: Tiered Planning Efficiency

**Core question:** Does the macro/meso/micro separation actually work in practice?

**Sub-questions:**
- Can the AI generate a useful macro plan from onboarding data alone, then flesh out meso and micro layers on demand?
- Does this reduce token usage meaningfully compared to generating everything at once?

**Status:** Not started

---

## POC 4: Interaction Flow

**Core question:** Do the three interaction modes (onboarding, proactive, open conversation) feel right in practice?

**Sub-questions:**
- Does guided onboarding feel natural?
- Does open conversation produce useful adaptations?
- Where does the AI need more guardrails or structure?

**Approach:** Build a minimal chat interface and test with real use.

**Status:** Not started

---

*Update status and add findings as POCs are completed. Findings from POCs should feed back into the [open questions tracker](../decisions/open-questions.md) and [decision log](../decisions/decision-log.md).*
