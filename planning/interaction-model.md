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

## Open Conversation

The user can message the system at any time with unstructured input. The AI interprets this against the full context of the user's profile, history, and active plan.

### Examples:

- "My knee feels tight after yesterday's run."
- "I have a work trip next week, can only run twice."
- "I feel great today — can I do something harder?"
- "Actually, I want to target a sub-2-hour half instead."

### Guardrail:

The AI always grounds its response in structured context — it is not a generic chatbot, it is an agent with deep knowledge of this specific user.
