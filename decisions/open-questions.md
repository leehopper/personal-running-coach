# Open Questions

Living tracker of unresolved questions, design tensions, and things that need POC validation. Update status as decisions get made and move resolved items to the [decision log](decision-log.md).

## Status Legend

- **Open** — No clear answer yet
- **Leaning** — Have a direction but not committed
- **Decided** — Resolved, moved to decision log
- **Blocked** — Needs POC work or external input before progress

---

## Interaction

| Question | Status | Notes |
|----------|--------|-------|
| How structured is onboarding vs. open conversation? | Leaning | Partially decided — onboarding is structured/sequential, but exact question flow not finalized. |
| Should the AI initiate proactively, or only respond? | Decided | Both. Proactive coaching is a core differentiator. See decision log. |
| What does workout logging look like? Chat-based, form, or hybrid? | Open | Chat-only might feel slow for simple logs. Form-only loses the conversational value. Hybrid likely but needs design exploration. Could let the AI ask what it needs based on the workout type. User could also describe recent workouts conversationally rather than filling in fields. |

## Optimization

| Question | Status | Notes |
|----------|--------|-------|
| What triggers a micro vs. meso vs. macro replan? | Decided | R-004 produced a concrete 5-level escalation ladder: Level 0 (absorb/log only), Level 1 (micro-adjust next 1-2 workouts), Level 2 (week restructure — first level needing LLM), Level 3 (phase reconsideration), Level 4 (plan overhaul — requires user confirmation). Specific thresholds defined using ACWR bands with hysteresis and EWMA trend detection. See DEC-012 and self-optimization.md. |
| How does the system weight subjective input vs. objective data? | Leaning | R-004's escalation ladder routes signals through EWMA trend detection — single subjective reports are absorbed (Level 0), but persistent patterns trigger escalation. The traffic-light framework (R-001) provides the reconciliation model: objective metrics (HR, pace) + subjective reports → composite readiness assessment. Needs POC validation for exact weighting. |
| How aggressively should it redistribute missed mileage? | Decided | R-004: NEVER redistribute missed mileage (universal coaching rule). Move forward. Use drift-band tolerance (±15% of weekly target) — if within band, no intervention. Use upcoming easy sessions to gradually correct, never compress recovery. See self-optimization.md escalation ladder for specific missed-workout routing. |

## Memory & Context

| Question | Status | Notes |
|----------|--------|-------|
| What context gets injected per AI call? How is it structured? | Decided | R-004 produced a concrete token budget (~15K tokens, 7.5% of 200K window) with interaction-specific assembly and positional optimization. Stable prefix (system + profile + plan, ~6.3K tokens) is cacheable. See DEC-013 and memory-and-architecture.md. POC 1 will validate in practice. |
| How is long-term history summarized to fit context windows? | Decided | R-004: 5-layer summarization hierarchy. Raw data (never in context) → per-workout (~100-150 tokens) → weekly (~200-300) → phase (~300-500) → trend narrative (~500 tokens, LLM-generated). All pre-computed by background jobs, not generated at query time. 80-90% token reduction. See memory-and-architecture.md. |
| How do conversation history and structured data relate? | Open | Leaning: both matter independently. Structured data (profile, plan, history) makes the AI competent. Conversation history makes it feel like a relationship (remembers you hate treadmills, knows about your schedule conflicts). Both get injected into context but serve different purposes. Exact architecture should emerge from POC 1 — this is a core question for context injection design. |

## UX

| Question | Status | Notes |
|----------|--------|-------|
| Calendar view, chat view, or both? What is the primary surface? | Open | Chat is the coaching relationship. Calendar is the plan artifact. Both feel necessary but which is the "home" screen? |
| How does the AI communicate plan changes? | Leaning | Light explanation by default with expandable detail. Avoids overwhelming casual users while letting curious users dig in. Needs UX pattern exploration. |
| How much does the AI "show its work" to build trust? | Open | Related to transparency — users need to feel the plan is personalized, not generic. But too much reasoning becomes noise. |
| How does the user know the app is working? | Open | Success is two things: short-term it's the feeling of being coached (someone is managing my training), long-term it's results (I PR'd, I finished the race, I'm running more consistently). The app should surface both — some kind of progress visibility that reinforces the value even before a race happens. Needs design exploration. |
| What's the approval model for plan changes? | Leaning | Default: AI proposes, user approves. But minor/logistical tweaks (day swaps, small adjustments) probably shouldn't require approval. Need to define the threshold — what counts as "minor enough" to auto-apply vs. "significant enough" to require confirmation. |
| Can users pin constraints the AI must respect? | Open | E.g., "long run is always Sunday," "never schedule before 7am." This gives users control without micromanaging every change. Needs design exploration. |
| How does the user undo / reset when the AI is on a bad track? | Leaning | R-004's event-sourced architecture (Marten) provides native undo: replay the event stream excluding the unwanted event and regenerate the projection. Every adaptation decision includes full audit trail (trigger, escalation level, monitoring snapshot, LLM rationale). "Reset my plan" = regenerate from macro tier downward. "Ignore what I said about my knee" = exclude specific events. The architecture makes this a solved problem at the data layer; UX for surfacing it still needs design. |

## Engagement & Re-engagement

| Question | Status | Notes |
|----------|--------|-------|
| What's the daily touchpoint model? | Open | Some mix of user-initiated and app-initiated. The AI should manage notification cadence intelligently — not just on a schedule. E.g., user says "I'm out of town next week" and the app goes quiet until they're back. |
| How does the proactive coach "reach" the user? | Open | Push notifications? Morning brief in-app? SMS? Depends on platform (native app vs. web). Needs to feel like a coach checking in, not marketing spam. |
| Can the AI learn notification preferences over time? | Open | Long-term vision: the AI optimizes when/how to reach out based on user behavior. Near-term: probably just user-set preferences with smart defaults. |
| How does the system handle user absence and re-engagement? | Leaning | Two phases: (1) During absence — long-term, light nudges that aren't annoying. User should be able to tell the AI why they're gone ("I'm sick," "knee pain"). (2) On return — prompt for what happened (did they exercise? other activity? were they sick?) to recalibrate, but with a manual override to skip and just pick up where they left off. The AI's first move back should feel like a coach catching up, not a guilt trip. |

## Scope

| Question | Status | Notes |
|----------|--------|-------|
| Running only, or multi-sport from the start? | Open | Leaning running-only for MVP to reduce complexity. Multi-sport adds significant planning logic. |
| How does the planning model work for goalless users? | Leaning | NOT a separate planning mode. The same flexible architecture handles both goal-driven and maintenance users — the AI reasons differently based on inputs, but the system doesn't fork. This reinforces the broader principle: build one flexible system, don't introduce granularity until forced to. Same applies to future multi-sport support — running is the focus now, but the architecture shouldn't have "running" baked in. |
| Does the MVP need any passive data integration? | Open | Manual logging might be enough for MVP. But auto-import from Strava/Garmin would significantly reduce friction. R-002 finding: Strava's API explicitly prohibits AI/ML use of its data. Garmin dropped its $5K developer fee and provides free API access with full .FIT data. Garmin-first is the viable integration path. |

## Business

| Question | Status | Notes |
|----------|--------|-------|
| Regeneration limits per tier: what are the right numbers? | Open | Depends on what "regeneration" means and what the actual API costs look like. R-002 finding: LLM costs have collapsed to $0.04–$1.00/month per user at 2-5 interactions/week. Cost may not be the constraint it was assumed to be — the pricing model may not need to be usage-gated at all. |
| What qualifies as a "regeneration" vs. a minor adjustment? | Open | Full replan of meso+micro = regeneration? Single workout swap = minor? Need a clear definition. |

## Data & Privacy

| Question | Status | Notes |
|----------|--------|-------|
| What are the data privacy implications of storing health-adjacent data? | Decided | R-003 clarifies: HIPAA does NOT apply (not a covered entity). FDA exempt (general wellness). But FTC Health Breach Notification Rule DOES apply — the app is a "vendor of personal health records" once it consumes Garmin/health data + user reports. Penalties: $43,792/violation/day. "Breach" includes unauthorized disclosures, not just cyberattacks. WA My Health My Data Act has private right of action with no revenue threshold. CCPA/CPRA likely at scale. Action: treat as PHR vendor from MVP-1, no sharing with analytics without consent, breach notification procedures, privacy policy, consent logging. See DEC-020 and safety-and-legal.md. |
| Where does user data live relative to the AI? | Open | When the AI processes a user's context, what gets sent to the model provider? If BYOM is in play, user data flows through their chosen provider. R-003 note: Whoop discloses a "zero-retention/zero training" policy with OpenAI — this is a reasonable standard to match. Need to understand what data exposure looks like per architecture choice. R-005 (BYOM) research will further inform this. |

## Safety

| Question | Status | Notes |
|----------|--------|-------|
| How to guardrail against unsafe training advice (injury risk)? | Decided | R-001 + R-003 provide a complete answer. Deterministic guardrails (DEC-010): single-run spike ≤30%, ACWR 0.8-2.0, ≥70% easy volume, max 3 quality sessions/week, min 1 easy day between hard. Health screening gate at MVP-1 connected to deterministic layer (DEC-018). Hard keyword triggers for medical scope boundaries (DEC-019). R-003 validates: ACWR is defensible but should not be oversold (reasonable guardrail, not validated prevention tool). Real underestimated risks are exercise addiction reinforcement, RED-S/nutritional blindspots, and scope creep into medical territory — all addressed by design (keyword triggers, screening, pattern detection). See safety-and-legal.md for full risk matrix. |
| What disclaimers or limitations are needed? | Decided | R-003 provides the complete answer. Tier 1 (AI-aware) disclaimers are the emerging standard (Strava, Whoop). Staged approach: beta agreement via clickwrap at MVP-1 ($50 liability cap, assumption of risk, health disclaimer, "as is"), full ToS at public beta (mandatory arbitration + class action waiver, AI disclosure, health disclaimer). Must include: "not medical advice," AI disclosure (EU AI Act Article 50), assumption of risk for physical activity, warranty disclaimer. Never make specific outcome claims without RCT evidence. See DEC-017 and safety-and-legal.md. |
| How does the system handle AI output failures? | Decided | R-001 + R-007 provide a complete answer. Structural outputs are validated by the deterministic layer (DEC-010). Qualitative coaching failures are caught by: (1) five red-line categories with binary pass/fail LLM-as-judge checks — medical scope violations, toxic positivity under injury signals, user-pleasing confirmation bias, inconsistent safety responses, and overtraining dismissal; (2) penalty-weighted scoring where a single dangerous response overwhelms positive quality scores (-10 to 0 scale); (3) multi-turn adversarial testing for gradual escalation patterns. Minimum 50-scenario adversarial library covering cardiac/emergency, injury diagnosis, overtraining/RED-S, nutrition/eating disorders, medication, jailbreaks, and toxic positivity. See DEC-016. |

## Coaching Communication

| Question | Status | Notes |
|----------|--------|-------|
| How does the AI communicate plan downgrades without demoralizing users? | Open | The escalation ladder (DEC-012) defines *what* changes happen, but not *how* they're communicated. A forced volume reduction after illness needs a different tone than a precautionary pullback after a single bad workout. Feeds into R-010 research. |
| How does the AI handle users who consistently over-perform on easy days? | Open | R-001 identified this as the most common coaching challenge. "Warrants a coaching conversation" is the current answer, but what does that conversation look like without sounding scolding? Need behavioral psychology input from R-010. |
| What does the "coach training phase" look like from the user's perspective? | Open | MVP feature but underspecified. First ~2 weeks where AI is learning the user — what data is collected, how is progress toward "trained" communicated, what happens if the user doesn't log enough? |
| How does the AI handle injury-specific situations? | Open | Current illness routing (self-optimization.md) covers duration-based response, but injury is different — "my knee hurts" vs. "I have plantar fasciitis" vs. "I twisted my ankle" require very different coaching responses. R-011 will inform the safety boundaries; this question is about the conversational approach. |

## Development Workflow

| Question | Status | Notes |
|----------|--------|-------|
| How to structure the project for clean phase-to-phase context handoff? | Leaning | R-008/R-009 research points strongly to: CLAUDE.md (<200 lines, stable project identity) + ROADMAP.md (living phase/status tracker) + plan files per feature/POC. Update CLAUDE.md's "Current Phase" section when transitioning. Use `/catchup` slash command to bootstrap each session. Monorepo with docs alongside code. Need to implement this as part of POC setup. |
| How to keep ideation and active build separated? | Leaning | Maintain an IDEAS.md parking lot in docs/. During dev sessions, quick "add to IDEAS.md" without breaking flow. Between sessions, promote worthy items to ROADMAP.md or features/backlog. The `#` prefix in Claude Code saves transient thoughts to memory. Current planning docs (features/backlog.md, open-questions.md) remain the structured home for promoted ideas. |
| What does the CLAUDE.md / agent scaffolding look like for this project? | Leaning | Start with 60-100 lines: project purpose, tech stack, directory structure, build/test/lint commands, conventions, "Current Phase: POC" pointing to plan files, post-task checklist. Reference planning docs: "Before starting any feature work, read docs/plans/." Grow iteratively based on what Claude gets wrong — don't over-design upfront. Subdirectory CLAUDE.md files for frontend/backend-specific context. |
| How to scope agent tasks for good autonomous outcomes? | Leaning | 30-45 minute focused sessions with one clear objective. Commit after every completed task. Plan-first cycle: research.md → plan.md → annotate → implement. Use hooks (PreToolUse, PostToolUse) for technical guardrails — text rules alone don't hold. Maintain GUARDRAILS.md for learned failure patterns. Treat AI output as untrusted junior dev code — review all diffs. |

---

*When a question gets resolved, update status to "Decided" and add an entry to [decision-log.md](decision-log.md) with the rationale.*
