# Research Prompt: Batch 2a — R-001
# Running Training Methodologies as Configurable Frameworks for an AI Coach

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

I'm building an AI-powered running coach that generates and adapts personalized training plans through conversation. I need to deeply understand how training methodologies work so I can design the AI's coaching knowledge layer — specifically what should be hardcoded as safety guardrails, what should be configurable per methodology, and what should be left to the AI's real-time judgment.

### Context

From competitive research I've already completed: plan generation is table stakes — every app does it. The differentiator is the quality of the coaching relationship, including transparent reasoning ("here's *why* I'm adjusting your plan"). Runna's 2026 injury controversy (PTs reporting stress fractures from aggressive algorithmic plans) shows that safety and explainability matter. My AI coach needs to not just prescribe workouts, but explain its reasoning in terms a runner would understand — which means it needs to genuinely understand the methodology it's drawing from.

My current belief (validate or challenge): training methodology should be configurable, not hardcoded. The AI selects or blends between approaches based on user profile and feedback, rather than the system committing to one philosophy.

### What I need researched

**1. Major training methodologies compared**

For each major methodology, I need:
- Core philosophy and what makes it distinct
- How it structures training phases (periodization approach)
- How it prescribes intensity (pace zones, heart rate, effort, or some combination)
- What types of runners it works best for (beginners vs. experienced, 5K vs. marathon)
- Where it's rigid vs. where a coach using this methodology would flex

Cover at minimum: Jack Daniels (VDOT), Pete Pfitzinger, Hal Higdon, Hanson Method, 80/20 Running (Fitzgerald/Seiler), Brad Hudson, Arthur Lydiard, MAF Method (Maffetone). Add others if significant.

**2. Where methodologies agree vs. diverge**

This is the most important section. What are the shared principles across all serious training methodologies? These become the AI's non-negotiable guardrails. Where do they meaningfully disagree? These become the configurable parameters.

For example: most agree on progressive overload and rest days, but disagree on long run intensity, the role of tempo runs, and how aggressive taper should be. I need this mapped out clearly.

**3. How human coaches actually blend methodologies**

Real coaches don't rigidly follow one system. How do experienced coaches draw from multiple methodologies for a single athlete? What triggers them to shift approaches mid-cycle? What are the decision points? This is the behavior the AI needs to replicate.

**4. Safety boundaries**

What are the concrete, programmable safety rules that should be hard guardrails regardless of methodology? Examples: max weekly mileage increase percentages, minimum rest days per week, load thresholds that trigger injury warnings, pace zones that indicate overtraining. I want specific numbers or formulas where they exist (e.g., the "10% rule" and its limitations).

**5. Where would an LLM likely get this right vs. wrong?**

Based on the training science landscape, where would a modern LLM likely have solid knowledge from its training data, and where would it need explicit guardrails or knowledge injection? For example: LLMs probably understand periodization conceptually, but might they miscalculate safe mileage progression rates or generate unrealistic pace targets?

### What "good" looks like

I need enough depth to confidently design the AI's coaching knowledge layer. Cite specific methodology sources (book titles, chapter references, key formulas like VDOT tables) where possible. Be concrete — "the 10% rule says X but research shows Y" is more useful than "mileage should increase gradually."

End with a **"Configurable Framework"** section: given everything you found, propose a structure for how the AI should organize its training knowledge — what's a universal guardrail, what's a per-methodology parameter, and what's left to the AI's conversational judgment.

### Output format

1. Methodology-by-methodology analysis
2. Shared principles vs. key divergences (the agreement/disagreement map)
3. How coaches blend in practice
4. Safety guardrails (specific, programmable)
5. LLM knowledge gaps
6. Configurable Framework (synthesis)
