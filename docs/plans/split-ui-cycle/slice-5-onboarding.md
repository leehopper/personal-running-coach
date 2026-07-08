# Slice 5 Design: Onboarding

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff § 5.5 + sheets 2e/5d (screen) and 3c (open input). Depends on Slice 0 only (onboarding renders outside the tab shell). **Contains the cycle's only LLM-touching change.**

## Purpose

Recompose the form-first intake (shipped in MVP-0 Slice 4C) into the designed numbered-section flow, and add the one net-new intake capability: `00 — IN YOUR OWN WORDS`, a narrative free-text field the coach reads first.

## Locked design decisions

- **D1 — Field parity is already exact.** The 4C form's schema matches the design's field set one-for-one: the five goal options, race-gated target event (name / distance / date / optional goal time), fitness 2×2 (weekly volume / longest recent / optional recent race + time), schedule (run days / session minutes / Mon-first day chips), injury switch → required description, hard-workouts + trails switches. **No field-level wire change for § 5.5's sections 01–05** — this is recomposition + restyle over the existing schema and `POST /api/v1/onboarding/answers`.
- **D2 — The narrative field (§ 5.5 "00", sheet 3c; backend + prompt).** Always-visible textarea directly under UNITS; placeholder "Coming back from a calf strain. 10K in October. Tuesdays are impossible, and I hate treadmills…"; helper "The coach reads this first. Plain words beat perfect forms — the form below keeps the numbers honest." Submits **with** the form as one free-text field on the answers payload, plumbed through the canonical answer record → `OnboardingView` → `AnswerCaptured` (additive), and injected **verbatim** into plan-generation prompt assembly alongside the existing per-topic nuance fields (`ContextAssembler` — same unsanitized posture as those fields, a known and accepted MVP-0 caveat). Consequences: OpenAPI codegen chain **and** DEC-074 hash-manifest regen + targeted plan-gen eval fixture re-record (funded key), Replay-verified. **Isolate this in its own PR** so the re-record blast radius is contained.
- **D3 — Section structure (§ 5.5).** Wordmark; title `TELL ME WHAT WE'RE WORKING WITH` + "Answer straight. The plan is only as honest as you are."; UNITS segmented first (DEC-086 units-before-distances preserved, including the reseed/remount flow); numbered 2px-rule sections `00`–`05`; option rows radio-right; `02 — THE RACE` revealed by the race goal; mono reassurance line under `03`; 7-day toggle chips; switches in `05`; `+ ADD DETAIL` collapsibles **only where a nuance field exists** — THE RACE has none, so it gets no collapsible (DEC-089 D7; the design's single drawn collapsible sits under 05).
- **D4 — CTA + building state.** `BUILD MY PLAN` 54px + mono "THE COACH DRAFTS 12 WEEKS IN ABOUT 30 SECONDS". Submit swaps to the shared **BUILDING YOUR PLAN** surface (built in Slice 0 — see its D6: full surface, clay indeterminate-honest progress, mono line) replacing today's plain `role="status"` text. The same component serves regenerate in Slice 6; this slice adopts it, it does not build it.
- **D5 — Preserved behaviors (§ 5.5 "Keep").** Units-change reseed/remount, resume hydration, idempotency-key rotation, the 422 re-submittable rejection flow (DEC-087/088 exhaustion path) — all pinned by existing specs that must keep passing.

## Functional requirements

- The full intake renders per design in both themes; conditional reveals (race section, injury description) animate with motion-reduce pairing.
- Narrative round-trips: submitted → persisted on the onboarding stream → visible to plan generation (assert the assembled prompt contains it verbatim) → present on resume hydration.
- Empty narrative is valid (field optional); a max length is enforced consistent with the nuance-field precedent (spec picks the bound).
- Building surface shows until the plan lands or the 422 path returns the form.

## Quality requirements

- Backend: handler/mapper validation tests for the new field; projection tests for the additive event field; prompt-assembly test asserting verbatim injection; eval suite green in Replay after the targeted re-record (existing targeted-re-record procedure).
- Frontend: RHF/Zod schema tests updated; resume/idempotency/units-reseed behavior specs pass unmodified; e2e onboarding journey realigned.
- Prompt-injection posture explicitly unchanged and documented in the PR (same caveat as nuance fields — pre-public-release hardening item stands).

## Scope: In

Narrative field end-to-end (backend + prompt + evals + codegen); full intake recomposition; BUILDING surface adoption; test realignment.

## Scope: Out (deferred)

Nuance field for THE RACE (none exists; not added); any sanitization change to free-text prompt paths (pre-public-release); regenerate wiring of the building surface (Slice 6).

## PR sketch

1. **PR-A** — backend narrative field + prompt injection + manifest/eval re-record + codegen.
2. **PR-B** — intake recomposition + narrative UI + building surface (may split if review size demands).

## References

- Handoff §§ 5.5, 6; sheets 2e/5d/3c.
- `SubmitStructuredAnswersRequestDto.cs`, `onboarding-form.schema.ts`, `ContextAssembler` prompt-assembly path, the DEC-074 manifest procedure + the targeted re-record steps documented in `EvalTestBase` (DEC-039 TTL extension included).
- DEC-086 (form-first + units), R-085 artifact (form-first architecture), DEC-089 D7.
