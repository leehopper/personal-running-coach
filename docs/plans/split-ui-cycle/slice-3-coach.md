# Slice 3 Design: Coach

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff § 5.2 + sheets 2b/5a (screen) and 3b (turn system). Depends on Slice 1. **Frontend-only** — every payload the design needs is already typed on the wire.

## Purpose

Rebuild the `/coach` transcript as the designed turn system: five typed turn kinds, each with the UI owning 100% of layout and the model owning 0%. "Richness comes from typed turn kinds, not from trusting the model to format."

## Locked design decisions

- **D1 — Preserved contracts (§ 5.2 "Keep").** `TranscriptScroller` behavior (`role="log"`, pin-to-bottom unless scrolled up), stream retry affordance, confirm mutation flow + idempotency (confirm reuses the SSE `clientMessageId` — DEC-077 posture), `Edit → navigate('/log', {state:{draft}})`. No markdown rendering, ever; text turns are plain text, `pre-wrap`.
- **D2 — User turn.** Right bubble, `--alp-raised`, radius 10/10/4/10, max-width 85%, pre-wrap + break-word, no clamp at any length. Mono meta `YOU · HH:MM` below — timestamps exist on the wire (`createdAt`) and are currently never rendered; this slice starts rendering them.
- **D3 — Coach text turn.** No bubble: mono clay label `COACH · HH:MM` + plain bone text. While streaming: clay block-cursor appended in place. On stream death: "That reply didn't go through." + RETRY (existing retry affordance, restyled).
- **D4 — Adaptation turn.** Nudge level = plain coach line, no card. Restructure = surface card with 2px clay left edge, `PLAN ADJUSTED` label, the LLM explanation pre-wrapped, and a `WHAT CHANGED ▾` expander **collapsed by default** revealing typed diff rows rendered from the existing `PlanAdaptationDiff` payload (`WorkoutChanges`: weekNumber + dayOfWeek + before/after workout; `WeeklyTargetChanges`: weekNumber + before/after km) — never parsed from prose. Row format: mono locus (`WK JUN 29 · SATURDAY` / `WK JUL 6 · VOLUME`) + `before → after` line with clay arrow. The locus calendar date is **derived client-side** from weekNumber + dayOfWeek against `PlanProjectionDto.PlanStartDate` (join with `GET /plan/current`; the turn itself carries no date) — via the **shared adaptation-diff presentation helper introduced in Slice 2**, not a second implementation of the date math.
- **D5 — Safety turn.** Amber: 3px amber left edge, heading `WORTH A PROFESSIONAL LOOK`. Red: 3px danger left edge, tinted background (`#1C1614` dark), heading `STOP — GET SEEN`. Content renders **in full — never clamped, collapsed, or crowded by competing CTAs**. Tier drives only accent + heading; the scripted content comes from the wire as-is.
- **D6 — Log draft card (design 3b·05/06).** `LOG THIS RUN?` + 2×2 condensed grid (DISTANCE / TIME / DATE / STATUS, status moss) + `ON-PLAN — <workout> · TARGET <n> KM` mono line when the existing `CandidatePrescriptionDto` matched + CONFIRM primary / EDIT secondary / CANCEL ghost. States: **saving** (buttons locked, card dims, `SAVING…`), **success** (card collapses to a persistent one-line receipt: moss check + `LOGGED — 9.2 KM · 41:00 · JUL 8` + `LOG BOOK →`), **failure** (card stays open, error toast, retry reuses the idempotency key). **Partial-parse (`MISSING`) states are deferred** — the backend never emits a partial draft (cycle plan § Captured; DEC-089 D6). Receipt persistence must come from the timeline's committed turn representation, not ephemeral component state — it survives remount/reload.
- **D7 — Date dividers.** Mono `TUE JUN 30` between hairlines, `TODAY — WED JUL 8` for today — client-side grouping of turns by `createdAt` calendar day.
- **D8 — Composer.** 48px input + 48px clay square send (arrow-up icon), pinned above the tab bar; placeholder "Ask, or describe a run to log…". Existing submit/Enter semantics preserved.

## Functional requirements

- All five turn kinds render per design 3b in both themes, including the streaming and errored states.
- The WHAT CHANGED expander renders every diff row kind (workout swap, current-week volume, upcoming-week volume) with correct loci.
- Confirmed logs show as persistent receipts on reload; cancel dismisses the draft without commit (existing advisory-draft posture).
- Suggestion-chip prefill/focus arriving from Today's digest continues to land in this composer — the receiver contract ships in Slice 2; this slice's restyle preserves it.

## Quality requirements

- Behavior-pinning specs for streaming/confirm/retry pass with only presentational updates; new specs pin: timestamp meta, date-divider grouping (incl. midnight/timezone edges), locus derivation, receipt persistence, safety-turn full-render (no clamp).
- Safety turns: a pathological-length scripted content still renders in full — that is the acceptance bar (AX-01).
- Trademark scrub discipline untouched (persisted prose already server-scrubbed — F2/DEC-081 lineage).
- Unit preference applies to draft-card values, receipts, and diff rows.

## Scope: In

Turn-kind restyles + the net-new behaviors above (timestamps, dividers, locus join, receipt states); composer restyle; transcript-scoped tests.

## Scope: Out (deferred)

Partial-parse draft states (unreachable — deferred with the design as contract); any wire change (none needed); Today digest (Slice 2).

## PR sketch

1. **PR-A** — text/user/safety turns + timestamps + date dividers + composer.
2. **PR-B** — adaptation card + diff rows + log-draft card states + receipt.

## References

- Handoff §§ 5.2, 6; sheets 2b/5a/3b.
- `PlanAdaptationDiff.cs`, `StructuredLogDraft.cs`, `CandidatePrescriptionDto.cs`, `conversation.model.ts` (generated turn types), `log-confirmation-card.component.tsx`, `transcript-scroller` specs.
- DEC-085 (conversation core), DEC-077 (idempotency), DEC-081 (safety referral surface), DEC-089 D6.
