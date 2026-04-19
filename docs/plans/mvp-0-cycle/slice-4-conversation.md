# Slice 4 Requirements: Open Conversation

> **Requirements only — not a specification and not an implementation plan.** Captures the "what" at a level that survives implementation discoveries. The "how" is written as a spec in a fresh session at build time. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`.

## Purpose

Turn the read-only chat panel from Slice 3 into an interactive coaching surface. The user can ask anything grounded in their plan, profile, and history — and the coach answers in voice, with the full context the deterministic + LLM stack can assemble. This slice completes the three-interaction-mode design from `docs/planning/interaction-model.md` (onboarding, proactive adaptation, open conversation).

## Functional requirements

When this slice is complete:

- The chat panel accepts user input and renders assistant responses.
- Responses stream (user sees tokens as they arrive) rather than blocking on completion.
- Conversations persist across sessions — reloading the page shows the recent history.
- Responses are grounded in the user's profile + current plan + recent workout logs + recent conversation turns.
- The three interaction modes from `interaction-model.md` are all working end-to-end: guided onboarding (Slice 1), proactive adaptation messages (Slice 3), open conversation (this slice).
- Questions that include safety-relevant signals (pain, injury, medical-scope, crisis) produce responses consistent with the coaching-persona safety playbooks, including professional-referral language where appropriate.
- The coaching responses reflect the established voice and tone (`docs/planning/coaching-persona.md`) — not generic chatbot speak.

## Quality requirements

- Integration tests cover the conversation endpoint: streaming completes, context is assembled correctly, conversation history is persisted in the right order.
- Evaluation scenarios cover representative open-conversation cases: "how am I doing?" (status check), "my knee feels tight" (injury signal), "can I skip tomorrow?" (schedule adjustment), "should I push harder?" (intensity question), plus a handful of safety-adjacent prompts.
- One E2E test: authenticated user asks a question in the chat panel, sees a streaming response grounded in their plan.
- Conversation history rendering is correct across reload and across multi-day gaps.

## Scope: In

- Streaming conversation endpoint accepting a user turn and returning the assistant turn.
- Full `ConversationTurn` persistence for user-initiated turns (slice 3 introduced the entity for adaptation explanations; this slice adds the user side).
- Interactive chat panel UI: input, streaming response rendering, scrollable history, always-visible on the home surface (right rail on desktop, bottom drawer on mobile per the hybrid UI direction from session brainstorm).
- `ContextAssembler` routing by query type per the existing design — different question shapes pull different history windows.
- Lightweight intent classification if the spec decides it's needed (to route "how was my run?" vs. "I want to change my goal" to different context assemblies).

## Scope: Out (deferred)

- Voice input / speech-to-text.
- Multi-device conversation continuity beyond what simple DB persistence gives (no cross-device real-time sync).
- Proactive unprompted messages that aren't tied to a log or an adaptation (later).
- User-initiated goal changes that rewrite the macro plan (DEC-012 level 4 — likely a separate slice or a later MVP).
- Conversation summarization to keep context small — relevant as history grows, not needed for personal-use Day 1.
- Multi-conversation management (threading, topics, archiving) — single rolling conversation is the model.
- Tool use / function calling on the coach side (e.g., letting the coach pull in external data mid-response).

## Pragmatic defaults for deferred decisions

- **Context window per turn:** assemble per the existing `ContextAssembler` design — static reference data (profile, plan, metrics) at the start, recent turns + current user turn at the end. Token budget: the existing ~15K target.
- **Recent-turns window:** last 10 turns (5 pairs) in context. The spec revisits based on eval performance.
- **Intent classification:** simplest viable. If the single-prompt approach produces good responses in eval, no classifier. If not, a lightweight Haiku classifier to pick a context-routing path.
- **Streaming transport:** the spec picks (Server-Sent Events, chunked HTTP, WebSockets). Default bias: simplest option that works with RTK Query + React 19 + the existing ASP.NET Core stack.
- **Safety handling:** respond within the coaching-persona guardrails, reference professional help where medical-scope or crisis-language is detected, never prescribe medical advice.

## Research to consult before writing the spec

- `docs/research/artifacts/batch-4a-coaching-conversation-design.md` — open-conversation tone, intent classification, response patterns (OARS, Elicit-Provide-Elicit, GROW).
- `docs/research/artifacts/batch-4b-special-populations-safety.md` — keyword triggers for safety escalation (injury, crisis, medical scope).
- `docs/research/artifacts/batch-2c-testing-nondeterministic.md` — eval patterns for open-conversation quality.
- `docs/research/artifacts/batch-6a-llm-eval-strategies.md` — LLM-as-judge for conversational quality.
- `docs/planning/interaction-model.md` — full interaction model, proactive tone, conversational tone, guardrails.
- `docs/planning/coaching-persona.md` — voice, the eight most common coaching conversation playbooks.
- `docs/planning/memory-and-architecture.md` — context injection strategy, positional optimization, prompt caching opportunities.

## Open items for the spec-writing session to resolve

- Streaming transport choice (SSE vs. chunked HTTP vs. WebSocket) and how it integrates with RTK Query's caching model (which isn't stream-native).
- Intent classification: needed or not, and if so, via what mechanism.
- Context-routing rules — which question shapes pull which history windows.
- Rate limiting on user turns (personal-use isn't adversarial but may still want reasonable bounds).
- When conversation history gets long enough to warrant truncation or summarization, what triggers it.
- How proactive messages (from Slice 3) and user-initiated turns coexist in the same panel — ordering, visual distinction.
- Error handling when the LLM fails mid-stream (partial response visible, retry affordance, etc.).

## How this feeds the spec

When Slice 4 implementation begins in a fresh session:

1. Read this doc + the cycle plan + research artifacts above + the `ContextAssembler` / `ConversationTurn` / chat-panel work shipped in Slices 1-3.
2. Brainstorm with the user (or `cw-spec`) — streaming transport and intent classification are the biggest open questions.
3. Write the spec under `docs/specs/slice-4-conversation/`.
4. User reviews before implementation.
5. Implement against the spec.

## Relationship to the cycle plan

The cycle plan's "Slice 4 — Open Conversation" section carries acceptance criteria and a brief scope summary; this doc elaborates without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match.
