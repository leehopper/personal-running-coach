# MVP-0 Close — Live-Pass Fixes (Cycle Close-Out Hardening)

**Origin.** The MVP-0 close gated live end-to-end validation pass ran 2026-07-07 —
the second live (non-Replay) run of the full coaching path and the first over the
form-first onboarding (DEC-086). The loop itself PASSED: form-first onboard →
live-Sonnet plan → log → adapt → converse, verified at the UI surface and in the
persisted Marten event store. The pass surfaced three findings Replay-mode CI
structurally cannot catch. Two are actionable fixes and form this doc; the third
(**F-LIVE-3**, a cosmetic coach day-attribution slip) is no-action per its
disposition. Full pass detail: cycle plan § Captured During Cycle (2026-07-07
MVP-0 close-out row); durable summary in memory `project_mvp0_close_live_pass`.

**Why now.** Both findings sit on the plan-generation surface — the first thing a
new runner hits. F-LIVE-1 makes onboarding stochastically dead-end on a generic
error; F-LIVE-2 renders a self-contradictory plan. Both are cycle close-out work
(all build slices 0–4C are complete); clearing them plus the § Captured During
Cycle ledger triage is what remains before MVP-0 formally closes.

**Done-gate.** Each finding is done when its fix is observable at the surface on a
**fresh live end-to-end validation pass** (new account, funded key, real browser).
CI/eval coverage alone does not close a finding — Replay cannot exercise the
live-Sonnet stochasticity that produced these. F-LIVE-1 and F-LIVE-2 may ship and
be verified independently.

**Progress.** F-LIVE-1 ✅ fix merged (this doc + DEC-087; **PR #271 merged to `main`
2026-07-07**). F-LIVE-2 ✅ fix implemented (this doc + DEC-088; deterministic
meso↔micro consistency validator + bounded corrective-hint micro retry, on
`fix/f-live-2-meso-micro-consistency`). Both findings' remaining step is the shared
live-pass done-gate (surface-level verification on a fresh funded-key run) — see
each section's Verification protocol.

---

## F-LIVE-1 — Bounded server-side retry on macro-plan validation rejection

**Status: ✅ Merged — DEC-087 (PR #271, `main` 2026-07-07).** Adds a bounded, corrective-hint retry around
the macro tier inside `PlanGenerationService.GeneratePlanAsync`, amending the
DEC-073/DEC-080 "no re-prompt" posture. On a `MacroPlanOutputValidator` rejection
(`PhaseSumMismatch` or `HorizonMismatch`) the service re-invokes the macro tier —
up to `CoachingLlmSettings.MacroValidationMaxRetries` extra attempts (default 1) —
appending a deterministic correction suffix that names the exact arithmetic the
model got wrong. On exhaustion it throws `PlanGenerationRejectedException` exactly
as today (onboarding maps it to the existing 422; the transaction still aborts with
zero staged events). Both callers (onboarding terminal branch + regenerate handler)
benefit because the retry lives inside the service.

**Finding.** Live Sonnet occasionally emits a macro whose phase weeks don't sum to
`TotalWeeks` (`PhaseSumMismatch`) — a cross-item invariant constrained decoding
can't enforce. `MacroPlanOutputValidator` correctly rejects it, but because
onboarding answer-capture is coupled to synchronous inline plan generation, the
runner hits `POST /api/v1/onboarding/answers` → 422 behind a generic "We couldn't
build a plan from your answers. Please submit again." The rollback is atomic (no
orphan events — data-integrity clean), and a second identical attempt succeeded in
the pass, but the only recovery is a manual resubmit. There is no server-side
retry. The same terminal-reject path applies to `HorizonMismatch`.

**Root-cause note (complementary, out of scope here).** The phase-sum invariant is
stated in the plan-gen user message **only for anchored (dated-race) horizons** and
only as a subordinate clause (`ContextAssembler.AppendPlanDateContext`); for
general-fitness plans the model is never told phases must reconcile to `TotalWeeks`.
Stating the invariant unconditionally would lower the *base* failure rate, but it
edits the byte-stable base prompt and forces an eval-cache re-record — a separate
blast radius from the recovery mechanism this finding delivers. Tracked as a
follow-up in § Captured During Cycle, not bundled here.

**Requirement.**
- **Bounded retry, macro tier only.** On a macro validation rejection, re-invoke the
  macro tier up to `MacroValidationMaxRetries` additional times (config-bound,
  default 1 → 2 total attempts; `N` means up to `N+1` attempts, mirroring the
  existing SDK `MaxRetries` semantics). Meso/micro tiers have not run yet, so a
  retry costs one macro call. No backoff delay (this is a bad-output re-roll, not a
  rate-limit; the SDK already owns transient/429 backoff below the adapter).
- **Corrective hint.** Each retry appends a deterministic correction suffix to the
  macro user message naming the exact defect — for `PhaseSumMismatch`, the observed
  phase-week sum vs. the declared `TotalWeeks`; for `HorizonMismatch`, the required
  target total weeks vs. what was emitted. Cache-safe: the suffix rides the
  never-cached user message; the `Ephemeral1h`-marked system block is untouched, so
  the retry reuses the primed cache exactly as meso/micro already do. The first
  (attempt-0) macro message is byte-identical to today's (no suffix), preserving the
  input-prompt-stability contract and eval-cache replay.
- **Scope.** Retry fires on any invalid macro — `PhaseSumMismatch` **and**
  `HorizonMismatch`. Both are stochastic LLM-output errors the corrective hint
  addresses; retrying one but not the other has no principled basis. (This broadens
  the written F-LIVE-1 disposition, which named only `PhaseSumMismatch` — the
  broadening is recorded explicitly in DEC-087.)
- **Terminal behavior unchanged.** On exhaustion, throw
  `PlanGenerationRejectedException(lastViolation)` — onboarding's existing 422 path
  and the atomic no-staged-events rollback are unchanged. The retry only shrinks the
  probability of reaching that terminal state.
- **Observability.** Each retry is warn-logged with attempt number + violation; the
  macro attempt count is stamped on the chain span and the plan-generation
  completion metric (success and failure) so dashboards can see how often the retry
  fires and whether it is masking a rising base failure rate.

**Acceptance.**
- Given the macro tier returns a `PhaseSumMismatch` macro on the first attempt and a
  valid macro on the retry, when a plan is generated, then the service returns the
  canonical six-event sequence, the macro LLM call was made exactly twice, and the
  caller never sees `PlanGenerationRejectedException` (no 422 surfaced).
- Given the macro tier returns a `HorizonMismatch` macro then a valid macro, when a
  plan generates against an anchored horizon, then it succeeds on the retry (same
  observable outcome as the phase-sum case).
- Given every macro attempt is invalid (bad macro on every call), when a plan is
  generated with `MacroValidationMaxRetries = N`, then the macro LLM call is made
  exactly `N + 1` times and the service throws `PlanGenerationRejectedException`
  carrying the last violation — and no meso/micro call is ever made.
- Given a retry fires, when the macro tier is re-invoked, then the retry user
  message contains the correction suffix naming the actual sum and declared total
  (phase-sum case) or the target vs. emitted weeks (horizon case), and the
  attempt-0 message carries no correction suffix (byte-identical to the no-retry
  path).
- Given the retry succeeds on attempt 2, when the completion metric is emitted, then
  it carries the macro-attempt count so a dashboard distinguishes "recovered on
  retry" from "first-try clean".

**Coverage boundary.** The retry is fully encapsulated inside `PlanGenerationService`;
the onboarding + regenerate handlers consume its returned event sequence and cannot
observe the attempt count, so their staging behavior is unchanged and already
covered (happy-path commit; terminal-rejection → 422 + atomic rollback + resubmit
via `OnboardingAnswersEndpointIntegrationTests`, which fakes the whole service to
force the terminal throw). The service-level recovery is unit-proven above; the
user-visible end-to-end recovery ("a stochastic first-attempt rejection no longer
dead-ends onboarding") is the live-pass done-gate below, not a new heavily-mocked
integration test.

**Verification protocol.** Backend unit + integration suite green in Replay. Then a
fresh live pass: onboard to plan generation and confirm a plan builds; because the
`PhaseSumMismatch` is stochastic, confirm via the emitted attempt-count metric /
logs that the retry path is exercised at least once across a few generations
(re-onboard on fresh accounts if the first is clean on attempt 0), and confirm no
generic "couldn't build a plan" 422 is surfaced when a first attempt is rejected.

---

## F-LIVE-2 — Deterministic meso↔micro plan-layer consistency validator + micro retry

**Status: ✅ Implemented — DEC-088.** Adds a deterministic cross-layer validator and a
bounded corrective-hint retry around the micro tier inside
`PlanGenerationService.GeneratePlanAsync`, mirroring F-LIVE-1's macro retry one tier
down. The meso week-1 template is the source of truth; on a validator rejection the
service re-invokes **only** the micro tier — config-bound by
`CoachingLlmSettings.MicroValidationMaxRetries` (default 1 → 2 attempts) — appending a
deterministic suffix naming the exact run-day schedule to reproduce. On exhaustion it
throws `MesoMicroConsistencyRejectedException`, which the onboarding controller maps to
the same 422 (atomic rollback) as a macro rejection.

**Finding.** The meso and micro layers are generated by independent structured-output
calls. `BuildMicroUserMessage` recaps only three meso scalars (phase, weekly target km,
deload flag), never the per-day slots, so the micro call independently decides run-day
count and placement — and live Sonnet emitted a micro week that disagreed with the meso
week (run-day count 3 vs. 4; a tempo-vs-easy day swapped). Home renders workout cards
from the micro layer but the week-summary narrative from the meso layer, so the
contradiction is user-visible on one screen. No cross-layer consistency validator
existed (confirmed: zero matches for any meso/micro consistency check in `backend/src`).

**Comparability (why a deterministic gate is possible).** Both layers carry per-day
`WorkoutType` using the **same enum**, and the meso `EnumerateDays()` day index
(`System.DayOfWeek`, Sunday=0..Saturday=6) matches `WorkoutOutput.DayOfWeek` — a bare
cast, no mapping table. The recorded eval fixtures confirm the intended relationship: a
consistent pair (james) matches exactly on `(dayOfWeek, workoutType)` for run days,
while independently-generated pairs (lee, priya) diverge — the exact class of
disagreement the finding describes. So a full per-day reconciliation is possible, not
merely a count check.

**Requirement.**
- **Deterministic run-day validator.** `MesoMicroConsistencyValidator.Validate(weekOneMeso, micro)`
  (pure static, beside `MacroPlanOutputValidator`) reconciles the micro workouts against
  the meso week-1 `Run` slots. The comparison is **exact** over run days: the set of
  `(dayOfWeek → WorkoutType)` pairs from the meso run slots must equal the set from the
  micro workouts. Scope: **run days only** — a meso `Rest`/`CrossTrain` slot is not
  expected in micro, and a micro `WorkoutType.CrossTrain` entry is excluded from the run
  set (mirroring `RestructureConsistencyCheck`'s cross-train exclusion). A meso run slot
  with a null `WorkoutType` constrains presence only. A duplicate micro workout on one day
  is reported. Violations: `RunDayCountMismatch` / `RunDaySetMismatch` / `WorkoutTypeMismatch`.
- **Bounded retry, micro tier only.** On rejection, re-invoke the micro tier up to
  `MicroValidationMaxRetries` additional times (config-bound, default 1 → 2 attempts;
  clamped against a compiled ceiling of 5). The meso week is authoritative and already
  paid for, so a re-roll costs exactly one micro call — no macro/meso work is redone. No
  backoff (a bad-output re-roll, not a rate-limit).
- **Corrective hint.** Each retry appends a deterministic suffix to the micro user message
  naming the meso run schedule (one line per run slot: day + workout type) and the schedule
  the rejected micro produced. Cache-safe: the suffix rides the never-cached user message;
  the `Ephemeral1h` system block is untouched, so the retry reuses the primed cache exactly
  as the meso/micro calls already do. Attempt 0 (`correction=null`) is byte-identical to the
  pre-fix micro message, preserving input-prompt stability and eval-cache replay.
- **Terminal behavior.** On exhaustion, throw `MesoMicroConsistencyRejectedException` (sibling
  to the macro `PlanGenerationRejectedException`) — the onboarding controller maps it to the
  same handled 422 and atomic no-staged-events rollback. The retry only shrinks the probability
  of reaching that terminal state.
- **Observability.** Each retry is warn-logged (attempt + violation); a `MicroAttempts` tag is
  stamped on the chain span and the `runcoach.plan.generation.completed` metric (success and
  failure tag bags), and `total_calls` includes micro retries — mirroring DEC-087's macro
  visibility so a dashboard distinguishes "recovered on retry" from "first-try clean".

**Acceptance.**
- Given the micro tier returns an inconsistent week-1 micro on the first attempt and a
  consistent one on the retry, when a plan is generated, then the service returns the canonical
  six-event sequence, the micro LLM call was made exactly twice, macro once and meso four times,
  and the caller never sees a rejection.
- Given every micro attempt is inconsistent, when a plan is generated with
  `MicroValidationMaxRetries = N`, then the micro LLM call is made exactly `N + 1` times and the
  service throws `MesoMicroConsistencyRejectedException` carrying the violation — macro and meso
  still run in full (the reject is downstream of them).
- Given a retry fires, when the micro tier is re-invoked, then the retry user message contains the
  correction suffix naming the meso run days the rejected micro omitted, and the attempt-0 message
  carries no correction suffix (byte-identical to the no-retry path).
- Given the retry succeeds on attempt 2, when the completion metric is emitted, then it carries
  `micro_attempts = 2` and `total_calls = 7` so a dashboard distinguishes recovered-on-retry from
  first-try clean.
- Given plan generation is terminally rejected for meso/micro inconsistency during onboarding
  completion, when the runner submits, then the endpoint returns a handled 422 with nothing staged
  and the form is re-submittable (verified by an integration test that fakes the terminal throw).
- The unit `MesoMicroConsistencyValidator` cases cover: a consistent pair, count mismatch (both
  directions), day-set mismatch, workout-type swap, cross-train exclusion, null-meso-type
  presence-only, a micro cross-train on a meso run day, and a duplicate micro run day.

**Coverage boundary.** The validator + retry are fully encapsulated inside `PlanGenerationService`;
the onboarding + regenerate handlers consume its returned event sequence unchanged. Service-level
recovery is unit-proven (`PlanGenerationServiceTests` micro-retry suite + `MesoMicroConsistencyValidatorTests`);
the onboarding 422 path is integration-proven (a faked terminal throw). The user-visible end-to-end
recovery ("a stochastic first-attempt inconsistency no longer renders a self-contradictory plan") is
the live-pass done-gate below.

**Cache-safety / eval.** No eval fixture re-record and no DEC-074 manifest regen: the plan-generation
eval suite does not route through `PlanGenerationService` (it calls a test-local structured-output
helper against the legacy `AssembleAsync` path), so a `PlanGenerationService`-internal change cannot
perturb any cache key — empirically consistent with PR #271's zero eval-cache diff. The default happy-path
test fixtures were made mutually consistent (`BuildMicro` now emits one workout per `BuildMeso` run slot)
so the validator passes on attempt 0 rather than spuriously retrying.

**Verification protocol.** Backend unit + integration suite green in Replay (1977/1977). Then a fresh
live pass: onboard to plan generation and confirm the rendered week-1 workout cards agree with the meso
week narrative (run-day count and workout types); because the inconsistency is stochastic, confirm via
the emitted `micro_attempts` metric / logs that the retry path is exercised at least once across a few
generations (re-onboard on fresh accounts if the first is clean on attempt 0), and confirm no generic
"couldn't build a plan" 422 is surfaced when a first attempt is inconsistent.

**Complementary root-cause follow-up (deferred).** Surfacing the meso week-1 per-day slots into the
micro *base* prompt (`BuildMicroUserMessage`, attempt 0) would lower the base inconsistency rate so the
retry rarely fires. Deferred, mirroring F-LIVE-1's deferral of the unconditional phase-sum reinforcement:
it changes attempt-0 live behavior, and the retry closes the finding on its own. Tracked in § Captured
During Cycle. (Note: unlike F-LIVE-1's prompt reinforcement, this one would *not* bust the eval cache,
since `BuildMicroUserMessage` is production-only — the eval suite never calls it.)

---

## Out of scope (triaged elsewhere)

- **F-LIVE-3 — coach day-attribution slip** (the coach conversationally muddled
  which day was skipped vs. over-run): cosmetic LLM attribution slip; recent-log
  context injection itself worked. No action per its 2026-07-07 disposition.
- **Unconditional phase-sum prompt reinforcement** (lower the F-LIVE-1 base failure
  rate by stating the invariant for general-fitness plans too): complementary to the
  retry, separate eval-re-record blast radius; § Captured During Cycle follow-up.
