# Slice 2 Design: Today

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff § 5.1 + sheets 2a/4a (screen) and 3a (digest contract). Depends on Slice 1.

## Purpose

Recompose home (`/`) as **Today** — the daily driver screen: what to run now, how the week stands, what the coach last said, what's next, and where the block is going. Replaces the `TodayCard` + `MacroPhaseStrip` + `MesoWeekBlock` card composition.

## Locked design decisions

- **D1 — Screen order is the contract (§ 5.1):** header (wordmark + `WEEK N OF 12 — PHASE`) → today hero → THE WEEK → FROM YOUR COACH → UP NEXT → THE BLOCK.
- **D2 — Hero.** Eyebrow `WEDNESDAY, JULY 8 — ON THE SCHEDULE` (clay condensed) → display title → one-sentence workout summary **composed client-side** from segments + coaching note → 3-cell hairline stat band (distance / pace range with `white-space: nowrap` / reps-or-duration) → `LOG RUN` primary + `DETAILS` secondary expanding the full segment list (reuses `MicroWorkoutSegmentRow` data). **Rest-day variant** (`slotType !== 'Run'`): title `REST DAY`, summary "Recovery is training.", next-workout line, no LOG RUN, week grid still shown.
- **D3 — Coach digest hard contract (sheet 3a).** Shows only the latest exchange; user line 1-line ellipsis; coach text `-webkit-line-clamp: 3`; **nothing here ever scrolls**; whole module navigates to `/coach`. When the latest turn is a restructure-level adaptation → one-line headline card (`PLAN ADJUSTED` + summary + chevron). **The summary is composed client-side, deterministically, from the typed `PlanAdaptationDiff` payload** (e.g. "This week 30 → 26 km. Saturday trims to 14.") — never clamped LLM prose, never parsed from prose (DEC-089 D3; no wire change — the diff is already typed: `PlanAdaptationDiff.cs`). The compose logic lives in a **shared adaptation-diff presentation helper** (headline text + weekNumber/dayOfWeek→calendar-date locus math against `PlanStartDate`) built in this slice and reused by Slice 3's WHAT CHANGED rows — one implementation of the date math, not two. Nudge-level adaptations render as a normal clamped coach line. Empty state: "Nothing yet…" + two suggestion chips (`HOW'S MY WEEK LOOK?` / `LOG THIS MORNING'S RUN`) that prefill/focus the `/coach` composer. The composer stub is a fake input that navigates to `/coach` and focuses the real composer. **This slice owns both ends of that handoff:** the senders here AND the net-new receiver on the `/coach` composer (router-state prefill + focus-on-arrival — no such plumbing exists today; the composer takes only `onSend`/`isStreaming`). Slice 3's restyle preserves the receiver contract.
- **D4 — THE WEEK.** Mono `N.N/NN.N KM` (logged running km this week / meso `WeeklyTargetKm`) + 7 day cells: done = moss fill + check (a log exists for that slot's date), today = 2px clay outline, planned run = raised + hairline, rest = flat surface. Data = meso day slots (`MesoWeekOutput` named-day slots) joined with this week's logs (history query) — the calendar-date↔slot mapping is derived client-side and must agree with server `PlanCalendar` semantics; pin the derivation with unit tests.
- **D5 — THE BLOCK.** Right goal chip (`10K — OCT 3`) from **structured target-event fields added to the plan projection** (DEC-089 D4 — backend delta: event name/distance/date ride `PlanProjectionDto`; `GET /onboarding/state` was rejected as the source because it reflects last-onboarding answers, not the active plan, and drifts on regenerate). 12 grid cells: current week = clay, remaining weeks of the current phase = raised + hairline, future phases = surface/dimmer. Phase span labels under (`BASE 1–4` … from `Macro.Phases`). Upcoming week rows `WK NN · summary · vol KM` with outline-moss `DELOAD` tag from `IsDeloadWeek`.
- **D6 — Client derivations own the header.** `WEEK N` via the existing `resolveCurrentWeek`; `OF M` from `Macro.TotalWeeks`; phase-for-week from cumulative `Macro.Phases[].Weeks` spans (replicates server `WeekContext` math — unit-test against the same fixtures).

## Functional requirements

- All six sections render per design in both themes, on run days and rest days, with a plan of any total length (the 12-cell block scales to `TotalWeeks`).
- Home fetches this week's logs for the grid/progress without breaking the history page's pagination cache semantics.
- Digest states 1–4 (short reply / clamped long reply / adaptation headline / empty + chips) all reachable and pinned by tests.
- DETAILS expander reveals the full segment list; collapsed by default.
- Deleted furniture (`TodayCard`, `MacroPhaseStrip`, `MesoWeekBlock` as home components) leaves no dead exports.

## Quality requirements

- Week/phase/date derivations unit-tested (including week-1 edges, plan start mid-week, last week, race week).
- Digest clamp behavior tested with pathological content (very long user line, multi-paragraph coach reply) — the § 3a rule "user content can never bloat it" is the acceptance bar.
- Units preference (km/miles) applies at every new render site (stat band, week progress, block rows, digest summary).
- No regression to the plan reload / regenerate flows.

## Scope: In

Full home recomposition; the client derivations above; week-log join; digest + focus handoff; **backend:** target-event fields on the plan projection + codegen chain.

## Scope: Out (deferred)

Coach screen itself (Slice 3); skeleton/failure audit (surface ships its own, Slice 7 audits); any adaptation-summary wire field (rejected — client-composed).

## PR sketch

1. **PR-A** — backend target-event fields + codegen (+ projection test).
2. **PR-B** — header + hero + THE WEEK (+ derivation unit tests).
3. **PR-C** — digest (+ the composer receiver + shared diff helper + digest state tests) + UP NEXT.
4. **PR-D** — THE BLOCK + furniture deletion (`TodayCard`/`MacroPhaseStrip`/`MesoWeekBlock`).

## References

- Handoff §§ 5.1, 6; sheets 2a/4a/3a.
- `PlanAdaptationDiff.cs` (typed diff), `MesoWeekOutput.cs` (day slots, `IsDeloadWeek`, `WeeklyTargetKm`), `MacroPlanOutput.cs` (`TotalWeeks`/`Phases`), `PlanProjectionDto.cs` (`PlanStartDate`, `GeneratedAt`).
- DEC-082 (date-aware horizon — the server-side week math being replicated), DEC-086 (units), DEC-089 D3/D4.
