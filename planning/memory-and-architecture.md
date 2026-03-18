# Memory & Agent Architecture

The AI needs to maintain knowledge across several domains. For the MVP, this is a single agent with structured memory, not a multi-agent system.

## Knowledge Domains

- **User Profile / Biodata:** Age, weight, resting HR, injury history, running experience, preferences. Changes slowly, referenced often.
- **Goal State:** Target race/event, target time, current assessed fitness level, deadline. Changes occasionally.
- **Training History:** Every completed workout, how it compared to the prescription, subjective effort, conditions. Grows continuously and needs summarization strategy.
- **Active Plan State:** The current macro/meso/micro plan and the user's position within it. The AI is the author and ongoing editor of this artifact.
- **Conversation Context:** Recent interactions, user-reported feelings, life events mentioned. Provides color that structured data misses.

## Context Injection Strategy

Each AI call receives a structured context payload assembled from the database. The challenge is deciding what to include and how to summarize. Recent data should be detailed; older data should be compressed into trends and summaries.

The exact structure of this payload is a key design decision that will evolve through POC work.

## Multi-Agent Consideration (Future)

A future architecture might split responsibilities:

- A coaching agent for plan decisions
- An analysis agent for pattern recognition
- A triage agent for routing user input to the right response type

For now, a single well-prompted agent with good context is sufficient and avoids orchestration complexity.
