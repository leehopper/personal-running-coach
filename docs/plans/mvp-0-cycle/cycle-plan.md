# MVP-0 + Adaptation Loop — Build Cycle Plan

> **Status:** Approved (2026-04-19). Active cycle.

## Status

- **Current Cycle:** MVP-0 + Adaptation Loop
- **Active Slice:** None yet — pre-slice-0 housekeeping pending
- **Next Step:** Pre-slice-0 housekeeping (compact `ROADMAP.md`, update `/catchup`, this doc becomes the front door), then Slice 0 (Foundation)
- **Blockers:** None
- **Cycle Plan:** `docs/plans/mvp-0-cycle/cycle-plan.md` (this file)

This status block is the single source of truth for "where are we?" — mirrored into `ROADMAP.md` so `/catchup` finds it. Update both whenever a slice completes or the active slice changes.

---

## Goal & Done-State

Build a real multi-tenant product where you can:

1. Sign up and log in.
2. Complete chat-driven onboarding that builds your user profile.
3. See a generated macro/meso/micro training plan on the home page.
4. Log a workout with as much or as little data as you want (bare minimum: distance + duration + completion; rich path: add RPE, HR, splits, HRV, weather — whatever you've got — plus freeform "what happened" notes).
5. Watch the plan adapt when logged workouts deviate from prescription, with the coach explaining *why* in a persistent chat panel.
6. Ask the coach open-ended questions grounded in your profile + plan + recent history.

"Done" = the above end-to-end loop works, you are personally using it to run, and the eval suite covers the adaptation scenarios that matter.

---

## In Scope

- **Auth:** ASP.NET Identity + JWT; register/login/logout UI; password reset deferred until pre-public-release.
- **Persistence:** PostgreSQL + EF Core (relational entities) + Marten (event-sourced plan state). Both run against the same Postgres instance; clear ownership boundaries per `backend/CLAUDE.md`.
- **Onboarding:** Chat-driven multi-turn flow covering the topics in `docs/planning/interaction-model.md` (primary goal, target event, current fitness, schedule constraints, injury history, preferences).
- **Plan generation + persistence:** Macro/meso/micro tiers per `docs/planning/planning-architecture.md`. Plan is a Marten event-sourced aggregate; projection = current plan document for LLM consumption.
- **Plan view:** Structured UI surface — this-week card, today's workout, upcoming list.
- **Workout logging:** Structured form with required core fields + optional rich metrics (JSONB) + freeform notes. Flexible: bare-minimum logs and rich logs both work; the LLM renders what's present and gracefully handles what isn't.
- **Adaptation loop:** Logged workout → deterministic deviation computation → LLM adaptation prompt with full context (including freeform notes) → structured-output plan modification → event appended to Marten stream → projection updated → plan view re-renders → chat panel surfaces the explanation.
- **Open conversation:** Persistent chat panel for ad-hoc coaching questions. Context-assembler routes by query type per the existing `ContextAssembler` design.
- **Eval-suite extension:** Slice 3 adds adaptation scenarios to the existing M.E.AI.Evaluation infrastructure. CI continues replay-only.

## Out of Scope (Deferred — Designed-For)

These are explicitly not built in this cycle, but the architecture leaves room so they bolt on without schema thrash or refactoring.

- **Apple HealthKit / iOS shim.** Auto-fill of workout metrics. Needs an iOS companion app per DEC-033. Design accommodation: `WorkoutLog.metrics` JSONB column takes whatever HealthKit gives us.
- **Garmin Connect integration.** Post-MVP-1 per `CLAUDE.md`. Design accommodation: same JSONB shape; webhook ingress pipeline is a future slice.
- **Pre-public-release safety scaffolding.** PAR-Q+ extended screening, medical-scope keyword triggers, population-adjusted guardrails, beta participation agreement, full ToS, LLC formation. Blocks public exposure, not personal use.
- **Tiered model routing.** Post-MVP-0 cost optimization per DEC-038. Design accommodation: existing `ICoachingLlm` interface is the natural seam.
- **Voice notes / mid-run logging.** Re-opens the temporal-binding problem (which workout does "I had to walk" refer to?). Further out.
- **Proactive notifications.** Light-touch missed-workout detection *may* land in Slice 4 if it's cheap; full proactive system deferred.
- **Coach personalities, multi-sport, nutrition guidance, injury prediction, social features.** All in `docs/features/backlog.md` as Future.

---

## Slice Structure

Each slice ships top-to-bottom through every layer (DB → repo → controller → frontend → tests) and is usable when done. The product is stoppable after any slice.

### Slice 0 — Foundation

**Acceptance — "I can…"**

- [ ] …run `docker compose up` and have Postgres + API + web all healthy.
- [ ] …hit `POST /api/v1/auth/register` and create an account.
- [ ] …hit `POST /api/v1/auth/login` and receive a JWT.
- [ ] …open the frontend, register/login through the UI, and see an authenticated empty home page.
- [ ] …see CI green on the slice-0 PR with all six required checks passing.

**Scope**

- Backend: `RunCoachDbContext` (EF Core) + Marten registration wired into DI; initial migration applied on startup in development. `Modules/Identity/` module with Identity tables, JWT issuance, register/login/logout endpoints. Global error-handling middleware.
- Frontend: `app/modules/auth/` with login + register pages, JWT stored in Redux + persisted, axios/fetch interceptor attaches the token, protected-route wrapper. Auth store slice.
- Tests: Integration tests for register/login/logout using `WebApplicationFactory` + Testcontainers. Component tests for auth pages. One Playwright happy-path E2E (register → login → see home).
- No business features — no plan, no logging, no coaching. Foundation only.

**Key risks**

- First time wiring Identity + EF Core + Marten + JWT together — integration surprises possible. Allocate time for this.
- Testcontainers + local Postgres configuration on macOS (Colima) — verified in existing CI but not yet exercised at this scope.

---

### Slice 1 — Onboarding → Plan

**Acceptance — "I can…"**

- [ ] …complete a multi-turn chat-driven onboarding flow that builds my user profile.
- [ ] …see a generated macro/meso/micro training plan on the home page after onboarding completes.
- [ ] …reload the page and see the same plan (persisted, not regenerated).
- [ ] …re-trigger plan generation from a settings action (for iteration / correction).

**Scope**

- Backend: `Modules/Training/` gains the Marten-backed `Plan` aggregate (events: `PlanGenerated`). `Modules/Coaching/` gains an `OnboardingController` — multi-turn: each POST returns either "next question" (with structured-output schema for the question + which profile field it fills) or "complete, plan generated." Uses the existing `ContextAssembler` and `ClaudeCoachingLlm`. `UserProfile` entity (EF Core) persists onboarding answers. Plan generation invokes the existing brain layer; projection materializes to a structured document.
- Frontend: `app/modules/onboarding/` — guided chat UI (progress indicator, "we're almost done" framing, not the day-to-day chat panel). `app/modules/plan/` — this-week card, today's workout, upcoming list. RTK Query slices.
- Tests: Integration tests for onboarding controller (multi-turn flow, completion). Eval cache extended with onboarding scenarios. Playwright: register → onboard → see plan.

**Key risks**

- Multi-turn onboarding state management — where does the "in-progress onboarding" live? Probably a `UserProfile.OnboardingStatus` column + per-turn requests that pass the accumulating state. Decide at slice-1 plan time.
- Plan projection shape — the existing brain layer emits plan documents; they need to land in a stable projection the frontend can render. Design the projection schema at slice-1 plan time.

---

### Slice 2 — Workout Logging

**Acceptance — "I can…"**

- [ ] …see today's prescribed workout on the home page.
- [ ] …open a log form, fill in at minimum distance + duration + completion, save it.
- [ ] …optionally expand "more details" and fill in RPE, HR avg/max, calories, splits, HRV, sleep score, weather — whatever I have — without the form yelling at me for missing fields.
- [ ] …write freeform "what happened?" notes and have them persisted.
- [ ] …see my logged workout appear in a history list, with notes visible.
- [ ] …verify via eval that the logged notes + metrics flow into LLM context (no adaptation wired yet — just context injection).

**Scope**

- Backend: `WorkoutLog` entity (EF Core). Required cols: `Id`, `UserId`, `PlannedWorkoutId` (nullable), `LoggedAt`, `Distance`, `Duration`, `CompletionStatus` (enum: complete/partial/skipped), `Notes`. One nullable JSONB col: `Metrics` (takes arbitrary keys: `rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`, etc. — no schema enforcement). Repo + log endpoint. `ContextAssembler` extension to include recent `WorkoutLog.Notes` + `Metrics` keys in the training-history block.
- Frontend: `app/modules/logging/` — today's workout card gets a "Log" action. Log form with collapsed "More details" expander. Render whatever metrics are present in the history list.
- Tests: Integration test for log endpoint. Unit tests for `ContextAssembler` extension (various metric shapes, including empty metrics). Playwright: today's workout → log with minimum → log with rich metrics → both appear in history.

**Key risks**

- JSONB key naming conventions — decide the canonical keys for the metrics most likely to come in (from manual entry now, from HealthKit later). Lock them in a shared constants file so manual logging and future auto-fill write the same shape.
- Metrics-absent LLM prompting — the context assembler must gracefully express "HR not provided" vs. "HR avg 142 bpm" without confusing the coaching prompt. Validate with eval scenarios.

---

### Slice 3 — Adaptation Loop

**Acceptance — "I can…"**

- [ ] …log a workout that deviates meaningfully from plan (e.g., distance way off, or freeform notes indicating walking/injury/external factor).
- [ ] …see the plan adjust in response, with the event stored in the Marten stream.
- [ ] …see the coach's explanation ("I adjusted your plan because…") appear in the chat panel.
- [ ] …verify via eval that adaptation handles the absorb/nudge/restructure cases correctly per DEC-012's escalation ladder (at least levels 1-3).

**Scope**

- Backend: Adaptation prompt + structured-output schema (events: `PlanAdaptedFromLog` with reason + modified workouts). Post-log hook triggers adaptation evaluation. Event appended to Marten stream; projection updated. Coach's explanation persisted as a `ConversationTurn` (new entity — see below).
- Frontend: Plan view re-renders after adaptation. Chat panel appears with the "I adjusted your plan because…" message. Panel is read-only in this slice — interactive input lands in Slice 4.
- Tests: Integration tests for the full log-triggers-adaptation path. Eval suite extended with 5-10 adaptation scenarios spanning DEC-012 levels 1-3 (absorb / nudge / restructure). Replay-mode in CI.
- `ConversationTurn` entity arrives here (not Slice 4) because adaptation explanations are the first conversational content.

**Key risks**

- Adaptation gate logic — when does a log trigger adaptation vs. no-op? Probably: always invoke the LLM adaptation prompt; let the LLM decide "no adjustment needed" and emit that as an event (or not, and skip the stream write). Decide at slice-3 plan time based on cost.
- Structured-output schema stability for plan modifications — the existing `MesoWeekOutput` restructuring lesson from DEC-042 applies; design structurally, not via `[Description]` hints.

---

### Slice 4 — Open Conversation

**Acceptance — "I can…"**

- [ ] …type a question into the chat panel ("how am I doing?", "should I push harder next week?", "my knee feels tight").
- [ ] …see a streaming response grounded in my profile + plan + recent logs.
- [ ] …have the conversation persist across sessions (chat history visible on reload).
- [ ] …see the system handle the three interaction modes from `docs/planning/interaction-model.md` — onboarding (slice 1), proactive adaptation messages (slice 3), and open conversation (this slice).

**Scope**

- Backend: Conversation endpoint (streaming). Full `ConversationTurn` persistence (user turns + assistant turns). `ContextAssembler` routes by query type per the existing design. Possibly a lightweight triage prompt to classify intent.
- Frontend: Chat panel becomes interactive — text input, streaming response rendering, conversation history. Panel is always visible (right rail on desktop, bottom drawer on mobile).
- Tests: Integration tests for the conversation endpoint (streaming, context routing). Eval scenarios for a few representative open-conversation prompts. Playwright: ask question → see grounded response.

**Key risks**

- Streaming response rendering in React + RTK Query — RTK Query isn't ideal for streams; may need raw fetch + state management for the chat panel alone.
- Context-assembler routing quality — the existing design mentions interaction-specific assembly; slice 4 is when that actually gets exercised in production flow (not just eval).

---

## Architecture Additions

### Backend module layout after this cycle

```
backend/src/RunCoach.Api/
  Program.cs
  Modules/
    Identity/                   # NEW — Slice 0
      AuthController.cs
      JwtIssuer.cs
      UserRegistrationService.cs
      Entities/                 # ApplicationUser (Identity-extended)
    Coaching/                   # EXISTING — extended in Slice 1, 3, 4
      ClaudeCoachingLlm.cs      # existing
      ContextAssembler.cs       # existing, extended in Slice 2
      OnboardingController.cs   # NEW — Slice 1
      AdaptationService.cs      # NEW — Slice 3
      ConversationController.cs # NEW — Slice 4 (read-only in Slice 3)
      Prompts/                  # existing
        coaching-v1.yaml
        onboarding-v1.yaml      # NEW — Slice 1
        adaptation-v1.yaml      # NEW — Slice 3
    Training/                   # EXISTING — extended in Slice 1, 2, 3
      Computations/             # existing (PaceZoneIndex, PaceZone, HR)
      Plan/                     # NEW — Slice 1
        PlanAggregate.cs
        Events/                 # PlanGenerated, PlanAdaptedFromLog, …
        Projections/            # current-plan document projection
      WorkoutLog/               # NEW — Slice 2
        WorkoutLog.cs
        WorkoutLogRepository.cs
        WorkoutLogController.cs
    Common/                     # existing
      BaseController.cs
  Infrastructure/
    ServiceCollectionExtensions.cs  # existing, extended
    RunCoachDbContext.cs            # NEW — Slice 0
    MartenConfiguration.cs          # NEW — Slice 0
    JwtMiddleware.cs                # NEW — Slice 0
    ErrorHandlingMiddleware.cs      # NEW — Slice 0
  Migrations/                       # NEW — Slice 0 onward (EF Core)
```

### Frontend module layout after this cycle

```
frontend/src/app/
  modules/
    auth/                       # NEW — Slice 0
      pages/{LoginPage,RegisterPage}.tsx
      store/authSlice.ts
      hooks/useAuth.ts
    onboarding/                 # NEW — Slice 1
      pages/OnboardingPage.tsx
      components/{ChatFlow,ProgressIndicator}.tsx
    plan/                       # NEW — Slice 1
      pages/HomePage.tsx
      components/{TodayCard,ThisWeek,UpcomingList}.tsx
    logging/                    # NEW — Slice 2
      components/{LogForm,MoreDetailsExpander,HistoryList}.tsx
    coaching/                   # NEW — Slice 3 (read-only) → Slice 4 (interactive)
      components/{ChatPanel,MessageList,ChatInput}.tsx
      store/chatSlice.ts
    common/                     # existing
    app/                        # existing — extended with ChatPanel layout slot
  api/                          # NEW — Slice 0 onward
    apiSlice.ts                 # RTK Query root
    auth.api.ts
    onboarding.api.ts           # Slice 1
    plan.api.ts                 # Slice 1
    workoutLog.api.ts           # Slice 2
    conversation.api.ts         # Slice 4
  pages/                        # existing (home/) — replaced/extended
```

---

## Data Model

### Relational (EF Core)

- **`ApplicationUser`** — extends `IdentityUser`. Identity-managed. No custom cols in this cycle.
- **`UserProfile`** — 1:1 with `ApplicationUser`. Onboarding answers (primary goal, target event + date, current fitness assessment, weekly schedule, injury history, preferences). `OnboardingStatus` column tracks in-progress onboarding.
- **`WorkoutLog`** — FK to `ApplicationUser`, optional FK to `PlannedWorkoutId` (matches a prescribed workout in the Marten projection). Required: `Distance`, `Duration`, `CompletionStatus`, `LoggedAt`, `Notes`. Nullable: `Metrics` (JSONB — arbitrary shape; canonical keys in a shared constants file).
- **`ConversationTurn`** — FK to `ApplicationUser`, `Role` (user/assistant/system-adaptation), `Content`, `CreatedAt`, optional FK to triggering `PlanEventId` (adaptation explanations link to the event that caused them).

### Event-sourced (Marten)

- **`Plan` aggregate** per user.
  - Events (evolve across slices):
    - `PlanGenerated` (Slice 1) — initial plan from onboarding.
    - `PlanAdaptedFromLog` (Slice 3) — plan modification triggered by a workout log. Includes reason + modified workouts.
    - `PlanRestructuredFromConversation` (Slice 4 or later) — plan modification triggered by a chat turn (goal change, injury report). Might land later.
    - `PlanRegenerated` (Slice 1+) — user-triggered regeneration for iteration/correction.
  - Projection: current plan document (macro phase schedule + this-week meso template + active-day micro prescriptions) as a single JSON document for LLM context injection.

### Why this split

- EF Core owns mutable user-state entities (profile, logs, turns) — standard CRUD, relational joins, Identity integration.
- Marten owns the plan — the coaching decisions, adaptation history, and audit trail. Event stream IS the audit trail per DEC-031 and `memory-and-architecture.md`.
- Both run against the same Postgres instance; the ownership boundary is entity-level, not database-level.

---

## Testing Strategy

- **Backend integration tests**: `WebApplicationFactory` + Testcontainers (real Postgres, not in-memory) per `backend/CLAUDE.md`. Every controller gets an integration test; the full log-triggers-adaptation path in Slice 3 is the flagship integration test.
- **Backend unit tests**: services, repositories, `ContextAssembler` extensions, computation extensions. Existing patterns hold.
- **Eval suite**: existing M.E.AI.Evaluation infrastructure. Extended in Slice 1 (onboarding scenarios — verify plan quality across profile types), Slice 3 (adaptation scenarios — absorb/nudge/restructure, freeform-notes interpretation), Slice 4 (open-conversation scenarios — coaching quality, safety). CI runs replay-only with committed fixtures per existing convention.
- **Frontend component tests**: Vitest + React Testing Library for significant components (forms, cards, chat panel).
- **Frontend E2E**: Playwright. One happy-path scenario per slice covering the "I can…" criteria end-to-end. Goal: "did this feature work end-to-end" — not exhaustive edge coverage.
- **Coverage**: maintain the existing 60% project / 70% patch Codecov thresholds. No new thresholds.

---

## Roadmapping Hygiene

This cycle introduces a three-tier roadmapping structure to stop `ROADMAP.md` from growing unboundedly and to make session-start catchup fast.

### Tier 1 — `ROADMAP.md` (front door)

Compacted to:

1. **Status block at top** — current cycle, active slice, next step, blockers, pointer to cycle plan. 10-15 lines max. Mirrored from this doc's Status section.
2. **Strategic links** — decision log, feature backlog, vision docs, forward-path items.
3. **Cycle History** — one-line-per-cycle log at the bottom. Each entry: cycle name, completion date, pointer to the cycle plan + key artifacts (PRs, specs, decisions). No narrative.

The 200-line "What's Been Done" narrative currently in `ROADMAP.md` moves out — decisions are already in `decision-log.md`, implementation details are in completed plan files under `docs/plans/`, git log holds the rest. `ROADMAP.md` is a status-first document, not a history document.

### Tier 2 — Cycle Plan (this doc)

Lives for the cycle duration at `docs/plans/{cycle-name}/cycle-plan.md`. Declares the active slice. Tracks slice acceptance criteria with checkboxes. When the cycle completes, it becomes a historical artifact (referenced from `ROADMAP.md` Cycle History).

### Tier 3 — Per-Slice Plan Files

`docs/plans/{cycle-name}/slice-N-{name}.md`. Written via the `superpowers:writing-plans` skill when each slice starts. Contains step-by-step implementation plan, test specs (BDD acceptance from this cycle plan), architectural decisions specific to the slice. DEC-008 plan-first rule applies — no slice implementation before the slice plan is written and reviewed.

### Per-Slice Hygiene Rule

Each slice's "done" criteria include:

- [ ] Slice acceptance checkboxes in this cycle plan marked complete.
- [ ] Cycle plan's Status section updated: active slice advanced to the next one (or "Cycle complete" if this was the last).
- [ ] `ROADMAP.md` Status block synced from this doc.
- [ ] If the slice produced durable architecture decisions, record them in `docs/decisions/decision-log.md`.
- [ ] Completed slice plan file stays in place (historical reference) — not deleted, not moved.

### `/catchup` Update

`.claude/commands/catchup.md` updated to walk the new tiers:

1. `ROADMAP.md` Status block — current cycle + active slice pointers.
2. The active cycle plan (path from Status block) — slice structure and progress.
3. The active slice plan if one exists (path from cycle plan Active Slice).
4. Last 5-10 commits on current branch.
5. Working-tree changes vs. `main` (if any).

Summarize in 3-5 sentences: current cycle, active slice, last shipped work, recommended next action.

### GitHub Issues vs. Plan Files (open process question)

Two reasonable tracking shapes for per-slice work:

- **Plan files** (current project precedent from POC 1): version-controlled, richer context, can include diagrams, closer to DEC-008 practice, private to the repo.
- **GitHub Issues + sub-issues**: better for atomic tracking, public visibility, integrates with PR-review tooling (`/review-pr`, CodeRabbit comments can reference issues).

**Default for this cycle: plan files.** Revisit at the slice-0/slice-1 boundary — by then slice 0 will have exposed whether the tracking granularity needs issue-level atomicity or plan-file-level connected reasoning. Decision deferred.

---

## Pre-Slice-0 Housekeeping

Before Slice 0 proper begins, a small housekeeping pass:

- [ ] Compact `ROADMAP.md` to the new shape (status block + strategic links + Cycle History).
- [ ] Move historical "What's Been Done" narrative out of `ROADMAP.md` — the detail is preserved in `decision-log.md`, existing plan files, git log, and merged PRs. `ROADMAP.md` should not duplicate it.
- [ ] Update `.claude/commands/catchup.md` to the new walk order.
- [ ] Verify `/catchup` on a fresh session and confirm the summary hits "where are we?" in one scan.
- [ ] Commit as a single atomic commit: `chore(roadmap): compact ROADMAP.md and /catchup for slice-based cycle tracking`.

This is a tiny pass — probably 30-60 minutes. It lands before the Slice 0 plan file is written so the new flow is in place when Slice 0 starts.

---

## Forward Path — Carried-Forward Learnings from the Brain

This cycle is built on top of real work that's already landed. Preserved patterns:

- **Deterministic + LLM split** — every slice respects the layering. Deterministic: plan structure, compliance, pace/HR zone computation, ACWR. LLM: coaching reasoning, adaptation narrative, open conversation. Never the other way around.
- **Prompts in versioned YAML** — `onboarding-v1.yaml` and `adaptation-v1.yaml` follow the `coaching-v1.yaml` pattern. Existing `YamlPromptStore` is the vehicle.
- **Constrained decoding via `AnthropicStructuredOutputClient`** — onboarding turns, plan generation, and adaptation all use structured output. Per the DEC-042 lesson: design invariants structurally, don't rely on `[Description]` hints.
- **Eval-first for LLM changes** — adaptation prompt changes are eval-gated. CI replay-only. `EVAL_CACHE_MODE` discipline preserved.
- **`ContextAssembler` as the central context-assembly primitive** — every new prompt invocation goes through it. Extensions for workout logs + conversation history happen here.
- **Floating model aliases** per DEC-037 — `claude-sonnet-4-6` for coaching, `claude-haiku-4-5` for judging. No hard-coded model IDs in application code.
- **Trademark discipline** per `CLAUDE.md` — user-facing text uses "Daniels-Gilbert zones" / "pace-zone index." Onboarding prompt, adaptation prompt, chat responses all subject to this.

---

## References

- `docs/planning/vision-and-principles.md` — why this exists, design principles.
- `docs/planning/interaction-model.md` — three interaction modes.
- `docs/planning/planning-architecture.md` — macro/meso/micro tiers, event-sourced plan state.
- `docs/planning/memory-and-architecture.md` — context injection strategy, five-layer summarization.
- `docs/planning/safety-and-legal.md` — safety guardrails (pre-public-release items live here).
- `docs/decisions/decision-log.md` — all architectural decisions.
- `docs/features/backlog.md` — feature backlog by priority tier.
- `backend/CLAUDE.md` — backend conventions, module-first organization, testing patterns.
- `frontend/CLAUDE.md` — frontend conventions.
- POC 1 plan: `docs/plans/poc-1-context-injection-plan-quality.md` (historical).
- POC 1 LLM testing plan: `docs/plans/poc-1-llm-testing-architecture.md` (historical).
