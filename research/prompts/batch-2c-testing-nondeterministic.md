# Research Prompt: Testing Non-Deterministic AI Outputs (R-007)

## What I Need

I'm building an AI running coach where an LLM generates coaching advice, plan adaptations, and workout explanations — but the actual pace calculations, load monitoring, and safety guardrails are deterministic code. I need a practical testing strategy for the LLM-driven parts, which are inherently non-deterministic.

## The Specific Architecture

My system has two layers:

**Deterministic layer (testable with conventional methods):**
- VDOT/pace zone calculations
- ACWR, CTL, ATL, TSB monitoring
- Safety guardrails (spike limits, intensity caps, minimum recovery)
- Mileage progression math
- 5-level escalation ladder routing (which level of adaptation to trigger)

**LLM layer (the hard part to test):**
- Coaching explanations ("here's why I'm changing your plan")
- Adaptation reasoning at Levels 2-4 (week restructure, phase reconsideration, plan overhaul)
- Generating daily workout prescriptions from macro constraints + meso templates
- Coaching conversations (responding to user questions, concerns, motivation)
- Trend narrative generation (Layer 4 summaries of training history)
- Goal recalibration conversations

The LLM outputs structured JSON (via Claude's structured outputs / constrained decoding) for plan modifications, and natural language for coaching conversation. The deterministic layer validates all structured outputs against safety constraints before applying them.

## What I Need to Understand

**1. Evaluation frameworks for LLM coaching quality**

How do you score whether a coaching explanation is good? Whether an adaptation rationale makes sense? Whether a workout prescription (generated from constraints) is appropriate for the athlete's current state? I need concrete rubrics, not abstract principles.

Look at how these domains evaluate non-deterministic AI outputs:
- Education (adaptive tutoring systems like Khanmigo, Duolingo) — how do they measure tutoring quality?
- Healthcare AI (clinical decision support) — how do they validate AI recommendations?
- Creative AI (writing assistants, code generation) — what evaluation frameworks exist?
- Game AI (NPC dialogue, procedural content) — how do they QA generated content?

I want specific scoring dimensions, not just "use human evaluation."

**2. LLM-as-judge patterns**

Claude evaluating Claude's output is increasingly common. What are the best practices? Specifically:
- How do you build effective judge prompts for domain-specific evaluation?
- What's the reliability of LLM judges vs. human judges in similar domains?
- How do you detect and mitigate self-preference bias?
- Can you use a cheaper/faster model as judge while the production model is more capable?
- What rubric structures produce the most consistent judge ratings?

I want real examples from production systems, not theoretical frameworks.

**3. Regression detection for prompt changes**

When I modify the system prompt, coaching persona, or methodology parameters, how do I know if the output got better or worse? I need:
- A test suite design that catches regressions in coaching quality
- Specific scenario definitions (the "unit tests" for coaching — e.g., "athlete reports knee pain after tempo run" should produce certain qualities of response)
- Statistical approaches for comparing non-deterministic outputs across prompt versions (how many runs do you need? what significance thresholds?)
- How to version and track prompt performance over time

**4. Safety-specific testing**

Even though the deterministic layer enforces hard safety constraints, the LLM can still give bad qualitative advice ("push through the pain," "you'll be fine," minimizing injury signals). How do I:
- Build an adversarial test suite for unsafe coaching advice?
- Define red lines for coaching responses (what should NEVER appear)?
- Test for appropriate uncertainty/deferral (the LLM should say "see a doctor" for medical concerns)?
- Catch subtle safety failures (overly optimistic reassurance, downplaying symptoms)?

**5. Practical implementation for a side project**

I'm a solo developer. I can't build a full evaluation pipeline on day one. What's the minimum viable testing approach that:
- Catches the most dangerous failures first (safety, then quality)?
- Scales from "manually review 10 scenarios" to "automated eval suite"?
- Integrates with a plan-first development workflow (test scenarios defined before implementation)?
- Gives me confidence that prompt changes don't break things?

What does the progression look like from MVP testing to production-grade evaluation?

## What I Don't Need

- Testing strategies for the deterministic layer (I know how to unit test code)
- General software testing advice
- Theoretical ML evaluation metrics (BLEU, ROUGE, etc.) unless they're specifically useful for coaching text
- Abstract principles without concrete implementation guidance

## Output Format

Structure your findings as:
1. **Evaluation dimensions** — the specific qualities to measure in coaching outputs, with scoring rubrics
2. **LLM-as-judge implementation** — concrete judge prompt patterns, reliability data, bias mitigation
3. **Regression testing design** — scenario library structure, statistical comparison methods, minimum sample sizes
4. **Safety testing** — adversarial scenarios, red-line definitions, escalation testing
5. **Progressive implementation roadmap** — what to build first, what to defer, how it scales

For each section, I want: what the approach is, why it works (evidence from production systems), and how to implement it concretely. Pseudocode or example prompts are welcome. Cite real systems and research where possible.
