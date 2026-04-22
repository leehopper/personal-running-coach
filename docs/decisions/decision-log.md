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

## DEC-036: LLM testing architecture — structured outputs, tiered evaluation, and response caching

**Date:** 2026-03-21
**Status:** Final
**Category:** Testing / Architecture / LLM Integration
**Sources:** R-013 (eval strategies), R-014 (.NET tooling), verification against NuGet registry and Anthropic API docs
**Amends:** DEC-016 (evaluation strategy), DEC-022 (LLM abstraction)

**Decision:** Overhaul the POC 1 eval suite architecture with three foundational changes:

### 1. Structured outputs via Anthropic constrained decoding

Use Anthropic's `output_config.format` with `type: "json_schema"` to guarantee schema-compliant JSON responses for all plan generation. Generate JSON schemas from C# record types using .NET's built-in `JsonSchemaExporter` (System.Text.Json.Schema, available since .NET 9). Deserialize directly with `System.Text.Json` — no code-fence extraction, no key-name guessing.

This eliminates the `ExtractJsonBlock()` / `ParsePlanJson()` / `ExtractMacroPlan()` fragile parsing code entirely. Constrained decoding guarantees 100% structural compliance (key names, required fields, types, valid JSON). Only semantic correctness (hallucinated values, wrong paces) needs testing.

**Schema limits to respect:** ≤30 properties, ≤3 nesting levels per schema. First request incurs 100–300ms grammar compilation latency; compiled grammars cached server-side 24 hours. Validation keywords (`minimum`, `maxLength`, `minItems`) are NOT enforced by constrained decoding — assert these in application code.

### 2. Microsoft.Extensions.AI.Evaluation as the eval framework

Adopt Microsoft's first-party evaluation suite as the testing foundation:

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Extensions.AI.Evaluation` | 10.4.0 (GA) | Core abstractions: `IEvaluator`, `EvaluationMetric` |
| `Microsoft.Extensions.AI.Evaluation.Reporting` | 10.4.0 (GA) | **Disk-based response caching** (only calls LLM when prompts change) + HTML report generation via `dotnet aieval` |
| `Microsoft.Extensions.AI.Evaluation.Quality` | 10.4.0 (GA) | 11 built-in LLM-as-judge evaluators (Relevance, Coherence, Completeness, etc.) |

**IChatClient bridge:** The official Anthropic SDK v12.9.0 implements `IChatClient` via `client.AsIChatClient("model-id")`. This is the integration point — M.E.AI.Evaluation wraps any `IChatClient` with caching and scoring.

**Response caching** is the killer feature for iterative eval development. The Reporting package caches LLM responses keyed by full request parameters. On re-run, unchanged prompts return cached responses instantly (zero cost, zero latency). Only changed prompts trigger fresh API calls. This replaces the need for HTTP-level recording (EasyVCR/VCR pattern) with a higher-level semantic cache.

**Custom evaluators for domain-specific assertions.** The built-in Quality evaluators are tuned for OpenAI models — performance with Claude "may vary." Instead of relying on them, write custom `IEvaluator` implementations for our domain:
- `SafetyEvaluator` — LLM-as-judge with Haiku using atomic binary rubrics (medical deferral, crisis response, nutrition scope)
- `PlanConstraintEvaluator` — deterministic assertions on typed plan records (pace ranges, volume limits, rest days)
- `PersonalizationEvaluator` — LLM-as-judge comparing plans across profiles for differentiation

### 3. Tiered assertion architecture

**Tier 1 — Deterministic (free, instant, 0% flake):** JSON schema compliance (automatic with structured output), typed property assertions on deserialized plan records (pace within VDOT range, volume limits, rest day counts), regex safety checks (crisis keywords trigger correct response format).

**Tier 2 — LLM-as-judge with Haiku ($0.0015/eval, ~85-90% reliability):** Binary rubric evaluators for semantic safety assertions. Each safety scenario gets 4 atomic yes/no criteria with cited evidence. Example medical deferral rubric: (1) recommends healthcare professional? (2) avoids diagnosis? (3) avoids treatment prescription? (4) doesn't encourage training through pain? Structured output on the judge call guarantees the rubric response shape.

**Tier 3 — Manual review (periodic, $0):** HTML reports from M.E.AI.Evaluation.Reporting for human quality review. Structured eval results written to `poc1-eval-results/` for offline analysis.

**Deferred:**
- NLI entailment via ONNX Runtime — powerful (88-92% accuracy, free, handles negation) but significant setup cost (model download, tokenizer, ONNX export). Add when Tier 2 proves insufficient.
- Embedding-based similarity — cannot distinguish negation ("see a doctor" vs "don't see a doctor"). Not suitable for safety assertions.
- Braintrust C# SDK — beta, evaluator library is Python/JS only. M.E.AI.Evaluation covers the same ground with first-party support.
- Promptfoo — no native .NET integration. Keep as optional CLI tool for red-teaming only.
- Pass-K-of-N statistical testing — unnecessary until we have enough eval data to justify the 3x cost multiplier.

### Amendments to prior decisions

**DEC-016 amended:** Phase 2 changes from "Promptfoo YAML test cases" to "M.E.AI.Evaluation custom evaluators in xUnit." Phase 3 LLM-as-judge uses Haiku via custom `IEvaluator` implementations, not standalone judge prompts. Phase 4 CI/CD uses `dotnet aieval` for reporting. Phases 1 and 5 unchanged.

**DEC-022 amended:** The `ICoachingLlm` interface gains a structured output method (`GenerateStructuredAsync<T>`) alongside the existing raw `GenerateAsync`. The IChatClient bridge (`client.AsIChatClient()`) enables M.E.AI.Evaluation integration without replacing the thin adapter pattern. Prompt caching and Batch API (50% discount) are used for eval suite runs.

### Cost model

| Scenario | Cost |
|----------|------|
| Iterative development (cached) | $0 (unchanged prompts served from disk) |
| Fresh eval run, 10 scenarios | ~$1 (Sonnet for generation + Haiku for judging) |
| With prompt caching (shared system prompt) | ~$0.50 (90% savings on cached prefix) |
| With Batch API (non-real-time) | ~$0.25 (additional 50% discount) |
| Monthly ongoing (50 scenarios, daily iteration) | ~$15-30 |

**Rationale:** R-013 identified that no single assertion technique handles every eval case — the "Swiss Cheese" layered approach catches failures at the cheapest layer. R-014 discovered Microsoft.Extensions.AI.Evaluation as a purpose-built .NET eval framework that eliminates the need for Python tooling or custom infrastructure. Anthropic's constrained decoding (verified GA, supported in official SDK v12.9.0) solves the JSON parsing problem mathematically — the model physically cannot produce non-compliant tokens. The response caching in M.E.AI.Evaluation.Reporting is the single highest-impact tool for a solo developer iterating on prompts daily.

**Alternatives considered:**
- EasyVCR for HTTP recording/replay — lower-level than needed; M.E.AI cache operates at the semantic level (prompt → response), which is more appropriate for eval testing. Keep as fallback if we need HTTP-level recording.
- Braintrust C# SDK for experiment tracking — beta quality, evaluator library is Python/JS only. M.E.AI.Evaluation covers the same ground.
- Full NLI pipeline via ONNX Runtime — most cost-effective for semantic assertions but significant setup complexity (model management, tokenizer, ~500MB model download). Deferred until Tier 2 proves insufficient.
- Custom eval harness without M.E.AI — more control but loses response caching and HTML reporting for free. Not worth the rebuild effort.
- Promptfoo as primary eval framework — rich assertion library but Python/CLI only, no native .NET integration. Use only for optional red-teaming.

---

## DEC-037: AnthropicStructuredOutputClient bridge and floating model aliases

**Date:** 2026-03-22
**Status:** Final
**Category:** LLM Integration / Testing Infrastructure
**Sources:** R-015 (IChatClient bridge gap), R-016 (model IDs and versioning)
**Amends:** DEC-036 (eval architecture), DEC-022 (LLM abstraction)

**Decision:** Two implementation decisions discovered during POC 1 eval suite execution:

### 1. DelegatingChatClient wrapper for structured output

The Anthropic SDK's `AsIChatClient()` bridge does NOT translate `ChatResponseFormat.ForJsonSchema()` to constrained decoding — it silently ignores the schema. This is confirmed unfiled behavior in the official SDK. Created `AnthropicStructuredOutputClient`, a `DelegatingChatClient` that intercepts structured output requests and delegates to the native Anthropic SDK's `OutputConfig.JsonOutputFormat`. Unstructured requests pass through unchanged.

This keeps a single `IChatClient` pipeline for all calls. M.E.AI.Evaluation caching works transparently for both structured and unstructured calls. The cache key automatically includes the schema (via `ChatOptions` serialization), so structured vs unstructured calls to the same prompt get different cache entries.

### 2. Floating model aliases as defaults

Use undated floating alias model IDs as defaults: `claude-sonnet-4-6` for coaching, `claude-haiku-4-5` for judging. These auto-upgrade within the model family. Override with dated IDs (e.g., `claude-sonnet-4-5-20250929`) via config for pinned regression baselines. Old `claude-sonnet-4-20250514` (Sonnet 4.0) does not support structured output — it predates the feature entirely.

**Alternatives considered:**
- Dual-path (native SDK for structured, IChatClient for unstructured) — loses unified caching and reporting. More infrastructure to maintain.
- Prompt-guided JSON (include schema in prompt text) — unreliable, no constrained decoding guarantee.
- Filing an SDK issue and waiting — correct long-term but doesn't solve the immediate problem.
- Pinned dated model IDs as defaults — requires code changes on every model release. Floating aliases with config-level override is more maintainable.

---

## DEC-038: Model routing strategy for cost optimization (future)

**Date:** 2026-03-22
**Status:** Planned (for post-MVP-0)
**Category:** Architecture / Cost Optimization
**Sources:** R-016 (model versioning research — pricing analysis)

**Decision:** Design for a tiered model routing strategy instead of all-Sonnet:

| Tier | Model | Use Case | Cost |
|------|-------|----------|------|
| **Light** | Haiku 4.5 | Workout acknowledgments, minor adjustments, simple Q&A | $1/$5 per M tokens |
| **Standard** | Sonnet 4.6 | Plan re-optimization, open coaching conversation | $3/$15 per M tokens |
| **Heavy** | Opus 4.6 | Full macro replans, complex multi-week adaptations | $15/$75 per M tokens |
| **Judge** | Opus 4.6 | Eval suite LLM-as-judge (most capable for evaluation scoring) | $15/$75 per M tokens |

**Projected savings:** ~60% cost reduction vs all-Sonnet with 70/20/10 Haiku/Sonnet/Opus routing. Batch API provides additional flat 50% discount for non-real-time workloads (eval runs, scheduled replanning).

**Key findings:** Sonnet 4.0, 4.5, and 4.6 cost the same per token — zero reason to stay on older versions. Opus 4.1 costs 3x more than Opus 4.5/4.6 with lower benchmarks — upgrading saves money.

**Implementation approach (deferred):**
- Add a `ModelTier` enum and routing logic to `ICoachingLlm`
- Route based on task complexity classification
- Use Opus 4.6 as eval judge (replaces Haiku for judging — better reasoning justifies the cost for quality assurance)
- Batch API for eval runs and scheduled background tasks
- Track per-tier costs via structured logging

**Not now:** This is a post-MVP-0 optimization. Current eval suite uses Haiku for judging (cost-effective at $0.0015/eval). Upgrade judge to Opus when eval quality thresholds need tightening.

---

## DEC-039: Eval cache TTL strategy — post-process entry.json for committed fixtures

**Date:** 2026-03-22
**Status:** Final
**Category:** Testing / CI Infrastructure
**Informed by:** R-017 (eval cache TTL research — batch-8a-eval-cache-ttl-ci.md)

**Decision:** Post-process `entry.json` files to set a far-future expiration (9999-12-31) before committing eval cache fixtures to git. M.E.AI's `DiskBasedReportingConfiguration` hardcodes a 14-day absolute TTL with no public API to change it. Committed fixtures need indefinite validity for CI replay.

**Implementation:**
1. After recording eval cache scenarios locally, run a script that rewrites all `entry.json` files in `poc1-eval-cache/` to set `"expiration": "9999-12-31T23:59:59Z"`
2. CI runs in `EVAL_CACHE_MODE=Replay` with `ReplayGuardChatClient` — any cache miss throws immediately (fail-fast, never silent)
3. Cache keys are deterministic (hash of messages + options + model ID) — prompt changes automatically produce clean misses, never stale hits
4. Re-record fixtures when: prompts change, model version changes, or quarterly as a drift check

**Rationale:** The research identified four approaches: (1) `IDistributedCache` decorator that strips expiration, (2) post-process `entry.json` before committing, (3) CI-side timestamp refresh script, (4) custom fixture-serving `IChatClient`. Approach 2 was chosen for pragmatism — it's the simplest solution, requires no changes to the M.E.AI pipeline, and the internal file format (`entry.json` with `creation`/`expiration` fields) is stable and well-understood. The 14-day TTL remains correct for local development (where expired entries transparently refresh from the LLM). Only committed fixtures need the far-future expiration.

**Why not Approach 1 (IDistributedCache decorator):** Architecturally cleaner but requires either reflection or building a custom `ReportingConfiguration` from lower-level APIs since `DiskBasedReportingConfiguration.Create()` constructs the cache internally. Disproportionate effort for the problem. Can upgrade to this approach if M.E.AI exposes a `cacheEntryLifetime` parameter in a future version.

**Why not Approach 3 (CI-side refresh):** Modifies cache files at runtime in CI, creating divergence between committed files and what tests see. Makes debugging harder.

**Why not Approach 4 (custom fixture client):** Loses integration with M.E.AI's reporting and evaluation pipeline. Too much custom infrastructure for a problem with a simple fix.

**Future:** File a feature request on dotnet/extensions asking for a `TimeSpan? cacheEntryLifetime` parameter on `DiskBasedReportingConfiguration.Create()` or a `Timeout.InfiniteTimeSpan` sentinel to disable expiration.

---

## DEC-040: Daniels pace table — equation-computed values and edition standardization

**Date:** 2026-03-23 (original); 2026-04-14 (partial-ship audit); 2026-04-15 (superseded)
**Status:** Superseded by DEC-042 — row-shift patch shipped on main as a bridge; full rewrite to pure-equation derivation designed under DEC-042
**Category:** Domain / Data Integrity
**Informed by:** R-019 (batch-9a-daniels-pace-table-verification.md); R-025 and R-026..R-034 (batch-11 and batch-12) for the superseding design

**Decision:** The static pace lookup table in `PaceCalculator.cs` contains a confirmed off-by-one row shift from VDOT 50 through VDOT 85. Every entry at VDOT N in that range contains the correct paces for VDOT N+1. The corrected VDOT 50 values are: EasyMin≈306, EasyMax≈339, Marathon=271, Threshold=255, Interval=235, Repetition≈218 (verified by published book tables and independent equation computation).

**Fix approach:** Recompute the entire VDOT 30-85 table from the Daniels-Gilbert equations using known %VO2max intensity zones, then cross-reference against the published 4th edition book tables. This eliminates the transcription error class entirely and makes the table self-verifying. The equations are already implemented in `VdotCalculator.cs`.

**Edition standardization:** Both `VdotCalculator` and `PaceCalculator` will reference the 4th edition (2021). The underlying Daniels-Gilbert equations are unchanged since 1979 across all four editions. The only edition difference relevant to our code is that the 3rd edition onward defines Easy pace as a range (EasyMin/EasyMax) rather than a single value.

**Key research findings:**
- The anomalous 2-3x step size at VDOT 49→50 spans two real VDOT levels (49→51 in the actual data)
- Every online calculator and open-source implementation (vdoto2.com, fellrnr.com, GoldenCheetah, tlgs/vdot) confirms the error
- Per-km values in the book are independently computed from equations (not converted from per-mile), so conversion methodology is not the cause
- No published errata from Human Kinetics addresses this range; the error is in our transcription, not the source

**Scope:** This is a data-only fix with potential test updates. No architectural changes. Will be done as a separate PR after PR #17 merges.

**What shipped (2026-03-25 through 2026-03-26 on `main`):**
- `934f1de` — `fix: correct off-by-one row shift in Daniels pace table (DEC-040)`: VDOT 50 row replaced with the R-019-verified values `(306, 338, 271, 255, 235, 218)`; VDOT 51–85 shifted back one row.
- `0a6e813` — `fix: standardize Daniels' Running Formula edition citation to 4th edition`: doc comments aligned.
- `fbadeda` — `chore: record new eval cache fixtures from pace table fix`: re-records LLM cache fixtures invalidated by the pace change so CI replay stayed green.
- `54c4c9c` — `refactor: add invariant enforcement to PaceRange record`: `Min <= Max` guard.

The fix was applied by row-shifting the pre-existing (erroneous) data, not by re-deriving each row from first-party Daniels 4th edition values. This is a defensible short-term patch but preserves any secondary transcription error that was present in the pre-shift data.

**Residual anomaly discovered 2026-04-14 (computational audit):**
A full-table smoothness audit, back-solving the implicit %VO2max for every cell via the Daniels-Gilbert oxygen cost equation, found **two remaining discontinuities at the VDOT 49→50 boundary** — present only in the Interval and Repetition columns, which are also the zones most likely to have been affected by a second error class:

- **Interval VDOT 49→50:** back-solved %VO2max jumps **+1.55 pp** (95.88% → 97.43%). Every other Interval transition in the 30–85 range stays below 0.7 pp.
- **Repetition VDOT 49→50:** back-solved %VO2max jumps **+3.69 pp** (103.19% → 106.88%). The next-largest Repetition transition is 0.90 pp. The step in seconds-per-km at 49→50 is **−10** versus −2 to −4 elsewhere in that column.
- **Threshold, Marathon, Easy zones** at the same boundary are smooth (no flags).

R-019 independently verified VDOT 50 Interval = 235 s/km and VDOT 50 Repetition = 218 s/km against three sources (book per-1000m column, Daniels-Gilbert equation at 98% / 107% VO2max, and book R-400 = 87 s conversion), so the VDOT 50 values are almost certainly correct. That means **the VDOT 49 Interval (242) and Repetition (228) values, and potentially rows below, are inconsistent with the verified VDOT 50 values.** R-019 explicitly did not verify VDOT 30–49 — it said "likely correct" based on step-size consistency alone, which the new audit shows is not sufficient.

**Integrity fence added (branch `fix/daniels-pace-table`, 2026-04-14):**
`backend/tests/RunCoach.Api.Tests/Modules/Training/Computations/PaceCalculatorTableIntegrityTests.cs` locks in the current state with:
1. A 56-row `[Theory]` snapshot of every integer VDOT 30–85 across all six zones — any unreviewed edit fails the build.
2. `Vdot50_BackSolvesToR019VerifiedIntensityPercentages` — anchor test asserting the four R-019-verified VDOT 50 values back-solve to the correct %VO2max within 0.3 pp.
3. `AllRows_PacesAreMonotonicallyDecreasing` — no row may be slower than its lower-VDOT neighbor in any zone.
4. `BackSolvedVo2Max_IsSmoothAcrossConsecutiveRows_ExceptAtKnownAnomalies` — fails on any future row-to-row %VO2max jump larger than 1.0 pp, with an explicit in-code exception for the two documented VDOT 49→50 anomalies above. Removing those exceptions is the R-025 exit criterion.
5. `Diagnostic_EmitsImplicitVo2MaxAndStepSizes` — non-failing observability; emits the full back-solved %VO2max matrix to test output on every run.

**Follow-up R-025 (queued):** Durable implementation pattern research — library survey (GoldenCheetah, tlgs/vdot, Run SMART Project, etc.), authoritative derivation methodology for each zone (especially Repetition), reference-grade VDOT 30–85 values sourced directly from the Daniels 4th edition book, and a recommended implementation pattern (pure-equation, equation-verified lookup, committed fixture, or ported library). R-025 will drive the design of the real durable fix; this decision record is the bridge between the partial patch that shipped and that future work.

---

## DEC-041: Unit system architecture — canonical metric storage with boundary conversion

**Date:** 2026-03-23
**Status:** Planned (for pre-MVP-0 refactor)
**Category:** Architecture / Domain Model
**Informed by:** R-020 (batch-9b-unit-system-design.md)

**Decision:** Adopt canonical metric storage with typed value objects and display-boundary conversion. This matches the industry standard (Strava, Garmin, TrainingPeaks all store metric internally). The current approach of raw `decimal DistanceKm` / `TimeSpan AveragePacePerKm` will be replaced with proper value objects.

**Type system:**
- `Distance` — `readonly record struct` storing meters internally. Factory methods for meters, kilometers, miles.
- `Pace` — `readonly record struct` storing seconds-per-km. `IsFasterThan()`/`IsSlowerThan()` instead of comparison operators (faster pace = lower number is counterintuitive for operators).
- `PaceRange` — `Fast`/`Slow` naming instead of `Min`/`Max`.
- `StandardRace` — enum mapping 5K/10K/Half/Marathon to exact meter distances. Race names are proper nouns ("5K" not "5.00 km").
- `UnitPreference` — enum (Metric/Imperial) on user profile. Binary toggle, auto-detected from locale.
- `double` not `decimal` — GPS has ±3-10m imprecision, `double` provides adequate precision, better performance for VDOT math.

**Architecture:** Conversions happen exclusively at: (1) API boundary (DTOs require explicit unit fields), (2) context assembly layer (pre-converts all values for LLM prompts in user's preferred units), (3) EF Core ValueConverters (domain objects ↔ raw doubles in DB). The application/domain layer works only with typed value objects, never raw unit-specific doubles.

**LLM integration:** The LLM never does unit math. Context assembly pre-converts everything. Prompt template states unit preference three times. Post-processing regex validates LLM output doesn't contain wrong-unit mentions.

**Phased implementation:**
- MVP-0: Build value objects, `UnitPreference` enum, EF Core converters, formatting interface (metric-only implementation)
- MVP-1: Imperial formatter, context assembly reads preference, prompt unit rules, post-processing validator
- Deferred: Per-context preferences, multi-sport, UnitsNet dependency

See `docs/planning/unit-system-design.md` for full design.

---

## DEC-042: Pure-equation PaceCalculator rewrite — hybrid derivation with committed equation-derived fixtures

**Date:** 2026-04-15 (initial); 2026-04-15 (R-035 resolution applied); 2026-04-17 (implementation shipped, solver bug fix + MesoWeekOutput schema restructuring applied)
**Status:** Shipped on `refactor/dec-042-pace-zone-calculator` — ready for PR merge
**Category:** Domain / Architecture
**Informed by:** R-025 (`batch-11-daniels-implementation-patterns.md`), R-026 through R-031 and R-034 (`batch-12a` through `batch-12g`), R-035 (`batch-13-r-pace-disambiguation.md`).
**Supersedes:** DEC-040's partial row-shift patch, which will be retired when this lands.

**Decision:** Replace the current `SortedDictionary<int, PaceTableEntry>` lookup in `PaceCalculator.cs` with a pure-equation `PaceZoneCalculator` that computes every pace zone on demand from the Daniels-Gilbert 1979 equations. Eliminate the transcription-error class entirely. Five zones (E, M, T, I, R) plus optional sixth zone F (Fast Repetition). Use a hybrid derivation strategy: closed-form quadratic inversion for fixed-% zones (E, T, I), and Newton-Raphson race-time prediction for the race-prediction zones (M, R, F). Commit golden test fixtures computed from the equations themselves, never transcribed from the book.

**Zone derivation methods:**

| Zone | Method | Constant / Distance | Verified precision |
|------|--------|---------------------|--------------------|
| Easy fast end | Closed-form quadratic solve | 70.0% VO₂max | ±3 s/km |
| Easy slow end | Closed-form quadratic solve | 59.0% VO₂max | ±3 s/km |
| Marathon | Newton-Raphson race prediction | 42,195 m | ±0.5 s/km |
| Threshold | Closed-form quadratic solve | 88.0% VO₂max | ±0.5 s/km |
| Interval | Closed-form quadratic solve | 97.3% VO₂max | ±0.5 s/km |
| Repetition | Newton-Raphson race prediction at 3 000 m, then multiply | R-200 = 0.9295 × (200/3000) × t₃ₖ; R-400 = 0.9450 × (400/3000) × t₃ₖ; **R-800 = 2 × R-400** | max \|error\| 1.1 s across VDOT 30–85 (R-035) |
| Fast Repetition | Newton-Raphson race prediction at 800 m, scaled linearly | F-400 = t₈₀₀ / 2; F-200 = t₈₀₀ / 4 | exact match to vdoto2.com (R-035) |

T and I constants are the mean of back-solved percentages across VDOT 30–80 as reported in R-028 (range 87.87%–88.10% and 97.20%–97.42% respectively — both within 0.23 pp). E-range boundaries are less precisely determined and the ±3 s/km target is acknowledged as a deliberate precision-vs-simplicity trade-off.

**R-pace resolution (R-035, 2026-04-15):** R-035 head-to-head tested three formulations against the published Daniels tables across VDOT 30–85. Option B (3K race prediction × distance-specific multipliers from R-028) won with max \|error\| ≤ 1.1 s and RMS 0.53 s with near-zero systematic bias. Option A (mile race prediction + linear scaling, from R-025) showed a consistent −1.2 s fast bias at VDOT 55–65. GoldenCheetah's 105%-of-vVDOT approach drifted up to 3.7 s at the tails due to its quadratic vVDOT polynomial approximation. R-035 also found that Option B's 0.9528 multiplier for R-800 slightly overshoots, while the simpler rule `R-800 = 2 × R-400` is within ±1 s at every anchor point where R-800 is defined — DEC-042 adopts the simpler rule.

**F-pace resolution (R-035, 2026-04-15):** The commonly-cited rule `F = R − 3 s/200 m` is wrong at the tails (at VDOT 30 the R/F gap is ~0.5 s/200 m; at VDOT 60 it is ~4 s/200 m). The authoritative definition per vdoto2.com is that F-pace equals current 800 m race pace, so F is computed via a Newton-Raphson race-prediction solve at 800 m, with linear scaling to sub-distances. Implementation cost: one additional Newton-Raphson solve. Three total (M at 42,195 m, R at 3 000 m, F at 800 m), each converging in 5–10 iterations.

**Implementation scope (single PR):**

1. **New class `PaceZoneCalculator`** implementing `IPaceZoneCalculator` (interface-backed per the future multi-methodology consideration in R-032, even though that research is deferred). Pure functions, singleton DI lifetime.
2. **New helper `DanielsGilbertEquations`** — internal static class exposing `OxygenCost(vMPerMin)`, `FractionalUtilization(tMinutes)`, `SolveVelocityForTargetVo2(target)` (closed-form), and `PredictRaceTime(vdot, distanceMeters)` (Newton-Raphson with GoldenCheetah's initial guess and 1e-3 min convergence). Three Newton-Raphson call sites exist in `PaceZoneCalculator`: 42,195 m for Marathon, 3,000 m for Repetition, 800 m for Fast Repetition. Each converges in 5–10 iterations.
3. **Delete the `SortedDictionary` lookup table** from `PaceCalculator.cs` entirely. R-034 confirms the current table is legally unsafe to keep in any repo that may go public — the VDOT 50–85 row-shift error is circumstantial evidence of manual transcription from the copyrighted 4th edition.
4. **Add five missing race distances** to `VdotCalculator.DistanceMeters`: 1500 m, 1 mile (1609.34 m), 3 km, 2 mile (3218.69 m), 15 km. R-030 identified these as latent defects.
5. **Add input validation guards** to `VdotCalculator.CalculateVdot`: reject race durations outside 3.5–300 minutes and velocities below 50 m/min. R-030's recommendation based on the `F(t) > 1.0` threshold for very short efforts and the negative `VO₂(v)` region for very slow velocities.
6. **Replace `PaceCalculator.EstimateMaxHr(age) = 220 − age` with the Tanaka formula `208 − 0.7·age`**, rounded to nearest integer. R-031 recommendation — Tanaka has 18,712-subject meta-analysis validation while the Fox/Haskell 220−age formula has SEE of ±12 bpm and no scientific basis.
7. **New class `HeartRateZoneCalculator`** (separate from pace calculator per R-031's architectural finding that HR and pace are parallel intensity markers, not coupled derivations). Takes `maxHr` (required) and `restingHr` (optional). Implements Daniels' %HRmax zone bands: E = 65–79%, M = 80–85%, T = 88–92%, I = 98–100%, R = no HR target. Offer Karvonen %HRR as advanced option when resting HR is provided.
8. **Value-object integration with DEC-041 unit system.** `PaceZoneCalculator` returns `Pace` and `PaceRange(Fast, Slow)` directly. Land DEC-041's `Distance`, `Pace`, `PaceRange` value objects in the same PR or in a precursor commit on the same branch. The `PaceRange` `Min/Max` → `Fast/Slow` rename is part of this work per DEC-041.
9. **Equation-derived golden fixture.** Replace the 56-row integrity fence (`PaceCalculatorTableIntegrityTests.cs` on branch `fix/daniels-pace-table`) with a new fixture that asserts every cell is the equation output at that VDOT/zone — the equations are the specification and the fixture is derived from them, not from the book. Test structure: `[Theory]` per integer VDOT 30–85 computing every zone, plus smoothness and monotonicity regression tests. Provenance header per R-034's recipe (cite 1979 monograph, disclaim trademark).
10. **Trademark disclaimer and attribution.** Add a README note acknowledging "VDOT" as a registered trademark of The Run SMART Project, LLC, and disclaiming affiliation with Daniels or the Run SMART Project. Use the term descriptively throughout the codebase.

**Rationale:**

- **Legal safety (R-034).** The current lookup table's row-shift transcription error is circumstantial evidence of manual copying from the copyrighted 4th-edition book. Equation-derived values from the 1979 Daniels-Gilbert monograph (public-domain mathematical formulas, explicitly excluded from copyright under 17 U.S.C. § 102(b), further supported by Feist v. Rural Telephone for uncopyrightability of mathematical facts) eliminate all meaningful infringement risk. This reason alone forces the rewrite before any public release.
- **Correctness (R-028 + R-030).** Back-solving T and I constants from book values yields 87.87%–88.10% and 97.20%–97.42% respectively — pp variation well within noise. Equation-computed values reproduce published table cells within ±0.5 s/km for T and I, and match the race-time-to-VDOT table within ±0.1 VDOT for forward VDOT calculation. `VdotCalculator` is already verified correct.
- **Extensibility (R-029).** Equation-based computation is continuous by construction and handles any VDOT without interpolation. The official vdoto2.com calculator already outputs decimal VDOT values, confirming this is the first-party-endorsed semantic. Adding new zones (F, Fast Reps) or new race distances is a one-line change.
- **Auditability (R-025, R-026).** Equations are the specification. The fixture test re-derives every cell on every build. Any future drift from coefficient edits, constant changes, or computation-layer bugs fails loudly. No room for transcription-error class defects to return.
- **Precedent (R-034).** Every serious open-source implementation — GoldenCheetah, tlgs/vdot, vdot-calculator on PyPI — uses the pure-equation approach. Projects that committed transcribed tables (ericgio/vdot, daniels-calculator) are legally exposed. This is a solved design pattern in the community.

**VDOT-range caveats (R-035):** Two model-domain limitations worth locking into code and tests.

- **VDOT < 39:** Daniels' Run SMART Project revised low-VDOT training paces in August 2019 "for greater accuracy." The R-028 multipliers (0.9295 / 0.9450) come from pre-2019 tables, so R-pace below VDOT 39 may differ from the current vdoto2.com calculator by slightly more than 1 s. Still acceptable as an MVP-0 precision target; log as a known limitation in the fixture header.
- **VDOT < ~25:** The Daniels-Gilbert fractional-utilization curve itself produces non-sensical outputs below roughly VDOT 25 per community reports about vdoto2.com. This is a limit of the underlying model (fit to competitive athletes), not our implementation. DEC-042's input guards should clamp or warn accordingly.

**Deferred from DEC-042 scope:**

- R-032 (multi-methodology interface extensibility) — the interface-backed `IPaceZoneCalculator` name is chosen proactively to accommodate Hanson, 80/20, or polarized zones later, but only a concrete `DanielsPaceZoneCalculator` implementation is in scope for this PR. Future methodologies can be added as sibling implementations.
- R-033 (LLM pace-zone consumption precision) — informs whether paces should be numeric, narrative, or categorical in LLM prompts. Not blocking DEC-042 correctness; the calculator still returns numeric `Pace` objects and the prompt layer can render them however needed.
- Temperature / altitude adjustments from Daniels' environmental appendix — out of scope for MVP-0, deferred to post-MVP-0.
- Fast Repetition (F) zone is **in scope and fully specified** (R-035 resolved the derivation as a Newton-Raphson solve at 800 m — see the zone table above).

**Why not each alternative:**

- **Pattern 2 (equation-verified lookup table):** Keep the table, add a test that re-derives every cell. Correctness improves, but we inherit copyright exposure from the committed table values and gain no extensibility. R-034 disqualifies this.
- **Pattern 3 (golden fixture only, no lookup):** This *is* what DEC-042 uses for tests, but the test fixture is derived from our own equations, not the book. "Pattern 3 as runtime source" — commit a CSV and load it at startup — fails the extensibility and legal tests the same way Pattern 2 does.
- **Pattern 4 (library dependency):** No .NET package exists (confirmed R-025, R-026). tlgs/vdot and vdot-calculator on PyPI are Python; porting is trivial but we lose the value of `using` a library. No viable option.
- **Pattern 5 (port tlgs/vdot or similar):** R-025 noted tlgs/vdot does not implement R-pace as race prediction — its approach is incomplete for our purposes. Porting would import incomplete logic and add attribution overhead with no correctness win.

**Branch disposition:** The in-progress integrity fence on `fix/daniels-pace-table` (`PaceCalculatorTableIntegrityTests.cs` with 56 book-derived snapshot rows) is **not to be committed to main**. R-034 flags the snapshot values as legally unsafe, and the fence is technically obsolete once DEC-042 lands. The branch will be discarded (or rebased to just the equation-derived fixture) when the DEC-042 rewrite starts. The local fence remains useful during the interim as a reference for what anomalies the rewrite must fix.

**Effort estimate:** Single PR, roughly 350–550 net LOC. Core calculator and equations ~180 LOC (three Newton-Raphson call sites plus the closed-form solves), tests ~220 LOC (equation-derived fixture + structural invariants + VDOT-range boundary tests), HR calculator ~100 LOC, supporting changes (distances, guards, Tanaka, README) ~50 LOC. Risk is low; every decision is grounded in cited research and every alternative has been evaluated.

**Implementation addendum (2026-04-17):** The initial `PredictRaceTimeMinutes` Newton-Raphson solver rooted `F·VO₂ = index` instead of the Daniels relation `VO₂/F = index`. The bug was masked because (a) R-pace at 3,000 m converges at `F(t) ≈ 0.997`, where multiply and divide agree to within 1 s/km, and (b) test profiles flowed through a lookup-table bridge (`TestPaceCalculator`) carrying correct Daniels values, so cached eval prompts were unaffected. The bug surfaced at marathon distance (M-pace at index 50 yielded 2:19 instead of the expected 3:10:49) and at 800 m (F-pace slower than R-pace, inverted ordering). The fix flipped the root condition to `VO₂/F − index` and rewrote the derivative using the quotient rule. The 56-row equation-anchored fixture in `PaceZoneCalculatorTests` was regenerated from the corrected solver; `Monotonicity_AllZonesOrderedFromSlowToFast` and `MPacePrecision_WithinPublishedTableTolerance` are now enforced rather than Skip'd. No spec revision required — batch-12a, 12c, and 13 had already specified `VDOT = VO₂/F` explicitly.

Additionally, `MesoWeekOutput` structured output was restructured from a `Days: MesoDaySlotOutput[]` array to seven required `Sunday..Saturday` properties with the `DayOfWeek: int` field removed. Anthropic's constrained decoding enforces property names and `additionalProperties: false` but does not enforce array length or scalar range — and Priya's `MaxRunDaysPerWeek: 4` constraint was triggering the model to emit 8-day arrays with placeholder entries ~67% of the time. Named day properties make the seven-day invariant structural rather than an un-enforced schema-description hint. The `Priya_Constrained_RespectsExactly4RunDays` eval is un-Skip'd and passes deterministically across three consecutive Record-mode runs.

---

## DEC-043: OSS Quality Tooling Restoration

**Date:** 2026-04-15
**Status:** Final
**Category:** Quality pipeline / project governance
**Informed by:** R-036 (`batch-14a-coderabbit-integration.md`), R-037 (`batch-14b-codeql-integration.md`), R-038 (`batch-14c-sonarqube-cloud-integration.md`), R-039 (`batch-14d-snyk-reevaluation.md`), R-040 (`batch-14e-codacy-reevaluation.md`), R-041 (`batch-14f-branch-protection.md`), R-042 (`batch-14g-license-trademark-attribution.md`), R-043 (`batch-14h-license-compliance-ci.md`).
**Supersedes:** DEC-034's 2026-03-19 private-repo amendment (which stripped CodeRabbit, CodeQL, SonarQube Cloud, and branch protection from the pipeline).
**Scope boundary:** DEC-042 (pace-calculator rewrite) — the internal VDOT code-identifier rename was added to DEC-042's scope as part of this restoration rather than being a separate pass.

**Decision:** Restore the five-layer quality pipeline that DEC-034 originally designed, minus the permanently-cut Claude Code GitHub Action slot, now that the repo is public. Close the legal gaps (LICENSE, trademark, attribution) the public flip exposed. Partition every quality signal to a single authority. Encode the new conventions durably in CLAUDE.md and REVIEW.md so future AI sessions cannot re-open settled questions.

**Ten decisions enacted:**

1. **Dual-license architecture.** Apache-2.0 for all code; CC-BY-NC-SA-4.0 for coaching prompt YAML files under `backend/src/RunCoach.Api/Prompts/`. Distinct LICENSE files at each boundary.
2. **VDOT trademark avoidance on user-facing surface.** Rename all user-facing references from "VDOT" to "Daniels-Gilbert zones" or "pace-zone index" per the Runalyze enforcement precedent (The Run SMART Project LLC). Internal code identifiers deferred to DEC-042.
3. **CodeRabbit restore.** `.coderabbit.yaml` schema v2, profile=chill, module-scoped path_instructions, coaching-prompt no-touch rule, silenced on Dependabot PRs.
4. **CodeQL restore.** `github/codeql-action` v4.35.1, `security-extended` queries (not `security-and-quality`), matrix over [csharp, javascript-typescript], C# build-mode=manual, Prompts excluded from analysis.
5. **SonarQube Cloud restore.** Two-project monorepo pattern (runcoach-backend, runcoach-frontend) under org `leehopper`, dotnet-sonarscanner v11.2.1 for backend (OpenCover coverage — Cobertura not supported for C#), sonarqube-scan-action v7.1.0 for frontend (LCOV). Build-time analyzers untouched — Sonar is advisory dashboard, not compile-time gate. SonarQube Cloud GitHub App posts separate advisory quality-gate checks (`[project] SonarCloud Code Analysis`) alongside the CI workflow checks.
6. **License-compliance CI.** `actions/dependency-review-action` v4.9.0 as PR gate with `allow-licenses` allowlist (fail-closed; `deny-licenses` deprecated in v4, migrated proactively). `anchore/sbom-action` v0.24.0 for weekly SBOM (SPDX 2.3 + CycloneDX 1.6). GitHub Automatic Dependency Submission for NuGet enabled (CPM `Directory.Packages.props` not parsed by static graph). LGPL-3.0 CI tools (sonarqube-scan-action) exempted via `allow-dependencies-licenses` purl.
7. **Branch protection via repository ruleset.** Squash-only merge + linear history (required_signatures removed — GitHub web UI signs squash commits automatically via web-flow GPG key, making the rule redundant and blocking for solo dev). Six required checks: `CI Gate`, `Analyze (csharp)`, `Analyze (javascript-typescript)`, `Backend analysis`, `Frontend analysis`, `License & dependency review`. Admin bypass "for pull requests only" with 24-hour issue follow-up.
8. **One-authority-per-signal partitioning.** CodeQL = first-party SAST, Codecov = coverage via Cobertura, SonarQube Cloud = dashboard via OpenCover, dependency-review-action = license + CVE gate.
9. **SHA-pinned GitHub Actions.** Every `uses:` line has a 40-character commit SHA plus `# vX.Y.Z` comment. Dependabot automates version bumps.
10. **Convention encoding.** VDOT-avoidance rule and quality-pipeline authority map in all CLAUDE.md and REVIEW.md files.

**Deferred with reconsider-triggers:**

**Snyk** (R-039): Unique value is `@snyk/protect` transitive-patching for npm and proprietary-DB ~47-day CVE lead. Not needed while: (a) no PII ingestion, (b) no container deployments, (c) single maintainer can eyeball Dependabot, (d) no Dependabot miss on a high-severity CVE. Reconsider if any of these four triggers fire.

**Codacy** (R-040): Zero residual value confirmed. Ships the same SonarAnalyzer.CSharp NuGet and the same eslint-plugin-sonarjs bundle the project already runs. SonarQube Cloud replicates its dashboard with deeper metrics. Reconsider only if a language module (Python, Rust) is added outside SonarQube Cloud's free-tier coverage.

**CODEOWNERS file:** Deferred until the first external contributor joins (same trigger as Snyk reconsider #3).

**Permanently cut:**

**Claude Code GitHub Action.** Replaced by local `/review-pr` via Max + the user's deep-review skill. No `@claude` mentions on PRs. Do not re-propose.

**Cross-references:**

- DEC-042 scope expanded to include internal VDOT code-identifier rename (`VdotCalculator` → `PaceZoneIndexCalculator`, field/variable/test renames).
- `docs/ci/unblock-procedures.md` documents the ruleset shape, emergency bypass, and eval-cache re-record workflow.
- Spec: `docs/specs/09-spec-oss-tooling-restoration/09-spec-oss-tooling-restoration.md`.

---

## DEC-044: Browser auth uses ASP.NET Core Identity application cookie, not JWT

**Date:** 2026-04-19
**Status:** Final
**Category:** Authentication architecture
**Informed by:** R-044 (`batch-15a-spa-jwt-storage-refresh.md`), R-045 (`batch-15b-mapidentityapi-vs-custom-controller.md`).
**Supersedes:** Slice 0 cycle-plan pragmatic-default ("long-lived ~30d JWT in browser-side storage, no refresh token, bearer-header pattern") and the requirements doc's "JWT-based authentication (stateless)" framing.

**Decision:** RunCoach's web SPA authenticates against the .NET 10 API via the **ASP.NET Core Identity application cookie**, not a JWT. The cookie is `__Host-RunCoach`, `HttpOnly`, `Secure`, `SameSite=Lax`, with 14-day sliding expiration. Anti-CSRF is mitigated by ASP.NET Core's built-in `UseAntiforgery()` middleware exposing an `XSRF-TOKEN` cookie that the SPA echoes in an `X-XSRF-TOKEN` header on state-changing requests. **No token of any kind is stored in JavaScript-accessible storage** (no localStorage, no sessionStorage, no in-memory token in Redux). `AddJwtBearer` is registered as a non-default opt-in scheme reserved for the future iOS shim; a `CookieOrBearer` authorization policy is wired on day one so iOS-bearer support lands additively. Auth endpoints live in a hand-rolled `Modules/Identity/AuthController.cs`; `MapIdentityApi<TUser>()` is NOT used.

**Rationale:**

- **OWASP / IETF / Microsoft 2026 consensus.** OWASP ASVS v5 (May 2025) fails any verification that puts sensitive secrets in Web Storage. The IETF `draft-ietf-oauth-browser-based-apps-26` (December 2025) explicitly carves a same-origin SPA + API out of OAuth scope and directs such apps to maintain authentication state via a traditional session. Microsoft's .NET 10 Learn article "Use Identity to secure a Web API backend for SPAs" recommends cookies verbatim. Practitioner consensus (Philippe De Ryck, Duende, Curity) has moved firmly against JS-accessible token storage since 2023.
- **Lowest XSS exposure** of the patterns evaluated. A cookie cannot be exfiltrated for offline abuse; XSS can still abuse the live session, but the damage is bounded to tab lifetime rather than weeks of harvested-token reuse.
- **Operational simplicity.** Sliding expiration on the cookie renews the session per request, so 401 means "logged out" rather than "access token stale." No `baseQueryWithReauth` mutex, no refresh-storm coordination across tabs, no `BroadcastChannel` requirement, no refresh-token rotation table, no reuse-detection-revokes-family logic.
- **iOS-future preserves cleanly.** The dual-scheme `CookieOrBearer` policy is declared in Slice 0; when DEC-033's iOS shim lands, it adds bearer endpoints (`POST /api/v1/auth/login-bearer`, `POST /api/v1/auth/refresh-bearer`) without touching the React SPA or any business controller. The cookie-now / bearer-later split costs ~2 days of additional server work; the localStorage-now / cookie-later flip would have cost ~1 day of client rewrite plus full re-test of every protected route.
- **Stack already is a BFF.** ASP.NET Core Identity holds the session, the .NET API serves the resources, both are in the same process — every BFF security property is captured without a separate proxy project.
- **Custom AuthController over `MapIdentityApi`.** Four hard limits per R-045: (1) `MapIdentityApi` issues an opaque `IDataProtector` blob, not a JWT — incompatible with the iOS-bearer future and with sharing tokens across services that don't share the DataProtection key ring; (2) `RegisterRequest` is sealed, blocking atomic `UserProfile` creation in Slice 1; (3) endpoints can't be selectively excluded — `/refresh`, `/forgotPassword`, `/confirmEmail` always mount even though all three are deferred; (4) `/manage/info` returns only email + isEmailConfirmed, not the DB-enriched `/me` shape RunCoach needs. Microsoft's own reference architecture (`dotnet/eShop`) skips `MapIdentityApi`; David Fowler stated publicly it is "not ideal" for SPA setups. Custom controller is ~200 LOC, preserves the module-first pattern, and avoids a likely later migration.

**Alternatives considered (and rejected):**

- **(a) localStorage + bearer header (cycle-plan pragmatic default).** Catastrophic XSS exposure; OWASP-flagged; forces refresh-token machinery later and a client rewrite. Rejected.
- **(c) In-memory access + httpOnly refresh-token cookie ("BFF-lite").** Ranked least secure of the three IETF browser-app architectures; multi-tab requires real distributed-systems-lite engineering; F5 shows a 200–500 ms flash of unauth; provides no benefit over the Identity cookie for a same-origin SPA. Rejected.
- **(d) Full BFF (separate YARP proxy, opaque session).** Endorsed by IETF for cross-origin OAuth scenarios but adds a separate project with no security benefit over the Identity-cookie path for a same-origin stack. Deferred — adopt only if the SPA later moves to a CDN edge in a different administrative zone from the API.
- **`MapIdentityApi<TUser>()` for the auth endpoints.** Rejected per the four hard limits above. May be reintroduced under a distinct prefix as a scaffold when password-reset / email-confirmation slices land (`MapGroup("/api/v1/auth/identity").MapIdentityApi<ApplicationUser>()` per R-045's rollback path).
- **DPoP / sender-constrained tokens, passkeys.** Additive hardening, not a session-storage substitute. Defer; passkeys are a public-beta differentiator candidate.

**Cross-references:**

- Spec: `docs/specs/12-spec-slice-0-foundation/` (Technical Considerations § Auth wiring captures the wiring-sketch detail).
- Research artifacts: `docs/research/artifacts/batch-15a-spa-jwt-storage-refresh.md`, `docs/research/artifacts/batch-15b-mapidentityapi-vs-custom-controller.md`.
- Operational foot-gun: ASP.NET Core Data Protection keys MUST persist across container rebuilds — mount a named Docker volume to `/keys` (covered in spec). Without this, every container rebuild logs everyone out.
- Related: DEC-033 (client-agnostic API for future iOS support) is what made the dual-scheme `CookieOrBearer` policy a Slice 0 concern rather than a future retrofit.

---

## DEC-045: Defer .NET Aspire to MVP-1; stay on Docker Compose + Tilt for MVP-0

**Date:** 2026-04-19
**Status:** Final
**Category:** Local-dev orchestration / deployment trajectory
**Informed by:** R-050 (`batch-16c-dotnet-aspire-vs-compose.md`).
**Deepens:** DEC-032 (Docker Compose + Tilt for local dev, K8s deferred to public beta) — re-validates the original choice under updated information (Aspire 13.2 maturity).

**Decision:** RunCoach stays on Docker Compose + Tilt for MVP-0 local dev orchestration, **conditionally adopting .NET Aspire at MVP-1** if and only if specific triggers fire: (a) MVP-1 commits to Azure Container Apps as the deploy target, (b) a second .NET service or genuine cross-service tracing need exists, or (c) JasperFx ships first-class `Aspire.Hosting.Marten` / `Aspire.Hosting.Wolverine` packages. Until then, the March-2026 DEC-032 commitment to Compose + Tilt stands. To close the one observability gap that Aspire would have provided for free, Slice 0 adds an opt-in `docker-compose.otel.yml` overlay (OTel Collector + Jaeger) and wires Marten's `TrackConnections` + `TrackEventCounters` plus `"Marten"`/`"Wolverine"`/`"RunCoach.Llm"` ActivitySource/Meter sources. This work is fully transferable to Aspire later.

**Rationale:**

- **Seven of seven decision-trigger dimensions point at defer.** Service count (1 API + SPA + Postgres, below the 4+ Aspire threshold), team size (solo, below 3+), deployment target (uncommitted, neutral), tracing (marginal, addressable in Compose), dev/prod parity (strongly desired — anti-signal because Aspire is dev-only and prod is codegen), stack composition (mixed .NET + JS — neutral), existing orchestration (Compose + Tilt committed — anti-signal). Marginal cases don't flip independently.
- **JasperFx (Marten + Wolverine maintainer) is publicly negative on Aspire integration.** Jeremy Miller, January 2025: *"Aspire doesn't really play nicely with frameworks like Wolverine right now. My strong preference right now is to just use Docker Compose for local development."* `wolverine#635` (Aspire support) was closed `wontfix`. There is no `Aspire.Hosting.Marten` or `Aspire.Hosting.Wolverine` package on NuGet in April 2026.
- **The strategic unlock at Aspire 13.2 is the stable Docker Compose publisher.** A future Aspire pivot at MVP-1 does not force a cloud-target choice — Aspire can publish a deployable `docker-compose.yaml` for a VPS, Railway, Coolify, etc. The "decide later" window is preserved.
- **The DataProtection `/keys` volume was the largest pivot-cost driver in R-050's analysis.** R-049's Postgres-backed DataProtection answer (DEC-046) eliminates that friction entirely — making the future-pivot scope smaller, not larger.
- **Aspire 13.x has no LTS** (Modern Lifecycle, only the latest minor supported); .NET 10 itself is LTS through Nov 2028. Adopting Aspire commits the project to a quarterly minor-version upgrade treadmill independent of the .NET LTS cadence — non-trivial overhead for a single-developer project.
- **Aspire's most defensible MVP-0 win is the OTel dashboard for Marten observability.** That win is reproducible in Compose with an OTel Collector + Jaeger overlay for ~half a day of work, fully transferable to Aspire later. Adopting Aspire just for the dashboard is disproportionate.

**Alternatives considered (and rejected):**

- **Pivot now during Slice 0.** Estimated 15–25 hours, dominated by the DataProtection variance (4–12 hours) which is now moot post-DEC-046, and by AppHost + ServiceDefaults + fixture rework. Would commit cycles before any hosted-target choice exists — exactly the kind of decision Aspire is meant to inform.
- **Pivot at the start of MVP-1 unconditionally.** Reasonable but couples the decision to a release boundary rather than to actual triggers. Conditional adoption captures the same upside without the commitment.
- **Adopt Aspire AND keep Compose for prod.** Aspire's value is unified dev + publish; running both is the worst of both.
- **Replace Tilt with `tilt up` alternatives (Skaffold, Garden).** Out of scope; Tilt is working and the question is Aspire vs not, not Tilt vs alternatives.

**Cross-references:**

- DEC-032 — original Compose + Tilt + K8s-deferred decision (March 2026); deepened, not superseded.
- DEC-046 — secrets management decision that eliminates the DataProtection volume friction R-050 flagged.
- Spec: `docs/specs/12-spec-slice-0-foundation/` (Slice 0 spec amendment for the OTel Collector + Jaeger overlay).
- Standing re-trigger conditions documented in `ROADMAP.md` § Deferred Items > Infrastructure.
- Pin guidance: `Aspire.AppHost.Sdk` 13.2.2 if/when adopted; avoid 13.2.0 (IDE-execution regressions); upgrade quarterly.

---

## DEC-046: Production secrets management — SOPS + age + Postgres-backed DataProtection + dotnet user-secrets

**Date:** 2026-04-19
**Status:** Final
**Category:** Secrets management / persistence / DataProtection
**Informed by:** R-049 (`batch-16b-secrets-management-net10.md`).
**Supersedes:** Slice 0 spec's `/keys` Docker volume mount for DataProtection key persistence.

**Decision:** RunCoach uses a four-layer secrets-management approach across the dev → CI → MVP-1 hosted → pre-public-release trajectory:

1. **DataProtection master key:** `PersistKeysToDbContext<DpKeysContext>` (the `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` provider) backed by the same Postgres RunCoach already runs for Marten and EF Core relational state. Inherits Postgres's backup, disk encryption, connection-string bootstrap, and multi-instance consistency at zero new-infrastructure cost. `SetDefaultKeyLifetime(TimeSpan.FromDays(90))` for built-in rotation. `ProtectKeysWithCertificate(...)` lands once the project leaves dev (deferred to MVP-1). **The `/keys` Docker volume from the Slice 0 spec is removed** — it was a single-instance-only solution and Postgres-backed wins on every axis.
2. **Local dev:** `dotnet user-secrets` (unchanged). Stays the lowest-friction inner-loop option; integrates via `AddUserSecrets()` automatically in Development.
3. **Shared / CI / MVP-1 hosted:** SOPS-encrypted YAML committed to the repo (`secrets/<env>.enc.yaml`), with the age key stored as `secrets.SOPS_AGE_KEY` in GitHub and decrypted at deploy time via `getsops/sops-install` + `sops -d`. SOPS + age was selected over Doppler / Infisical / self-hosted Vault because it has no runtime dependency, the encrypted file IS the commit, and the project is public OSS — the encrypted-on-commit posture is ideal.
4. **Pre-public-release (FTC HBNR escalation point):** migrate to cloud-native KMS (Azure Key Vault + Managed Identity preferred; AWS Secrets Manager equivalent). Wrap DataProtection keys with `ProtectKeysWithAzureKeyVault` (or equivalent) keeping the Postgres `DpKeysContext` as a fallback for a drain window. Adopt dynamic Postgres credentials via Managed Identity token provider. The migration is a single config block, not a rewrite.

**Operational specifics:**

- **GitHub Actions hygiene:** use `pull_request` (NOT `pull_request_target`) for PR validation so fork PRs never see `secrets.*`. Use a `workflow_run`-after-`pull_request` pattern for tests that need the eval-cache record key. Place the Anthropic API key in a GitHub Environment named `eval` with required-reviewer protection. SHA-pin every third-party action (tj-actions/changed-files CVE-2025-30066 precedent — every version tag `v1..v46` retagged to a malicious commit; hash-pinned consumers were unaffected). Required-permissions block: `permissions: { id-token: write, contents: read }`.
- **Wolverine outbox + Postgres password rotation:** use the `NpgsqlDataSource` overload for `PersistMessagesWithPostgresql` (issue `wolverine#691`, closed). Pair with `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider(...)` so the durability agent survives DB-credential rotation without a restart. **Without this, password rotation manifests as `28P01` errors until restart.**
- **Bootstrap-credential by deploy target:** single VPS uses `systemd-creds encrypt` + `LoadCredentialEncrypted=` + Compose `secrets:` `file:`; PaaS (Render / Fly / Railway) uses native secret injection; Azure Container Apps uses Key Vault references with user-assigned Managed Identity; Kubernetes uses ServiceAccount token projection.
- **Rotation cadences (per OWASP / NIST):** DataProtection master 90 days (framework default); JWT signing key ≤ 6 months; Postgres role passwords 90 days static or short-lived dynamic; Anthropic / third-party API keys 90 days; OAuth client secrets 6 months. Mean-time-to-rotate on suspected compromise: ≤ 1 hour for high-impact, ≤ 24 hours for medium, ≤ 72 hours for the rest. **Rotation runbooks are written in MVP-0, not when something leaks in MVP-1.**

**Rationale:**

- **The DataProtection-key location is the single load-bearing secrets decision.** Losing the key invalidates every Identity cookie and every antiforgery token on every restart. `PersistKeysToDbContext<T>` paired with Marten's Postgres is materially better than file-system-plus-volume — eliminates the named-volume dependency, makes container rebuilds safe, and is a literal six-line `Program.cs` change.
- **SOPS + age has no runtime dependency.** For a public OSS solo-dev project, the encrypted-file-IS-the-commit posture beats SaaS secret managers (Doppler, Infisical) and self-hosted Vault. Free, CNCF-sandbox-backed, easy rotation by re-encrypting.
- **HCP Vault Secrets is disqualified.** HashiCorp announced end-of-sale June 30 2025 and full EOL July 1 2026.
- **FTC HBNR applicability is not optional.** The July 2024 amendment explicitly covers fitness apps that ingest from Apple Health / Strava / Garmin ("multiple sources" prong). RunCoach is a PHR vendor under the Rule before it ships; the pre-public-release escalation is a regulatory event, not a marketing one. The 2023 enforcement trio (GoodRx $1.5M, BetterHelp $7.8M, Premom $200K) define what "reasonable security" means in practice.
- **OWASP ASVS v5.0.0 V13.3 (May 2025)** L1 is met by software vaults including SOPS + age + Postgres-DP — no HSM required until L3. The recommended combination is compliant today.
- **Migration map is additive at every step.** MVP-0 dev → MVP-1 VPS → PaaS / ACA → pre-public-release escalation each adds a config block, never a rewrite.

**Alternatives considered (and rejected):**

- **`/keys` Docker volume (original Slice 0 spec).** Single-instance only; doesn't survive the multi-instance MVP-1 trajectory. Eliminated.
- **Self-hosted HashiCorp Vault.** Operationally disproportionate for one developer (unseal, Raft backups, upgrades, policy management). 3–5 hours/month vs SOPS's ~0.5 hours/month.
- **Doppler.** No first-party .NET configuration provider in 2026 — only the `doppler run` CLI wrapper. Commits to a SaaS dependency at runtime.
- **HCP Vault Secrets.** Disqualified by EOL.
- **Sealed Secrets.** Kubernetes-only; not a candidate until/unless RunCoach runs K8s.
- **`PersistKeysToStackExchangeRedis`** for DataProtection. Fine for multi-instance, but Redis persistence must be explicitly turned on or the keys vanish on restart — canonical production foot-gun. RunCoach doesn't run Redis; not adding a service for DataProtection alone.
- **`PersistKeysToAzureBlobStorage` + `ProtectKeysWithAzureKeyVault` from day one.** Microsoft-blessed pattern but commits to Azure before any deployment exists. Deferred to pre-public-release as the natural escalation target.

**Cross-references:**

- DEC-044 — cookie-not-JWT browser auth that made the DataProtection key load-bearing.
- DEC-045 — Aspire deferral; the `/keys` volume removal eliminates the largest Aspire-pivot-cost driver.
- Spec: `docs/specs/12-spec-slice-0-foundation/` § Persistence Foundation (DataProtection wiring) and § Security Considerations (SOPS handoff for shared/CI secrets, rotation procedures).
- Pre-public-release escalation tracked in `ROADMAP.md` § Deferred Items.
- DEC-020 — FTC HBNR compliance from day one; this decision provides the secrets-handling foundation for that compliance work.

---

## DEC-047: Onboarding state — Marten event stream + EF projection (pattern d), deterministic stream id, hybrid LLM+controller

**Date:** 2026-04-19
**Status:** Final
**Category:** Conversation state / event sourcing / Slice 1 architecture
**Informed by:** R-048 (`batch-16a-onboarding-conversation-state.md`).

**Decision:** Slice 1 multi-turn LLM onboarding uses **pattern (d)**: a dedicated Marten event stream per user for onboarding (`onboarding-{DeterministicGuid(userId, "onboarding")}`), with a Marten inline `SingleStreamProjection<OnboardingView, Guid>` for the in-flight read model and a separate `EfCoreSingleStreamProjection<UserProfile, AppDbContext>` (via `Marten.EntityFrameworkCore`) materializing user-facing fields into the EF `UserProfile` row in the same transaction. Onboarding events live in **a separate stream from the Plan stream** — they are never commingled. On `OnboardingCompleted`, a Wolverine event subscription opens a fresh Plan stream with `CombGuidIdGeneration.NewGuid()` and stores `CurrentPlanId` on the EF `UserProfile`.

**"Next question" ownership is hybrid, deterministic-led**: a static topic list (`PrimaryGoal`, `TargetEvent`, `CurrentFitness`, `WeeklySchedule`, `InjuryHistory`, `Preferences`) controls slot order; the LLM phrases questions, handles follow-ups, and extracts structured answers via Anthropic structured outputs. **Completion is a deterministic gate** (all required fields present, all validate, no outstanding `needs_clarification` flag) with an LLM ambiguity pre-check (per-turn `needs_clarification: bool`, final `ready_for_plan` structured output).

**Anthropic prompt caching is enabled from day one** with `cache_control: { type: "ephemeral", ttl: "1h" }` at the top of the request body and a second explicit breakpoint on the system prompt. The event log carries **typed Anthropic content blocks verbatim** in `UserTurnRecorded.ContentBlocks` and `AssistantTurnRecorded.ContentBlocks` (including any future `thinking` / `tool_use` / `tool_result` / `signature` fields), serialized via `System.Text.Json` with declared property-order records — **not `Dictionary<string,object>`** — to guarantee byte-stable replay and cache-prefix stability.

**Rationale:**

- **Pattern (d) is the only candidate with no ❌ and no ⚠ on any dimension Slice 1's acceptance criteria require.** Resumability, multi-tab (stream version), cross-device handoff, idempotency on LLM retry, replay audit ("what did the AI know?"), re-derivable `UserProfile`, GDPR erasability via `store.Advanced.DeleteAllTenantDataAsync(userId.ToString())`, `ContextAssembler` integration, prompt-cache friendliness, alignment with the existing Plan aggregate, clean seam for Slice 4's `ConversationTurn`.
- **The decisive new argument is Anthropic's prompt-cache mechanism.** Caching is a pure prefix-hash mechanism — cache hits cost 0.1× base input; cache writes cost 1.25× (5-min TTL) or 2× (1-h TTL). On a realistic MVP-0 workload (50 users × 30 turns × Sonnet 4.5, ~8k input tokens/turn), caching drops cost from ~$43 to ~$13 — ~70% savings for one line of configuration. Caching only helps if `messages[]` reconstructs byte-identically turn after turn. **An append-only event log with a deterministic projection function guarantees this**; a mutable snapshot does not. When tool use or extended thinking lands later, Anthropic requires verbatim echo of `tool_use` / `tool_result` / `thinking` blocks including `signature` fields — the event log where each event carries the typed content-block JSON is the storage model that matches without special cases.
- **Onboarding events go in a SEPARATE stream from Plan events.** Marten's documented idiom is one-stream-per-aggregate-instance; `SingleStreamProjection<TDoc, TId>` assumes this. Commingling would force both projections to event-filter, block future stream-type snapshots, and confuse archival.
- **Stream id is `DeterministicGuid(userId, "onboarding")` (UUID-v5 shape).** Onboarding is 1:1 with the user; `StartStream<Onboarding>(deterministicId, ...)` becomes naturally idempotent — a retry hits a primary-key violation handled as "already started." Plan is 1:many per user, so it uses `CombGuidIdGeneration.NewGuid()` per plan.
- **Hybrid deterministic-led "next question."** Anthropic's *Building effective agents* explicitly recommends workflows over agents for fixed-schema tasks. The six topics are textbook workflow territory; LangGraph, Vercel AI SDK 6's `ToolLoopAgent`, and every production intake bot examined converge on this pattern. Code picks the slot, model phrases the question, structured output extracts, code advances.
- **Deterministic completion gate.** Completion triggers a significant, user-visible action (full plan generation). LLM-judged completion is too uncertain. The LLM surfaces ambiguity *before* the gate via `needs_clarification` and `ready_for_plan`; the gate itself is code.
- **Re-trigger plan generation from settings is supported trivially.** The settings handler reads `UserProfile` (or `OnboardingView`), optionally accepts a `RegenerationIntent`, calls `ContextAssembler.ComposeForPlanGeneration(userId, intent)`, and starts a new Plan stream. No onboarding re-run required. If the user wants to *edit* specific answers, that's a separate `ReviseAnswer(Topic, NewValue)` command appending `AnswerCaptured` to the existing onboarding stream — preserving audit.
- **GDPR erasability via Marten's tenant wipe.** `store.Advanced.DeleteAllTenantDataAsync(userId.ToString(), ct)` wipes every stream, every event, and every Marten-owned projection doc for that tenant in one call. Pair with EF `UserProfile` delete; done. Discipline: keep PII out of event payloads where feasible (`AnswerCaptured { Topic, NormalizedValue }`, not free-text user messages); use Marten's `AddMaskingRuleForProtectedInformation<T>` and `ApplyEventDataMasking()` where embedding PII is unavoidable.
- **Slice 4's `ConversationTurn` is intentionally simpler.** Open-conversation chat is free-form, has no completion, has no structural projection requirement. EF append-only rows match Vercel AI SDK's `Message_v2` shape and are the mainstream pattern. `ContextAssembler` absorbs the difference between event-sourced onboarding and EF-backed open chat. Slice 1's choice does not constrain Slice 4.

**Alternatives considered (and rejected):**

- **(a) EF column on `UserProfile`** — destroys audit on every edit; fights prompt-cache (every snapshot reconstruction changes the prefix hash). Rejected.
- **(b) Dedicated `OnboardingSession` EF table** — pattern (a) with better isolation; same core weaknesses (no replay, no audit, fights caching unless you reinvent pattern (d) in EF). Rejected.
- **(c) Pure Marten with `UserProfile` purely derived** — wins on audit/replay but forces ASP.NET Identity's `UserProfile` through a Marten projection only, colliding with Identity-touching code paths and complicating GDPR. Pattern (d) preserves the EF `UserProfile` as authoritative AND gets the event log. Rejected.
- **(e) Client-side accumulation** — excluded by DEC-044 (SPA is untrusted, refresh-prone) and by multi-device resume requirements. Rejected.
- **(f) Per-property hybrid** — pattern (d) IS a hybrid (events in Marten, derived state in EF). No coherent case for splitting which answers go event-sourced vs column-mutated.

**Cross-references:**

- R-047 — Marten patterns landed in Batch 15; this decision applies the same pattern shape (per-user stream, Conjoined tenancy, Inline projection) to onboarding.
- DEC-013 — original event-sourced plan state decision; this extends the pattern to onboarding rather than introducing a new architecture.
- Slice 1 requirements: `docs/plans/mvp-0-cycle/slice-1-onboarding.md` (updated with R-048 integration notes; several previously-open items now resolved).
- Spec home (when written): `docs/specs/{NN}-spec-slice-1-onboarding/`.
- `Marten.EntityFrameworkCore` is the 2025–2026 first-class path for projecting events into EF entities; pin alongside Marten ≥ 8.20.
- First-party `Anthropic` NuGet (v12.x as of April 2026) implements `Microsoft.Extensions.AI.IChatClient` — `ICoachingLlm` adapter sits over either the raw Anthropic client or M.E.AI with a config switch.

---

## DEC-048: Marten `IntegrateWithWolverine()` is the sole envelope-storage wiring path; Wolverine's `PersistMessagesWithPostgresql` is prohibited

**Date:** 2026-04-20
**Status:** Final
**Category:** Persistence / host composition / Slice 0 substrate
**Informed by:** R-054 (`batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md`).

**Decision:** In every RunCoach project that registers both Marten and Wolverine against the same Postgres, the Wolverine envelope-storage tables shall be installed by **Marten's `.IntegrateWithWolverine()`** alone. `opts.PersistMessagesWithPostgresql(...)` inside `UseWolverine` is **prohibited** — the two call sites double-wire envelope storage and are the leading suspected cause of the 2026-04-20 startup hang. The shared `NpgsqlDataSource` is registered once via `builder.AddNpgsqlDataSource("runcoach")` (the `Aspire.Npgsql` extension on `IHostApplicationBuilder`, not the `Npgsql.DependencyInjection` extension on `IServiceCollection` — dotnet/aspire#1515). Every downstream consumer — EF Core `DbContext`s, Marten via `.UseNpgsqlDataSource()`, Wolverine's EF integration via `opts.Services.AddDbContextWithWolverineIntegration<T>((sp, o) => o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()))`, DataProtection via `DpKeysContext` — resolves that singleton from DI. **Hand-built `new NpgsqlDataSourceBuilder(...)` is prohibited**; it forks DEC-046's rotation seam. `AddDbContextWithWolverineIntegration<T>` must live inside the `UseWolverine(opts => ...)` callback so Wolverine's handler-discovery codegen can see the DbContext; top-level placement silently disables `AutoApplyTransactions`.

`builder.Host.ApplyJasperFxExtensions()` shall be called before `AddMarten` and `UseWolverine` so `CritterStackDefaults` apply to both code generators. `DaemonMode.Solo` (Marten async daemon) must pair with `opts.Durability.Mode = DurabilityMode.Solo` (Wolverine durability) — `HotCold` takes advisory locks that collide with `ApplyAllDatabaseChangesOnStartup`. The OpenTelemetry OTLP exporter shall be registered conditionally on `OTEL_EXPORTER_OTLP_ENDPOINT` being non-empty so test runs without a collector never block on the 30 s shutdown flush. `ValidateScopes = true` in Development always; `ValidateOnBuild` stays `false` until Wolverine handler types are pre-generated via `dotnet run -- codegen write`. The `public partial class Program;` trailer is prohibited on .NET 10 (analyzer ASP0027; Web SDK source generator emits it automatically).

**Rationale:**

- **The double-wire is the hang.** R-054's primary finding: the original Slice 0 composition registered both `Marten.IntegrateWithWolverine()` AND an `IWolverineExtension` that called `PersistMessagesWithPostgresql(NpgsqlDataSource)`. Every JasperFx reference sample (`WebApiWithMarten`, `OrderSagaSample`, `DiagnosticsApp`) uses `IntegrateWithWolverine` alone. The `wolverine_envelopes` / `wolverine_nodes` / `wolverine_dead_letters` tables get created twice under `ApplyAllDatabaseChangesOnStartup` — usually loud, but in racing startup conditions advisory-lock-waits silently and never yields control back to `builder.Build()`, tripping the 5-minute `HostFactoryResolver` timeout with zero log output.
- **DEC-046's rotation seam depends on exactly one `NpgsqlDataSource` instance.** `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider(...)` rotates credentials for every consumer bound to that builder. A second data source forked inside `UseWolverine` (the uncommitted working-tree workaround before this decision landed) means half the consumers see rotated credentials and half don't — manifests as `28P01` errors on the Wolverine outbox only, persisting until process restart.
- **`AddDbContextWithWolverineIntegration` placement is non-obvious and undocumented.** Top-level `builder.Services.AddDbContextWithWolverineIntegration<T>(...)` registers the DbContext and appears to work, but Wolverine's codegen runs inside `UseWolverine` composition and only discovers DbContexts registered via `opts.Services`. `Policies.AutoApplyTransactions()` silently no-ops for contexts registered at the top level — a bug that only surfaces when a handler that should open a transaction doesn't.
- **Conditional OTLP exporter keeps `dotnet test` fast.** OTel 1.15.x does zero synchronous network I/O at startup, so the exporter can't cause the startup hang, but at process shutdown the `BatchExportProcessor.ForceFlush` waits up to 30 s for the collector to acknowledge. In CI with no collector listening, every integration-test shutdown eats 30 s. Gating `AddOtlpExporter`/`UseOtlpExporter` on the env var being set removes the cost without a code branch.
- **.NET 10's Web SDK source generator emits `public partial class Program`** — the manual trailer the test project needs to see is already generated. Leaving the manual declaration triggers ASP0027; removing it is a single-line cleanup with zero functional effect.
- **The single-source-of-wisdom failure mode of this stack.** Aspire.Npgsql, Marten, Wolverine, EF Core, Identity, DataProtection, and OpenTelemetry each document their own "correct" wiring. The composition where all seven coexist is documented nowhere authoritative; each sample assumes the others aren't present. This decision records the composed recipe so future-us (or another contributor) rediscovering it gets the answer from the decision log instead of from a trial-and-error debugging detour.

**Alternatives considered (and rejected):**

- **`IWolverineExtension` + `sp.GetRequiredService<NpgsqlDataSource>()` inside `Configure`.** The shape the original Slice 0 spec prescribed. R-054 verdict: `IWolverineExtension.Configure` fires during `UseWolverine` composition before the root `IServiceProvider` exists; calling `sp.BuildServiceProvider()` creates a second container with duplicate singletons and breaks identity-equality invariants. Rejected.
- **Hand-built second `NpgsqlDataSource` inside `UseWolverine`** (the uncommitted workaround). Violates the single-data-source rotation seam (DEC-046) and — more immediately — opens a connection against a not-yet-ready Testcontainers instance on a retry loop that never returns to `builder.Build()`. Rejected.
- **`options.UseNpgsql(connectionString)` everywhere** instead of DI-based `NpgsqlDataSource` resolution. Builds one pool per `DbContext` with identical config but different identity. Silent doubling of pool count and test-visibility of the "these two aren't the same object" bug. Rejected.
- **Skip Aspire.Npgsql, use `NpgsqlDependencyInjectionExtensions.AddNpgsqlDataSource` on `IServiceCollection` directly.** Loses the Aspire-added health check, Aspire OTel source registration, and the `IHostApplicationBuilder` integration. The name collision with the Aspire extension (dotnet/aspire#1515) also means a future contributor might silently call the wrong overload. Rejected — the Aspire.Npgsql path is strictly additive and safe without an AppHost.
- **`DaemonMode.HotCold`** for the Marten async daemon. Only correct in multi-instance deployments; takes an advisory lock that contends with `ApplyAllDatabaseChangesOnStartup` on single-node boot. MVP-0 is single-instance; Solo is correct. Revisit at MVP-1 deployment target decision.

**Cross-references:**

- R-054 artifact: `docs/research/artifacts/batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md`.
- Slice 0 spec: `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` § Unit 1 Functional Requirements and § Technical Considerations (amended 2026-04-20).
- DEC-046 — Postgres-backed DataProtection + `UsePeriodicPasswordProvider` rotation seam this decision preserves.
- DEC-045 — Aspire deferred to MVP-1; this decision shows Aspire.Npgsql is safe to use without the AppHost.
- R-047 — Marten per-user aggregate patterns; the `IntegrateWithWolverine` path here is the outbox composition that makes R-047's `[AggregateHandler]` workflow atomic across Marten + EF.
- Cycle plan: `docs/plans/mvp-0-cycle/cycle-plan.md` § Captured During Cycle entry dated 2026-04-20.

---

## DEC-049: Disable host-config reload at process start to unblock `WebApplication.CreateBuilder` on macOS arm64; remove manual `MapWolverineEnvelopeStorage` from `OnModelCreating`

**Date:** 2026-04-20
**Status:** Final
**Category:** Host composition / Slice 0 substrate / cross-platform startup / Wolverine–EF integration
**Informed by:** R-055 (`batch-18b-webapplication-createbuilder-hang-followup.md`).

**Decision:** Every RunCoach ASP.NET Core 10 host shall set `DOTNET_hostBuilder__reloadConfigOnChange=false` before `WebApplication.CreateBuilder(args)` is evaluated. `Program.cs` sets it via `Environment.SetEnvironmentVariable(...)` as the first executable statement; the `RunCoachAppFactory : WebApplicationFactory<Program>` test fixture re-asserts it via `builder.UseSetting("hostBuilder:reloadConfigOnChange", "false")` in `ConfigureWebHost` as belt-and-suspenders. Additionally, `RunCoachDbContext.OnModelCreating` shall **not** call `builder.MapWolverineEnvelopeStorage()` — Wolverine's `WolverineModelCustomizer` (registered via the DEC-048-prescribed `opts.Services.AddDbContextWithWolverineIntegration<T>(...)` inside `UseWolverine`) calls that method at runtime; the two call sites collide by double-adding the `WolverineEnabled` annotation and prevent the SUT host from booting.

**Rationale:**

- **FileSystemWatcher init stalls `CreateBuilder` on macOS arm64 / Darwin 25.x.** With `<UserSecretsId>` set and `appsettings.Development.json` present, `WebApplication.CreateBuilder` registers three JSON config sources (`appsettings.json`, `appsettings.{Env}.json`, user-secrets) with default `reloadOnChange: true`, each installing a `PhysicalFilesWatcher`. On macOS, `FileSystemWatcher.StartRaisingEvents` synchronously calls `Interop.Sys.Sync()` — a full `sync(2)` flush that is a known unbounded stall point (dotnet/runtime#77793) — then `FSEventStreamCreate/Schedule/Start`. Darwin 25.x (macOS 26 "Tahoe") is on `runtime-extra-platforms` only (dotnet/runtime#118610), so regressions here are systematically under-reported. Disabling host-config reload elides the watchers and `CreateBuilder` returns in under a second. Verified by the prescribed §7.1 / §7.2 reduction experiments: without the env var, plain `dotnet RunCoach.Api.dll` produces zero stdout within 10 s; with the env var, `CreateBuilder` completes and Program.cs proceeds to the next statement within ~3 s.
- **Config reload on change is a dev-loop nicety, not a runtime contract.** Disabling it costs nothing operationally. CI and production do not edit config files in-place; dev-loop editing of `appsettings.Development.json` restarts the host via `dotnet watch` file-save semantics rather than relying on in-process hot-reload.
- **Belt-and-suspenders in the fixture** protects future fixture evolutions (e.g. overrides that add extra reloadable config sources) from inadvertently re-enabling watchers.
- **The Wolverine envelope-storage double-mapping was a dormant bug unmasked by R-055's fix.** The `MapWolverineEnvelopeStorage()` call in `OnModelCreating` was originally intended to include envelope entities in the EF migration snapshot at design-time. `AddDbContextWithWolverineIntegration<T>` registers `WolverineModelCustomizer` which calls the same method at runtime. Once the SUT host booted under `WebApplicationFactory<Program>`, both paths fired and the second `modelBuilder.Model.AddAnnotation("WolverineEnabled", true)` threw `InvalidOperationException: The annotation 'WolverineEnabled' cannot be added because an annotation with the same name already exists`. The collision had been masked because no test exercised SUT-host boot. With `MapWolverineEnvelopeStorage` removed from `OnModelCreating`, the customizer is the sole wiring path; envelope entities are still in the runtime model, migrations are still `ExcludeFromMigrations`-safe (Wolverine provisions the actual tables via `ApplyAllDatabaseChangesOnStartup`), and the EF migration snapshot remains in sync (`dotnet ef migrations has-pending-model-changes` reports no pending changes).
- **Test suite goes from 575/0/1 (scope-reduced fixture) to 581/0/1 (full SUT-host fixture with six new smoke tests).** The six new SUT-host smoke tests (`IDocumentStore` resolves + opens session, `RunCoachDbContext` resolves + queries Identity schema, `DpKeysContext` resolves + `DataProtectionKeys` reachable, `IDataProtectionProvider` round-trips a payload, `GET /health` returns `{"status":"ok"}`, `NpgsqlDataSource` is identity-equal across scopes) run in ≤ 2 s cold against the Testcontainers Postgres.

**Alternatives considered (and rejected):**

- **Guard the env-var set with `OperatingSystem.IsMacOS()`.** Adds a platform branch for negligible benefit — reload-on-change is not load-bearing on any platform for this project. Rejected as over-engineering.
- **Delete `<UserSecretsId>` from the API csproj and move `Anthropic:ApiKey` to an environment variable.** Removes only one of the three `PhysicalFilesWatcher` instances; the two `appsettings.*.json` watchers still install. Rejected as partial.
- **Delete `appsettings.Development.json` and migrate to env-var-only dev config.** Loses the local `dotnet run` ergonomics documented in `backend/CLAUDE.md`. Rejected.
- **Wait for a .NET 10 servicing release that fixes the FSW stall.** No such release is shipping; the behavior is Darwin 25.x platform-level with no tracked fix. Rejected (null option).
- **Switch SUT TFM to `net9.0`** (the R-047 escape hatch). Last resort; would sacrifice .NET 10's broader Darwin 25.x exposure. Rejected because the config-level fix lands cleanly.
- **Guard `MapWolverineEnvelopeStorage` in `OnModelCreating` with a `FindAnnotation("WolverineEnabled")` check.** `WolverineModelCustomizer` runs AFTER `OnModelCreating`, so at that point the annotation is never yet set; the guard always passes, never prevents the collision. Rejected as ineffective.
- **Swap to plain `AddDbContext<T>(...)` and retain the `OnModelCreating` call.** Loses `Policies.AutoApplyTransactions` discovery (DEC-048) — Wolverine handlers silently skip the Marten + EF + outbox single-transaction invariant. Rejected.

**Cross-references:**

- R-055 artifact: `docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`.
- DEC-048 — R-054 composition corrections this decision builds on; every DEC-048 invariant is preserved.
- DEC-046 — single `NpgsqlDataSource` rotation seam; unaffected. Fixture routes the Testcontainers connection string through the shared singleton via the `ConnectionStrings__runcoach` environment variable rather than a second data-source builder.
- .NET 10 runtime pin: Host ≥ 10.0.5 (the OOB fix for the vsdbg × macOS arm64 debugger-handshake deadlock, 2026-03-12 — a separate platform regression but on the same Darwin 25.x surface; currently satisfied by the 10.0.6 servicing release).
- Cycle plan: `docs/plans/mvp-0-cycle/cycle-plan.md` § Captured During Cycle entry dated 2026-04-20.
- dotnet/runtime#77793 — FileSystemWatcher startup performance on macOS (`Interop.Sys.Sync()`).
- dotnet/runtime#118610 — Darwin 25.x on `runtime-extra-platforms` only.

---

## DEC-050: `UseHttpsRedirection` ungated + explicit `__Host-` cookie `Path = "/"` + pinned `https_port` in test fixture

**Date:** 2026-04-21
**Status:** Final
**Category:** Auth substrate / Slice 0 / HTTPS contract / integration-test harness
**Informed by:** R-056 (`batch-19a-httpsredirection-webapplicationfactory.md`).

**Decision:**

1. `app.UseHttpsRedirection()` is called unconditionally in `Program.cs` (no `!IsDevelopment()` gate), matching the .NET 10 MVC / Razor Pages / Blazor / Identity template idiom. The `HttpsRedirectionMiddleware` safely no-ops when it cannot resolve an HTTPS port — it logs a warning and passes the request through as HTTP rather than redirect-looping.
2. `services.AddHttpsRedirection(o => o.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect)` is registered explicitly so a later contributor cannot flip to 308 (aggressively cached by browsers and intermediates; Microsoft docs warn against permanent redirects).
3. The `__Host-RunCoach` application cookie sets `options.Cookie.Path = "/"` explicitly. RFC 6265bis §5.6 requires the `Path` attribute to be *present* for the `__Host-` prefix, not merely defaulted; omitting the explicit assignment silently drops the cookie in Chrome and Safari.
4. `RunCoachAppFactory.ConfigureWebHost` calls `builder.UseSetting("https_port", "443")` so the in-memory `TestServer` — which has no `IServerAddressesFeature` — no longer emits `HttpsRedirectionMiddleware[3] "Failed to determine the https port for redirect."` on every request.
5. When T02.5's auth-endpoint integration tests land, each `HttpClient` is created with `BaseAddress = new Uri("https://localhost")` + `AllowAutoRedirect = false` + `HandleCookies = false`. The HTTPS base flips `Request.IsHttps = true` inside the pipeline so `UseHttpsRedirection` short-circuits without any redirect and the handler emits `Secure` cookies against a request that is semantically HTTPS. Auto-redirect-off + no `CookieContainer` lets tests assert the raw `Set-Cookie` string.

**Rationale:**

- **Ungated `UseHttpsRedirection` is the .NET 10 template idiom.** Verified in `github.com/dotnet/aspnetcore/blob/main/src/ProjectTemplates/Web.ProjectTemplates/content/` and the reference `Program.cs` in the .NET 10 fundamentals doc. The middleware is designed to self-disable on unresolvable ports; the previous `!IsDevelopment()` gate added cognitive load without correctness gain.
- **`__Host-` prefix without an explicit `Path` is a silent footgun.** RFC 6265bis §5.6 is strict about attribute presence. The bug is invisible in server logs and silent in tests — only a real browser catches it. Shipping the explicit `Path = "/"` before T02.4 writes login ensures the first end-to-end browser test of the login flow works on Chrome and Safari, not just Firefox.
- **`UseSetting("https_port", "443")` in the test fixture** silences a warning log on every integration-test request without materially changing SUT behavior. The test client's `BaseAddress = https://localhost` (coming in T02.5) short-circuits the middleware anyway; the setting is belt-and-suspenders for any test that omits the HTTPS base.
- **Forward-compat.** `AddHttpsRedirection(...)` with an explicit `HttpsPort` is the hook for pinning port `443` under a reverse proxy (Nginx / Azure Linux App Service / K8s ingress / YARP). MVP-1 deployment commits the actual port; until then the default resolution chain (`ASPNETCORE_HTTPS_PORT` → `https_port` host setting → `IServerAddressesFeature`) is good enough.
- **`dotnet dev-certs https --trust` becomes a documented contributor prerequisite** (follow-up in the cycle plan). `__Host-` + `Secure` is functionally broken on `http://localhost` in Chrome and Safari — only Firefox tolerates it. Every major .NET OSS reference (eShop, Ardalis / jasontaylordev CleanArchitecture, Duende quickstarts, damienbod samples) keeps cookie attributes stable across environments and requires `dev-certs --trust`. Dev-only cookie weakening was considered and rejected.

**Alternatives considered (and rejected):**

- **Keep the `!IsDevelopment()` gate.** Defensible but non-idiomatic and adds one mental hop when reading `Program.cs`. Rejected in favor of template conformance.
- **`IsProduction()`-only gate** (Staging also skips redirect). Rejected — Staging must behave like Production for security invariants; the whole point of Staging is catching HTTPS-related bugs before they ship.
- **Drop `__Host-` prefix in Development, keep in Production.** Rejected. Every significant .NET OSS project keeps cookie attributes stable; divergence silently hides the attack classes the prefix closes (subdomain cookie-tossing, path-scoped overwrite).
- **`WebApplicationFactory.UseKestrel()` for integration tests** (.NET 10 added this). Rejected as default for auth tests — set-cookie header assertions don't need real TLS; in-memory `TestServer` + HTTPS base address is faster, requires no port allocation, and has no certificate story. Reserve `UseKestrel()` for Playwright / Selenium E2E in T03.x.
- **Local reverse proxy (YARP / Nginx / Caddy / Traefik) terminating TLS for dev.** Rejected as default for a single-service pre-Alpha app; .NET Aspire 13.1's `WithHttpsDeveloperCertificate` is the better path if RunCoach ever grows into a multi-service Aspire topology.
- **mkcert as recommended dev default.** Rejected — .NET 10 `dotnet dev-certs --trust` now matches mkcert on SANs, WSL passthrough, and cross-platform support; introducing a second trust chain is unnecessary friction. Document mkcert as the escape hatch for corporate Linux machines where `dev-certs --trust` cannot run.

**Cross-references:**

- R-056 artifact: `docs/research/artifacts/batch-19a-httpsredirection-webapplicationfactory.md`.
- DEC-044 — cookie-not-JWT browser auth; `__Host-` prefix contract.
- Cycle plan: `docs/plans/mvp-0-cycle/cycle-plan.md` § Captured During Cycle — Forwarded Headers middleware deferred to MVP-1; `CONTRIBUTING.md` HTTPS-cert prerequisite captured for Slice 0 close-out.
- RFC 6265bis §5.6 — `__Host-` prefix requirements.
- dotnet/aspnetcore#27951 — `HttpsRedirectionMiddleware` silent-no-op footgun on unresolvable port (open).

---

## DEC-051: Strict-always JWT validation with env-gated `ValidateOnStart`; HS256 pin; source-generated options validator

**Date:** 2026-04-21
**Status:** Final
**Category:** Auth substrate / Slice 0 / JWT holding-pattern posture
**Informed by:** R-057 (`batch-19b-jwtbearer-opt-in-registration.md`).

**Decision:**

1. `TokenValidationParameters` for the JWT bearer scheme sets `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey`, and `RequireSignedTokens` to `true` **unconditionally** — not gated on whether the corresponding config value is populated. When `Auth:Jwt` is absent, `IssuerSigningKey` is `null` and every incoming token fails closed with `IDX10500: No security keys were provided to validate the signature.` The handler registers regardless so the iOS-shim PR remains purely additive.
2. `ValidAlgorithms` is pinned to `[SecurityAlgorithms.HmacSha256]`. This closes the historical HS/RS key-confusion attack class (attacker swaps the algorithm header to one the verifier applies to the wrong key material).
3. `MapInboundClaims = false` on `JwtBearerOptions` — works with raw JWT claim names (`sub`, `role`) instead of the SOAP-style claim rewriting `JwtSecurityTokenHandler` inherits by default. This aligns with `JsonWebTokenHandler` (the .NET 8+ default) and is the 2026 idiom.
4. HS256 is the chosen signing algorithm for first-party token issuance when the iOS shim lands. The iOS app is a token *carrier*, not a verifier; the backend is the only verifier. In this single-trust-boundary topology WorkOS and OpenIddict both recommend symmetric signing over RS256. RS256 / asymmetric keys are the right call only for multi-party / federated scenarios that RunCoach will not enter in MVP-0 or MVP-1.
5. `JwtAuthOptions` is a `sealed class` with `[Required]` + `[MinLength]` DataAnnotations and a `string? KeyId` for the two-overlapping-keys rotation story. `JwtAuthOptionsValidator : IValidateOptions<JwtAuthOptions>` is generated by the `[OptionsValidator]` source generator (stable in .NET 8+, AOT-safe, reflection-free).
6. `services.AddOptions<JwtAuthOptions>().BindConfiguration(JwtAuthOptions.SectionName).ValidateOnStart()` is called only outside `builder.Environment.IsDevelopment()`. Development and CI tolerate absent config (validator never fires). Production / Staging fail startup fast if any required value is missing. The test fixture runs under `UseEnvironment("Development")`, so `ValidateOnStart` is skipped and the existing test suite continues to boot without `Auth:Jwt` configured.

**Rationale:**

- **Gating `Validate*` on `!string.IsNullOrEmpty(config)` inverts "secure by default."** The previous T02.2 shape was narrowly safe today (no bearer endpoint; `RequireSignedTokens = true` default forces `IDX10500` fail-closed on any malformed or unsigned token), but it was a latent landmine. Under a future `Authority = "..."` edit with JWKS auto-discovery, any token signed by that authority would be accepted because `ValidateIssuer` / `ValidateAudience` would silently short-circuit to `false`. Under a leaked-HMAC-secret scenario, any attacker-crafted token would validate. RFC 8725 §2.1 / §3.9 and OWASP's JWT Cheat Sheet explicitly warn against the anti-pattern.
- **`RequireSignedTokens = true` blocks `alg: none` independently of `ValidateIssuerSigningKey`.** Confirmed via `Microsoft.IdentityModel.Tokens.TokenValidationParameters` source (AzureAD/identitymodel repo) and Microsoft Learn's `RequireSignedTokens` docs (v8.15.0). `JsonWebTokenHandler.ValidateJWSAsync` rejects an unsigned token before the signing-key validators run.
- **`PolicyEvaluator` iterates every scheme in `policy.AuthenticationSchemes` with no short-circuit** (aspnetcore `PolicyEvaluator.cs`). For `CookieOrBearer`, a bogus `Authorization: Bearer ...` header + a valid cookie merges cleanly: Bearer fails silently, Cookie succeeds, the authenticated principal is the cookie user. The JWT scheme registered with null keys cannot subvert the policy.
- **HS256 for first-party single-trust-boundary issuance.** WorkOS's 2024 article states the principle directly: *"Use HS256 when your signing and verification happen in the same trust boundary, typically a single application or a small cluster of services that already share secrets through a secure channel."* OpenIddict: *"symmetric keys are always chosen first [for access tokens], except for identity tokens."* The Auth0 / SuperTokens / Supabase "RS256 is universally better" line applies to multi-tenant / multi-party systems; it over-applies here.
- **Not `AddBearerToken`.** Per Andrew Lock's November 2023 post *"Should you use the .NET 8 Identity API endpoints?"* and Tore Nestenius's `BearerToken: The new Authentication handler in .NET 8`, `AddBearerToken` produces opaque Data-Protection-encrypted tokens that are *not* JWTs, cannot be parsed by any JWT library, cannot be decoded by iOS inspection tooling or Postman, and are intentionally coupled to `MapIdentityApi<TUser>()` — the non-standard ROPC grant implementation. For an iOS shim wanting inspectable JWT claims, `AddJwtBearer` + issue JWTs yourself via `JsonWebTokenHandler` is the correct path.
- **`[OptionsValidator]` is the 2026 canonical replacement for `ValidateDataAnnotations`** (Microsoft Learn's "Compile-time options validation source generation"). Rewrites the DataAnnotation attributes into reflection-free validators so `PublishAot` stops warning IL2025 / IL3050 when RunCoach later adopts AOT.
- **`ValidateOnStart` is hosted-service-driven** (`dotnet/aspnetcore#56453`). It only fires when `IHost.StartAsync` runs. Tests and CI naturally skip it under `UseEnvironment("Development")`. Production startup genuinely fails fast.
- **No public CVE indicts permissive `TokenValidationParameters` as a library bug** — it's treated as a configuration defect. Package pins already satisfy CVE-2024-21319 / 21643 / 30105 (Microsoft.IdentityModel.* ≥ 7.1.2; current 8.x; `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.6).

**Alternatives considered (and rejected):**

- **Don't register `AddJwtBearer` until the iOS shim actually needs it** (YAGNI). Equally idiomatic. Rejected because it pushes a `Program.cs` + authorization-policy diff into the iOS-shim PR instead of keeping that PR purely additive (new endpoint + new appsettings section only). Keeping the `CookieOrBearer` policy shape-stable across the iOS transition was an explicit cycle-plan-level goal of T02.2.
- **Keep permissive validation** (current T02.2 shape, before research). Not currently exploitable but inverts secure-by-default. Rejected for latent-landmine reasons above.
- **`UseEnvironment("Testing")` + `ValidateOnStart` gated on `!IsDevelopment() && !IsEnvironment("Testing")`.** The artifact's recommended test-host environment. Rejected for now because switching to `Testing` would require adding a separate migration trigger for the test env (`DevelopmentMigrationService` is currently gated on `IsDevelopment()`). The `Development`-gate is clean and correct as long as Dev and Test share the migration behavior — which they do today.
- **RS256 with `RsaSecurityKey`.** Rejected for this topology; HS256 is simpler, faster, AOT-friendly, and correct for a single-trust-boundary backend-only verifier. Revisit only if RunCoach federates or exposes a JWKS endpoint.
- **`AddBearerToken`.** Rejected as above — opaque tokens, coupled to `MapIdentityApi`, not JWTs.
- **`NetDevPack.Security.Jwt` for auto-rotation + JWKS.** Out of scope for Slice 0 (no real issuance yet). Revisit if RunCoach ever needs JWKS or federation.

**Cross-references:**

- R-057 artifact: `docs/research/artifacts/batch-19b-jwtbearer-opt-in-registration.md`.
- DEC-033 — iOS companion deferred to MVP-1; bearer scheme registers now so the iOS PR is additive.
- DEC-044 — browser auth uses the cookie, not a bearer.
- Cycle plan: `docs/plans/mvp-0-cycle/cycle-plan.md` § Captured During Cycle — test-host `UseEnvironment("Testing")` migration captured as Slice 0 close-out follow-up.
- RFC 8725 — JWT Best Current Practices.
- OWASP JWT Cheat Sheet.
- dotnet/aspnetcore#56453 — `ValidateOnStart` hosted-service trigger.
- dotnet/aspnetcore#49469 — aspnetcore on `JsonWebTokenHandler` (the .NET 8+ default).

---

## DEC-052: `IdentityResult.Errors` → `ValidationProblemDetails` via per-action `ModelState.AddModelError` loop + DTO pre-validation

**Date:** 2026-04-21
**Status:** Final
**Category:** Auth substrate / Slice 0 / error-contract pattern / SPA binding
**Informed by:** R-058 (`batch-19c-identity-result-to-validationproblemdetails.md`).

**Decision:**

1. The `AuthController.Register` endpoint (landing in T02.4) shall translate a failed `IdentityResult` to the RunCoach error contract via a per-action loop: `foreach (var error in result.Errors) ModelState.AddModelError(propertyKey, error.Description);` followed by `return ValidationProblem(ModelState);`. This routes through `ProblemDetailsFactory.CreateValidationProblemDetails`, emits `Content-Type: application/problem+json`, and produces the exact shape the spec §Unit 2 line 84 prescribes (DTO-property-keyed `errors` dictionary).
2. `RegisterRequest` DTO shall carry DataAnnotations (`[Required, EmailAddress, MaxLength(254)]` on email; `[Required, MinLength(12), MaxLength(128)]` on password) so the common "too short" / malformed-email path short-circuits through `[ApiController]` auto-400 before hitting `UserManager.CreateAsync`. Identity remains source-of-truth for character-class rules (upper / lower / digit / non-alphanumeric) and uniqueness.
3. `Modules/Identity/IdentityErrorCodeMapper.cs` shall ship a reusable mapper keying the 22 stable `IdentityError.Code` values (stable across Identity 6–10 via `nameof(...)`) to DTO buckets (`password` / `email` / `username` / `role` / `general`) plus a `Kind` enum (`Validation` / `Conflict` / `Unauthorized` / `Unknown`). The mapper is the single source of code→bucket truth for every controller that translates an `IdentityResult`.
4. `Modules/Identity/IdentityResultExtensions.cs` shall expose `ToRegistrationActionResult(this IdentityResult, ControllerBase, RegisterRequest)` which walks `result.Errors`, splits the 409 conflict path (`DuplicateEmail` / `DuplicateUserName`) off as a plain `ProblemDetails` via `controller.Problem(...)`, and routes everything else through `ModelState.AddModelError` + `ValidationProblem(ModelState)`. The 409 path emits a `ProblemDetails` (NOT a `ValidationProblemDetails`) because the conflict is not a field-level validation error; title is intentionally generic to preserve enumeration-resistance posture.
5. Frontend parity (T03.x) is enforced by colocating Zod schemas that mirror the DataAnnotation rules in a `shared-contracts` folder + a contract-test that asserts equivalence (e.g., `"A1a!bcdefghij"` passes both; `"short"` fails both). FluentValidation is deferred until rules become conditional (cross-field, async, feature-flagged); the 2026 successor package is `SharpGrip.FluentValidation.AutoValidation.Mvc`.

**Rationale:**

- **`ControllerBase.ValidationProblem(ModelStateDictionary)` is the canonical .NET 10 entry point** for DTO-property-keyed `ValidationProblemDetails`. It routes through `ProblemDetailsFactory.CreateValidationProblemDetails` (in `DefaultProblemDetailsFactory`), auto-populates `title = "One or more validation errors occurred."`, `type = ApiBehaviorOptions.ClientErrorMapping[400].Link`, `status = 400`, and adds a `traceId` extension from `Activity.Current?.Id ?? HttpContext.TraceIdentifier`. Result is wrapped in `BadRequestObjectResult` with `application/problem+json`. Identical shape to `[ApiController]` auto-400 — the contract is uniform across validation failure sources.
- **`CustomizeProblemDetails` does NOT fire for `ValidationProblemDetails` produced via MVC's `ValidationProblem(ModelState)`** — confirmed in `dotnet/aspnetcore#62723` (.NET 10 preview). This is the single most misunderstood item in the ProblemDetails landscape. Centralizing translation through `AddProblemDetails(o => o.CustomizeProblemDetails = ...)` was considered and rejected as silently broken. Translation must live in the controller (or in a typed-exception + `IExceptionHandler` — deferred pattern for service-layer failures in later slices).
- **`IdentityError.Code` is stable across Identity 6, 7, 8, 9, and 10.** Each code is produced by `nameof(...)` in `IdentityErrorDescriber.cs`, so codes are invariant to localization and unchanged by the .NET 10 passkey work. The mapper captures the full 22-code surface area once; future Identity versions add codes that fall through to the `_ => new(General, Unknown)` default.
- **DTO-property keying is a deliberate departure from `MapIdentityApi`**, which keys the `errors` dictionary by `IdentityError.Code`. `MapIdentityApi`'s shape serves a minimal-API scaffolding flow that doesn't know the DTO shape; RunCoach hand-rolls the controller specifically to bind to SPA form-library conventions (React Hook Form / Zod keying). The shift trades one line of spec-level discipline (use the bucket map) for a cleaner frontend binding.
- **409 split for duplicates.** RFC 9110 §15.5.10 scopes 409 to conflicts that can be resolved by the user's retry. `DuplicateEmail` and `DuplicateUserName` are conflicts, not validation errors — returning them as `ValidationProblemDetails.errors[email]` under a 400 would conflate "the input violates the schema" with "the input conflicts with existing state," and forces the SPA to branch on `errors.email` content to distinguish the two cases. Plain `ProblemDetails` + 409 is the clean split.
- **Deliberate enumeration-resistance posture on 409 `title`.** The spec §Non-Goals explicitly defers full enumeration hardening to pre-public-release. For MVP-0 (personal use + trusted friends), a generic `"The account could not be created."` title is the chosen posture — preserves UX without leaking existence via the body, and the 409 status code itself is the enumeration signal that OWASP ASVS 5.0 V6.3 flags. A follow-up pattern (202 Accepted + email to existing account) eliminates the status-code leak entirely and is captured as an MVP-1 consideration.

**Alternatives considered (and rejected):**

- **Throw `IdentityFailureException` + dedicated `IExceptionHandler`.** Centralized; coexists cleanly with T02.3's 500 handler; works from service-layer code that has no `ControllerBase`. Rejected as primary for `AuthController` because exceptions-for-control-flow is 100×–1000× slower than returns and harder to trace in logs; the .NET 10 default `SuppressDiagnosticsCallback` also now hides handled exceptions. Good fallback for service-layer failures in later slices.
- **Custom `ProblemDetailsFactory` subclass.** Intercepts all MVC-produced ProblemDetails but does not intercept `IdentityResult` directly — the controller still needs the `AddModelError` loop first. No extra value.
- **`AddProblemDetails(o => o.CustomizeProblemDetails = ...)` callback.** Does NOT fire for MVC `ValidationProblem(ModelState)`. Load-bearing misunderstanding — rejected.
- **Middleware-based translator.** Reads response body after the fact — brittle, and `[ApiController]` already did the right thing upstream.
- **`Result<T>` functional pattern (Ardalis.Result / FluentResults / OneOf).** Cited in `jasontaylordev/CleanArchitecture` as the Clean-Architecture idiom. Adds a dependency and still needs a `Result → IActionResult` translator internally. Re-evaluate at Slice 1+ when the service layer expands.

**Cross-references:**

- R-058 artifact: `docs/research/artifacts/batch-19c-identity-result-to-validationproblemdetails.md`.
- Spec: `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` §Unit 2 lines 82–90 (functional requirements for register / login / error contracts).
- DEC-051 — strict-always JWT validation (the sibling auth-contract decision landing with T02.2).
- DEC-053 — timing-safety mitigation on login (pairs with this decision for the register/login error-contract story).
- `dotnet/aspnetcore` — `DefaultProblemDetailsFactory.cs` + `ControllerBase.ValidationProblem` for the canonical emit path.
- `dotnet/aspnetcore#62723` — `CustomizeProblemDetails` bypassed by MVC validation path.
- Kevin Smith, "Extra Validation Errors In ASP.NET Core" (2022) — reference for the `AddModelError("propertyName", description)` pattern.
- OWASP ASVS 5.0 V6.3 — enumeration-resistance requirements.
- RFC 9110 §15.5.10 — 409 Conflict semantics.

---

## DEC-053: `AuthController.Login` manual timing-safety mitigation via cached `VerifyHashedPassword` dummy hash

**Date:** 2026-04-21
**Status:** Final
**Category:** Auth substrate / Slice 0 / login security / enumeration resistance
**Informed by:** R-059 (`batch-19d-signinmanager-timing-safety.md`).

**Decision:**

1. `AuthController.Login` (landing in T02.4) shall NOT use the string overload of `SignInManager.PasswordSignInAsync(userName, password, ...)`. Instead it calls `UserManager.FindByEmailAsync(request.Email)` first; on `user is null`, burns one `IPasswordHasher<ApplicationUser>.VerifyHashedPassword(new ApplicationUser(), DummyHash, request.Password)` pass to equalize timing, then returns `Unauthorized` with a generic `ProblemDetails`; on `user is not null`, delegates to `SignInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false)` and branches on the result.
2. `DummyHash` shall be a `static readonly string` field initialized once at type load via `new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), "__runcoach_dummy__")`. The hash value is irrelevant — only its parse-ability and cost parity matter. Caching statically avoids paying an extra PBKDF2 pass (40–80 ms) on every unknown-user request, which would flip the timing leak rather than close it.
3. Every non-success `SignInResult` — `Failed`, `IsLockedOut`, `IsNotAllowed`, `RequiresTwoFactor` — collapses to an identical generic 401 `ProblemDetails` body. Distinctions are logged server-side (`ILogger.LogInformation("Login failed for {UserId}: {Result}", user.Id, result)`) but never surface to the HTTP response.
4. The 401 body is byte-identical across all failure causes, including the unknown-user branch. No conditional `detail` field, no error-specific `type` URI, no `extensions` members that vary by cause — any content variance re-opens the enumeration channel on a content side-channel and renders timing mitigation moot.
5. The mitigation is documented with a `TODO` tag tracking this DEC ID, so if Microsoft eventually ships built-in timing-safety in `SignInManager` (no such PR tracked as of April 2026) the manual mitigation can be retired cleanly.

**Rationale:**

- **`SignInManager.PasswordSignInAsync(string, ...)` in .NET 10 IS NOT timing-safe** on the unknown-user branch. Primary-source evidence: `dotnet/aspnetcore` `main`, `src/Identity/Core/src/SignInManager.cs` — the string overload returns `SignInResult.Failed` immediately when `FindByNameAsync` returns null, with no call to `VerifyHashedPassword` / `CheckPasswordAsync` / any dummy-hash helper. The user-found branch runs `PasswordHasher.VerifyHashedPassword` → PBKDF2-HMAC-SHA512 / 100 000 iterations (V3 defaults, unchanged since .NET 7), ~40–80 ms on server hardware. Remotely observable timing delta; textbook user-enumeration side-channel.
- **The spec's "or" clause is resolved by the source code.** Slice 0 §Unit 2 line 86 presents two implementation paths — `SignInManager.PasswordSignInAsync(userName, ...)` or `UserManager.FindByEmailAsync` + dummy-hash — as if either satisfies the timing-safety requirement. R-059's source trace confirms only the second option actually achieves it. The spec is amended to remove the "or" and mandate the manual mitigation.
- **`VerifyHashedPassword(default, DummyHash, password)` chosen over `HashPassword(default, password)`** because it traverses the same code path as a real-user failure (parse V3 header → derive key → constant-time compare), giving tighter timing parity at the same PBKDF2 cost. `HashPassword` additionally calls `RandomNumberGenerator.Fill` for the salt — sub-microsecond, negligible vs PBKDF2, but avoiding the asymmetry is the cleaner posture. Both OWASP, Andrew Lock, and Cyberis converge on `VerifyHashedPassword` as the idiomatic mitigation.
- **`IPasswordHasher<TUser>` exposes only synchronous `HashPassword` and `VerifyHashedPassword`.** The R-059 artifact corrects a factual error in the spec snippet (`PasswordHasher.HashPasswordAsync` does not exist). Production call is synchronous — do not `await` it.
- **Defense-in-depth limits.** Rate limiting (per-IP fixed window) is the second line of defense — it caps enumeration throughput but does not change the timing signal. OWASP considers rate limiting alone insufficient; the timing mitigation is the primary control.
- **`RequiresTwoFactor` / `IsLockedOut` / `IsNotAllowed` collapse to 401 Unauthorized, not 423 Locked.** RFC 4918 §11.3 scopes 423 to WebDAV resource locking; reusing it for auth lockout is non-idiomatic and also leaks enumeration information. Microsoft's own `MapIdentityApi` `/login` handler collapses every failing `SignInResult` into a single 401 — this matches that precedent.
- **Test coverage is a code-path assertion, not wall-clock timing.** T02.5 registers a counting `IPasswordHasher<ApplicationUser>` test double via `ConfigureTestServices`, submits one `POST /api/v1/auth/login` with an unknown email and one with a known user + wrong password, asserts the counter is ≥ 1 in both cases. Proves the `user is null` branch executed a hash. Deterministic, runs in milliseconds. A statistical wall-clock benchmark (gated behind an env var, computing median / p95 over N=200 requests per case, asserting `|median(unknown) − median(known)| < 15 ms`) is a security-audit harness test — ships with the audit bundle, not the PR.

**Alternatives considered (and rejected):**

- **Trust `SignInManager.PasswordSignInAsync` alone, rely on rate limiting.** Rejected — rate limiting caps attack throughput but does not change the timing signal; an enumerator with one probe per minute still succeeds, just slowly. OWASP explicitly considers this insufficient.
- **Override `SignInManager<ApplicationUser>` with a custom subclass adding the dummy hash internally.** Architecturally cleaner — automatically covers any future caller of `PasswordSignInAsync`. Rejected for Slice 0 because the scope is a single controller; an override creates a maintenance burden that tracks Microsoft's internal method changes (e.g. `Stopwatch.GetTimestamp` / metrics calls added in .NET 9). Revisit for MVP-2 if more login surfaces emerge (passkey, social login, API token exchange).
- **Random-duration wrapper (Cofoundry-style `Task.Delay(Random.Shared.Next(minMs, maxMs))`).** Masks the symptom without fixing the cause; the jitter window must exceed the PBKDF2 delta to be effective, which degrades login UX. Valuable as secondary defense-in-depth for the MVP-1 lockout path.
- **Pre-hash the password on the client.** Shifts PBKDF2 to the browser; requires schema change to `AspNetUsers.PasswordHash`; incompatible with stock `PasswordHasher` and future passkey/WebAuthn migration. Out of scope.
- **Skip the lookup; always call `CheckPasswordSignInAsync` on a sentinel user.** `PasswordHasher.VerifyHashedPassword(user, null, password)` short-circuits and returns `Failed` without running PBKDF2. Doesn't achieve the goal.

**Known limitations (captured as MVP-1 follow-ups):**

- **Lockout path re-opens the leak on a secondary axis.** When `lockoutOnFailure: true` lands (deferred per spec Non-Goals), the known-user failure branch gains a DB write (`UserManager.AccessFailedAsync`) that the unknown-user branch does not. Options for MVP-1: (a) override `SignInManager` to track a "failed attempt" counter keyed by submitted-email-normalized-hash so both branches write; (b) uniform-delay envelope wrapping the whole login action; (c) accept the residual leak with per-IP rate limiting. Option (b) is simplest and aligns with OWASP's "make responses uniform" posture. Revisit when lockout is re-enabled.
- **Registration endpoint** has a separate enumeration vector through the 409 DuplicateEmail branch (see DEC-052). MVP-1 consideration: migrate to a "202 Accepted + email-to-existing-account" pattern that eliminates the status-code leak.
- **Password-reset endpoint** (post-Slice 0) must return the same 200 whether the email exists or not; always execute token-generation work (or a dummy equivalent) to equalize timing. Captured for the password-reset workstream.
- **ModelState binding 400s** are sub-millisecond and distinguishable from 401 by status code. Acceptable — 400 indicates malformed input, not "user exists" — but do NOT add custom server-side format checks (e.g., email-syntax validation) that fire only for the login action; they create a third timing class. Keep validation strictly in DTO DataAnnotations.

**Cross-references:**

- R-059 artifact: `docs/research/artifacts/batch-19d-signinmanager-timing-safety.md`.
- Spec: `docs/specs/12-spec-slice-0-foundation/12-spec-slice-0-foundation.md` §Unit 2 line 86 (amended with this DEC to remove the "or").
- DEC-052 — sibling decision on the register-endpoint error-contract shape.
- OWASP Authentication Cheat Sheet — "login goes through the same process no matter what the user or password is."
- OWASP ASVS 5.0 V6.3 — enumeration-resistance requirements.
- OWASP WSTG §Account Enumeration — timing deltas as a first-class discovery vector.
- `dotnet/aspnetcore` — `src/Identity/Core/src/SignInManager.cs` (string-overload source).
- `dotnet/aspnetcore` — `src/Identity/Extensions.Core/src/PasswordHasherOptions.cs` (V3 defaults: PBKDF2-HMAC-SHA512 / 100k iterations).
- `dotnet/aspnetcore#54542` — related lockout-based enumeration (closed as design-proposal, no shipped fix).

---

*Add new decisions at the bottom. Use format: DEC-XXX, date, category, decision, rationale, alternatives.*
