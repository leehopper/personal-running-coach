# RunCoach — Roadmap

**Current cycle:** MVP-0 + Adaptation Loop — `docs/plans/mvp-0-cycle/cycle-plan.md`
**Active slice:** Slice 0 (Foundation) — Unit 1 persistence substrate PR (#49) open. DEC-048 composition corrections and DEC-049 startup unblock both applied; full `WebApplicationFactory<Program>` fixture + six SUT-host smoke tests green locally (581 passing / 0 failing / 1 skipped before the PR #49 review pass; after review changes: 582 passing / 0 failing / 0 skipped).
**Next step:** Resolve CI checks on PR #49 (Linux-only backend test failure, license-review false positives), then address CodeRabbit review findings and merge. After merge, begin Unit 2 (Auth API — T02.x). DEC-044 (cookie-not-JWT browser auth), DEC-045 (Aspire deferred to MVP-1), DEC-046 (SOPS + Postgres-DataProtection secrets), DEC-047 (onboarding event-source pattern), DEC-048 (Marten `IntegrateWithWolverine` as sole envelope-storage wiring), and DEC-049 (disable host-config reload on macOS arm64 + drop manual `MapWolverineEnvelopeStorage`) capture the architecture pivots and the R-055 fix.
**Blockers:** None. R-055 resolved 2026-04-20 — artifact at `docs/research/artifacts/batch-18b-webapplication-createbuilder-hang-followup.md`, fix captured in DEC-049.

This is the front door. For the full picture on session start, run `/catchup`. For anything deeper than the Status block above, open the cycle plan.

---

## Entry Points

Agents arriving cold should resolve intent to a file before reading:

- **"What should I work on?"** → active cycle plan (pointer above).
- **"What's the active slice doing?"** → active slice spec under `docs/specs/` (pointer in cycle plan's Status section, once a slice is underway).
- **"How does X work?"** → `docs/planning/{topic}.md` + the relevant module under `backend/src/RunCoach.Api/Modules/` or `frontend/src/app/modules/`.
- **"Why was X decided?"** → `docs/decisions/decision-log.md` (DEC-001 through DEC-047).
- **"Has this been researched?"** → `docs/research/research-queue.md` + `docs/research/artifacts/`.
- **"What are the rules for code changes?"** → root `CLAUDE.md`, `backend/CLAUDE.md`, `frontend/CLAUDE.md`, `REVIEW.md` files (root / backend / frontend).
- **"I found an unknown — can I just pick one and move on?"** → No. See `CLAUDE.md` § Research Protocol and the active cycle plan's "When Agents Encounter Unknowns" section.
- **"Where do I capture a 'we should also do this' item?"** → the active cycle plan's "Captured During Cycle" section (scoped to the cycle); or the "Deferred Items (Cross-Cycle)" section below for items that span cycles.

---

## Strategic Links

- Vision & principles: `docs/planning/vision-and-principles.md`
- Interaction model (three modes): `docs/planning/interaction-model.md`
- Planning architecture (macro/meso/micro, event-sourced plan): `docs/planning/planning-architecture.md`
- Memory & context injection strategy: `docs/planning/memory-and-architecture.md`
- Coaching persona playbooks: `docs/planning/coaching-persona.md`
- Safety & legal: `docs/planning/safety-and-legal.md`
- Self-optimization: `docs/planning/self-optimization.md`
- Unit system design: `docs/planning/unit-system-design.md`
- Decision log: `docs/decisions/decision-log.md` (47 entries)
- Feature backlog: `docs/features/backlog.md`
- Research queue & artifacts: `docs/research/research-queue.md`, `docs/research/artifacts/`
- POC roadmap (historical framing, superseded by cycle plans): `docs/planning/poc-roadmap.md`

---

## MVP Milestones

- **MVP-0 (personal validation):** Onboarding + plan generation + workout logging + adaptation loop. Builder uses it on own runs. **Currently building** — see cycle plan.
- **MVP-1 (friends / testers):** Adds proactive coaching + Apple Health integration. The adaptive differentiator becomes externally visible.

---

## Cycle History

Chronological log of completed cycles / phases, most recent first. One line per cycle — full detail lives in the linked artifacts (decision log, plan files, PRs).

| Cycle / Phase | Completed | Primary Artifacts | Key Outcomes |
|---|---|---|---|
| Spec 11 — TestPaceCalculator migration + VDOT residue scrub + eval cache re-record | 2026-04-18 | PR #45 | Closed both DEC-042 follow-ups: `TestPaceCalculator` bridge deleted and all four race-carrying profiles migrated to real `PaceZoneCalculator`; `FitnessEstimate.EstimatedVdot` → `EstimatedPaceZoneIndex`; `RaceTime` XML doc and four `AssessmentBasis` literals scrubbed; parameterized `ContextAssemblerTests` Theory guards full assembled prompt against VDOT regression for all 5 profiles; Sonnet + Haiku eval cache re-recorded. |
| DEC-042 pure-equation pace-zone calculator + DEC-041 value objects | 2026-04-17 | PR #44; `batch-11`, `batch-12a-g`, `batch-13` research | Replaced Daniels lookup table with `DanielsGilbertEquations` + `PaceZoneCalculator`; `VdotCalculator` → `PaceZoneIndexCalculator`; `Distance`/`Pace`/`PaceRange` value objects; eval cache re-recorded. |
| OSS quality tooling restoration (DEC-043) | 2026-04-15 | `docs/specs/09-spec-oss-tooling-restoration/`; `batch-14a-h` research | CodeRabbit / CodeQL / SonarQube Cloud / license-compliance pipeline; `main-protection` ruleset; one-authority-per-signal partitioning. |
| POC 1 review rounds + CI filter fix | 2026-03-22 | `docs/specs/05-spec-*` through `08-spec-*`; PR #18 | DEC-037, DEC-039; xUnit v3 + MTP migration; committed eval-cache CI. |
| POC 1 eval refactor | 2026-03-21 | `docs/plans/poc-1-llm-testing-architecture.md` | M.E.AI.Evaluation infrastructure; `AnthropicStructuredOutputClient`; YAML prompt storage. |
| POC 1 initial implementation | 2026-03-21 | `docs/plans/poc-1-context-injection-plan-quality.md`; PR #17 | Training-science computation layer; `ContextAssembler`; `ClaudeCoachingLlm`. |
| Project scaffolding + quality pipeline | 2026-03-19 | `docs/plans/setup-steps-3-4-handoff.md`; `docs/plans/quality-pipeline-private-repo.md` | DEC-031 through DEC-036; .NET 10 / React 19 scaffolding; Docker + Tilt; Lefthook + commitlint. |
| Planning phase | 2026-03-18 | `docs/planning/*.md`; 18 research artifacts (batches 1-9) | DEC-001 through DEC-030; vision, architecture, safety, coaching persona, interaction model, tiered plan model. |

---

## Deferred Items (Cross-Cycle)

Items that span cycles or are permanently deferred. **Active-cycle follow-ups live in the cycle plan's "Captured During Cycle" section, not here.** This section is only for items that outlive a single cycle.

### From DEC-041 (unit system — partial shipment)

Shipped with DEC-042: `Distance`, `Pace`, `PaceRange(Fast, Slow)`, `TrainingPaces` value objects. Remaining scope deferred to pre-MVP-0: `StandardRace` enum, `UnitPreference` enum, EF Core `ValueConverter` mappings, full controller-layer adoption. See `docs/planning/unit-system-design.md`.

### From POC 1 cleanup

- `EvalTestBase` relative path navigation (`"../../../../../"`) — fragile if structure changes.
- `AsIChatClient()` not on `ICoachingLlm` interface — add to interface or mark internal.
- `WeekGroup` nested record uses mutable `List<WorkoutSummary>` — use `IReadOnlyList`.
- Nested types in `YamlPromptStore` — extract to own files or document as intentional.

### Structured output post-deserialization validation (pre-MVP-0)

Anthropic's constrained decoding enforces property names, types, and `additionalProperties: false`, but does NOT enforce `minItems`/`maxItems` on arrays or numerical `minimum`/`maximum` on scalars. `MesoWeekOutput` addressed structurally via DEC-042. Still open: audit `MacroPlanOutput`, `MicroWorkoutListOutput`, and any future structured outputs for similar invariants; audit eval suite for assertions that depend on LLM compliance with schema descriptions rather than structural enforcement.

### Infrastructure

- Kubernetes — deferred to public beta per DEC-032.
- Garmin Connect integration — deferred to post-MVP-1; Apple Health prioritized per DEC-033.
- Frontend visual design planning — flagged, not yet started.
- **Marten 9 upgrade** — current pin is Marten 8.28; Marten 9 (undated) drops sync LINQ ops (tied to Npgsql 10), flips Conjoined PK ordering to `TenantId_Then_Id`, and will formally certify .NET 10. Both changes are mechanical — sync removal is a pass with `LoadAsync`-style replacements, PK reorder is a one-time index-rebuild migration. No load-bearing rewrite risk. Monitor `JasperFx/marten` repo; revisit when v9 ships. If a `.net10`-specific Marten 8 bug surfaces before v9 lands, the escape hatch is targeting the test assembly at `net9.0` while keeping the SUT on `net10.0`. Captured per R-047.

### Cost optimization (post-MVP-0, DEC-038)

Tiered model routing (Haiku / Sonnet / Opus) for ~60% cost reduction; Batch API for eval runs (50% discount); Opus 4.6 as eval judge.

### Quality tooling (DEC-043 — deferred / cut)

- Claude Code GitHub Action — **permanently cut.** Replaced by local `/review-pr` + user's `deep-review` skill. Do not re-propose.
- Snyk — **deferred** (R-039). Reconsider triggers: PII ingestion, container deployment, second contributor, Dependabot miss >30 days on high-severity transitive CVE.
- Codacy — **deferred** (R-040). Reconsider only if a language module outside SonarQube Cloud free-tier coverage is added.
- CODEOWNERS — **deferred** until first external contributor joins.

### Quality tooling (add later regardless of visibility)

- Performance regression testing in CI — deferred per DEC-034 (GitHub runner variance).
- Trivy container image scanning — add when deploying Docker images.

### Pre-public-release gate (from `docs/features/backlog.md`)

Everything under "Pre-Public Release" in the feature backlog — extended health screening (PAR-Q+), expanded medical-scope keyword triggers, population-adjusted safety guardrails, beta participation agreement, LLC formation, privacy policy, full ToS. Required before anyone beyond the builder and trusted friends uses the product. Tracked in the feature backlog, not here.
