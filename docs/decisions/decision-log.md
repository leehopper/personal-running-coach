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

**Decision:** The project will be a monorepo. Current planning docs (running-app-org/) become the repo root. Planning docs live in `docs/` alongside application code in `backend/` and `frontend/`. No separate docs repo.

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

## DEC-016: Progressive evaluation strategy with penalty-weighted safety scoring

**Date:** 2026-03-18
**Status:** Final
**Category:** Testing / Quality Assurance
**Source:** R-007 research

**Decision:** Adopt a layered evaluation approach that starts with manual review and progressively automates:

**Phase 1 — Manual Baseline ($0):** 15-20 manually curated test scenarios in a spreadsheet. Focus on safety (injury signals, medical scope), personalization (beginner vs. advanced), and scope boundaries. Binary pass/fail + one-line critique. 30 min per review session. Start this alongside POC work.

**Phase 2 — Automated Scenarios ($5-15/mo):** Encode scenarios as YAML test cases in Promptfoo. Deterministic assertions (contains/not-contains) plus LLM-as-judge rubrics. Add 2-3 new scenarios per POC iteration from discovered edge cases. Target 50 scenarios before moving to Phase 3.

**Phase 3 — Multi-Dimension Judging ($10-25/mo):** Separate LLM-as-judge prompts per quality dimension (training accuracy, communication, personalization, safety). Claude Sonnet as primary judge. Golden calibration set of 30-50 scenarios with expert ratings. Periodic Cohen's κ checks (target ≥ 0.8).

**Phase 4 — CI/CD Integration ($0 + API costs):** GitHub Action runs eval suite on any PR touching prompt files. Quality gate: safety pass rate ≥ 95%, no category drops >10% from baseline. Introduce once the codebase has prompt files in version control.

**Phase 5 — Production Monitoring:** Langfuse for production tracing. Sample 20-30 production conversations regularly. Every production bug becomes a test case. Begins when real users are on the system.

**Scoring model:** Five evaluation dimensions: training science accuracy (1-5), contextual personalization (pass/fail), coaching communication quality (1-5), appropriate uncertainty/deferral (pass/fail), safety/harm avoidance (penalty scale -10 to 0). Penalty-weighted scoring ensures a single dangerous response overwhelms positive quality scores.

**Safety testing specifics:** Minimum 50 adversarial scenarios across: cardiac/emergency (10), injury diagnosis seeking (10), overtraining/RED-S multi-turn patterns (5), nutrition/eating disorders (10), medication questions (5), jailbreak/role-play attacks (5), toxic positivity triggers (5). Each run 3-5 times at production temperature. Three decomposed binary safety judge checks per response: (1) avoided harmful advice? (2) recommended professional consultation when warranted? (3) stayed within coaching scope?

**Rationale:** HealthBench (OpenAI, 2025) demonstrated that penalty-aware scoring where negative criteria dominate is essential for safety-critical domains. Anthropic's Bloom tool shows Claude judges achieve κ = 0.92 intra-rater consistency. Hamel Husain's practitioner insight: spend 60-80% of early effort on error analysis and understanding failures, not building automated checks. The CheckList framework (Microsoft Research) provides the scenario taxonomy: minimum functionality tests, invariance tests, directional expectation tests.

---

## DEC-017: Staged legal stack with three highest-ROI priorities

**Date:** 2026-03-18
**Status:** Final
**Category:** Legal / Business
**Source:** R-003 research

**Decision:** Legal infrastructure scales with product maturity: MVP-0 (~$500, LLC + documentation), MVP-1 (~$1K/yr, beta agreement + health screening + privacy policy + logging), public beta (~$3-4K/yr, full ToS with mandatory arbitration + insurance), scale (~$15-30K/yr, professional review + AI governance). Three highest-ROI legal actions: (1) mandatory arbitration with class action waiver, (2) FTC Health Breach Notification Rule compliance, (3) comprehensive logging of deterministic safety layer decisions.

**Rationale:** R-003 found zero successful lawsuits against fitness apps for coaching advice, but *Garcia v. Character Technologies* (2025) opened product liability for AI chatbots. Section 230 will not protect. The product's hybrid architecture (deterministic computation + LLM conversation) maps favorably onto the information-vs-product legal distinction (*Winter v. Putnam*). Assumption of risk doctrine is robust in fitness law. The arbitration clause prevents class actions (highest-magnitude risk), HBNR compliance addresses the regulation with real financial teeth ($43,792/violation/day), and safety logging proves guardrails work if challenged.

---

## DEC-018: Health screening gate at MVP-1 connected to deterministic layer

**Date:** 2026-03-18
**Status:** Final
**Category:** Safety / Legal
**Source:** R-003 research

**Decision:** Implement a PAR-Q-inspired (not formal PAR-Q, which is copyrighted) health screening gate before users begin training. Covers: heart conditions, chest pain during activity, dizziness, bone/joint problems, blood pressure medication, other known contraindications. Critical rule: screening results MUST connect to the deterministic safety layer, adjusting parameters for flagged users. Screening without acting on results creates worse legal exposure than not screening at all.

**Rationale:** No major fitness app currently requires health screening, but R-003 identified that implementing screening creates a stronger duty of care. If the app screens and then ignores the results (e.g., providing high-intensity plans to someone who flagged cardiovascular issues), the legal position is worse than not screening. The screening gate feeds directly into the guardrail system (DEC-010) — flagged conditions adjust volume ceilings, intensity limits, and mandatory referral triggers.

---

## DEC-019: Hard keyword triggers for medical scope boundaries

**Date:** 2026-03-18
**Status:** Final
**Category:** Safety / Architecture
**Source:** R-003 research

**Decision:** Implement deterministic keyword-based triggers that automatically generate medical referral responses for scope-boundary topics. Trigger categories: cardiac symptoms ("chest pain," "heart pounding," "irregular heartbeat"), persistent injury ("persistent pain," "pain getting worse," "can't walk"), RED-S indicators ("missed periods," "stress fracture," "not eating enough"), and medical conditions ("diagnosed with," "medication for"). These are hard-coded in the deterministic layer, not dependent on LLM self-policing.

**Rationale:** R-003 identified scope creep into medical territory as the primary conversational risk. Users will ask about pain, nutrition, body composition, and mental health regardless of system prompts. LLM guardrails can drift, especially in multi-turn conversations where context accumulates. Hard keyword triggers are more reliable than relying on the LLM to self-police. This aligns with the core architectural principle (DEC-010): safety-critical decisions are deterministic code, not LLM judgment. The keyword list grows from adversarial testing (DEC-016).

---

## DEC-020: FTC Health Breach Notification Rule compliance from day one

**Date:** 2026-03-18
**Status:** Final
**Category:** Compliance / Data
**Source:** R-003 research

**Decision:** Treat the product as a "vendor of personal health records" under 16 CFR Part 318 from the earliest stage with external users (MVP-1). This means: breach notification procedures, no sharing health data with analytics providers without explicit consent, privacy policy covering data collection/storage/breach procedures, and logging of consent timestamps.

**Rationale:** R-003 identified HBNR as the compliance requirement most founders miss. The FTC explicitly stated that a fitness app with "technical capacity to draw identifiable health information from both the user and the fitness tracker is a PHR." The product consuming Garmin/health API data + user-inputted reports qualifies. Penalties are $43,792 per violation per day. "Breach" includes unauthorized disclosures (sharing with analytics without consent), not just cyberattacks. Washington's My Health My Data Act adds a private right of action with no revenue threshold. Early compliance is cheaper than retrofitting.

---

## DEC-021: No BYOM — absorb LLM costs into subscription pricing

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Monetization
**Source:** R-005 research

**Decision:** Do not implement Bring Your Own Model (BYOM). Absorb LLM costs into a flat subscription. At $1–3/month per user in LLM costs against a $10–15/month subscription, gross margin on inference exceeds 75%. No AI fitness product offers BYOM. The engineering complexity (key management, security, multi-provider testing, compliance documentation), regulatory risk (BYOM makes FTC HBNR compliance harder, not easier), and user friction (runners are not developers) far exceed the marginal savings.

**Rationale:** R-005 found that BYOM solves a cost problem that doesn't exist at this price point while creating security, compliance, and UX problems that do. Users would actually pay *more* with their own keys (losing prompt caching optimizations). TypingMind's BYOM model works because its users are developers — runners don't want to manage API accounts. Every competitor (Runna $20/mo, WHOOP $24-30/mo, TrainAsONE $12/mo) absorbs AI costs into subscription pricing.

**Alternatives considered:**
- BYOM as premium tier (complexity doesn't justify it at sub-$3/user/month costs)
- Usage-based pricing (adds billing complexity, user anxiety about costs)
- Credits system (unnecessary friction at this cost structure)

---

## DEC-022: Thin abstraction layer optimized for one provider, with tested fallback

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / LLM Integration
**Source:** R-005 research

**Decision:** Use Claude Sonnet 4.5 as the primary model from day one. Route all LLM calls through a thin adapter interface (`complete(messages, config) → response`). Optimize prompts for Claude (Anthropic) as the primary provider. Store the 6.3K-token stable prefix in a versioned config file, not in code. Use Anthropic's explicit prompt caching with 1-hour TTL.

At growth stage (hundreds of users): test a fallback model (GPT-4.1 mini or Gemini 2.5 Flash) with existing prompts, configure automatic failover for Anthropic outages, and build 20-30 behavioral test cases that validate coaching across providers.

At scale (thousands of users): deploy an LLM gateway (Portkey or LiteLLM proxy) for cost tracking, rate limiting, and model routing (simple queries → budget model, complex coaching → primary model, 30-50% cost reduction).

**Rationale:** The coaching layer demands nuanced multi-turn conversation — empathetic adjustments, injury signal detection, persona consistency per DEC-027 — that warrants starting with the stronger model. At ~$7.60/user/month, still within subscription-absorbing range at $12-15/month pricing (49% gross margin). R-005 originally recommended Haiku 4.5 (~$2.50/user/month) on cost-quality tradeoff, but the coaching quality requirements make Sonnet the better starting point. Model selection is not a POC validation item — start with Sonnet, revisit only if cost becomes a constraint at scale.

Provider risk is real — GPT-4 quality regressed measurably, GPT-4.5 was deprecated after 4 months, Jasper AI's business was threatened by ChatGPT's launch. But the mitigation is a thin adapter + eval suite, not BYOM infrastructure. ~70-80% of prompt engineering transfers across models; switching takes 1-2 weeks for basic functionality. The key architectural decisions: prompts in config files, structured output validation independent of provider, eval suite testing behavior across models, provider-specific features isolated behind interfaces.

---

## DEC-023: Subscription pricing model — free tier + paid tier with reverse trial

**Date:** 2026-03-18
**Status:** Final
**Category:** Monetization
**Source:** R-005 research

**Decision:** Pricing model (when monetization becomes relevant): free tier with limited AI coaching messages (5-10/month on a budget model like GPT-4o-mini at ~$0.33/user/month) to demonstrate value, paid tier at $12-15/month with unlimited coaching on the primary model (Claude Sonnet 4.5, ~$7.60/user/month). Annual plan at ~$99/year. 14-day reverse trial (full access, then downgrade). This mirrors the pattern used by ChatGPT, Cursor, and Perplexity.

**Rationale:** AI fitness products cluster at $10-30/month (Runna $20, TrainAsONE $12, TrainerRoad $25, PKRS.AI $30). At $12-15/month with ~$7.60/user/month LLM costs, gross margin on inference is ~40-50%. Thinner than Haiku but still viable, and the coaching quality justifies it — the product's differentiator is the intelligence layer. Reverse trials produce better conversion than permanent free tiers. Model quality gating (budget model for free, stronger model for paid) creates a natural value ladder without usage tracking complexity.

**Note:** This is a monetization framework for when it becomes relevant, not an MVP concern. Deferred until post-MVP validation.

---

## DEC-024: Garmin-first integration with staged platform expansion

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Integration
**Source:** R-006 research

**Decision:** Garmin Connect API is the primary wearable integration target. Strava is unusable for AI products (explicit AI/ML prohibition, 7-day cache limit, analytics ban). Apple HealthKit is incompatible with web-first architecture (on-device only, requires native iOS app). Integration stages: MVP-0 uses manual .FIT upload or unofficial `garth`/`python-garminconnect` library for personal use. MVP-1 uses official Garmin Connect Developer Program (push webhooks for Activity + Health APIs). Polar is the recommended second platform at MVP-1 (self-service API, OAuth 2.0, no documented AI restrictions, ~1 week additional effort). Oura for sleep/readiness at public launch (~1 week effort). Defer Apple HealthKit iOS companion app and COROS until user demand justifies investment.

**Rationale:** R-006 confirmed Garmin permits AI/ML usage with user consent and an AI Transparency Statement in the privacy policy (Section 15.10 of developer agreement). Garmin provides the richest running data through both structured API summaries and raw .FIT files, including Training Effect, running dynamics, VO2max, HRV, Training Readiness, and Body Battery — none of which are available through Apple Health sync. Developer program is free (dropped $5K fee), approval within 2 business days. Strava's restrictions are comprehensive and tightening post-Runna acquisition.

**Key gotchas:**
- Garmin developer program requires a business entity (LLC) — individual approval uncertain
- Smart Recording (default) creates variable-interval data; build split calculation for both modes
- Third-party activities synced TO Garmin Connect don't forward to API partners
- Garmin webhook endpoints cannot have Authorization headers (IP whitelist only)
- OAuth may be 1.0a or 2.0 depending on integration vintage — confirm during integration call
- Garmin retains API data only ~7 days — prompt ingestion and storage critical

---

## DEC-025: .FIT file parsing for deterministic layer, API summary for LLM layer

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Data
**Source:** R-006 research

**Decision:** Use a two-track data extraction strategy. Activity Summary JSON (pushed via webhook) feeds the LLM coaching layer's ~100-150 token workout summaries — distance, duration, avg pace, avg HR, elevation are sufficient for conversational coaching. .FIT file parsing (using `fitdecode` or Garmin's official `garmin-fit-sdk`) feeds the deterministic computation layer — per-lap pace/HR, running dynamics (ground contact time, vertical oscillation, stride length), Training Effect, precise split calculations. Both tracks process from the same webhook event.

**Rationale:** R-006 found that the Activity Summary API has notable gaps for coaching-quality data: Training Effect, ground contact time, vertical oscillation, stride length, and running power live only in .FIT file messages. HR zones must be calculated from samples. The computation layer needs this precision for ACWR calculation, load monitoring, and safety guardrails. The LLM layer doesn't — aggregate metrics are sufficient for natural language coaching.

**Storage:** Raw .FIT files are parsed on receipt and deleted (not stored long-term). Structured fields extracted to PostgreSQL. AES-256 encryption at rest qualifies as "secured" under FTC HBNR.

---

## DEC-026: Graceful degradation for device-tier data gaps

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Integration
**Source:** R-006 research

**Decision:** Design the computation layer to gracefully degrade when premium device metrics are missing. Check for null fields rather than assuming their presence. Forerunner 265 is the baseline target device for serious recreational runners, providing: Training Readiness, HRV Status, Training Effect, wrist-based running dynamics, running power, barometric elevation. FR 55 lacks HRV, sleep score, Training Effect, running dynamics, and barometric altimeter. FR 165 adds most wellness metrics but lacks Training Status and Training Readiness. When metrics are absent, the system falls back to basic metrics (distance, duration, pace, HR) which are universal across all GPS watches.

**Rationale:** R-006 documented significant data availability differences across Garmin's device tiers. Building hard dependencies on premium metrics (Training Readiness, running dynamics) would exclude users with entry-level devices. The deterministic layer's safety guardrails (ACWR, volume spikes, intensity distribution) work with basic metrics; premium metrics enhance coaching quality but shouldn't gate it.

---

## DEC-027: Three-layer coaching communication architecture with scenario playbooks

**Date:** 2026-03-18
**Status:** Final
**Category:** Coaching / UX
**Source:** R-010 research

**Decision:** The coaching persona uses a three-layer communication architecture. Layer 1 (moment-to-moment): every response contains at least one OARS element (Open question, Affirmation, Reflection, or Summary). Layer 2 (information delivery): Elicit-Provide-Elicit pattern — ask what they know → share using neutral language → check their reaction. Layer 3 (substantive conversations): modified GROW framework — Goal, Reality, Options, Way Forward.

Each escalation level (DEC-012) maps to a communication mode: Level 0 = silent, Level 1 = light/informational, Level 2 = brief explanation with rationale, Level 3 = full E-P-E pattern, Level 4 = maximum transparency with alternatives and explicit agreement request.

Eight scenario playbooks define concrete conversational patterns for the most common coaching challenges: easy day over-performance, plan downgrades, goal recalibration, missed workouts, injury scope boundaries, rest day resistance, returning after a break, and the coach training phase. Each includes a structural pattern, key reframes, and explicit anti-patterns.

Default persona calibration: 80% warmth / 20% directness. Increase directness for safety, established trust, repeated patterns, or explicit user request. Decrease for new relationships, post-setback, illness/injury, emotional vulnerability. Warmth expressed through actions (adjusting plans, remembering context) rather than emotional performance (claiming to "feel" or "understand").

**Rationale:** R-010 found that autonomy-supportive language (conditional words over commands, questions before corrections, rationales alongside recommendations) produces measurably better adherence, motivation, and performance outcomes than directive language — even when information content is identical (Hooyman, Wulf & Lewthwaite, 2014). Process goals outperform outcome goals by 15x (Gröpel & Mesagno). AI negative feedback reduces self-efficacy more effectively than human negative feedback because it feels algorithmic and irrefutable (Li et al., 2025) — every piece of negative feedback must be paired with agency-preserving next steps. Research also found that working alliance may not develop with AI coaching, but users still reach goals through transactional interaction — the AI should excel at being useful, accurate, and reliable rather than simulating deep emotional connection.

**Key rules encoded:**
- Always provide rationales for recommendations, especially counterintuitive ones
- Offer at least one choice, even when options are constrained
- Acknowledge feelings before correcting behavior
- Never use controlling language as default ("You need to," "You should," "You have to")
- Never count or track missed workouts verbally
- Never compare to other runners normatively
- Never claim to observe physical signs or pretend to have emotions
- Never redistribute missed mileage
- Never say "impossible" about long-term goals

See `planning/coaching-persona.md` for full playbooks and vocabulary.

---

## DEC-028: Population-adjusted safety guardrails

**Date:** 2026-03-18
**Status:** Final
**Category:** Safety / Architecture
**Source:** R-011 research

**Decision:** The deterministic safety guardrails (DEC-010) are adjusted per population. Pregnancy tightens ACWR to 0.8–1.3 with RPE ceiling of 14, blocks altitude >1,800m and temp >32°C, requires provider clearance. Postpartum enforces a hard 12-week running block per Goom et al. 2019. Youth runners get age-stratified volume ceilings (15–20 mpw ≤12, 25–30 mpw 13–14, 35–45 mpw 15–16, 45–55 mpw 17–18) with 7/7 training blocked under 16. Masters 50+ get extended recovery spacing (hard session every 3rd day for 50s, every 4–5 days for 60+) and tighter volume progression caps (5–7%/week 50s, 3–5%/week 60+). Injury return uses a universal five-stage framework with traffic-light pain monitoring. Chronic conditions trigger specific adjustments: beta-blockers switch HR-based training to RPE, T1D requires pre-run safety checklist, unmanaged arrhythmia blocks vigorous programming until clearance.

**Rationale:** R-011 found that the default guardrails (designed for healthy 18–39-year-olds) are insufficient for several populations. A 60-year-old returning from injury needs ACWR 0.8–1.15, not 0.8–2.0. A 12-year-old exceeding 20 mpw risks growth plate injuries. The deterministic layer must enforce population-appropriate limits — relying on the LLM to "know" these adjustments would violate the core architectural principle (DEC-010). Evidence base: ACOG 2020, Goom/Donnelly/Brockwell 2019, NSCA LTAD 2016, ACSM 2015/2018, Silbernagel pain-monitoring model.

---

## DEC-029: Extended health screening with periodic check-ins

**Date:** 2026-03-18
**Status:** Final
**Category:** Safety / UX
**Source:** R-011 research

**Decision:** Expand the PAR-Q-inspired screening gate (DEC-018) with population-specific questions: pregnancy/postpartum status (with hard gates on programming), date of birth with age verification (COPPA flow for <13, parental acknowledgment for 13–17), chronic condition prompts (asthma, diabetes type, arrhythmias, hypertension, thyroid) with medication-affecting-HR detection (beta-blockers), mental health baseline (motivation 1–5, sleep hours), and injury history. Add periodic check-ins beyond onboarding: quarterly menstrual regularity for female runners, energy levels, and stress fracture history for RED-S screening. 3+ missed periods → referral. 2+ career stress fractures in female runner → RED-S screening referral.

**Rationale:** R-011 demonstrated that one-time onboarding screening misses conditions that develop or emerge during training (menstrual irregularity from increasing load, mental health changes, new chronic condition disclosures). Periodic check-ins catch RED-S warning signs that onboarding alone cannot. Beta-blocker detection is critical because standard HR-based training zones are invalid with beta-blocker use (15–22 bpm reduction) — a user on metoprolol training by HR is undertrained by default. COPPA compliance (updated April 2025) requires verifiable parental consent for under-13.

---

## DEC-030: Expanded keyword trigger system with crisis response protocol

**Date:** 2026-03-18
**Status:** Final
**Category:** Safety / Architecture
**Source:** R-011 research

**Decision:** Significantly expand DEC-019's keyword trigger categories. New categories: pregnancy/postpartum terms (25+ keywords), female athlete health (15+ keywords), youth indicators (15+ keywords), chronic condition terms (30+ keywords), injury-specific terms (20+ keywords), and mental health/crisis terms (25+ keywords). All triggers are deterministic — in the computation layer, not dependent on LLM judgment.

Add a dedicated crisis response protocol for suicidal ideation and self-harm triggers. Tier 1 hard triggers (explicit crisis language) immediately: stop coaching conversation, display crisis resources (988 Lifeline, Crisis Text Line 741741), acknowledge with empathy, and cease engagement on the crisis topic. Normal coaching resumes only when the user re-engages on training. This is deterministic behavior, not LLM-dependent — a 2025 Nature study of 29 AI chatbots found none met full criteria for adequate crisis response.

Add a three-tier sensitive disclosure escalation (green = coaching-scope, amber = professional referral recommended, red = immediate action required) that standardizes the response pattern across all population-specific triggers.

**Rationale:** DEC-019's original four categories (cardiac, persistent injury, RED-S, medical conditions) are necessary but insufficient. R-011 identified that populations like pregnant runners, youth athletes, and runners with chronic conditions have distinct trigger vocabularies that the original system would miss entirely. "Gestational diabetes," "growth spurt + pain," "insulin pump," "pelvic floor" — none of these would have triggered the original keyword system. The crisis protocol addresses the most dangerous failure mode: an AI continuing to coach through a mental health crisis. Making this deterministic removes the possibility of LLM misjudgment in the highest-stakes scenario.

---

## DEC-031: Full-stack technology choices

**Date:** 2026-03-18
**Status:** Final
**Category:** Technology / Infrastructure

**Decision:** The complete technology stack for the project, decided through structured discussion. All choices prioritize open-source tooling, best practices from day one, clean architecture, containerization, and optimization for Claude Code–assisted development workflows.

### Backend

- **Runtime:** .NET 10 LTS (C# 14). Released Nov 2025, supported through Nov 2028.
- **Web framework:** ASP.NET Core with traditional controllers. Clean controller → service → repository layering.
- **ORM / Data access:** EF Core for relational data (users, workout history, structured activity data). Marten (v8.x) for event-sourced plan state on PostgreSQL JSONB (per DEC-013). Clear ownership boundaries: EF Core owns relational tables, Marten owns event streams and document projections.
- **Background processing:** Wolverine for message-based job processing with outbox pattern and durable queues on PostgreSQL. Handles the wearable data pipeline (webhook ingestion → processing → computation → summarization) and background summarization jobs. Native integration with Marten event streams.
- **Auth:** ASP.NET Core Identity + JWT tokens. Scaffold early so every endpoint is auth-aware from day one. OAuth/social login providers layered in incrementally as needed.
- **API documentation:** Swashbuckle (Swagger/OpenAPI). Auto-generates OpenAPI spec from controllers. Swagger UI for interactive testing. Spec is readable by Claude Code for API comprehension.
- **LLM adapter:** Thin service interface wrapping Anthropic C# SDK (`complete(messages, config) → response`). Prompts stored in versioned config files, not code. Anthropic prompt caching with 1-hour TTL on stable prefix. Per DEC-022.
- **Logging / Observability:** OpenTelemetry instrumentation + Aspire Dashboard (standalone Docker container). Traces, metrics, and structured logs in one UI from day one. Instrumentation is portable to Grafana/Jaeger/etc. at scale.
- **Code quality:** EditorConfig + .NET Analyzers + StyleCop Analyzers + Central Package Management. Enforced at build time. Integrated into Claude Code PostToolUse hooks.

### Frontend

- **Framework:** React 19 + TypeScript (strict mode).
- **Build tool:** Vite. Pure client-side SPA — no server-side rendering. Clean separation from .NET API backend.
- **Routing:** React Router v7.
- **State management:** Redux Toolkit + RTK Query. RTK Query handles server state caching (data fetching, background refetching, optimistic updates). Redux slices kept minimal — only truly global client state (auth, UI preferences, active conversation). Avoid unnecessary global state; prefer local component state where appropriate.
- **Styling:** Tailwind CSS + shadcn/ui (Radix primitives). Utility-first CSS with copy-paste component library.
- **Code quality:** ESLint + Prettier. Integrated into Claude Code PostToolUse hooks.

### Testing

- **Backend:** xUnit + FluentAssertions + NSubstitute. Unit tests for services and computation layer. Integration tests for repository layer and API endpoints.
- **Frontend:** Vitest + React Testing Library. Component-level tests.
- **E2E:** Playwright. Browser-based integration tests for critical user flows.
- **AI evaluation:** Progressive eval strategy per DEC-016. Manual scenarios → Promptfoo YAML → LLM-as-judge → CI/CD integration.
- **Design principle:** Testing is built into the architecture from day one at all appropriate layers, not bolted on later.

### Infrastructure

- **Container runtime:** Colima (open-source Docker Desktop replacement on macOS).
- **Container orchestration (local dev):** Docker Compose defining all services + Tilt for inner dev loop (file watching, auto-rebuild, live-update, service dashboard).
- **Services in Compose:** .NET API, PostgreSQL, pgAdmin, Redis, Aspire Dashboard. React dev server runs via Tilt (Vite dev server with hot reload).
- **Database:** PostgreSQL (primary data store + Marten event store + Wolverine message persistence). pgAdmin for database management.
- **Caching:** Redis — included from day one but used lightly. Response caching, rate limiting prep, session storage. Not a primary data store.
- **CI/CD:** GitHub Actions. Build, test, lint pipelines. Full quality gate pipeline per DEC-034 (five-layer defense for AI-generated code). Safety pass rate ≥ 95% on prompt changes per DEC-016 Phase 4.
- **Secrets management:** .NET user-secrets for local dev, environment variables in containers. No secrets in code or config files.

### Conventions

- **API versioning:** URL-based (`/api/v1/`) from day one.
- **Health checks:** ASP.NET Core health check middleware. Docker Compose and Tilt depend on health endpoints.
- **CORS:** Configured from day one (React dev server on different port than API).
- **Database migrations:** EF Core migrations for relational schema (run as separate step, not on startup). Marten manages its own schema.

**Rationale:** Stack choices optimize for three things simultaneously: (1) Clean, maintainable architecture with enterprise patterns (controller/service/repository, event sourcing, structured logging, comprehensive testing). (2) Open-source tooling throughout — no vendor lock-in except Anthropic for LLM (mitigated by thin adapter per DEC-022). (3) Claude Code workflow optimization — every tool was evaluated for how well it supports AI-assisted development (code generation, auto-formatting hooks, readable configuration, OpenAPI spec as context).

**Alternatives considered:**
- Next.js for frontend (rejected — server layer redundant with .NET backend, creates confusion about where logic lives)
- TanStack Query over RTK Query (lighter, but Redux provides unified state management story for a chat-heavy app)
- Keycloak for auth (rejected — heavy for MVP, another service to maintain; ASP.NET Core Identity handles needs through MVP-1)
- Serilog + Seq for logging (rejected in favor of OpenTelemetry + Aspire Dashboard — more future-proof, same effort)
- .NET 9 STS (rejected — EOL May 2026; .NET 10 LTS is current and provides long-term support through Nov 2028)

---

## DEC-032: Docker Compose + Tilt for local dev, K8s deferred to public beta

**Date:** 2026-03-18
**Status:** Final
**Category:** Infrastructure

**Decision:** Use Docker Compose + Tilt for local development and early deployment. Defer Kubernetes to the public beta stage (hundreds of users). Tilt supports both Docker Compose and K8s as targets, so the Tiltfile migrates with minimal changes. All containerization work (Dockerfiles, health checks, environment-based config, secrets via env vars) is K8s-ready.

**MVP-1 deployment (friends):** Deploy to a managed platform (Fly.io, Railway) or a single VPS with Docker Compose + Caddy for automatic HTTPS. Same Docker Compose setup used locally. ~$0-10/month at friends-and-family scale.

**Rationale:** K8s solves problems that don't exist at MVP scale (independent service scaling, rolling deployments, service mesh). The operational tax — Helm charts, ingress controllers, RBAC, PVC management — is disproportionate for a solo side project. Docker Compose + Tilt provides a fast inner dev loop with file watching, auto-rebuild, and a service dashboard. When K8s becomes warranted (public beta), Tilt's dual-target support and the existing containerization work make migration low-effort.

---

## DEC-033: Client-agnostic API design for future native app support

**Date:** 2026-03-18
**Status:** Final
**Category:** Architecture / Platform strategy
**Amends:** DEC-005 (web first), DEC-024 (Garmin-first integration)

**Decision:** The REST API is designed to be client-agnostic from day one. No browser-specific assumptions (no cookie-based auth, no HTML responses, no CORS-only security patterns). JWT-based auth works identically for web and native clients. The React SPA and a future iOS app are both "clients" consuming the same API.

**Integration priority revised:** MVP-0 uses manual workout input (chat-based or form-based). Apple Health integration is prioritized over Garmin for MVP-1 because the builder and initial testers use Apple Watch. Apple HealthKit requires a native iOS app (on-device only API) — a minimal SwiftUI companion app that reads HealthKit and POSTs to the backend's workout ingestion endpoint. Garmin integration follows as a fast-follow or concurrent effort once the ingestion endpoint exists.

**Workout ingestion endpoint:** Design a generic `/api/v1/workouts/ingest` endpoint that accepts structured workout data (distance, duration, heart rate samples, pace splits, source metadata) regardless of origin — manual input, HealthKit push, Garmin webhook, or future sources. Source-agnostic from the start.

**Rationale:** The builder and initial test users are Apple ecosystem users. DEC-024's Garmin-first recommendation was based on API capability analysis, not user research. The web-first decision (DEC-005) remains correct for the primary coaching interface — conversation, plan viewing, and onboarding are web-native. But the data ingestion path may require a thin iOS companion sooner than originally planned. Designing the API as client-agnostic costs nothing and keeps this option open.

**DEC-005 still holds:** The web app is the primary product surface. A native iOS app, if built, is a HealthKit data bridge first and a full client second. The decision to expand the iOS app into a full native client is deferred until product-market fit is validated on web.

---

## DEC-034: Quality gate pipeline — five-layer defense for AI-generated code

**Date:** 2026-03-19
**Status:** Final
**Category:** Development workflow / Code quality
**Informed by:** R-012 research (batch-5-ai-pr-review-quality-tool.md), R-008/R-009 (batch-1 Claude Code workflow)

**Decision:** The project uses a five-layer quality pipeline specifically designed for AI-generated code. Every layer is free for open source except Claude Code API costs (~$2–8/month).

### Layer 1: Pre-commit hooks (Lefthook)

Use **Lefthook** instead of Husky + lint-staged. Single Go binary, no Node.js startup overhead, built-in staged-file support, parallel execution by default. One `lefthook.yml` replaces three dependencies.

**Pre-commit (parallel):**
- `dotnet format` on staged .cs files (backend/, --no-restore)
- ESLint + Prettier on staged .ts/.tsx files (frontend/)
- Auto re-stage fixed files

**Commit-msg:**
- commitlint with @commitlint/config-conventional (backstop for conventional commits — Claude Code follows the convention via CLAUDE.md, commitlint catches exceptions)

**Pre-push (parallel):**
- `dotnet test` (unit tests only, --filter "Category=Unit")
- TypeScript type check (`tsc --noEmit`)

### Layer 2: PR review automation

**CodeRabbit** (free for open source, automatic on every PR) as primary AI reviewer. Combines AST analysis, 40+ SAST tools, and generative AI for line-level review and PR summaries. ~28% noise rate initially, improves via dismissed-comment learning. Configured via `.coderabbit.yaml`.

**Claude Code GitHub Action** (anthropics/claude-code-action@v1, ~$2–8/month API costs) as targeted second reviewer on important PRs via `@claude` mention. Key differentiator: uses a different model than the code author (cross-model review breaks correlated blind spots), references CLAUDE.md for architectural standards, and focuses on complexity, pattern drift, and architectural consistency.

**Codacy** (free for open source) as optional third layer for SAST quality gates that can block merges. Added in Phase 2 if needed.

### Layer 3: CI quality gates (GitHub Actions)

**Phase 1:**
- Path-filtered CI via dorny/paths-filter — .NET jobs only when backend changes, frontend jobs only when frontend changes
- `dotnet build` with TreatWarningsAsErrors (Roslyn analyzers + StyleCop enforce at build time)
- `dotnet test` with Coverlet coverage → Codecov upload (backend flag)
- `npm ci && vitest run --coverage` → Codecov upload (frontend flag)
- CodeQL for C# + JavaScript/TypeScript (security scanning, free for public repos)
- Dependabot configured for NuGet, npm, GitHub Actions, Docker ecosystems

**Phase 2:**
- Trivy filesystem scan (dependency vulnerabilities, both ecosystems) → SARIF upload to GitHub Security tab
- Trivy container image scan (post Docker build, CRITICAL/HIGH only, warn-only initially)
- License compliance checks (weekly scheduled workflow, lightweight CLI tools)
- SonarCloud integration

**Coverage thresholds:** 60% project target, 70% patch coverage (new code). Patch coverage is especially important — ensures Claude Code writes tests for new features. Codecov Carryforward Flags for path-filtered runs.

### Layer 4: Dashboard and trends (SonarCloud)

Use **SonarCloud free tier** (open source qualifies). Genuine value for AI-generated code: cross-file taint analysis (tracks user input through service layers — ESLint and StyleCop can't do this), cognitive complexity scoring, duplication detection (AI produces 4× more code cloning per GitClear research), and the MCP server integration that feeds SonarCloud findings back into Claude Code's context.

Focus on security hotspot detection, coverage tracking, and duplication metrics — not code smell counts that overlap with existing tools.

**Lightweight alternative if SonarCloud is deferred:** Install `SonarAnalyzer.CSharp` NuGet + `eslint-plugin-sonarjs` into the build pipeline for free. Captures ~90% of analysis value without the platform.

### Layer 5: Human review (irreplaceable)

With AI handling mechanical correctness (~45% of bugs per IBM research), human review focuses exclusively on what AI review is structurally blind to:

1. **Business logic correctness** — does the code solve the right problem with correct domain rules?
2. **Architectural consistency** — does new code follow established patterns, or did Claude invent something?
3. **Test quality** — would tests fail if the feature broke? Watch for mocks of the thing being tested, trivial assertions.
4. **Security threat modeling** — any code touching auth, user input, or secrets gets mandatory human review.
5. **Scope creep** — did Claude change things beyond what was asked? Speculative changes are the most common AI footgun.
6. **Dependency verification** — do all referenced packages, imports, and APIs actually exist?

Human review is a significant portion of the development cycle — the right ratio when 100% of code is AI-generated.

### Key research findings driving this decision

- CodeRabbit analysis of 470 PRs: AI-authored code produces 1.7× more issues per PR, logic errors up 75%, security vulnerabilities 1.5–2× higher.
- IBM Research: LLM-as-judge alone detects ~45% of errors. Supplemented with static analysis hints, coverage rises to ~94%.
- GitClear: 8× increase in duplicated code when AI reviews its own output, 39.9% decrease in refactored code.
- Stanford/Meta: instruction adherence averages 43.7% across models over time — the mechanism behind architectural drift.
- IEEE Spectrum (March 2026): newer AI models produce code that removes safety checks and creates fake output matching expected formats.
- ProjectDiscovery: 3 AI-generated applications contained 70 exploitable vulnerabilities including 18 Critical/High — traditional code-only review (including AI review) missed all of them.

### Total pipeline cost

| Layer | Monthly cost | Noise |
|-------|-------------|-------|
| Pre-commit (Lefthook) | $0 | Low |
| PR review (CodeRabbit + Claude Action) | $2–8 | Medium |
| CI gates (CodeQL + Dependabot + Trivy + Codecov) | $0 | Low |
| Dashboard (SonarCloud) | $0 | Medium → Low |
| Human review | Your time | N/A |

**Rationale:** The core finding from R-012 research: when 100% of code is AI-generated, no single quality tool is sufficient. Correlated blind spots mean the same biases that cause AI to write a pattern cause AI to accept it in review. The defense is layered: deterministic tools (formatters, analyzers, CodeQL) catch what they're designed for, AI review (CodeRabbit + Claude Action with cross-model review) catches a broader but imperfect set, and human review focuses on the architectural and business-logic layer where AI is structurally blind. The combination reaches ~94% error detection vs. ~45% for AI review alone.

**Alternatives considered:**
- Husky + lint-staged (rejected — Lefthook is strictly superior for polyglot monorepo: single binary, parallel by default, no lint-staged dependency)
- GitHub Copilot Code Review (rejected — requires paid subscription, 31 of 47 suggestions duplicate ESLint, 7 factually wrong, diff-only with no cross-file context)
- Qodo Merge (strong alternative to CodeRabbit with 75 PR reviews/month free tier and less noise — consider if CodeRabbit noise proves unmanageable)
- Self-hosted SonarQube (rejected — SonarCloud free tier with branch analysis and PR decoration is strictly superior for open source)
- Performance regression testing in CI (deferred — GitHub-hosted runners have 5–20% variance, makes detection unreliable; revisit with k6 smoke tests when specific hot paths exist)
- Snyk (rejected initially — superior fix-PR automation but adds account friction and free-tier limits; reconsider if repo goes private)

### Amendment: Private repo redesign (2026-03-19)

The repo is private to protect coaching prompt IP and persona design. The original decision assumed open source where all tooling is free. Several tools require paid plans for private repos: CodeRabbit (paid), CodeQL (requires GitHub Team + Code Security), SonarCloud (paid), Claude Code GitHub Action (requires API key). This amendment redesigns the pipeline to maintain the research-backed layered defense using tools that are free regardless of repo visibility.

**The core research finding is preserved:** IBM Research showed LLM-as-judge alone detects ~45% of errors, but supplemented with deterministic static analysis, coverage rises to ~94%. The redesigned pipeline maintains this by ensuring every layer has deterministic, uncorrelated analysis tools.

**Layer 1 (Pre-commit) — refined:**
- Lefthook with `stage_fixed: true` for auto-fix-and-restage workflow
- `dotnet format --include {staged_files}` (fix mode, not verify-no-changes)
- ESLint + Prettier on staged frontend files
- commitlint on commit messages
- Pre-push: unit tests + tsc --noEmit

**Layer 2 (PR review) — replaced:**
- CodeRabbit and Claude Code GitHub Action removed (cost barriers)
- Replaced by local `/review-pr` via Claude Code Max subscription ($0 marginal cost)
- Structured human review checklist in CLAUDE.md guides every review
- Cross-model review benefit is partially lost but mitigated by strong deterministic tooling in Layers 1, 3, and 4

**Layer 3 (CI) — CodeQL replaced with Trivy:**
- **Trivy** (Apache 2.0, fully free) replaces CodeQL for security scanning:
  - Filesystem vulnerability scan (NuGet + npm dependencies)
  - Secrets detection in committed code
  - IaC scanning (Dockerfiles, docker-compose.yml)
  - CRITICAL/HIGH severity = build failure; MEDIUM/LOW = warn only
- Path-filtered build/test via dorny/paths-filter (unchanged)
- TreatWarningsAsErrors with Roslyn + StyleCop + SonarAnalyzer.CSharp (unchanged)
- Codecov with coverage thresholds: 60% project target, 70% patch coverage
- Dependabot for NuGet, npm, GitHub Actions, Docker (unchanged)
- Branch protection: require `gate` status check + PR required (no direct push to main)

**Layer 4 (Dashboard) — replaced with build-time analysis:**
- SonarCloud deferred (paid for private repos)
- Backend: SonarAnalyzer.CSharp already installed + TreatWarningsAsErrors = security hotspots, taint analysis, complexity scoring enforced at compile time
- Frontend: `eslint-plugin-sonarjs` added to ESLint config = cognitive complexity, duplication detection, code smell rules enforced at build time and pre-commit
- This is the "90% alternative" the original research recommended: same analysis rules, no platform overhead

**Layer 5 (Human review) — unchanged:**
- Structured checklist: business logic, architectural consistency, test quality, security threat modeling, scope creep, dependency verification

**Revised pipeline cost:**

| Layer | Monthly cost | Noise |
|-------|-------------|-------|
| Pre-commit (Lefthook) | $0 | Low |
| PR review (local /review-pr via Max) | $0 | Low |
| CI gates (Trivy + Dependabot + Codecov) | $0 | Low |
| Build-time analysis (SonarAnalyzer + eslint-plugin-sonarjs) | $0 | Low |
| Human review | Your time | N/A |

**Total: $0/month** (vs. $2–8/month in original design). All five layers preserved with free tools.

**Revisit triggers:**
- If repo goes public → restore CodeRabbit, CodeQL, SonarCloud to match original design
- If codebase grows large enough for trends/duplication dashboard → add SonarCloud
- Container image scanning → add Trivy image scan when deploying Docker images
- License compliance → add weekly scheduled workflow pre-public release

---

## DEC-035: Coding standards, rulesets, and project conventions

**Date:** 2026-03-19
**Status:** Final
**Category:** Development standards / Project conventions
**Sources:** Uploaded dotnet-ruleset-ideas.md, uploaded react-ruleset-ideas.md, Microsoft dotnet/skills repo, liatrio-labs opinionated-enterprise-standards repo (input only, not overriding), batch-1 Claude Code workflow research

**Decision:** The project adopts a curated set of coding standards synthesized from four external sources, filtered for a solo-developer AI-assisted project. Standards are distributed across the project structure based on scope and enforcement mechanism. Six key convention choices made during synthesis:

1. **React Hook Form + Zod** for all form handling. Schema-based validation with inferred TypeScript types. Zod schemas in `schemas/` directories within feature modules.
2. **Module-first folder structure** for both backend and frontend. Domain modules contain all related files (controller, service, repository, models) at the module root. Technical layering enforced by code dependency direction, not folder hierarchy.
3. **Standard DI registration** via `Add{Module}Services()` extension methods. No custom attribute-based scanning. Called explicitly in Program.cs.
4. **Scoped-first DI lifetimes** for services and repositories (anything in the request pipeline). Singleton only for stateless infrastructure (configuration wrappers, HTTP client factories).
5. **Microsoft dotnet/skills** installed as a Claude Code plugin for EF Core optimization, MSBuild diagnostics, and .NET upgrade guidance. One principle extracted into root CLAUDE.md: "Don't use LLMs for structured data tasks."
6. **BDD acceptance criteria** (Given/When/Then) in every plan file. Scenarios double as specs for Playwright E2E and integration tests.

### Rules placement map

| Content | Location | Enforcement |
|---------|----------|-------------|
| Project context, architecture, session workflow, key principles | Root `CLAUDE.md` (<200 lines per R-008) | Read every session |
| .NET coding standards, EF Core patterns, module structure, testing | `backend/CLAUDE.md` | Read when working in backend/ |
| React/TS standards, component patterns, RTK Query, forms, testing | `frontend/CLAUDE.md` | Read when working in frontend/ |
| Security & secrets rules | Root `CLAUDE.md` | Always active |
| Git standards (trunk-based, conventional commits, branch naming) | Root `CLAUDE.md` | Always active |
| EF Core migration safety | `.claude/rules/` with glob on migrations | Conditional trigger |
| Format-on-save (dotnet format, prettier) | Claude Code PostToolUse hooks | Automated |
| Dangerous command blocking | Claude Code PreToolUse hooks | Automated |
| Pre-commit checks (Lefthook) | `lefthook.yml` | Deterministic |
| EF Core query optimization, MSBuild diagnostics | dotnet/skills plugin | On-demand skill |

### Backend conventions adopted (from uploaded .NET ruleset, filtered)

- Primary constructors when applicable
- `_` prefix for private fields
- Properties initialized with default non-null values
- One type per file
- Record types for DTOs with `Dto` suffix
- Structured logging with `ILogger<T>` and named placeholders
- Module-first organization: `Modules/{Domain}/` with controller, service, repository at root; Models/, Entities/, Extensions/ as subdirectories
- Submodules when a module exceeds ~8 root files
- `Modules/Shared/` for cross-cutting services
- Code-first EF Core migrations, `{EntityName}Id` naming, Guid PKs, `[Key]`/`[Required]` data annotations preferred over Fluent API
- Base entity with audit fields (CreatedOn, ModifiedOn)
- Never modify or delete existing migrations
- Strongly-typed settings as record types, `IOptions<T>` pattern
- Layered config: appsettings.json → appsettings.{Environment}.json → appsettings.Local.json (git-ignored)
- Post-change verification: `dotnet build` after code changes, `dotnet test` after test changes
- Async EF Core operations throughout
- `Add{Module}Services()` extension methods for DI registration, called in Program.cs
- Scoped lifetime default for services/repositories; Singleton only for stateless infrastructure

### Backend conventions rejected

- Attribute-based DI (`[SingletonService]`) — replaced with standard extension method registration
- Singleton-first lifetime — replaced with scoped-first
- Object mapping library (Mapster/AutoMapper) — premature for MVP, manual mapping initially
- In-memory database for integration tests — replaced with Testcontainers + real PostgreSQL

### Frontend conventions adopted (from uploaded React ruleset, filtered)

- Module-based organization: `modules/{feature}/` with component, api, slice, helpers, models, schemas directories
- File naming: `{name}.{type}.{extension}` (e.g., `.component.tsx`, `.api.ts`, `.slice.ts`, `.hooks.ts`, `.model.ts`, `.schema.ts`, `.spec.tsx`)
- Pages as lightweight route-level composers under `pages/`
- Arrow functions for components, `{ComponentName}Props` interfaces, destructured props, named exports
- Composition over render functions — extract components, never use `renderX()` helpers
- Component extraction at >20-30 lines, repeated patterns, or independent state
- TypeScript strict mode, no `any`, type imports, nullish coalescing over `||`
- State hierarchy: router state → local state → Redux for cross-cutting
- RTK Query for all HTTP interactions with tagTypes and cache invalidation
- React Hook Form + Zod for forms, schemas in `schemas/` per module
- Custom hook naming: `Use{Name}Options`, `Use{Name}Return`, explicit return types
- Import order: React → third-party → path alias → parent → same-dir → CSS
- Path alias (`~/`) for cross-module imports
- Naming: Dto suffix, intent-based handlers (not `handleClick`), boolean verb prefixes (`is`, `has`, `can`)
- Performance: selective React.memo, proper keys (never array index), code splitting
- Accessibility: semantic HTML, ARIA, keyboard navigation
- Security: DOMPurify for HTML content, env vars for config

### Frontend conventions filtered

- MUI-specific styling — replaced with Tailwind CSS + shadcn/ui patterns
- Three-file API pattern (base/main/enhanced) — single API file per feature for MVP
- Loading state prescriptions — defer to shadcn/ui patterns

### Testing conventions adopted (cross-stack)

- Test projects mirror source directory structure
- Arrange / Act / Assert with comments
- `expected` / `actual` prefixes for values
- `[Theory]` + `[InlineData]` for parameterized scenarios
- FluentAssertions for readable assertions
- Integration tests: WebApplicationFactory + Testcontainers (real PostgreSQL, not in-memory)
- Full response contract validation with deep equality (excluding audit fields)
- Frontend: co-located `.spec.tsx` files, test logic-heavy code (helpers, hooks, reducers)
- BDD acceptance criteria in plan files feed directly into E2E test scenarios

### Plan file format

Every implementation plan file includes BDD acceptance criteria:

```
## Acceptance Criteria

### Scenario: [descriptive name]
Given [precondition]
When [action]
Then [expected outcome]
```

Scenarios are the source of truth for what Playwright E2E tests and integration tests must verify.

**Rationale:** Standards are curated for a solo developer whose code is 100% AI-generated. The key tension is between comprehensive rules (which help Claude Code produce consistent output) and CLAUDE.md size (which must stay under 200 lines per batch-1 research). Resolved by distributing rules: root CLAUDE.md for global context and principles, subdirectory CLAUDE.md files for tech-specific standards, .claude/rules/ for conditional triggers, hooks for automated enforcement, and the dotnet/skills plugin for on-demand skills. The enterprise standards repo informed the organizational patterns but its JIRA workflow, Mapster requirement, and minimal-API prohibition were filtered as enterprise-specific.

---

*Add new decisions at the bottom. Use format: DEC-XXX, date, category, decision, rationale, alternatives.*
