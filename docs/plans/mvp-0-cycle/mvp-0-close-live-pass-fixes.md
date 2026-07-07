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

**Progress.** F-LIVE-1 🚧 in progress (this doc + DEC-087, PR pending). F-LIVE-2 ⏳
not started (separate follow-up — deterministic meso↔micro consistency validator;
needs its own DEC + spec).

---

## F-LIVE-1 — Bounded server-side retry on macro-plan validation rejection

**Status: 🚧 In progress — DEC-087.** Adds a bounded, corrective-hint retry around
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

## F-LIVE-2 — Deterministic meso↔micro plan-layer consistency validator

**Status: ⏳ Not started — needs its own DEC + spec.** Separate follow-up, tracked
here as the sibling MVP-0-close finding.

**Finding.** The meso and micro plan layers disagreed for the same week (run-day
count 3 vs. 4; a tempo-vs-easy day swapped). Home renders workout cards from the
micro layer but the week-summary narrative from the meso layer, so the contradiction
is user-visible. No cross-layer consistency validator exists.

**Requirement (candidate, to be pinned in its own DEC).** A deterministic validator
reconciles the micro layer's per-day slot types/count for week 1 against the meso
week it expands; a mismatch is handled (reject-and-retry in the same spirit as
F-LIVE-1, or a deterministic reconciliation) rather than silently rendered.

**Acceptance.** TBD in the F-LIVE-2 spec.

---

## Out of scope (triaged elsewhere)

- **F-LIVE-3 — coach day-attribution slip** (the coach conversationally muddled
  which day was skipped vs. over-run): cosmetic LLM attribution slip; recent-log
  context injection itself worked. No action per its 2026-07-07 disposition.
- **Unconditional phase-sum prompt reinforcement** (lower the F-LIVE-1 base failure
  rate by stating the invariant for general-fitness plans too): complementary to the
  retry, separate eval-re-record blast radius; § Captured During Cycle follow-up.
