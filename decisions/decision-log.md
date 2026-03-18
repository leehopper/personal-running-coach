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
**Status:** Tentative — pending research (R-004) and POC 3
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

*Add new decisions at the bottom. Use format: DEC-XXX, date, category, decision, rationale, alternatives.*
