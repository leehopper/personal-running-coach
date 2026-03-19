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

## LLM Provider Strategy (from R-005)

**Primary model: Claude Sonnet 4.5** (~$7.60/user/month). The coaching layer demands nuanced multi-turn conversation — empathetic adjustments, injury signal detection, persona consistency (DEC-027) — that warrants starting with the stronger model. Still within subscription-absorbing range at $12-15/month pricing. See DEC-022.

**Abstraction:** All LLM calls route through a thin adapter interface. Use Vercel AI SDK (TypeScript) or LiteLLM as SDK import (Python) — near-zero latency overhead (~500µs). Prompts stored in versioned config files, not code. Anthropic's explicit prompt caching with 1-hour TTL on the stable prefix.

**No BYOM.** At $1-3/month per user LLM costs, BYOM solves a problem that doesn't exist while creating security, compliance, and UX problems. No AI fitness product offers it. See DEC-021.

**Fallback strategy (growth stage):** Test GPT-4.1 mini or Gemini 2.5 Flash with existing prompts. ~70-80% of prompt engineering transfers across models. Configure automatic failover for Anthropic outages. Build 20-30 behavioral test cases that validate coaching responses across providers.

**Model routing (scale):** Route simple queries (greetings, FAQ) to a budget model (~$0.25/user/month). Route complex coaching (adaptation reasoning, injury response) to primary model. 30-50% average cost reduction. Deploy via LLM gateway (Portkey or LiteLLM proxy).

**Provider risk mitigation:** The key architectural decisions are prompts in config files, structured output validation independent of provider, eval suite testing across models, and provider-specific features isolated behind interfaces. Switching takes 1-2 weeks basic, 4-8 weeks production quality.

## Multi-Agent Consideration (Future)

A future architecture might split responsibilities:

- A coaching agent for plan decisions
- An analysis agent for pattern recognition
- A triage agent for routing user input to the right response type

For now, a single well-prompted agent with good context is sufficient and avoids orchestration complexity.

## Wearable Data Pipeline (from R-006)

The app sits on top of wearable data as a "planning intelligence layer." Four-stage pipeline designed around the two-layer architecture:

**Stage 1 — Ingress** (stateless): Receive webhook POST → verify sender (IP whitelist for Garmin) → store raw payload → return 200 immediately. Zero business logic.

**Stage 2 — Process** (async worker): Dequeue raw webhook → check idempotency → extract structured fields from Activity Summary JSON AND parse .FIT file → extract wellness data (sleep, HRV, Training Readiness, VO2max) from Health API → write to canonical tables.

**Stage 3 — Compute** (deterministic layer): Calculate derived metrics (pace zones, HR zone time, ACWR, race-readiness) → update five-layer summarization hierarchy → write computed results to event-sourced plan state.

**Stage 4 — Summarize** (LLM layer): Generate ~100-150 token workout summary → incorporate daily wellness context → feed to coaching conversation.

### Data split between layers

Activity Summary JSON feeds the LLM coaching layer's summaries (distance, duration, avg pace, avg HR, elevation — sufficient for conversational coaching). .FIT file parsing feeds the deterministic computation layer's detailed analysis (per-lap pace/HR, running dynamics, Training Effect, precise split calculations).

### Storage strategy (minimized for FTC HBNR compliance)

- Raw webhooks: 30-day retention then delete (debugging only)
- Raw .FIT files: parse on receipt, extract structured fields, delete the file
- Structured activity data: retained for account lifetime, encrypted at rest (AES-256)
- LLM summaries and trend narratives: retained for account lifetime
- Daily wellness data: retained for account lifetime

AES-256 encryption qualifies data as "secured" under FTC HBNR — breaches of properly encrypted data don't trigger the 60-day notification requirement.

**Critical:** event source the plan state (coaching decisions, adaptations) but do NOT event source imported workout data. Workouts are external facts (idempotent CRUD); plan adaptations are domain events (append-only).

### Multi-source deduplication

Match on start time ±5 minutes + sport type + duration ±10%. Source priority for running: Garmin direct > COROS/Polar direct > Strava > Apple Health. For sleep: Oura > WHOOP > Garmin > Apple Watch. Complementary data (watch for running + ring for sleep) links by user + date, no dedup needed.
