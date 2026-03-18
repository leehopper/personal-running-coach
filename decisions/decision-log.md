# Decision Log

Record of decisions made during planning. Each entry captures what was decided, why, and what alternatives were considered. Provides a trail for future reference when context is needed.

---

## DEC-001: Proactive coaching is in scope

**Date:** Pre-planning
**Status:** Final
**Category:** Interaction

**Decision:** The AI coach will both respond to user input AND proactively initiate based on detected patterns and data signals.

**Rationale:** Proactive behavior is a core differentiator. A coach that only answers questions is just a chatbot with context. The "orchestrator" behavior — managing the plan on the user's behalf — is what separates this from static plan generators and generic AI chat.

**Alternatives considered:**
- Response-only model (simpler, lower risk of annoying users, but loses the coaching feel)
- Proactive-only for specific trigger types (middle ground, but hard to draw the line)

**Open follow-ups:** Need to define the right frequency and sensitivity for proactive nudges so the system doesn't become noisy.

---

## DEC-002: Single agent for MVP, multi-agent deferred

**Date:** Pre-planning
**Status:** Final
**Category:** Architecture

**Decision:** MVP will use a single well-prompted AI agent with structured context injection, not a multi-agent orchestration system.

**Rationale:** A single agent with good context avoids orchestration complexity. Multi-agent (coaching agent, analysis agent, triage agent) is interesting for the future but adds latency, cost, and failure modes that aren't justified until the single-agent approach hits clear limits.

**Alternatives considered:**
- Multi-agent from day one (more modular, but premature complexity)
- Hybrid with lightweight classifier + single agent (possible future step)

---

## DEC-003: Three-tier planning model (macro/meso/micro) — needs validation

**Date:** Pre-planning
**Status:** Final — validated by R-004 research, with 4 modifications (see DEC-014)
**Category:** Planning architecture

**Decision:** The training plan operates across three layers — macro (season), meso (weekly cycles), micro (daily prescriptions) — with different update frequencies and context detail levels.

**Rationale:** This is both a coaching best practice (periodization) and a technical optimization (context window efficiency). The AI never needs to hold an entire year of daily detail in a single call.

**Important caveat:** This structure should live in the AI prompting and presentation layer, NOT be baked into the data model, until validated. Keep the underlying storage flexible enough to support alternative plan decompositions if the three-tier model doesn't hold up in practice.

**Alternatives considered:**
- Flat daily plan (simpler but doesn't scale and loses strategic structure)
- Two-tier (weekly + daily) — loses the seasonal/periodization layer

---

## DEC-004: Planning intelligence, not workout execution

**Date:** 2026-03-17
**Status:** Final
**Category:** Scope / Product identity

**Decision:** The app is a planning and coaching intelligence layer. It builds the plan, consumes workout results (from manual input or integrations), and optimizes the plan. It does not provide live workout tracking, GPS recording, or real-time coaching during runs.

**Rationale:** This positions the app as complementary to tools runners already use (Strava, Garmin, Apple Health) rather than competing with them. It dramatically simplifies the MVP, keeps the focus on the core differentiator (adaptive intelligence), and avoids the crowded workout-tracking market.

**Alternatives considered:**
- Full-stack training app with live tracking (massive scope increase, competes with established players)
- Hybrid with basic run tracking for users without a watch (adds complexity, dilutes focus)

**Open follow-ups:** Integration strategy for consuming workout data from external sources. Which platforms to support first and how to handle users who don't use any tracking tool.

---

## DEC-005: Web first, native later

**Date:** 2026-03-17
**Status:** Final
**Category:** Platform

**Decision:** Build as a responsive web app first. Plan for native mobile (iOS/Android) once the product is validated and interaction patterns are settled.

**Rationale:** Web gives the fastest iteration loop during the exploration and validation phase. Aligns with "design for pivots" — no app store gatekeeping, instant deploys, easier to test and change. The core value (plan generation, conversation, adaptation) doesn't require native capabilities. Push notifications and deep wearable integration can wait until the product-market fit is clearer.

**Trade-offs accepted:**
- Limited push notification reliability (web push is weaker than native)
- No direct Apple Health / HealthKit access from web (requires native bridge or integration via Strava/Garmin APIs instead)
- Proactive coaching will lean on in-app experiences early on rather than push-driven engagement

**Alternatives considered:**
- Native from day one (best UX for daily companion, but slow to iterate and premature commitment)
- React Native / cross-platform hybrid (middle ground, but still app store gated)

---

## DEC-006: Monorepo with planning docs alongside code

**Date:** 2026-03-17
**Status:** Final
**Category:** Development workflow
**Source:** R-008/R-009 research

**Decision:** The project will be a monorepo. Current planning docs (running-app-org/) become the repo root. Planning docs live in `docs/` alongside application code in `src/`. No separate docs repo.

**Rationale:** Strong practitioner consensus that monorepo is significantly better for AI-assisted development. A single context window with access to planning docs, schema, API definitions, and components enables holistic reasoning. Plan files serve as session-handoff context that survives context resets. Version control captures plan evolution alongside code.

**Alternatives considered:**
- Separate planning repo + code repo (breaks AI context, requires manual cross-referencing)
- External docs tool like Notion (invisible to Claude Code, no version control tie-in)

---

## DEC-007: CLAUDE.md + ROADMAP.md as context infrastructure

**Date:** 2026-03-17
**Status:** Final
**Category:** Development workflow
**Source:** R-008/R-009 research

**Decision:** Use a lean CLAUDE.md (<200 lines, stable project identity) plus a living ROADMAP.md (current phase, status, priorities) as the primary context handoff mechanism between sessions. Supplement with a `/catchup` slash command and per-feature plan files.

**Rationale:** This is the emerging best practice across Claude Code power users. CLAUDE.md auto-loads every session. ROADMAP.md provides the "where are we" state that changes between sessions. Plan files give deep context for specific features/POCs. Together they solve the cold-start problem without over-engineering.

---

## DEC-008: Plan-first development cycle

**Date:** 2026-03-17
**Status:** Final
**Category:** Development workflow
**Source:** R-008/R-009 research

**Decision:** Adopt the research → plan → annotate → implement cycle for all feature work. No implementation without a reviewed plan file. Sessions capped at 30-45 minutes with one clear objective.

**Rationale:** Strongest consensus finding across practitioners. Separating planning from execution dramatically reduces rework and scope creep. Plan files double as documentation and session-handoff context. Short focused sessions prevent context degradation (the #1 documented failure mode in autonomous Claude Code usage).

---

## DEC-009: Single agent + built-in subagents, no orchestration frameworks

**Date:** 2026-03-17
**Status:** Final
**Category:** Development workflow
**Source:** R-008/R-009 research

**Decision:** Use a single well-configured Claude Code instance with built-in subagents (Explore, Plan, general-purpose). Skip multi-agent orchestration frameworks (Gas Town, claude-flow, Agent Teams). Revisit only if parallel feature work becomes a regular need — then Claude Squad is the minimal option.

**Rationale:** Multi-agent setups are expensive (3-4x tokens), complex, and unnecessary for a solo side project. The built-in Plan subagent covers most needs. Orchestration frameworks add failure modes without proportional value at this scale.

**Alternatives considered:**
- Gas Town (powerful but requires multiple Claude Max accounts, $300+/month)
- Agent Teams (experimental, 3-4x token cost, overkill for solo work)
- Claude Squad (reasonable middle ground, defer until parallel work is needed)

---

## DEC-010: Deterministic computation layer + LLM coaching layer (separation of concerns)

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture
**Source:** R-001 research

**Decision:** The system has two distinct layers. A deterministic computation layer handles all numerical work: VDOT/pace calculations, ACWR load monitoring, mileage progression math, single-run spike checks, safety guardrail enforcement, environmental adjustments. The LLM layer handles coaching: explanation, adaptation reasoning, methodology rationale, daily judgment calls, goal recalibration conversations, and pattern recognition.

**Rationale:** R-001 identified five specific areas where LLMs will fail at training science (pace precision, load math, methodology-term disambiguation, edge-case overconfidence, temporal reasoning). Making safety guardrails deterministic code means the AI literally cannot prescribe a dangerous workout — this is the architectural moat against Runna-style failures. The LLM's strength (natural language, reasoning, explanation) is applied where it adds the most value.

**Build order:** Computation layer first (this is the safety foundation), methodology parameters second, LLM coaching layer third.

---

## DEC-011: Configurable methodology with blended defaults

**Date:** 2026-03-18
**Status:** Final
**Category:** Training science
**Source:** R-001 research

**Decision:** Training methodology is configurable, not hardcoded. The system uses a three-layer knowledge architecture: universal guardrails (hard-coded safety rules no methodology violates), methodology parameters (configurable per user — ~12 key parameters like long run max, tempo definition, periodization model), and real-time coaching judgment (LLM-driven).

Default blending logic by experience level:
- Beginners (<6mo, <20mpw): Higdon simplicity + 80/20 intensity + MAF easy pacing
- Intermediate (6+mo, 20-50mpw, time goals): Daniels pacing + Pfitzinger periodization
- Advanced (50+mpw, competitive): Hudson adaptive approach with Daniels zone precision, or explicit methodology selection

**Rationale:** Knopp et al. (2024) analysis of 92 marathon plans found all converge on ~79% easy-zone training — the universal guardrail layer is larger than expected. Divergences (long run distance, tempo definition, periodization model) are real but parameterizable. Human coaches blend methodologies routinely; the AI should too, with transparent explanation of which elements come from where.

---

## DEC-012: Five-level escalation ladder with PID dampening and hysteresis

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Adaptation
**Source:** R-004 research

**Decision:** Adaptation responses follow a 5-level escalation ladder: Level 0 (absorb — log only, no LLM), Level 1 (micro-adjust — deterministic swap of 1-2 workouts), Level 2 (week restructure — first level requiring LLM coaching judgment), Level 3 (phase reconsideration — LLM re-evaluates periodization), Level 4 (plan overhaul — requires user confirmation). State transitions use hysteresis thresholds (different values for entering vs. exiting concern states). Signal processing uses EWMA trend detection — only EWMA threshold crossings trigger changes, not single data points. PID-inspired dampening (proportional, integral via EWMA, derivative for rate-of-change) prevents oscillation.

**Rationale:** Cross-domain validation from control theory (PID controllers used in 95% of industrial systems), game AI (Left 4 Dead Director's intensity pacing with hysteresis), and financial rebalancing (drift-band thresholds to avoid excessive trading). The central failure mode without this is cascading over-correction. TrainerRoad's Adaptive Training (trained on millions of workouts) validates the approach of adapting difficulty within stable structure.

**Key rules:**
- Never redistribute missed mileage — move forward
- Easy day over-performance warrants coaching conversation, not plan upgrade
- Key workout over-performance → conservative 2-3% target increases only
- Level 4 changes always require user confirmation

---

## DEC-013: Event-sourced plan state with Marten on PostgreSQL

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Data
**Source:** R-004 research

**Decision:** Plan state uses a hybrid document + event log pattern via Marten (v8.x, MIT licensed). Current plan state lives as a JSON document (for LLM consumption via structured outputs). Every modification is recorded as an immutable event. Both the deterministic computation layer and the LLM coaching layer append to the same Marten event stream. Wolverine handles reliable message processing with outbox patterns.

**Rationale:** A pure document model has no change history and creates conflicts between computation and LLM layers. Pure event sourcing makes it harder to pass full state to the LLM. The hybrid approach gives both. Marten is native to the .NET + PostgreSQL stack, provides event streams per entity, inline projections, optimistic concurrency, and snapshots — approximately 10 lines of configuration. The event stream IS the audit trail, enabling native undo (replay excluding unwanted events).

**Alternatives considered:**
- Pure document store (no audit trail, no conflict resolution)
- Pure event sourcing (projection complexity makes LLM context assembly harder)
- Separate audit logging (redundant with event sourcing, extra maintenance)

---

## DEC-014: Constraints-and-templates plan storage with on-demand micro generation

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Planning
**Source:** R-004 research

**Decision:** Macro tier stores phase schedule with constraints (~200 tokens). Meso tier stores weekly templates with slot types (~150 tokens). Micro tier (daily prescriptions) is generated on demand from macro constraints + meso template + monitoring state, then stored after generation. No pre-stored 16-week daily schedule.

**Rationale:** R-004's central insight: the primary failure mode of the three-tier model is rigidity of stored data. Storing constraints and regenerating on disruption means every edge case is a regeneration trigger, not a data corruption problem. Also token-efficient (~3,150 tokens for full coaching context). For goalless runners, the macro tier degrades to a simple "current emphasis" tag — same architecture, no codepath fork. Validated by Copilot Workspace's Spec → Plan → Implementation pipeline and HTN (Hierarchical Task Network) planning from AI research.

---

## DEC-015: Context injection with positional optimization and prompt caching

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / LLM Integration
**Source:** R-004 research

**Decision:** Context payload totals ~15K tokens (7.5% of 200K window). A deterministic ContextAssembler selects and positions context based on interaction type. Stable prefix (~6.3K tokens: system prompt + user profile + plan state) placed at the START for prompt caching (90% cost reduction). Conversational context placed at the END. Interaction-specific assembly: workout feedback gets last 1-3 workouts; race readiness gets full plan + 16-week summaries. Training history uses a 5-layer pre-computed summarization hierarchy (raw → daily → weekly → phase → trend narrative).

**Rationale:** Research shows LLMs have 30%+ accuracy drop for information buried in the middle of context. Placing critical reference data at start/end matches the U-curve. Pre-computed summaries (validated by Google's PH-LLM and Mem0's production system) outperform raw data injection and achieve 80-90% token reduction. Prompt caching on the stable prefix saves ~$2.70 per million tokens.

---

*Add new decisions at the bottom. Use format: DEC-XXX, date, category, decision, rationale, alternatives.*
