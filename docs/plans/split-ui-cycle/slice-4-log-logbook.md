# Slice 4 Design: Log & Log Book

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff §§ 5.3, 5.4 + sheets 2c/5b (Log), 2d/5c (Log Book), 3d (deviation framing), 5h (splits). Depends on Slice 1. Carries the cycle's two non-LLM backend deltas.

## Purpose

Restyle the logging write surface around the plan ("record what you actually ran — the plan adapts to the truth") and rebuild history as the LOG BOOK ledger. The log form's wire behavior (DEC-075 string-backed fields, unit-aware schema, idempotent create) is untouched.

## Locked design decisions

- **D1 — Prescribed banner (§ 5.3, sheet 3d).** Clay square marker + mono `PRESCRIBED — THRESHOLD INTERVALS · 9.0 KM · 4:00–4:30/KM`, sourced from the plan's slot **for the selected date** (back-dating included); hidden when none. **Backend delta:** a `GET` prescribed-slot-for-date endpoint reusing the existing `PlanCalendar.ResolveSlot` + prescription resolver — that logic is today reachable only through the SSE conversational path, and the client cannot replicate it without re-deriving prescription semantics (DEC-089 D5(a)). Rides the full codegen chain.
- **D2 — Form recomposition (§ 5.3).** Header `LOG RUN` + tappable date chip (calendar icon + `WED, JUL 8` + chevron → native date input); sub copy; DISTANCE / TIME as two large numeric cells (condensed 30px, mono unit suffix, `inputMode="decimal"`); derived display-only **PACE** row once both fields parse (client math); COMPLETION segmented control replacing radios (same wire enum); `HOW DID IT GO?` label + deviation-framing helper + placeholder, sitting directly under the prescribed banner so "differed from what?" is on screen; `MORE DETAILS` collapsible (mono `RPE · HR · ELEVATION` hint) → existing metric fields; `SAVE RUN` 54px primary, disabled until schema-valid. Unit-preference gating, validation, and error/retry behavior unchanged.
- **D3 — Ledger rows (§ 5.4).** Week groups headed `WEEK OF JUL 6` + mono client-side aggregate `15.2 KM · 2 RUNS` (`· 1 SKIP` when present). Entries as hairline-separated ledger rows, not cards: left day numeral (condensed 26) over mono weekday; middle title + status mono tag (COMPLETED moss / PARTIAL amber / SKIPPED danger) + `ON-PLAN` suffix when matched + note snippet or metrics line; right column `9.2 KM / 41:00 / 4:27 /KM` (pace client-derived). Skipped rows dim to 75%, stats `—`.
- **D4 — Backend delta for the ledger (DEC-089 D5(b)).** The history-row DTO (`WorkoutLogDto`) gains `IsOnPlan` (server-resolved against the stored prescription snapshot) and a nullable prescribed-title. This **narrowly amends** the deliberate MVP-0 stance of not serializing the prescription snapshot: two display-scoped fields only; the snapshot itself stays private. Unplanned runs fall back to a generic "Run" label client-side. Rides the codegen chain (same PR as D1's endpoint).
- **D5 — Splits expander (sheet 5h).** `N SPLITS ▾` inline expander; existing lazy-mount behavior kept; table restyled mono + hairline rows, `tabular-nums`, HR column only when any split carries one, **no condensed type in dense tables**. `LOAD OLDER` = full-width secondary over the existing keyset pagination.

## Functional requirements

- Banner appears/disappears correctly as the selected date changes, across plan boundaries (pre-start, post-race, rest days ⇒ hidden or rest handling per spec).
- Derived pace updates live, handles unit preference, and never blocks submission (display-only).
- Segmented completion posts the identical wire values as today's radios.
- Week aggregates count running logs only and match the design's `KM · RUNS · SKIP` arithmetic; weeks group exactly as the existing helper defines them.
- ON-PLAN rows and titles render from the new fields; old rows without snapshots degrade gracefully.

## Quality requirements

- Log-form behavior-pinning specs (schema validation, idempotent create, toasts, back-dating) pass with presentational-only updates.
- New unit tests: pace derivation (zero/garbage/unit-flip inputs), aggregate math, banner date-boundary cases.
- Backend: endpoint + DTO additions covered by integration tests (slot-for-date across the calendar; on-plan resolution for matched/unmatched/legacy rows).
- E2E log→history journey realigned to the new selectors where components were replaced.

## Scope: In

Both screens' recomposition/restyle; the two backend deltas + codegen; client derivations (pace, aggregates); splits/pagination restyle; test realignment.

## Scope: Out (deferred)

Any further prescription-snapshot exposure; server-side aggregates (client-side per design); WorkoutLog input-size caps (pre-public-release backlog, unchanged).

## PR sketch

1. **PR-A** — backend: prescribed-slot GET + `IsOnPlan`/title fields + integration tests + codegen.
2. **PR-B** — `/log` frontend.
3. **PR-C** — `/history` frontend.

## References

- Handoff §§ 5.3, 5.4, 6; sheets 2c/5b/2d/5c/3d/5h.
- `PlanCalendar.cs` (`ResolveSlot`), `WorkoutLogDto.cs`, `CandidatePrescriptionDto.cs` (the conversational sibling of D1), `workout-log-form.schema.ts` (DEC-075 string-backed), `week-grouping.helpers.ts`.
- DEC-075 (form schema), DEC-077 (idempotency), DEC-086 (units), DEC-089 D5.
