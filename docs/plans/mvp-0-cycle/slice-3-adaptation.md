# Slice 3 Requirements: Adaptation Loop

> **Requirements only — not a specification and not an implementation plan.** Captures the "what" at a level that survives implementation discoveries. The "how" is written as a spec in a fresh session at build time. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`.

## Purpose

Make the adaptive-coaching differentiator visible. When a user logs a workout that deviates from prescription — quantitatively (distance/duration/HR off) or qualitatively (notes describe a setback, injury, external constraint) — the plan adjusts and the coach explains why. This is the slice that turns the product from "AI plan generator" into "AI coach."

## Functional requirements

When this slice is complete:

- Logging a workout triggers an adaptation evaluation by the coaching LLM.
- The LLM decides whether the deviation absorbs (no change), nudges (minor adjustment), or restructures (meaningful pattern change) — matching DEC-012's escalation ladder at levels 1-3 at minimum.
- When adaptation occurs, the plan view re-renders with the adjusted upcoming workouts.
- A persistent chat panel surfaces the coach's explanation of the change ("I adjusted your plan because...") in the moment, grounded in the log's notes + metrics.
- The chat panel in this slice is **read-only** — it displays adaptation-triggered messages but doesn't accept user input yet (Slice 4).
- Adaptation events are captured in the Marten event stream, queryable as the plan's evolution history.
- Log narratives that contain safety-relevant signals (injury mentions, crisis language) are handled with appropriate caution per the safety guardrails in `docs/planning/safety-and-legal.md`, even though the full pre-public-release safety scaffolding is deferred.

## Quality requirements

- Integration tests cover the full log → adaptation → event-appended → projection-updated path.
- Evaluation scenarios extend the existing eval suite across DEC-012 levels 1-3 (absorb / nudge / restructure), including:
  - "Absorb" cases where notes explain the deviation (kid in stroller, ran out of time).
  - "Nudge" cases where a pattern starts to show (second back-to-back easy pace higher than expected).
  - "Restructure" cases where notes signal a meaningful issue (knee pain, multiple missed sessions).
- Eval runs replay-mode in CI via committed fixtures, consistent with existing eval infrastructure.
- Safety-relevant signals in notes produce the appropriate response per the coaching-persona playbooks.
- The read-only chat panel renders streaming responses correctly.

## Scope: In

- Adaptation prompt + structured-output schema for plan modifications.
- Event-sourced plan modification: `PlanAdaptedFromLog` (or equivalent) event appended to the Marten stream; current-plan projection updated.
- `ConversationTurn` entity (arrives here, not Slice 4, because adaptation explanations are the first conversational content).
- Read-only chat panel UI on the home surface.
- Linking adaptation explanations back to the triggering `WorkoutLog` and the resulting plan-modification event.

## Scope: Out (deferred)

- User-initiated chat / open conversation (Slice 4 makes the chat panel interactive).
- Proactive coaching messages unrelated to logs (later — fatigue detection, taper initiation, missed-workout detection).
- Adaptation from non-log signals (missed workouts without a log, wearable passive data).
- DEC-012 level 4+ (plan overhaul, goal change) — this slice covers levels 1-3; level 4 typically involves explicit user confirmation which belongs to Slice 4 or later.
- Server-side rate limiting of adaptation runs (if we log N times quickly, N adaptations might be overkill — spec decides).

## Pragmatic defaults for deferred decisions

- **Adaptation triggering rule:** the LLM always runs on log — it may return "no adjustment" as a structured response. No upfront heuristic gating. The spec revisits if cost is a concern.
- **Structured output for plan modifications:** design structurally, not via `[Description]` hints (per the `MesoWeekOutput` lesson from DEC-042). The spec picks the exact schema.
- **Conversation message routing:** adaptation messages are a specific `ConversationTurn` role/type (not indistinguishable from user chat turns) so Slice 4 can render them distinctly.
- **Safety handling:** if notes mention injury, crisis, or medical-scope topics, the adaptation response is cautious and references professional help rather than prescribing changes. The existing coaching persona playbooks are the reference.

## Research to consult before writing the spec

- `docs/research/artifacts/batch-2b-planning-architecture.md` — event-driven recomposition, DEC-012 escalation ladder mapping, hysteresis thresholds to prevent flip-flopping.
- `docs/research/artifacts/batch-4a-coaching-conversation-design.md` — how to communicate plan changes (OARS, Elicit-Provide-Elicit, traffic-light shorthand).
- `docs/research/artifacts/batch-4b-special-populations-safety.md` — safety gates that must trigger before volume or intensity increases.
- `docs/research/artifacts/batch-2c-testing-nondeterministic.md` — adaptation evaluation patterns.
- `docs/research/artifacts/batch-6a-llm-eval-strategies.md` — LLM-as-judge patterns for adaptation quality.
- `docs/planning/coaching-persona.md` — voice and playbooks for the eight most common coaching conversations; adaptation messaging lives here.
- `docs/planning/interaction-model.md` — proactive coaching tone (DEC-027) and communication-mode mapping to escalation levels.
- `docs/decisions/decision-log.md` — DEC-012 (escalation ladder) specifically.

## Open items for the spec-writing session to resolve

- Adaptation-triggering rule: always invoke vs. gate by deviation threshold. Cost vs. quality.
- Exact structured-output schema for plan modifications. Must be robust to the DEC-042 invariant-enforcement lesson.
- How concurrent / rapid-fire logs are handled (debounce? queue? supersede?).
- Idempotency when the same log is submitted twice.
- UI for rendering the adaptation message in the chat panel: inline vs. expandable, with/without a "show the change" affordance that surfaces the before/after diff.
- What the user does if they disagree with an adaptation (accept as-is in this slice; manual override likely belongs to Slice 4 or later).
- Streaming vs. blocking for adaptation responses (streaming makes the "why" visible earlier; blocking is simpler to implement).

## How this feeds the spec

When Slice 3 implementation begins in a fresh session:

1. Read this doc + the cycle plan + research artifacts above + what shipped in Slices 1-2 (plan projection shape, `WorkoutLog` entity, `ContextAssembler` extensions).
2. Brainstorm with the user (or `cw-spec`) to resolve open items — the adaptation-triggering rule and structured-output schema are the biggest.
3. Write the spec under `docs/specs/slice-3-adaptation/`.
4. User reviews before implementation.
5. Implement against the spec.

## Relationship to the cycle plan

The cycle plan's "Slice 3 — Adaptation Loop" section carries acceptance criteria and a brief scope summary; this doc elaborates without crossing into implementation. If they conflict, the cycle plan wins — update this doc to match.
