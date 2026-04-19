# Slice 1 Requirements: Onboarding → Plan

> **Requirements only — not a specification and not an implementation plan.** Captures the "what" at a level that survives implementation discoveries. The "how" is written as a spec in a fresh session at build time. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`.

## Purpose

Turn a freshly-authenticated user into a user with a training plan. Multi-turn chat-driven onboarding builds the user profile; on completion, the system generates a macro/meso/micro plan and renders it on the home surface.

## Functional requirements

When this slice is complete:

- An authenticated new user is directed into an onboarding flow.
- The flow is conversational and multi-turn — not a single form — covering the profile topics the coaching layer needs (primary goal, target event + date if any, current fitness level and recent running history, weekly schedule constraints, injury history, preferences).
- The user can see where they are in the flow (progress indication of some kind).
- On completion, the system generates the user's initial training plan and persists it.
- The home surface renders the plan — at minimum this week's prescribed work plus the upcoming structure (macro phase, meso template).
- Reloading the page after onboarding shows the same plan (not regenerated from scratch each time).
- The user can re-trigger plan generation (from a settings action or equivalent) if their onboarding answers change.

## Quality requirements

- Integration tests cover the onboarding controller across the multi-turn flow, including completion triggering plan generation and persistence.
- Evaluation scenarios (extending the existing M.E.AI.Evaluation infrastructure) cover plan quality across the five existing test profiles and several safety-adjacent onboarding inputs.
- One E2E test: register → complete onboarding → see plan on home.
- Plan projection is stable enough that the frontend renders it without special-casing.

## Scope: In

- Multi-turn onboarding API (one turn per request, server maintains or receives the accumulating state).
- `UserProfile` EF Core entity (1:1 with `ApplicationUser`) holding the onboarding answers and the current fitness assessment.
- Marten-backed `Plan` aggregate: `PlanGenerated` event, current-plan projection document.
- Plan rendering surface on the home page (this-week card + upcoming structure).
- Settings-level "regenerate plan" action.
- Versioned onboarding prompt YAML (same pattern as existing `coaching-v*.yaml`).

## Scope: Out (deferred)

- Workout logging (Slice 2).
- Plan adaptation from logs (Slice 3).
- Open-ended coaching conversation (Slice 4).
- Pre-public-release safety scaffolding: extended health screening (PAR-Q+), expanded medical keyword triggers, population-adjusted guardrails. Onboarding asks about injury history for planning context; it does NOT yet enforce the pre-public-release safety gates (see `docs/planning/safety-and-legal.md`).
- Proactive coaching messages (later).

## Pragmatic defaults for deferred decisions

- **Onboarding completion criterion:** profile completeness across a defined set of fields. The spec picks the exact set.
- **State management:** the spec decides whether in-progress onboarding lives in a `UserProfile` status column, in the Marten stream, or is passed by the client each turn. Default bias: simplest option that allows a user to close the browser mid-onboarding and resume.
- **Plan generation invocation:** use the existing brain layer (`ContextAssembler` + `ClaudeCoachingLlm` + prompt store + training-science calculators). Do not introduce a parallel coaching path.
- **Plan projection schema:** structured enough that the frontend can render without LLM calls; evolvable enough that later slices can add fields (adaptations, logs) without a projection rebuild.

## Research to consult before writing the spec

- `docs/research/artifacts/batch-2a-training-methodologies.md` — what the AI needs to know about plan structure and methodology selection.
- `docs/research/artifacts/batch-2b-planning-architecture.md` — macro/meso/micro tier semantics, event-sourcing patterns, event-driven recomposition.
- `docs/research/artifacts/batch-4a-coaching-conversation-design.md` — onboarding tone, question ordering, OARS / GROW-style patterns, trust-building.
- `docs/research/artifacts/batch-6a-llm-eval-strategies.md` + `batch-6b-dotnet-llm-testing-tooling.md` — eval patterns to apply to onboarding scenarios.
- `docs/research/artifacts/batch-7a-ichatclient-structured-output-bridge.md` — structured output via Anthropic constrained decoding (each onboarding turn has a structured schema for the next-question decision).
- `docs/research/artifacts/batch-4b-special-populations-safety.md` — what onboarding can reasonably surface (injury history, pregnancy, chronic conditions) even though the pre-public-release safety gates are deferred.
- `docs/planning/interaction-model.md` — the "guided onboarding" interaction mode.
- `docs/planning/coaching-persona.md` — voice and register for the onboarding experience.

## Open items for the spec-writing session to resolve

- Exact onboarding question set and ordering (the spec picks based on `interaction-model.md` topics + `coaching-persona.md` voice).
- In-progress onboarding state persistence strategy (column vs. Marten stream vs. client-held).
- Plan projection schema — structure that frontend renders directly, that adaptation slices can append to, and that survives re-projection from the event stream.
- Whether the macro plan is generated in one LLM call or tiered into separate macro/meso/micro calls (research artifact 2b covers the tradeoff; spec decides).
- Where "regenerate plan" lives in the UI — settings action, home-page action, or chat-panel command.
- What happens if plan generation fails (retry, fallback, error surface).
- Whether `UserProfile.OnboardingStatus` (or equivalent) is needed given the Marten event stream records `PlanGenerated`.

## How this feeds the spec

When Slice 1 implementation begins in a fresh session:

1. Read this doc + the cycle plan + the research artifacts listed above + the existing `ContextAssembler` / prompt store / training-science implementations from POC 1.
2. Brainstorm with the user (or `cw-spec` skill) to pick concrete answers to the open items above.
3. Write the spec under `docs/specs/slice-1-onboarding/` (or the agreed location).
4. User reviews the spec before implementation starts.
5. Implement against the spec. Discoveries amend the spec.

## Relationship to the cycle plan

The cycle plan's "Slice 1 — Onboarding → Plan" section carries the acceptance criteria and a brief scope summary. This doc elaborates one level deeper without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match.
