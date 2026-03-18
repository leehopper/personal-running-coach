# Research Prompt: Batch 2b — R-004
# AI Planning Architecture: State Management, Context Injection, and Adaptive Replanning

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

I'm building an AI-powered running coach — a web app where an LLM builds and adapts personalized training plans through conversation. I've completed research on the competitive landscape and training science, and now need to solve the core technical architecture question: how does an AI system manage a multi-layered, evolving plan that adapts based on real-world feedback?

### What I already know (from prior research)

**The system has two distinct layers (decided):**
- A **deterministic computation layer** that handles all numerical work: pace calculations (VDOT tables), load monitoring (ACWR), mileage progression math, single-run spike checks, safety guardrail enforcement, environmental adjustments. This layer is code, not AI.
- An **LLM coaching layer** that handles explanation, adaptation reasoning, daily coaching judgment, pattern recognition, goal recalibration conversations, and methodology rationale.

**Between these layers sits plan state** — the training plan itself, which the computation layer constrains and the LLM layer reasons about and adapts. This plan state is the artifact I need architectural guidance on.

**My current (unvalidated) model:** Three tiers of plan state — macro (months/season periodization phases), meso (weekly structure), micro (daily workout prescriptions). The idea is that different tiers change at different frequencies and the AI only needs detailed context for the current timeframe, keeping token usage efficient. But this is an assumption I want challenged.

**Other context:** Web app, single LLM agent for MVP, methodology is configurable (12+ parameters per methodology), plan needs to support both goal-driven runners and goalless "just keep me healthy" runners through the same flexible architecture.

### What I need researched

**1. Plan state management**

How should the AI system store and manage a training plan that it's continuously editing?

- What data structure best represents a training plan that needs to be: structured enough for the computation layer to validate, flexible enough for the LLM to reshape, and versioned so you can understand what changed and why?
- Should the plan state be one artifact or decomposed? If decomposed, what's the right decomposition (is macro/meso/micro the right split, or something else)?
- How do you handle versioning and auditability? When the AI adjusts the plan, how do you track what changed, why, and whether it can be undone?
- Are there examples of production AI systems that manage "AI-authored persistent artifacts" — where the AI is both the creator and ongoing editor of structured data? How do they handle state?

**2. Context injection for LLM-based coaching**

Each AI call needs a structured context payload assembled from stored data. The challenge is deciding what to include and how to compress.

- What's the optimal structure for a context payload that gives the LLM everything it needs to make coaching decisions? (User profile, current plan state, recent training history, recent conversations, computed metrics)
- How do you compress older training history without losing important signals? (Summarization strategies — rolling averages, trend narratives, statistical aggregates?)
- How do you decide what to include vs. exclude per call? (Is there a relevance filtering pattern?)
- What's the practical token budget look like? With current model context windows (100K-200K tokens), how much training history can you actually fit alongside the system prompt, plan state, and user message?
- Are there production systems doing structured context injection for personalized AI products that I can learn from?

**3. The adaptation loop**

When new data arrives (logged workout, reported feeling, life event, health data), the system needs to decide how to respond.

- What patterns exist for routing incoming signals to the right level of response? (Absorb the deviation → adjust next few days → restructure the week → reconsider the macro plan)
- How do you prevent cascading over-corrections? (One bad workout shouldn't blow up the whole plan)
- How do you distinguish signal from noise? (One missed workout is noise, three consecutive missed midweek runs is a signal)
- Are there control theory, reinforcement learning, or state machine concepts that apply here — even if the implementation is prompt-based rather than model-based?
- What does the "decision tree" look like for common scenarios: missed workout, under-performance, over-performance, illness, travel, user requests a change, new race goal?

**4. Cross-domain patterns**

What can we learn from AI systems managing adaptive plans in other domains?

- Adaptive learning platforms (education) — how they adjust lesson plans based on student performance
- Treatment plan management (healthcare AI) — how plans adapt to patient response
- Game AI — adaptive difficulty, NPC behavior planning over time
- Financial planning AI — portfolio rebalancing triggers
- Project management AI — timeline adaptation when reality diverges from plan

For each: what's the state management pattern, how do they handle the "plan vs. reality" gap, and what transfers to a running coach?

**5. Validate or challenge the three-tier model**

Given everything above: is macro/meso/micro the right decomposition? Are there better models? What are the failure modes of this approach? Specifically:
- When a micro-level deviation (missed workout) should propagate up to the meso or macro level — how do you decide when that threshold is crossed?
- Does the three-tier model work for goalless users (no macro periodization target)?
- Is there a simpler model that achieves the same context efficiency without the added complexity?

### What "good" looks like

I need architectural patterns I can actually implement, not abstract theory. If there are open-source implementations, published system designs, or well-documented case studies, those are gold. Pseudocode or data structure sketches are more useful than paragraphs of description.

End with a **"Recommended Architecture"** section: given the computation layer + LLM layer already decided, what's the recommended approach for the plan state layer that sits between them? Include a concrete data model sketch if possible.

### Output format

1. Plan State Management (data structures, versioning, decomposition)
2. Context Injection Patterns (payload structure, compression, token budgets)
3. The Adaptation Loop (routing, over-correction prevention, decision patterns)
4. Cross-Domain Patterns (what transfers from other domains)
5. Three-Tier Model Assessment (validate/challenge/alternative)
6. Recommended Architecture (synthesis with data model sketch)
