# MVP-0 + Adaptation Loop — Build Cycle Plan

> **Status:** Approved (2026-04-19). Active cycle.

## Status

- **Current Cycle:** MVP-0 + Adaptation Loop
- **Active Slice:** Slice 0 (Foundation) — Unit 1 persistence substrate ready for PR. DEC-048 composition corrections and DEC-049 (R-055 resolution) both applied 2026-04-20; full `WebApplicationFactory<Program>` fixture is green at 581/0/1.
- **Active Slice Spec:** `docs/specs/12-spec-slice-0-foundation/`
- **Next Step:** Commit the DEC-048 + DEC-049 work (squash-anyway PR strategy), open the Slice 0 Unit 1 PR (first PR of the cycle), begin Unit 2 (T02.x — Auth API). Slice 1 requirements doc (`./slice-1-onboarding.md`) was amended with R-048 / DEC-047 integration 2026-04-19.
- **Blockers:** None. R-055 resolved — artifact at `docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`; fix captured in DEC-049.

Pre-slice-0 housekeeping landed in PR #46 (commit `9d4c51e`). Slice 0 spec written 2026-04-19. Batch 15 research (R-044 through R-047) and Batch 16 research (R-048 through R-050) landed and integrated 2026-04-19 across two passes — the headline architectural pivots are: DEC-044 (cookie-not-JWT browser auth), DEC-045 (Aspire deferred to MVP-1, stay on Compose + Tilt with `docker-compose.otel.yml` overlay), DEC-046 (SOPS + age + Postgres-backed DataProtection + dotnet user-secrets), DEC-047 (onboarding event-sourcing pattern locked for Slice 1). Unit 1 implementation landed across six commits 2026-04-20 (`dc047b0` → `9b95291`), but a reproducible startup hang surfaced during T01.5 (`WebApplicationFactory<Program>` + SUT boot). R-054 / Batch 18a research returned the canonical composition recipe 2026-04-20: Marten's `IntegrateWithWolverine()` subsumes Wolverine's `PersistMessagesWithPostgresql()` — never call both. DEC-048 codifies the invariants; code changes applied. Investigation showed the hang is **inside `WebApplication.CreateBuilder(args)` itself** (Main runs but the framework builder-creation never returns) — deeper than R-054 diagnosed. R-055 / Batch 18b queued for a second deep-research pass focused on that symptom. Unit 1 ships with the scope-reduced IAsyncLifetime fixture; Unit 2 auth-endpoint integration tests are on hold pending R-055.

This status block is the single source of truth for "where are we?" — mirrored into `ROADMAP.md` so `/catchup` finds it. Update both whenever a slice completes or the active slice changes.

---

## Captured During Cycle

Running log of "we should also do this" items found during the cycle but intentionally deferred — preserves the affordance the old `ROADMAP.md` Deferred Items section had, scoped to the active cycle so the list doesn't grow unboundedly.

**How to use**

- Any agent (or human) may append an entry when finding work that shouldn't block the current slice but shouldn't be lost.
- Each slice's PR description includes a `### Follow-ups found` section (empty is fine); items move into this table at slice completion.
- At cycle completion, every entry gets one of four dispositions:
  - (a) promoted to `docs/features/backlog.md`,
  - (b) becomes its own `docs/decisions/decision-log.md` entry,
  - (c) becomes a research prompt (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)),
  - (d) scheduled into the next cycle.
- The table does not survive cycle completion un-triaged.

| Found | In slice | Item | Triage disposition |
|---|---|---|---|
| 2026-04-19 | (cycle-plan) | Frontend/backend breakdown pass on cycle-plan organization — sections currently mix layers, may read cleaner with explicit F/E vs B/E separation | Deferred; re-evaluate after Slice 1 when the shape of per-slice plans clarifies whether a layer-split would help |
| 2026-04-19 | Slice 0 (Batch 15 audit) | LLM context assembly for the projected `Plan` document — R-047 covers Marten's `WriteLatest<Plan>` zero-copy HTTP streaming, but RunCoach's `ContextAssembler` builds prompts internally (not over HTTP). The Plan-projection-to-prompt-tokens shape is undefined. | **Partially answered by R-048 / DEC-047** — `ContextAssembler.ComposeForClaude(view, appendUser: ...)` event-replay pattern is the shape; finalize the Plan-side projection→prompt at Slice 1 spec-writing time. Open. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Production deployment topology — single VPS / managed PaaS / container orchestrator / managed Postgres / CDN. R-046's bundle-as-Job production migration assumes *some* target. No env exists yet. | **Partially answered by R-049 / DEC-046** — secrets bootstrap layer is per-target (systemd-creds for VPS, native PaaS, ACA Key Vault references). Target choice itself still open. Pre-MVP-1 research prompt when target is committed. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Observability strategy — OTel exporter + collector + dashboard. R-047 listed Marten daemon metrics worth alerting on; zero observability infrastructure decided. | **Resolved for Slice 0 by R-050 / DEC-045** — `docker-compose.otel.yml` overlay (Collector + Jaeger) wired with Marten / Wolverine / `RunCoach.Llm` ActivitySource + Meter sources; transferable to Aspire later. Production observability remains a pre-MVP-1 prompt. |
| 2026-04-19 | Slice 0 (Batch 15 audit) | Database backup / restore / data lifecycle — Plan adaptation history is irreplaceable once real users exist. | Pre-MVP-1 research prompt; coordinate with the production-deployment-topology decision. Disposition (c) — research prompt. |
| 2026-04-19 | Slice 0 (Batch 16 integration) | FTC HBNR pre-public-release escalation point — R-049 confirmed the rule applies to RunCoach as a PHR vendor the moment any Apple Health / Strava / Garmin ingest exists; the migration to Azure Key Vault + Managed Identity wrapping `ProtectKeysWithAzureKeyVault` happens before the first non-alpha user. | Captured in DEC-046 cross-reference. Promote to a concrete pre-public-release task list (rotation runbook, breach runbook, DPAs with Anthropic + analytics, formal program against ASVS L1 V13.3) at MVP-1 cycle start. |
| 2026-04-19 | Slice 0 (Batch 17 audit) | Batch 17 research queued (R-051 LLM observability, R-052 Anthropic SDK choice, R-053 multi-turn eval pattern). All three target Slice 1's LLM call sites; **none block Slice 0**. | Slice 0 implementation can begin in parallel with Batch 17 research. Slice 1 spec session awaits the three artifacts. Disposition (c) — research prompts at `docs/research/prompts/batch-17{a,b,c}-*.md`. |
| 2026-04-20 | Slice 0 (T01.5 wrap-up → R-054 Batch 18a) | Reproducible startup hang surfaced during `WebApplicationFactory<Program>` SUT boot — 5-min `HostFactoryResolver.CreateHost` timeout, zero log output. Scope-reduced test shipped with T01.5 as documented follow-up. R-054 deep-research pass returned the canonical composition recipe (artifact: `batch-18a-dotnet10-marten-wolverine-aspire-otel-startup-composition.md`); spec amended 2026-04-20. | **Resolved (DEC-048 + code)**. Applied: delete `WolverinePostgresqlDataSourceExtension.cs`, drop `PersistMessagesWithPostgresql` (Marten's `IntegrateWithWolverine` subsumes it), add `ApplyJasperFxExtensions` before `AddMarten`/`UseWolverine`, pair `DaemonMode.Solo` + `DurabilityMode.Solo`, make OTLP exporter conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`, drop obsolete `public partial class Program`, resolve `NpgsqlDataSource` from DI via `sp.GetRequiredService<NpgsqlDataSource>()`, enable `ValidateScopes=true`/`ValidateOnBuild=false` in Dev. DEC-048 landed. |
| 2026-04-20 | Slice 0 (DEC-048 verification → R-055 Batch 18b) | After applying every R-054 correction the SUT host **still** hangs. Instrumented `Program.cs` tracing showed `Main` enters and writes the first trace line, then `WebApplication.CreateBuilder(args)` itself blocks for the full 5-min `HostFactoryResolver` window — deeper than R-054 diagnosed. R-054's three hypotheses (second `NpgsqlDataSource` connecting too early, Roslyn codegen in the 5-min window, `ValidateOnBuild=true` + scoped-from-root) do not match. `ASPNETCORE_PREVENTHOSTINGSTARTUP=true` does not unblock; `HOSTINGSTARTUPASSEMBLIES` was already empty. | **Resolved (DEC-049 + code).** R-055 artifact (`docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`) identified the cause as synchronous `FileSystemWatcher` init on macOS arm64 / Darwin 25.x — `PhysicalFilesWatcher.StartRaisingEvents` calls `Interop.Sys.Sync()` (dotnet/runtime#77793) which stalls unboundedly when three JSON config sources each install a watcher (appsettings, appsettings.Development, user-secrets — all default `reloadOnChange: true`). The prescribed §7.1 / §7.2 reductions confirmed it (without env var: zero stdout for 10 s; with `DOTNET_hostBuilder__reloadConfigOnChange=false`: `CreateBuilder` returns in ~3 s). Applied: `Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false")` at top of `Program.cs`, `builder.UseSetting(...)` belt-and-suspenders in `RunCoachAppFactory.ConfigureWebHost`, connection-string override via `ConnectionStrings__runcoach` env var in `InitializeAsync` (takes precedence over `appsettings.Development.json`). **Dormant bug unmasked:** `RunCoachDbContext.OnModelCreating`'s manual `MapWolverineEnvelopeStorage()` collided with `WolverineModelCustomizer`'s runtime call (duplicate `WolverineEnabled` annotation); removed from `OnModelCreating`. **Outcome:** 575/0/1 (scope-reduced) → 581/0/1 (full `WebApplicationFactory<Program>` fixture with six SUT-host smoke tests — IDocumentStore, RunCoachDbContext, DpKeysContext, IDataProtectionProvider, `/health`, NpgsqlDataSource identity-equality — all green in ≤ 2 s cold). DEC-049 captures the invariants. **Unit 2 (auth endpoints) is now unblocked.** |

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

**Tier-3 requirements docs live alongside this file** — one per slice, ~90 lines each. They elaborate requirements without crossing into implementation. When a slice's implementation session starts, that session reads the per-slice requirements doc + this cycle plan + the referenced research artifacts, then writes a spec under `docs/specs/`.

| # | Name | Requirements doc |
|---|---|---|
| 0 | Foundation | [`./slice-0-foundation.md`](./slice-0-foundation.md) |
| 1 | Onboarding → Plan | [`./slice-1-onboarding.md`](./slice-1-onboarding.md) |
| 2 | Workout Logging | [`./slice-2-logging.md`](./slice-2-logging.md) |
| 3 | Adaptation Loop | [`./slice-3-adaptation.md`](./slice-3-adaptation.md) |
| 4 | Open Conversation | [`./slice-4-conversation.md`](./slice-4-conversation.md) |

### Slice 0 — Foundation

**Requirements:** [`./slice-0-foundation.md`](./slice-0-foundation.md)


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

**Relevant research artifacts**

- `batch-10b-dotnet-backend-review-practices.md` — backend conventions applied here.
- `batch-10c-ci-quality-gates-private-repo.md` — CI pipeline structure.
- `batch-14a` / `batch-14b` / `batch-14c` / `batch-14f` — CodeRabbit, CodeQL, SonarQube, branch protection patterns.
- `batch-10a-frontend-latest-practices.md` — React 19 + TS + Vite conventions for the auth module.

---

### Slice 1 — Onboarding → Plan

**Requirements:** [`./slice-1-onboarding.md`](./slice-1-onboarding.md)

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

**Relevant research artifacts**

- `batch-2a-training-methodologies.md` — training-science basis for plan generation.
- `batch-2b-planning-architecture.md` — macro/meso/micro tier semantics, event-sourcing patterns.
- `batch-4a-coaching-conversation-design.md` — onboarding tone, question ordering, OARS/GROW patterns.
- `batch-6a-llm-eval-strategies.md` + `batch-6b-dotnet-llm-testing-tooling.md` — eval patterns for onboarding scenarios.
- `batch-7a-ichatclient-structured-output-bridge.md` — structured output for per-turn onboarding responses.
- `batch-4b-special-populations-safety.md` — safety considerations for onboarding profile questions (injury history, pregnancy, chronic conditions) even though pre-public-release safety scaffolding is deferred.

---

### Slice 2 — Workout Logging

**Requirements:** [`./slice-2-logging.md`](./slice-2-logging.md)

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

**Relevant research artifacts**

- `batch-3c-wearable-integrations.md` — what metric shapes to anticipate for future HealthKit/Garmin ingestion; informs canonical JSONB key choices.
- `batch-9b-unit-system-design.md` — distance/pace unit handling.
- `batch-10b-dotnet-backend-review-practices.md` — EF Core + JSONB patterns.

---

### Slice 3 — Adaptation Loop

**Requirements:** [`./slice-3-adaptation.md`](./slice-3-adaptation.md)

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

**Relevant research artifacts**

- `batch-2b-planning-architecture.md` — event-driven recomposition, DEC-012 escalation ladder mapping, hysteresis thresholds.
- `batch-4a-coaching-conversation-design.md` — how to communicate plan changes (OARS, Elicit-Provide-Elicit, traffic-light shorthand).
- `batch-4b-special-populations-safety.md` — safety gates that must trigger before any pace/volume increase.
- `batch-2c-testing-nondeterministic.md` — adaptation evaluation patterns.
- `batch-6a-llm-eval-strategies.md` — LLM-as-judge patterns for adaptation quality.

---

### Slice 4 — Open Conversation

**Requirements:** [`./slice-4-conversation.md`](./slice-4-conversation.md)

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

**Relevant research artifacts**

- `batch-4a-coaching-conversation-design.md` — open-conversation tone, intent classification, response patterns.
- `batch-4b-special-populations-safety.md` — keyword triggers for safety escalation (injury, crisis, medical scope).
- `batch-2c-testing-nondeterministic.md` — eval patterns for open-conversation quality.

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

### Tier 3 — Per-Slice Spec + Tasks

Each slice moves through a conceptual pipeline. The durable parts are the **artifact shapes and locations** and the **ordering discipline** — not the specific tools used to produce them. Pick the skills that fit the moment; the structure below is what the docs commit to.

1. **(Optional) Preliminary codebase research** — only when the slice's requirements doc + the cycle-plan Slice N section + the named research artifacts don't give enough codebase-grounded context. Output lives under `docs/specs/research-{topic}/`.
2. **Spec** — inputs: the slice's requirements doc (`./slice-N-{name}.md`), this cycle plan's Slice N section, and any relevant research artifacts. Output: `docs/specs/{NN}-spec-{topic}/spec.md` with demoable units, acceptance criteria, and proof-artifact definitions. Existing project precedent: see `docs/specs/05-spec-*` through `09-spec-*`.
3. **Task decomposition** — break the spec into dependency-aware tasks on a task board (or equivalent tracker). Independent tasks are marked for parallel execution.
4. **Execution** — tasks are implemented, tested, and committed per the project's normal conventions (see `backend/CLAUDE.md`, `frontend/CLAUDE.md`).
5. **Validation** — coverage matrix against the spec's acceptance criteria, then code review, before merge.

The `docs/specs/{NN}-spec-{topic}/` convention was adopted from the `claude-workflow` plugin's output shape — that attribution is the only reason the plugin is named here. How each step is actually executed (which skill, which tool, whether a skill at all) is a per-slice judgment call. A small slice can use a hand-written spec and skip the task board; a larger one benefits from more structure. Don't encode the tool choice in these docs.

DEC-008 plan-first rule applies — no slice implementation until the spec has been reviewed. The requirements doc at `./slice-N-{name}.md` is durable across implementation churn; the spec under `docs/specs/` is allowed to churn as implementation reveals detail.

### Per-Slice Hygiene Rule

Each slice's "done" criteria include:

- [ ] Slice acceptance checkboxes in this cycle plan marked complete.
- [ ] Cycle plan's Status section updated: active slice advanced to the next one (or "Cycle complete" if this was the last).
- [ ] `ROADMAP.md` Status block synced from this doc.
- [ ] Follow-ups discovered during the slice captured in the **Captured During Cycle** section. The slice PR description must include a `### Follow-ups found` checklist (empty is fine; omission is not).
- [ ] If the slice produced durable architecture decisions, record them in `docs/decisions/decision-log.md`.
- [ ] Completed slice spec directory under `docs/specs/` stays in place (historical reference) — not deleted, not moved.

### `/catchup` Update

`.claude/commands/catchup.md` updated to walk the new tiers:

1. `ROADMAP.md` Status block — current cycle + active slice pointers.
2. The active cycle plan (path from Status block) — slice structure and progress.
3. The active slice spec under `docs/specs/` if one exists (path from cycle plan Active Slice).
4. Last 5-10 commits on current branch.
5. Working-tree changes vs. `main` (if any).

Summarize in 3-5 sentences: current cycle, active slice, last shipped work, recommended next action.

### Tracking shape

Per-slice work is tracked on a task board (dependency-aware, atomic tasks) with the slice spec under `docs/specs/` holding the connected reasoning. GitHub Issues remain available for cross-cycle items (bugs found in main, cross-repo coordination) but are not the primary per-slice tracker. Revisit at the slice-0/slice-1 boundary if the granularity proves insufficient.

---

## When Agents Encounter Unknowns

The baseline rule lives in the project-root `CLAUDE.md` § Research Protocol and applies to every agent session, not just this cycle. **Never guess at implementation.** This section adds cycle-specific affordances.

### Prompt template

When writing a deep-research prompt, follow the existing `docs/research/prompts/batch-*.md` format:

```markdown
# R-XXX: {Topic}

## Context
{why this question came up — slice N scope, specific decision being blocked}

## Research Question
{primary question + 2-5 sub-questions that would make the answer actionable}

## Why It Matters
{what this unblocks, what happens if we get it wrong}

## Deliverables
- Concrete recommendation with rationale
- Alternatives considered and why rejected
- Library/tool version pins if applicable
- Gotchas, security implications, version compatibility notes
```

### Handoff protocol

1. Write the prompt at `docs/research/prompts/{filename}.md`.
2. Add the entry to `docs/research/research-queue.md` following the existing table format (next `R-XXX` number, Status = `Queued`, Artifact = `(pending)`).
3. Return control to the user: *"I encountered X in slice N, needs research before I proceed. Prompt at `docs/research/prompts/{file}.md`. Please run it in a separate research agent and provide the artifact."*
4. Wait for the artifact to land at `docs/research/artifacts/{file}.md`.
5. Integrate findings into the relevant planning doc, decision-log entry, or active slice spec before resuming implementation.

### Unknowns likely to surface in this cycle

Pre-flagged so agents know these are explicit research triggers, not "pick one and see." This is not a commitment to research all of them — some may be resolved by existing research once an agent reads it. The list exists so "guess and move on" is never the default for these topics.

- **Marten event-sourcing patterns for a per-user plan aggregate** — stream-per-user vs. stream-per-plan, projection update strategy (inline vs. async), snapshot frequency. `batch-2b-planning-architecture.md` has the conceptual model; implementation-time Marten conventions may need a dedicated research pass.
- **RTK Query streaming patterns for LLM responses** — RTK Query's caching model isn't a great fit for streams. Slice 4's chat panel will need either raw fetch + manual state or an alternative library; not obvious which.
- **JWT rotation / refresh-token strategy for long-lived personal-use sessions** — Slice 0 scope decision. Existing research covers CI/quality gates, not client-side token lifecycle.
- **PostgreSQL JSONB query patterns for `WorkoutLog.Metrics`** — Slice 2 stores heterogeneous metric shapes. If any downstream slice needs to query by metric value (e.g., "show me all runs where HR avg > 150"), indexing and query strategy is non-obvious — GIN vs. expression indexes, search performance at scale.
- **Onboarding conversation-state persistence** — Slice 1 multi-turn onboarding needs a state model. Whether in-progress state lives in a column, in the Marten stream, or in the client is a real architectural question.

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

### Planning & Decisions

- `docs/planning/vision-and-principles.md` — why this exists, design principles.
- `docs/planning/interaction-model.md` — three interaction modes.
- `docs/planning/planning-architecture.md` — macro/meso/micro tiers, event-sourced plan state.
- `docs/planning/memory-and-architecture.md` — context injection strategy, five-layer summarization.
- `docs/planning/safety-and-legal.md` — safety guardrails (pre-public-release items live here).
- `docs/decisions/decision-log.md` — all architectural decisions (DEC-001 through DEC-044).
- `docs/features/backlog.md` — feature backlog by priority tier.
- `backend/CLAUDE.md` — backend conventions, module-first organization, testing patterns.
- `frontend/CLAUDE.md` — frontend conventions.

### Cross-Cutting Research Artifacts

Apply throughout this cycle — read these before starting any slice, not just the one you're working on. Per-slice artifacts live in each slice's "Relevant research artifacts" subsection.

- `batch-1-claude-code-workflow.md` — agent workflow patterns, context management.
- `batch-7b-anthropic-model-ids-versioning.md` — floating model aliases, DEC-037.
- `batch-10a-frontend-latest-practices.md` — React 19 + TS + Vite + Tailwind conventions.
- `batch-10b-dotnet-backend-review-practices.md` — .NET 10 conventions, anti-patterns.
- `batch-10c-ci-quality-gates-private-repo.md` — CI strategy.
- `batch-6a-llm-eval-strategies.md` — eval patterns applied cycle-wide.
- `batch-7a-ichatclient-structured-output-bridge.md` — structured-output bridge pattern used by every LLM call in this cycle.

### Research Queue & Prompts

- `docs/research/research-queue.md` — full queue of topics (R-001 through R-039), status, and artifact pointers.
- `docs/research/prompts/` — deep-research prompts. Follow this format when writing new ones (see [When Agents Encounter Unknowns](#when-agents-encounter-unknowns)).
- `docs/research/artifacts/` — completed research artifacts.

### Historical

- POC 1 plan: `docs/plans/poc-1-context-injection-plan-quality.md`.
- POC 1 LLM testing plan: `docs/plans/poc-1-llm-testing-architecture.md`.
