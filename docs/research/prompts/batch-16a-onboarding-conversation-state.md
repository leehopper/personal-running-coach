# Research Prompt: Batch 16a — R-048

# Multi-Turn LLM Onboarding Conversation-State Persistence Patterns (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a multi-turn, LLM-driven onboarding flow that incrementally builds a user-profile entity over the course of a chat conversation, what are the current 2026 industry patterns for persisting in-progress conversation state, and which fits a stack with ASP.NET Core 10 + EF Core (relational user state) + Marten (event-sourced plan state) + a thin LLM adapter (`ICoachingLlm` over Anthropic)?

## Context

I'm preparing to write the spec for **Slice 1 (Onboarding → Plan)** of MVP-0 of an AI-powered running coach (RunCoach). Slice 0 (`docs/specs/12-spec-slice-0-foundation/`) just landed with foundation patterns informed by Batch 15 research (R-044 through R-047). The next slice introduces:

- A multi-turn chat-driven onboarding flow that covers: primary goal, target event + date, current fitness, weekly schedule, injury history, preferences (per `docs/planning/interaction-model.md`).
- A `UserProfile` entity (EF Core, relational) that the onboarding flow populates.
- Plan generation that runs once onboarding completes — emits a `PlanGenerated` event into the per-user Marten stream (per R-047's stream-per-user Guid + Inline projection model).
- A "re-trigger plan generation" action accessible from settings (for iteration/correction).

The cycle plan's "Unknowns likely to surface" section pre-flagged: *"Onboarding conversation-state persistence — Slice 1 multi-turn onboarding needs a state model. Whether in-progress state lives in a column, in the Marten stream, or in the client is a real architectural question."*

Existing project context that constrains the answer:

- The backend is module-first per `backend/CLAUDE.md`. EF Core owns mutable relational state (Identity, `UserProfile`, future `WorkoutLog`, `ConversationTurn`). Marten owns event-sourced state (the `Plan` aggregate).
- LLM calls go through `ICoachingLlm` and `ContextAssembler` (existing primitives from POC 1). Each turn is one LLM call.
- Anthropic is the LLM provider. The Anthropic API is stateless (no thread/session resource) — every turn must reconstruct full context server-side. This is different from OpenAI Assistants API (which has a stateful Thread) and from LangChain conversation memory (which assumes process-local Python state).
- DEC-044 (just landed) made the browser auth a cookie-based session; the SPA is **not** holding a session token. That doesn't directly affect onboarding, but it does mean "store state in the client" patterns must reckon with the SPA being an untrusted, refresh-prone surface.
- Marten registration (Slice 0) uses `TenancyStyle.Conjoined` with `tenant_id = userId.ToString()` and `StreamIdentity.AsGuid`. Streams-per-user is established for the `Plan` aggregate.
- The cycle plan's Slice 1 acceptance includes: re-trigger plan generation from settings (so onboarding answers must be persistent, not ephemeral); reload page mid-onboarding (so in-progress state must survive a refresh).

## Research Question

**Primary:** What is the current 2026 best-practice pattern for persisting multi-turn onboarding-conversation state in a server-side application stack like the one above, and which of the candidate models — EF column, Marten event stream, hybrid, or client-driven — fits RunCoach's invariants?

**Sub-questions (must be actionable):**

1. **The candidate models — survey and tradeoffs.** Compare each across: resumability (user closes tab mid-flow), idempotency on retry, replay of the conversation for audit, ability to re-derive the `UserProfile` from history, complexity of the per-turn handler, multi-tab behavior:
   - (a) **EF column on `UserProfile`** — `OnboardingStatus` enum + accumulating `Answers JSONB` column updated each turn.
   - (b) **Dedicated `OnboardingSession` EF table** — separate row per session with status, answers JSONB, last-activity timestamp; cleared/archived on completion.
   - (c) **Marten event stream per user-onboarding** — events like `OnboardingStarted`, `AnswerCaptured`, `OnboardingCompleted`; current `UserProfile` shape derived via projection.
   - (d) **Marten event stream PLUS `UserProfile` EF projection** — events drive the audit trail, projection materializes the user-facing entity.
   - (e) **Client-side accumulation** — each request carries the full accumulating state; server is stateless beyond Identity.
   - (f) **Hybrid** — pick mix based on which sub-property of the state.

2. **The 2026 industry pattern for chatbot-style multi-turn capture.** What do production systems (LangChain memory backends, OpenAI Assistants threads, Anthropic stateless replays, Vercel AI SDK + Postgres examples, GitHub-discoverable patterns from chatbot-ish onboarding products) actually do? Are server-side append-only logs the current default, or do most systems lean on the LLM provider's own conversation primitive?

3. **Coupling to Anthropic stateless API specifically.** Anthropic does not have an OpenAI-Assistants-style stateful Thread. Every call sends the full message list. Does this favor an event-stream model (replay-friendly) over a column-snapshot model (rebuild-from-snapshot per turn)? What's the prompt-cost implication of either approach at MVP-0 scale?

4. **`ContextAssembler` integration.** RunCoach's existing `ContextAssembler` builds prompts by composing structured context blocks. For onboarding turns, the assembler needs (a) the current accumulated answers, (b) the original onboarding question script, (c) the next question's intent. Which state model gives the cleanest assembler interface? Where does the "next question to ask" decision live — in the LLM, in a deterministic state machine, or in a hybrid?

5. **Re-trigger plan generation later (cycle plan acceptance criterion).** The user can later re-trigger plan generation from settings. Does this mean: (a) re-run onboarding from scratch (state model: archived prior session + new session), (b) accept new "regeneration intent" without re-asking everything (state model: stable `UserProfile` with `last_regenerated_at`), or (c) something hybrid? Whichever the answer, which state model supports it without backflips?

6. **Resumability across browser refresh and across devices.** User starts onboarding on laptop, finishes on phone — does the state model handle this? What's the typical session-handoff pattern in 2026?

7. **Multi-tab behavior.** User opens onboarding in two tabs. What happens? (Locking? Last-write-wins? Merge?) Same question for a second device. The interesting question is what the *recommended* posture is, not what's possible.

8. **Idempotency and retry safety.** LLM calls fail. Network drops. What's the retry-safety story for each model? Append-only event streams are naturally idempotent if events carry deterministic ids; mutable EF columns require optimistic-concurrency tokens or upserts. Which is right for this volume?

9. **Eventual `Plan` event interaction.** When onboarding completes and the `PlanGenerated` event lands in the user's Marten stream (per R-047), should the onboarding events be in the SAME stream (one stream-per-user with everything including onboarding events) or a SEPARATE stream (`onboarding-{userId}` distinct from `plan-{userId}`)? What's the Marten-community 2026 idiom?

10. **Onboarding completion criteria.** "Onboarding complete" might be deterministic (all required questions answered) or LLM-judged (the LLM decides we have enough). Which is better for Slice 1, and how does the state model express it?

11. **Conversation-history persistence beyond MVP.** Slice 4 introduces open conversation. Does the onboarding-state pattern align with the eventual conversation-history pattern (`ConversationTurn` per the cycle plan), or are they intentionally different? If different, why?

12. **Privacy / data-deletion implications.** Onboarding captures injury history and may capture sensitive context (per `batch-4b-special-populations-safety.md`). Does the chosen state model affect a future "delete my data" request — append-only event streams complicate erasure under GDPR-ish expectations. The cycle plan defers full ToS / privacy to pre-public-release, but the state model decides what's *possible* later.

## Why It Matters

- **Slice 1 is the next slice** — the state model is on the critical path for the next implementation session.
- **Marten registration is now live** — Slice 0 wired Marten with `StreamIdentity.AsGuid` per-user. The choice of "onboarding events go in the per-user stream OR a separate stream OR no Marten at all" interacts directly with that registration.
- **Set the conversation pattern for the rest of MVP-0** — Slice 4 adds open conversation; the persistence model picked for onboarding likely sets the precedent.
- **Pre-MVP-1 audit-trail expectations** — once friends/testers join, "what did the AI know about me when it generated this plan?" becomes a real question. Append-only state has a much better answer than a mutable column.
- **The same-pattern-as-Batch-15 logic applies:** rework cost is highest at the foundation layer (the state-model choice). Research before — not after — Slice 1 implementation begins.

## Deliverables

- **A concrete recommendation** with one chosen pattern (a–f) and the explicit rationale.
- **A capability matrix** comparing the candidate patterns across resumability, idempotency, replay/audit, multi-tab/multi-device, retry safety, GDPR-erasability, and `ContextAssembler` integration cost.
- **A "next question" ownership recommendation** — deterministic state machine, LLM-decided, or hybrid — with rationale.
- **An onboarding-completion-criteria recommendation** — deterministic, LLM-judged, or hybrid.
- **A wiring sketch** — for the recommended pattern, the EF entity / Marten events / handler shape that Slice 1 will implement, including the integration with `ContextAssembler`.
- **A re-trigger-plan-generation handling note** — how the recommended pattern supports the cycle plan's settings-action regeneration flow without architectural rework.
- **An interaction note with the Slice 4 `ConversationTurn` pattern** — does the onboarding choice set or constrain it?
- **Citations** — current docs from Vercel AI SDK / LangChain / Anthropic SDK / Marten / EF Core, plus 2025–2026 community patterns.

## Out of Scope

- The training-science domain content of the onboarding questions themselves — already covered by `batch-2a-training-methodologies.md` and `batch-4a-coaching-conversation-design.md`.
- Voice / mid-run logging — explicitly out of MVP-0 scope per cycle plan.
- Multi-modal input (audio, photo of GPS watch screen) — out of scope.
- Onboarding for users with imperial-unit preference — `batch-9b-unit-system-design.md` is the reference; not relevant to state-model choice.
- The conversation-quality eval scenarios for onboarding — covered by `batch-6a-llm-eval-strategies.md`; not relevant here.
