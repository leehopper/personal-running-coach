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

## Pre-resolved by R-048 / DEC-047

The following decisions were locked by R-048 (`docs/research/artifacts/batch-16a-onboarding-conversation-state.md`) and DEC-047. The spec MUST honor them; per-spec amendment is allowed only if implementation surfaces a contradicting constraint.

- **State persistence:** Pattern (d) — dedicated Marten event stream per user for onboarding (`onboarding-{DeterministicGuid(userId, "onboarding")}` via UUID-v5 shape), inline `SingleStreamProjection<OnboardingView, Guid>` for the in-flight read model, separate `EfCoreSingleStreamProjection<UserProfile, AppDbContext>` (via `Marten.EntityFrameworkCore`) materializing user-facing fields into the EF `UserProfile` row in the same transaction. **Onboarding events live in their own stream** — NOT commingled with the Plan stream. On `OnboardingCompleted`, a Wolverine event subscription opens a fresh Plan stream with `CombGuidIdGeneration.NewGuid()` and stores `CurrentPlanId` on the EF `UserProfile`.
- **"Next question" ownership:** Hybrid, deterministic-led. A static topic list (`PrimaryGoal`, `TargetEvent`, `CurrentFitness`, `WeeklySchedule`, `InjuryHistory`, `Preferences`) controls slot order; the LLM phrases questions, handles follow-ups, and extracts structured answers per turn via Anthropic structured outputs.
- **Completion criterion:** Deterministic gate (all required fields present, all validate, no outstanding `needs_clarification` flag) with an LLM ambiguity pre-check (per-turn `needs_clarification: bool`, final `ready_for_plan` structured output).
- **Anthropic prompt caching:** Enabled from day one with `cache_control: { type: "ephemeral", ttl: "1h" }` at the top of the request body and a second explicit breakpoint on the system prompt. Per-turn `messages[]` is reconstructed by replaying onboarding events, NOT by snapshot — replay is byte-stable, snapshot is not.
- **Content-block serialization:** typed Anthropic content blocks carried verbatim in `UserTurnRecorded.ContentBlocks` and `AssistantTurnRecorded.ContentBlocks` (including future `thinking` / `tool_use` / `tool_result` / `signature` fields). `System.Text.Json` with declared property-order records — NOT `Dictionary<string, object>` — to guarantee byte-stable replay and cache-prefix stability.
- **Re-trigger plan generation from settings:** Reads `UserProfile` (or `OnboardingView`) — no replay needed; optionally accepts `RegenerationIntent`; calls `ContextAssembler.ComposeForPlanGeneration(userId, intent)`; starts a new Plan stream. Re-running onboarding from scratch is NOT required. If the user wants to *edit* specific answers, that's a separate `ReviseAnswer(Topic, NewValue)` command appending `AnswerCaptured` to the existing onboarding stream — preserving audit.
- **Per-turn handler:** Wolverine `[AggregateHandler]` over the onboarding stream. Idempotency via a client-supplied `IdempotencyKey` (UUID per user action, retained 24–48h in an `IdempotencyStore` EF row). Handler returns `(events, OutgoingMessages)`; Wolverine handles `FetchForWriting`, optimistic concurrency, transactional append + projection update.
- **GDPR erasability:** `store.Advanced.DeleteAllTenantDataAsync(userId.ToString(), ct)` wipes every Marten stream + projection doc for the tenant; pair with an EF `UserProfile` delete. Discipline: keep PII out of event payloads where feasible (`AnswerCaptured { Topic, NormalizedValue }` not free-text); use Marten's `AddMaskingRuleForProtectedInformation<T>` and `ApplyEventDataMasking()` where embedding PII is unavoidable.
- **Library pins:** Marten ≥ 8.20 (current 8.28) + `Marten.EntityFrameworkCore` for the EF projection; first-party `Anthropic` NuGet (v12.x as of April 2026) implementing `Microsoft.Extensions.AI.IChatClient` so the existing `ICoachingLlm` adapter sits over either the raw client or M.E.AI with a config switch.

## Pragmatic defaults for the remaining open decisions

- **Plan generation invocation:** use the existing brain layer (`ContextAssembler` + `ClaudeCoachingLlm` + prompt store + training-science calculators). Do not introduce a parallel coaching path.
- **Plan projection schema:** structured enough that the frontend can render without LLM calls; evolvable enough that later slices can add fields (adaptations, logs) without a projection rebuild.
- **`UserProfile.OnboardingCompletedAt` column** is set by the EF projection's `Apply(OnboardingCompleted)` handler — replaces a separate `OnboardingStatus` enum.

## Research to consult before writing the spec

- `docs/research/artifacts/batch-16a-onboarding-conversation-state.md` — **PRIMARY** input. Pattern (d), wiring sketch, completion gate, prompt-cache argument.
- `docs/research/artifacts/batch-15d-marten-per-user-aggregate-patterns.md` — Marten registration shape (already locked in Slice 0).
- `docs/research/artifacts/batch-2a-training-methodologies.md` — what the AI needs to know about plan structure and methodology selection.
- `docs/research/artifacts/batch-2b-planning-architecture.md` — macro/meso/micro tier semantics, event-sourcing patterns, event-driven recomposition.
- `docs/research/artifacts/batch-4a-coaching-conversation-design.md` — onboarding tone, question ordering, OARS / GROW-style patterns, trust-building.
- `docs/research/artifacts/batch-6a-llm-eval-strategies.md` + `batch-6b-dotnet-llm-testing-tooling.md` — eval patterns to apply to onboarding scenarios.
- `docs/research/artifacts/batch-7a-ichatclient-structured-output-bridge.md` — structured output via Anthropic constrained decoding.
- `docs/research/artifacts/batch-4b-special-populations-safety.md` — what onboarding can reasonably surface (injury history, pregnancy, chronic conditions) even though the pre-public-release safety gates are deferred.
- `docs/planning/interaction-model.md` — the "guided onboarding" interaction mode.
- `docs/planning/coaching-persona.md` — voice and register for the onboarding experience.

## Open items for the spec-writing session to resolve

- Exact onboarding question set and ordering (the spec picks based on `interaction-model.md` topics + `coaching-persona.md` voice).
- Plan projection schema — structure that frontend renders directly, that adaptation slices can append to, and that survives re-projection from the event stream.
- Whether the macro plan is generated in one LLM call or tiered into separate macro/meso/micro calls (research artifact 2b covers the tradeoff; spec decides).
- Where "regenerate plan" lives in the UI — settings action, home-page action, or chat-panel command.
- What happens if plan generation fails (retry policy, fallback, error surface).
- The shape of the `OnboardingView` projection (which fields, which validation rules, which `needs_clarification` semantics).
- Eval-suite extension pattern — which onboarding scenarios become eval fixtures and how cache-replay works for multi-turn flows.

## How this feeds the spec

When Slice 1 implementation begins in a fresh session:

1. Read this doc + the cycle plan + the research artifacts listed above + the existing `ContextAssembler` / prompt store / training-science implementations from POC 1.
2. Brainstorm with the user (or `cw-spec` skill) to pick concrete answers to the open items above.
3. Write the spec under `docs/specs/slice-1-onboarding/` (or the agreed location).
4. User reviews the spec before implementation starts.
5. Implement against the spec. Discoveries amend the spec.

## Relationship to the cycle plan

The cycle plan's "Slice 1 — Onboarding → Plan" section carries the acceptance criteria and a brief scope summary. This doc elaborates one level deeper without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match.
