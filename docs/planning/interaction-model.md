# Interaction Model

The system supports three distinct interaction modes, each serving a different purpose in the coaching relationship.

## Guided Onboarding

First-time setup follows a structured, sequential flow. The AI asks targeted questions to build the user's profile and generate an initial plan. This is conversational in tone but deterministic in structure — it guarantees the system collects the data it needs.

### Onboarding topics include:

- Primary goal (race training, general fitness, return from injury, etc.)
- Target event and date, if applicable
- Current fitness level and recent running history
- Weekly schedule constraints (available days, time windows)
- Injury history and physical limitations
- Preferences (terrain, indoor/outdoor, cross-training willingness)

### Onboarding output:

A complete user profile and an initial plan spanning all three planning tiers (macro, meso, micro).

## Proactive Coaching

The system does not only respond — it initiates. Based on logged data and (eventually) passive health data, the AI can proactively suggest adjustments.

### Examples of proactive behavior:

- Detecting consecutive under-performance and suggesting a recovery adjustment
- Noticing poor sleep data and swapping a hard session for an easy one
- Recognizing a missed workout and offering redistribution options
- Flagging that race day is approaching and initiating a taper discussion

This is the "orchestrator" behavior — the system managing the plan on the user's behalf rather than waiting to be told.

### Proactive communication tone (DEC-027)

Proactive messages map to the escalation ladder's communication modes. Light adjustments (Level 1) get informational tone — "I nudged tomorrow's run down slightly." Notable changes (Level 2) get a brief explanation with rationale. Significant changes (Level 3) use the full Elicit-Provide-Elicit pattern. Plan overhauls (Level 4) get maximum transparency with alternatives and explicit agreement. Traffic-light shorthand: Green = minimal commentary, Amber = brief check-in question, Red = clear direct communication with safety framing.

## Open Conversation

The user can message the system at any time with unstructured input. The AI interprets this against the full context of the user's profile, history, and active plan.

### Examples:

- "My knee feels tight after yesterday's run."
- "I have a work trip next week, can only run twice."
- "I feel great today — can I do something harder?"
- "Actually, I want to target a sub-2-hour half instead."

### Conversational approach (DEC-027)

Open conversation uses the three-layer communication architecture from R-010. Every response applies OARS (Open question, Affirmation, Reflection, or Summary). When sharing training knowledge or correcting behavior, the Elicit-Provide-Elicit pattern applies. Substantive coaching conversations (goal changes, setback processing, plan adjustments) follow a modified GROW structure (Goal-Reality-Options-Way Forward).

Key behavioral rules: acknowledge feelings before correcting behavior, ask before assuming ("What was going on?" not "You ran too fast"), provide rationales for counterintuitive recommendations, offer at least one choice even when options are constrained, and end substantive messages with a clear next step or question.

See `coaching-persona.md` for full scenario playbooks covering the 8 most common coaching conversations.

### Guardrail:

The AI always grounds its response in structured context — it is not a generic chatbot, it is an agent with deep knowledge of this specific user.
