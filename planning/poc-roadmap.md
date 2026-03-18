# POC Exploration Roadmap

Rather than planning a full build, these are the experiments and proofs-of-concept that will inform the real architecture. Each POC should answer a specific question.

## MVP Staging

Two distinct milestones, not one:

- **MVP-0 (Personal validation):** Conversation + plan generation. Enough for the builder to use it on their own runs and validate that the AI produces genuinely good plans. Core loop: onboard → generate plan → see upcoming workouts.
- **MVP-1 (Friends/testers):** Adds adaptation. This is the minimum bar before handing it to anyone else, because without adaptation it's just a plan generator — the differentiator isn't visible. Core loop: onboard → generate plan → log workouts → see the plan intelligently adjust.

POCs below are ordered to support this progression: POC 1 and POC 4 feed MVP-0, POC 2 feeds MVP-1.

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

**Approach:** Test with a simulated sequence of workouts with intentional deviations.

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
