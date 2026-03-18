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
| What triggers a micro vs. meso vs. macro replan? | Open | Micro: any logged workout deviation. Meso: cumulative pattern over a week+. Macro: goal change, injury, major timeline shift. But where exactly are the thresholds? |
| How does the system weight subjective input vs. objective data? | Open | "I feel great" vs. HRV says you're fatigued. Who wins? Probably need a framework for reconciling conflicting signals. |
| How aggressively should it redistribute missed mileage? | Open | Too aggressive = injury risk. Too passive = falls behind plan. Needs POC testing with simulated scenarios. |

## Memory & Context

| Question | Status | Notes |
|----------|--------|-------|
| What context gets injected per AI call? How is it structured? | Open | This is the foundational design question. POC 1 should explore this directly. |
| How is long-term history summarized to fit context windows? | Open | Rolling summaries? Statistical aggregates? Trend narratives? Needs experimentation. |
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
| How does the user undo / reset when the AI is on a bad track? | Open | Biggest challenge: users will prompt the AI in unexpected or potentially malicious ways, and AI state builds on itself — one bad input can cascade. Need a way to backtrack. Could range from simple ("reset my plan") to nuanced ("ignore what I said last week about my knee"). Touches safety, trust, and architecture. Not MVP but needs early thinking. |

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
| Does the MVP need any passive data integration? | Open | Manual logging might be enough for MVP. But auto-import from Strava/Garmin would significantly reduce friction. |

## Business

| Question | Status | Notes |
|----------|--------|-------|
| Regeneration limits per tier: what are the right numbers? | Open | Depends on what "regeneration" means and what the actual API costs look like. |
| What qualifies as a "regeneration" vs. a minor adjustment? | Open | Full replan of meso+micro = regeneration? Single workout swap = minor? Need a clear definition. |

## Data & Privacy

| Question | Status | Notes |
|----------|--------|-------|
| What are the data privacy implications of storing health-adjacent data? | Open | Training history, body metrics, injury history, sleep data, subjective wellbeing — this is sensitive even pre-scale. Not a blocker now, but make sensible default choices: encrypt at rest, don't log PII unnecessarily, don't send health data to third-party analytics. Becomes a real compliance question (HIPAA-adjacent, GDPR) if the product scales or goes international. |
| Where does user data live relative to the AI? | Open | When the AI processes a user's context, what gets sent to the model provider? If BYOM is in play, user data flows through their chosen provider. Need to understand what data exposure looks like per architecture choice. |

## Safety

| Question | Status | Notes |
|----------|--------|-------|
| How to guardrail against unsafe training advice (injury risk)? | Open | The AI could suggest dangerous load increases. Need guardrails around rate of progression, rest requirements, injury signals. |
| What disclaimers or limitations are needed? | Open | "Not medical advice" at minimum. Need to research what similar apps do here. |
| How does the system handle AI output failures? | Open | LLMs hallucinate — what happens when the AI produces an incoherent plan, contradicts itself, or suggests something nonsensical? Two layers to consider: (1) programmatic validation that catches obvious issues (no rest days, unsafe mileage jumps, impossible paces) before showing output to user, and (2) user feedback mechanism (flag/thumbs down) for subtle problems the system can't catch. The R-007 scoring criteria could double as a production validation layer. |

## Development Workflow

| Question | Status | Notes |
|----------|--------|-------|
| How to structure the project for clean phase-to-phase context handoff? | Open | Planning docs → POC → MVP → post-MVP. Each phase needs to absorb prior context and produce artifacts the next phase can consume. Current planning docs are layer one. Need a convention for how POC findings flow back, how the codebase references planning decisions, and how a fresh agent session gets up to speed fast. See R-009. |
| How to keep ideation and active build separated? | Open | Ideas will keep coming during the build. They need a path back to the planning docs (features/backlog, open-questions) without polluting the codebase. Need a lightweight workflow — not a process tax — that captures ideas in the right place during dev sessions. |
| What does the CLAUDE.md / agent scaffolding look like for this project? | Open | The repo needs a CLAUDE.md (or equivalent) that gives any agent session enough context to work autonomously. What goes in it? How does it reference these planning docs? How much is static vs. dynamically assembled? See R-008 and R-009. |
| How to scope agent tasks for good autonomous outcomes? | Open | Side project = limited supervision. Agents that go off-track waste scarce time. Need clear task scoping conventions, guardrails, and possibly a "preflight checklist" pattern for each agent session. |

---

*When a question gets resolved, update status to "Decided" and add an entry to [decision-log.md](decision-log.md) with the rationale.*
