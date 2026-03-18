# Planning Architecture

The plan operates across three tiers of granularity. This is both a coaching framework and a technical optimization — the AI never needs to hold an entire year of daily detail in context. R-004 research validated this model against coaching science, robotics (three-layer deliberative/sequencing/reactive), and adaptive learning systems, with four crucial modifications.

## Core Insight: Constraints, Not Schedules

The three tiers store **lightweight constraints and templates**, not detailed schedules. Micro-level prescriptions are **generated on demand** from macro constraints + meso templates + current monitoring state. This makes the plan inherently adaptive — regenerating a day's workout is cheap, while the strategic vision remains stable. The plan is a living, regenerable artifact, not a rigid document.

## Macro Layer (Months / Season)

A **phase schedule with constraints**: phase type (base, build, peak, taper, race, recovery, maintenance), date ranges, target weekly volume range, intensity distribution targets, and allowed workout types. ~200 tokens in context.

- Changes only when goals shift fundamentally (new race, changed timeline, major injury, extended illness >2 weeks)
- For goalless runners, degrades to a simple "current emphasis" tag (aerobic focus, speed maintenance, strength block) that rotates every 4-8 weeks — same architecture, lighter content

## Meso Layer (Weekly Cycles)

A **weekly template with slot types**: which days are hard/easy/long/rest, plus emphasis weights and volume targets. ~150 tokens per week.

- Adjusts based on cumulative fatigue patterns, life events, and phase transitions
- Includes automatic deload every 3rd or 4th week (Norwegian world-class coaching pattern, 25-35% volume reduction)
- For goalless runners, becomes a rolling 4-week block with auto-deload

## Micro Layer (Daily Prescriptions)

**Generated on demand** for the current or next day, using macro constraints + meso template + recent performance data + monitoring state. ~500 tokens per workout. Not pre-stored for the full plan.

- This is where most of the AI's reactive work happens
- Generated just-in-time, stored after generation for audit trail
- Key insight from TrainerRoad: for routine adjustments, **adapt the difficulty, not the structure** — replace workouts with equivalent ones at the appropriate level rather than rearranging the weekly pattern

## Event-Driven Recomposition

When edge cases arise (injury, goal change, extended vacation), don't patch — **regenerate from the appropriate tier downward** (Hierarchical Task Network pattern from AI planning):

- Injury → regenerate macro (new phase schedule for recovery → rebuild → return), cascade to meso and micro
- Vacation → regenerate meso for the return week, keep macro intact
- Bad weather or schedule conflict → regenerate micro only
- Multi-race seasons → macro tier contains multiple peak targets with mini-cycles between them

The primary failure mode of the three-tier model is rigidity of stored data. By storing constraints and regenerating downward on disruption, every edge case becomes a regeneration trigger, not a data corruption problem.

## Continuous Monitoring (Cross-Cutting)

ACWR and TSB monitoring operates **independently of the tier hierarchy** and can trigger replanning at any level:

- ACWR > 1.5 → immediate micro adjustment (reduce today's workout)
- ACWR persisting above 1.3 for 2+ weeks → meso adjustment (insert recovery week)
- CTL dropping >15% → macro re-evaluation (athlete is detraining, timeline may need extension)

Uses hysteresis thresholds (different values for entering vs. exiting concern states) to prevent flip-flopping.

## Plan State Management

R-004 recommends **event-sourced plan state** with a hybrid document + event log pattern:

- Current plan state lives as a JSON document (for easy LLM consumption via structured outputs)
- Every modification recorded as an immutable event for auditability
- Both the computation layer (emitting load constraints) and the LLM layer (emitting adaptation rationale) append to the same event stream
- Recommended framework: **Marten** (v8.x, MIT licensed) — .NET event store + document database on PostgreSQL JSONB. Provides event streams per entity, inline projections, optimistic concurrency, and snapshots. ~10 lines of configuration.
- Companion: **Wolverine** for reliable message processing with outbox patterns

The event stream IS the audit trail. To understand why a workout changed, query the stream. To undo a change, replay excluding the unwanted event and regenerate. Claude's structured outputs guarantee schema-valid plan modifications via constrained decoding.

## Context Window Efficiency

Full context payload consumes **~15,000 tokens — about 7.5% of the 200K window.** The bottleneck is not token count but relevance and positional accuracy (LLMs show 30%+ accuracy drop for information buried in the middle).

Token budget per coaching interaction:

- System prompt (persona, safety, format): ~3,000
- User profile: ~800
- Current plan state: ~2,500
- Computed metrics (ACWR, pace zones, fitness): ~1,500
- Recent training history (7 days raw, 2-4 weeks summarized): ~4,000
- Conversation history (last 5-10 turns): ~3,000
- Current user message: ~200
- **Total: ~15,000 tokens** with ~180K headroom

Positional optimization: place static reference data (profile, plan, metrics) at the START, conversational context at the END — matches the U-curve's high-accuracy zones. Prompt caching on the stable prefix (~6.3K tokens) gives 90% cost reduction on cached reads.

See memory-and-architecture.md for the 5-layer summarization hierarchy.
