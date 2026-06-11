# Slice 3B — Live-Pass Fixes (Post-Slice-3 Hardening)

**Origin.** The Slice 3 gated live end-to-end validation pass ran 2026-06-11 — the
first live (non-Replay) run of the full coaching path. The loop itself passed:
registration → six-step onboarding (live Sonnet turns) → 16-week plan generation →
three under-performing logs walking the rolling score 1.0 → 2.0 → 3.0 → a live L2
restructure rendered in the Coach panel with the before/after diff → dead-zone
suppression of a would-be second restructure on a fourth log. The pass also surfaced
six findings that Replay-mode CI structurally cannot catch. Four are immediate fixes
and form this slice; the other two are triaged in the cycle plan's Captured During
Cycle table (onboarding slot-merge → Slice 4; narrative staleness + rationale-claim
drift → deferred).

**Why now.** Two of the four are a safety gap and a trademark exposure — both
hard-rule territory (DEC-019/DEC-030/DEC-079 safety posture; root `CLAUDE.md`
trademark rule). The other two make the product visibly wrong to its first real
user (the builder). All four sit in shipped Slice 3 / Slice 1 surfaces, not new
feature ground, so they precede Slice 4.

**Done-gate.** The slice is done when a **fresh live end-to-end validation pass**
(new account, funded key, real browser) passes with all four fixes observable at
the surface. CI/eval coverage alone does not close this slice.

---

## F1 — Amber safety referral surfaces on every escalation path

**Finding.** `EvaluateAdaptationHandler` appends the Amber `SafetySignalRaised`
referral only inside the L2 restructure path. An Amber-classified log whose
escalation decision is L0/L1 absorb — including the post-restructure cooldown
dead-zone — returns with no safety turn at all. Live repro: a log note reading
"sharp pain in my left shin … stopped early" (matches injury rule SG-I01) during
the cooldown produced total silence in the Coach panel.

**Requirement.** An Amber classification always appends the scripted referral
(`SafetySignalRaised` + `AmberReferralContent`) and surfaces a safety turn in the
panel, regardless of the escalation outcome the same log produces. The Red
short-circuit (step 5) is unchanged. Idempotency semantics unchanged: one
evaluation per log.

**Acceptance.**
- Given a plan inside the restructure cooldown dead-zone, when a log with an
  Amber-matching injury note is created, then the Coach panel shows the scripted
  Amber referral turn (tier accent), the plan is unchanged, and no second
  restructure fires.
- Given a first-ever log (no prior signal state) that is on-target but carries an
  Amber-matching note, when it is created, then the referral turn appears even
  though the deviation outcome is absorb.
- Eval/integration coverage exists for Amber × {L0 absorb, score-driven L1,
  dead-zone hold} so the gap cannot silently reopen.

## F2 — Trademark scrub on all persisted LLM prose

**Finding.** The live macro generation emitted the trademarked term into the
persisted `plan_generated_v1` event: `Macro.Rationale` reads "Using Daniels'
Running Formula, your VDOT sits around 38". Existing guards cover the assembled
prompt (ContextAssembler tests) and the adaptation output (PR6 eval guard) — not
the macro/meso/micro structured outputs' prose fields, and the leak is persisted
in the event stream and rides the plan projection toward the API surface.

**Requirement.** No trademarked term in any persisted or API-visible LLM-authored
prose field (rationales, notes, warnings, week summaries, coaching notes — all
plan-generation and adaptation structured outputs). Defense in depth: (a) the
generation prompts prohibit the term and name the approved vocabulary
("Daniels-Gilbert", "pace-zone index"); (b) a deterministic guard at the
structured-output boundary rejects or scrubs offending output before append;
(c) the eval trademark guard extends to every persisted prose field.

**Acceptance.**
- Given a live plan generation, when the macro/meso/micro outputs are appended,
  then no persisted field contains the term (case-insensitive).
- Given an LLM output containing the term in any prose field, when it passes the
  output boundary, then the guard rejects/scrubs it and the eval suite has a
  fixture proving so.

## F3 — Plan horizon anchors to the target event date

**Finding.** Onboarding declared a 10K on 2026-08-15 (nine weeks out);
the macro plan generated `TotalWeeks: 16` with Base/Build/Peak/Taper spanning to
late September — race day lands mid-Peak and the taper sits six weeks after the
race.

**Requirement.** Plan generation is date-aware: the prompt context carries the
current date and the target event date, and the generated horizon must land race
week at the end of the final (taper) phase. A deterministic validator rejects a
macro whose phase arithmetic does not place the event inside the final phase's
last week (tolerance: ±1 week). Behavior when no event date exists (general
fitness goal) is unchanged.

**Acceptance.**
- Given a runner with an event N weeks away (N within plannable bounds), when the
  plan generates, then total weeks ≈ N and the taper ends race week.
- Given a macro output whose horizon contradicts the event date, when validated,
  then it is rejected (terminal `Kind=Error` consistent with DEC-073/DEC-080
  posture) rather than silently appended.

## F4 — Restructure internal-consistency validation

**Finding.** The live L2 restructure proposed Week 1 `WeeklyTargetKm: 24` while
its own edited micro week sums to 30 km (Mon 4 + Wed 8 + untouched Thu 6 + Sat 12)
— the diff edited three workouts, left Thursday untouched, and the weekly target
matches only the three edited workouts. (The same output increased the Saturday
long run 11 → 12 km inside a fatigue load-cut; that is judge/calibration
territory, captured in the Captured During Cycle table, not a hard rule here.)

**Requirement.** `PlanAdaptationOutputValidator` (or a post-validation step on the
restructure path) enforces arithmetic consistency on the edited week: the proposed
weekly target must equal the sum of that week's resulting workout distances within
a small tolerance. Violations are terminal (`Kind=Error`, nothing staged) per the
existing single-call DEC-080 policy.

**Acceptance.**
- Given the recorded live output shape (target 24 vs sum 30), when validated,
  then it is rejected.
- Given a restructure whose weekly target matches its micro-week sum, when
  validated, then it passes unchanged.
- An eval scenario pins the rule against the cached restructure fixtures.

---

## Out of scope (triaged elsewhere)

- **Onboarding Schedule slot-merge loop** (free-text days/duration answers not
  merged into slots; four round-trips to escape) and the input-kind/assistant-text
  mismatch — scheduled into Slice 4 with the conversation/prompt work.
- **Rationale-claim drift** (restructure rationale described a Thursday change the
  patch does not contain) — Haiku communication-judge calibration scenario; joins
  the PR6 eval-robustness deferral row.
- **Stale week narratives after adaptation** (new volume numbers beside old prose)
  — deferred UX polish; adaptation deliberately does not regenerate summaries.

## Verification protocol

Re-run the live pass end to end on a fresh account with a funded key: onboard with
a dated race goal, confirm the plan horizon (F3) and clean persisted prose (F2),
walk three under-performing logs to a live restructure and confirm weekly-target
arithmetic (F4), then log an Amber-note workout inside the cooldown and confirm
the referral turn renders (F1). Update ROADMAP / cycle-plan status with the
outcome either way.
