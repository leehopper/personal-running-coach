# Memory & Agent Architecture

The AI needs to maintain knowledge across several domains. For the MVP, this is a single agent with structured memory, not a multi-agent system.

## Knowledge Domains

- **User Profile / Biodata:** Age, weight, resting HR, injury history, running experience, preferences. Changes slowly, referenced often.
- **Goal State:** Target race/event, target time, current assessed fitness level, deadline. Changes occasionally.
- **Training History:** Every completed workout, how it compared to the prescription, subjective effort, conditions. Grows continuously and needs summarization strategy.
- **Active Plan State:** The current macro/meso/micro plan and the user's position within it. The AI is the author and ongoing editor of this artifact.
- **Conversation Context:** Recent interactions, user-reported feelings, life events mentioned. Provides color that structured data misses.

## Context Injection Strategy (validated by R-004)

Each AI call receives a structured context payload assembled by a deterministic `ContextAssembler` in the .NET backend. Total payload: ~15,000 tokens (~7.5% of the 200K window). The bottleneck is relevance and positional accuracy, not token count.

**Positional optimization:** Static reference data (profile, plan state, metrics) at the START of context. Conversational context at the END. Matches the U-curve finding that LLMs have 30%+ accuracy drop for information in the middle.

**Prompt caching:** The stable prefix (~6.3K tokens: system prompt + user profile + plan state) changes at most weekly. Placing it at the start enables Anthropic's prompt caching for 90% cost reduction on cached reads.

**Interaction-specific assembly:** The assembler selects context based on query type. "How was my run?" → current week + last 1-3 workouts. "Am I ready for my race?" → full remaining plan + 2-week summaries + trend metrics. "I'm feeling tired" → fatigue metrics + last 5-7 days raw + injury flags.

### Five-Layer Summarization Hierarchy

Training history is stored at five compression layers, each pre-computed by background jobs (not generated at query time):

- **Layer 0** (raw data, never in context): GPS tracks, per-second HR, every split. Used by computation layer only.
- **Layer 1** (per-workout, ~100-150 tokens each): "Tue 3/12: Easy run, 6.2mi, 8:45/mi avg, HR 142, felt 'good', slight left knee tightness." Generated after each workout sync.
- **Layer 2** (weekly, ~200-300 tokens each): Aggregate compliance, workout breakdown, pace trends, notable events. Generated weekly.
- **Layer 3** (phase, ~300-500 tokens): Phase summary with period trends, compliance, injuries. Generated at phase transitions.
- **Layer 4** (trend narrative, ~500 tokens): LLM-generated synthesis of patterns, concerns, and trajectory. Generated weekly.

This achieves 80-90% token reduction while improving response quality through selective summarization over blanket data inclusion. Validated by Mem0's production system and Wang et al.'s recursive summarization research.

## Multi-Agent Consideration (Future)

A future architecture might split responsibilities:

- A coaching agent for plan decisions
- An analysis agent for pattern recognition
- A triage agent for routing user input to the right response type

For now, a single well-prompted agent with good context is sufficient and avoids orchestration complexity.
