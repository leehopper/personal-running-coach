# Research Prompt: Batch 28d — R-080

# Two light questions for Slice 2b: (1) binding the committed M.E.AI.Evaluation cache to prompt-template content for local pre-test drift detection, and (2) Apple HealthKit / Strava metric-key naming to lock canonical JSONB keys

Copy the prompt below and hand to your deep research agent. **This is a deliberately light, two-part prompt** — both parts are de-risking confirmations, not open-ended design.

---

## PROMPT

This prompt has two independent parts. Answer both; keep each tight.

---

### PART 1 — Bind the committed eval cache to `Prompts/*.yaml` content so a prompt edit fails LOCALLY before push

Research Topic: For a `Microsoft.Extensions.AI.Evaluation` 10.6.0 test suite that runs in CI in **Replay mode** against a committed disk cache (zero live API calls), how is the cache key derived, and does it incorporate `ChatOptions.AdditionalProperties` (a.k.a. `additionalValues`) — and given the answer, what is the lowest-overhead mechanism to make an edit to a `Prompts/*.yaml` template **fail locally before push** with an actionable "run `rerecord-eval-cache.sh`" message, rather than only surfacing as a CI Replay failure after the prompt change has already merged?

Sub-questions:
1. **Cache-key derivation in M.E.AI.Evaluation 10.6.0.** Confirm from the actual library (source/docs) exactly which inputs the response-caching layer (`DistributedCachingChatClient` / the Evaluation.Reporting `DiskBasedResponseCache`) hashes into the cache key: the `ChatMessage[]`, the `ChatOptions`, and specifically whether `ChatOptions.AdditionalProperties` / any `additionalValues` participate. `batch-17c` *recommended* injecting `options.AdditionalProperties["runcoach.prompt_version"]` to force cache busting on prompt-version bumps — verify this actually changes the key in 10.6.0, or report what the supported cache-busting/keying knob is instead.
2. **Is prompt content already in the key?** Since the system prompt text is part of the `ChatMessage[]`, a prompt edit already produces a clean cache miss in Replay mode. So the real gap is *local, pre-push detection*, not correctness. Confirm this framing.
3. **Lowest-overhead local detector.** Compare: (a) a **prompt-hash sentinel file** committed alongside the cache (e.g., SHA-256 of each `Prompts/*.{id}.{version}.yaml`) that a pre-test/pre-commit step diffs against current content, failing with "Prompts/X changed — run rerecord-eval-cache.sh"; (b) **versioning the prompt into the cache key** via the supported knob from (1) plus a check that the active version matches the recorded version; (c) a **pre-commit hook that re-runs the affected evals in Replay** when any `Prompts/*` file is staged. Recommend one for a solo-builder repo that already has `prepush-eval-check.sh` (Replay pre-push) and `rerecord-eval-cache.sh` (record + TTL-patch) lefthook hooks. Favor the simplest mechanism that fails *at commit/test time locally* with a clear remediation message and adds minimal moving parts.
4. **Placement.** Should the detector be a pre-commit hook, a pre-test fixture assertion inside `EvalTestBase`, or a standalone script wired into lefthook? Note that the repo's eval tests are `[Trait("Category","Eval")]`, CI sets `EVAL_CACHE_MODE=Replay`, and committed cache entries are TTL-patched to `9999-12-31`.

**Part 1 context:** Slice 2b iterates `Prompts/*.yaml` more than Slice 1 did. Today a prompt edit silently invalidates the committed cache; the only signal is a CI Replay failure on the *next* push, after merge. `YamlPromptStore` computes **no content hash**; the YAML `version:` field is a human label not used in keying. This is a Slice 1 carry-forward deferred into the 2b spec. `batch-8a` (eval-cache TTL/CI) and `batch-17c` (multi-turn eval pattern) already cover the TTL-patching and the `additionalValues` *recommendation*; this part only needs the **verification** of (1) and a **mechanism pick** for (3)/(4). Keep it light.

---

### PART 2 — Confirm Apple HealthKit / Strava metric-key naming to lock canonical JSONB keys

Research Topic: To finalize the canonical key set for the `WorkoutLog.Metrics` JSONB bag so that **manual entry today and Apple HealthKit / Strava ingestion later write the same keys**, what are the authoritative source-side identifiers and units for the metrics RunCoach will store — and does the prior Garmin-centric research (`batch-3c`) miss any naming that Apple/Strava use?

Sub-questions:
1. **Apple HealthKit identifiers.** For each metric in the canonical set, give the `HKQuantityTypeIdentifier` / `HKWorkout` / `HKWorkoutStatistics` source name and its native unit: average & max heart rate, active energy burned (calories), distance, running cadence/step length, HRV (`heartRateVariabilitySDNN`), resting HR, VO2max, sleep, elevation/flights, running power, ground-contact-time/vertical-oscillation/stride-length (the "running dynamics" that HealthKit exposes as of 2026). Note which are workout-level vs sample-level.
2. **Strava activity fields** for the same metrics (Strava's `DetailedActivity` field names + units), where they differ from Apple/Garmin.
3. **Reconcile to one canonical key set.** Propose the final canonical camelCase JSONB key list + canonical unit per key (consistent with the project's canonical-meters/seconds storage rule, `batch-9b`), explicitly mapping each canonical key ← {HealthKit name, Garmin name (from `batch-3c`), Strava name}. Flag any metric where the three sources disagree enough that a naming choice has downstream cost. Cover the originally-listed keys (`rpe`, `hrAvg`, `hrMax`, `calories`, `splits`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`) and any high-value additions the integrations make cheap (cadence, elevation, power).
4. **`splits`/laps shape.** Confirm the canonical per-lap object shape (index, distance, duration, pace, optional avg HR) against how HealthKit (`HKWorkoutEvent`/segments) and Strava (`laps`/`splits_metric`) represent laps, so the array we design now matches import later.

**Part 2 context:** The canonical-key constants are a Slice 1 carry-forward; the keys must be chosen *now* (in the 2b spec, single C# file `WorkoutMetricKeys`) and not change when integrations land post-MVP-0. `batch-3c` (wearable integrations) is Garmin/`Activity Summary`-centric; Apple HealthKit is the post-MVP-0 priority per DEC-033, so its naming is the gap. This part is confirmation/reconciliation, not feasibility research — HealthKit/Strava integration itself is out of scope.

## Why It Matters

Part 1: prompt iteration that silently invalidates the eval cache turns every prompt edit into a delayed-CI-failure landmine; a local pre-push/commit detector with a clear remediation message removes a recurring "why is CI red on a prompt-only change" surprise during the prompt-heavy 2b/3 work. Part 2: the canonical JSONB keys are effectively permanent once real workout history is written; choosing keys that already match HealthKit/Strava/Garmin source names means future auto-fill populates the same bag with no migration and no dual-naming.

## Deliverables

- **Part 1:** a verified statement of M.E.AI.Evaluation 10.6.0 cache-key inputs (does `AdditionalProperties` participate?), the "prompt content already busts the key; the gap is local detection" framing confirmed, and a single recommended local-detector mechanism + placement, with a sub-50-LOC sketch.
- **Part 2:** the final canonical key list with per-key canonical unit and a source-name mapping table (HealthKit ← → Garmin ← → Strava), plus the canonical `splits`/lap object shape.

## Out of scope

- Implementing HealthKit/Strava/Garmin ingestion (post-MVP-0/MVP-1).
- The JSONB column representation / EF mapping (separate prompt, R-077) — Part 2 only fixes *key names and units*, not storage mechanics.
- Eval scoring rubrics / judge design (`batch-6a`/`batch-17c`).
- The 14-day TTL patching (already solved by `rerecord-eval-cache.sh` / `check-eval-cache-ttl.sh`).

The artifact lands at `docs/research/artifacts/batch-28d-meai-evalcache-prompt-binding-and-healthkit-keys.md` and integrates into the Slice 2b spec (Part 1 → the eval-cache section / a DEC if the detector becomes a standing convention; Part 2 → the `WorkoutMetricKeys` canonical-key list, alongside R-077's storage decision).
