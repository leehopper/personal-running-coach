# Research Prompt: Batch 31a — R-085

# Form-First Hybrid Onboarding Intake Over an Event-Sourced Pattern-B Flow — React 19 + shadcn/ui + RTK Query + .NET 10 Marten/EF (2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a React 19 + TypeScript-strict + Vite SPA (Tailwind + shadcn/ui + RTK Query + React Hook Form + Zod + React Router v7) backed by a .NET 10 event-sourced onboarding flow (Marten per-user stream + inline projection + a co-transactional EF projection, deterministic-led over a static topic list, Pattern-B structured output), what is the current 2026 best-practice pattern for **replacing a fully-conversational multi-turn onboarding with a form-first hybrid** — a structured intake form for the known fields plus optional per-area free-text for nuance — **without** breaking the event-sourced projection model, the deterministic completion gate, or the byte-stable prompt cache? Which concrete UX + data-flow pattern should land in Slice 4C?

## Context

I'm preparing Slice 4C (onboarding redesign + km/miles units) of MVP-0 for RunCoach, an AI running coach. This is a direct follow-up to R-065 (`batch-21a-onboarding-chat-ux-react19.md` → DEC-063), which recommended a guided **conversational** onboarding chat built on shadcn primitives. That flow shipped in Slice 1 and works, but a live end-to-end validation pass (2026-06-13) surfaced two problems the builder wants fixed:

1. **The fully-conversational six-topic flow feels heavy.** The builder wants a **structured starting form** for the known fields (primary goal, target event + date, current fitness / weekly volume, weekly schedule = days + typical session duration, injury history, unit preference) with a **free-text box per area** for nuance/variance the form can't capture — rather than eliciting everything as back-and-forth free text.
2. **A slot-merge loop.** Today, answers are captured all-or-nothing: the `WeeklyScheduleAnswer` requires all fields (7 day booleans + max-run-days + typical-session-minutes + a description) in **one** LLM emission, and the onboarding prompt feeds back only the captured-slot summary + the current turn's text (**no conversation transcript**). So a runner who gives days on one turn and duration on the next can never have them merged — the model re-requests clarification until one message states everything at once. A form that collects structured slots directly sidesteps this entirely.

The builder has **already decided the shape** (do not re-litigate these — research the *how*, not the *whether*):

- **Hybrid form-first, not full-conversational and not form-only.** A structured form is the primary path; an optional per-area free-text box captures nuance that feeds coaching context. The LLM's role shrinks to nuance + phrasing, not primary elicitation.
- **The form must originate the same `AnswerCaptured` events** the LLM extraction produces today, so the event-sourced model is preserved (see backend constraints). Form submission is a deterministic event origin; it must NOT write the EF projection row directly.
- **Units are collected up front** (first field) and stored in a dedicated `UserSettings` store (out of scope for this prompt — a sibling 4C-units workstream owns it); the form writes the chosen unit there and renders numeric inputs in the chosen unit.

### Backend constraints (locked — the redesign must fit these)

- **DEC-047** — onboarding state = a per-user Marten event stream + an inline `SingleStreamProjection<OnboardingView>` (drives the deterministic next-topic selector + completion gate) + an `EfCoreSingleStreamProjection<RunnerOnboardingProfile>` writing the `UserProfile` row inside Marten's transaction. Static six-topic list: `PrimaryGoal, TargetEvent (skipped unless PrimaryGoal==RaceTraining), CurrentFitness, WeeklySchedule, InjuryHistory, Preferences`. Deterministic completion gate = all required slots captured + no outstanding clarifications.
- **DEC-060** — the EF row is written by a projection **inside** Marten's transaction (no dual-write). A deterministic form must still **originate events**, not write the EF row directly.
- **DEC-058** — onboarding output is Pattern-B: one byte-stable frozen JSON schema (`OnboardingSchema.Frozen`) with six nullable `Normalized*` slots + a `Topic` discriminator, validated at runtime (Anthropic rejects `minimum`/`maximum`/`oneOf`/`pattern`/`format`). Any new/changed onboarding field edits this frozen schema and busts the DEC-074 prompt-hash manifest + eval cache.
- **DEC-084** — onboarding voice is gruff-direct with a deterministic `VoiceProseGuard` STYLE gate (no em/en dashes, no exclamation, no filler); `onboarding-v1` is an enforced prose surface. Any prompt rewrite regenerates the DEC-074 manifest and re-records the onboarding eval.
- **DEC-085 PR3b** — unit conversion is deterministic-layer-only: the LLM extracts a runner-stated value + unit verbatim; `WorkoutDraftUnitConverter` does the SI math. The onboarding free-text nuance must follow this — the LLM never converts.
- Multi-turn threading today = a per-turn idempotency GUID + the server-side event stream; no session token. `POST /api/v1/onboarding/turns`, `GET /api/v1/onboarding/state`, `POST /api/v1/onboarding/answers/revise` (the revise path is currently unused by the UI).

### Frontend stack + current state (locked)

- **React 19**, TypeScript strict (`noUncheckedIndexedAccess`), Tailwind + shadcn/ui, RTK Query, React Hook Form + Zod-v4, React Router v7, Vitest + RTL, Playwright. `radix-ui` (unified) installed; **no** `motion/react` (DEC-063 deferred, defined adoption path), **no** `react-markdown`, **no** `react-day-picker`/Calendar, **no** i18n framework.
- Current onboarding is a Redux `onboardingSlice` + a per-turn RTK Query mutation (`useSubmitOnboardingTurnMutation`) rendering a chat transcript; input control is chosen from a server `SuggestedInputType` during live turns and from a brittle `pickInputTypeForTopic` topic-guess on the resume path (the source of an input-kind/assistant-text mismatch, since `GET /state` carries no transcript). Existing turn-input vocabulary: single-select, multi-select (day picker, comma-joins days as free text), numeric (hardcoded "Weekly distance (km)"), date, text.

## Research Question

**Primary:** What is the current 2026 best-practice pattern — form architecture, data flow, event-origination design, accessibility, RTK Query integration, and migration path — for a **form-first hybrid onboarding** on this exact stack that (a) collects the known fields as a structured form, (b) originates the same event-sourced `AnswerCaptured` events a deterministic form submission would produce (preserving DEC-047/060 projections and the completion gate), (c) captures optional per-area free-text nuance that feeds coaching context via the LLM without the LLM doing structured extraction or unit conversion, and (d) retires the slot-merge loop and the resume-path input-kind mismatch?

**Sub-questions (must be actionable):**

1. **Form-first vs conversational hybrid — UX pattern survey.** For a known-field intake with per-area nuance, what 2026 patterns exist (single long form, a short stepped wizard, an accordion of field groups with inline nuance boxes, a "form with a chat escape hatch")? Which best fits a 6-topic training intake on mobile-first, and how does each handle the conditional `TargetEvent` topic (only for race-training goals) and the required-vs-optional split? Recommend one with rationale.

2. **Event origination from a deterministic form.** Given DEC-047/060 (events drive both the Marten `OnboardingView` and the EF `UserProfile` row inside one Marten transaction), what is the clean pattern for a form submission to **originate `AnswerCaptured` events** without an LLM and without writing the EF row directly? Options: a new `SubmitStructuredAnswers` command that appends `AnswerCaptured` events per completed slot (bypassing the LLM path); a batched single-turn append; reuse of the existing `AnswerCaptured` event shape vs a new `StructuredAnswersSubmitted` event upcast into the projections. Address idempotency (the current per-turn GUID model), partial-save/resume, and replay.

3. **Whole-record vs field-level slots.** The current answer records are atomic (whole-record `AnswerCaptured`). A form naturally produces complete records per topic, which dissolves the merge loop — but does a form-first design want per-field progressive save (append as each field is filled) or per-topic-complete save? Trade-offs for resume, event volume, and the completion gate. If per-field, what changes to the event/projection model are required, and are they worth it vs per-topic-complete?

4. **Optional per-area free-text nuance → coaching context.** The nuance boxes must feed the LLM for later coaching **without** the LLM doing structured slot extraction or unit conversion (DEC-085). Where does the nuance live — a free-text field on each `Normalized*` slot record (already present as `Description` on several), a separate note stream, or a `ContextAssembler` layer? How is it surfaced into plan-gen / conversation context? Does capturing it require any LLM call during onboarding at all, or is it purely stored text rendered into later prompts?

5. **Keep, shrink, or retire the conversational Pattern-B path.** With a form as primary, is there still a role for the LLM per-turn call during onboarding (e.g. a single "anything else?" phrasing turn, or nothing)? If the LLM slot-extraction path is retired for the form flow, what is the migration path for the shipped `onboarding-v1.yaml` / `OnboardingTurnOutput` / `OnboardingTurnHandler` — deprecate-in-place, feature-flag, or hard cutover — and how do existing in-progress onboarding streams (event-sourced) behave across the cutover? Note the DEC-058 frozen-schema + DEC-074 manifest + eval-record implications of any prompt/schema change.

6. **The slice-4-conversation.md carry-forwards.** Two carry-forwards assumed the conversational path continues: a server-driven `SuggestedInputType` redesign (retire the client `pickInputTypeForTopic` mirror) and an explicit `(topic, hasOutstandingClarification, isResume)` state-machine contract. Does a form-first redesign make these **moot** (the form carries structured state, no per-turn input-affordance guessing), or should the form still adopt a server-driven field-affordance contract? Recommend, and say explicitly whether these carry-forwards are obsoleted.

7. **Schedule / days + duration collection UI.** The WeeklySchedule slot needs days-of-week + max-run-days + typical-session-minutes together. Today a comma-joined multi-select day picker + a separate numeric. For a form: a day-of-week toggle group (radix `ToggleGroup`) + numeric session duration + max-days in one field group? Is a `react-day-picker`/Calendar ever warranted here (probably not — day-of-week ≠ calendar), and if the research says yes, justify the new dependency against the existing multi-select + date turn inputs. Accessibility of a day-toggle group.

8. **Units-up-front input in the chosen unit.** Units are the first field (stored in a sibling `UserSettings` store). Subsequent numeric inputs (weekly volume, recent race distance) should accept entry in the chosen unit and convert miles→km on write (deterministic, mirroring `WorkoutDraftUnitConverter`'s `1609.344`). What's the round-trip-safe RHF + Zod pattern for a unit-aware numeric field (display in miles, persist in km, edit without drift)? How is the miles↔km round-trip kept lossless for editing?

9. **RTK Query integration for a form-first flow.** The conversational flow used a per-turn mutation + a Redux transcript slice. A form-first flow: one `SubmitStructuredAnswers` mutation (whole form or per-topic), `GET /state` for resume, RTK cache + tag invalidation. What's the 2026 pattern for a multi-section form with progressive/resumable save over RTK Query, keeping the `__Host-` cookie + XSRF antiforgery base-query contract? Interaction with the known RTK per-user cache-reset-on-auth gap.

10. **Accessibility.** WCAG 2.2 / ARIA APG 2026 for a multi-section intake form: fieldset/legend grouping, error summary + inline errors, required-field indication, focus management on section advance, the day-toggle group, and the conditional TargetEvent reveal. Mobile (iOS VoiceOver / Android TalkBack), virtual-keyboard/viewport, safe-area.

11. **Eval implications.** The 2026-06-11 finding asked for "eval scenarios for multi-slot free-text merges." A form that collects structured slots directly **removes the free-text-merge failure mode**. Does 4C-onboarding still need those eval scenarios, or only evals for the optional nuance path + any residual LLM phrasing turn? What's the eval shape for a mostly-deterministic form-first onboarding under the DEC-074 manifest + DEC-084 voice guard?

12. **Testing pattern.** Vitest + RTL for a multi-section form (section advance, conditional reveal, unit-aware numeric, day-toggle, error states); Playwright E2E for the full form → plan-generation handoff. Pitfalls vs the existing chat-transcript test shape (the current E2E stubs the LLM and asserts control-swap-per-turn — a form flow changes that contract).

13. **Migration & differentiation from the 4B conversation panel.** Onboarding is a full-page, guided, terminating flow; the 4B `CoachChat` is an always-on streaming conversation. Which primitives (if any) are shared vs divergent? Ensure the form-first onboarding doesn't accidentally re-introduce a chat dependency it doesn't need.

## Why It Matters

- **First impression.** Onboarding is the first genuine "set up my coach" moment; the live pass flagged the conversational flow as heavy and loop-prone. Getting the form-first shape right is high-leverage.
- **Event-sourcing is load-bearing.** DEC-047/060 drive both projections from one Marten transaction; a form that writes the EF row directly, or that changes the event model carelessly, breaks the co-transactional guarantee and the replay story. The event-origination pattern (sub-question 2) is the crux.
- **The slot-merge loop is a correctness bug, not a polish item.** It cost four round-trips live. A form collecting structured slots is the durable fix, but only if it fits the event model.
- **Don't over-reach on dependencies.** DEC-041/DEC-063 defer i18n, `UnitsNet`, and `motion/react`; the existing radix + multi-select + date primitives likely cover the form. Any new dependency needs explicit justification.

## Deliverables

- **A concrete recommendation** — one form-first hybrid pattern (form architecture + where nuance lives + whether any LLM turn remains) with explicit rationale and rejected alternatives.
- **An event-origination design** — how a form submission originates `AnswerCaptured` (or a new upcast event) preserving DEC-047/060 projections, the completion gate, idempotency, and resume; whole-record-per-topic vs per-field, with a recommendation.
- **A migration path** for the shipped conversational `onboarding-v1` / `OnboardingTurnHandler` / `OnboardingTurnOutput` (deprecate-in-place / flag / cutover) and behavior of in-flight event streams across the change, with the DEC-058/DEC-074/DEC-084 implications spelled out.
- **A component-shape sketch** — React 19 + TS-strict + shadcn form composition for the intake: section grouping, a unit-aware numeric field (display-unit vs persist-km), the day-toggle schedule group, the conditional TargetEvent reveal, per-area nuance boxes, error/summary a11y.
- **An explicit verdict on the two slice-4-conversation.md carry-forwards** (server-driven `SuggestedInputType`; explicit state-machine contract) — obsoleted by form-first, or still needed.
- **An RTK Query wiring sketch** — the submit mutation(s), resume `GET /state`, cache/tag invalidation, cookie+XSRF base-query.
- **An eval-scope recommendation** — what survives of the "multi-slot free-text merge" eval ask under a form-first design.
- **A testing sketch** — Vitest + RTL + Playwright for the form flow.
- **A dependency verdict** — whether the existing radix/multi-select/date primitives suffice, or any new dependency (`react-day-picker`, etc.) is justified, checked against DEC-041/DEC-063.
- **Citations** — current shadcn/RHF/Zod/radix docs (2026 versions), WCAG 2.2 / ARIA APG references, 2025-2026 form-first onboarding case studies, and any event-sourced form-intake precedent.

## Out of Scope

- **The km/miles display/conversion layer + `UserSettings` store** — the sibling 4C-units workstream owns it (DEC-086 / frontend-display-only). This prompt only needs units as an up-front form field written to that store and unit-aware numeric input.
- **The LLM speaking miles** — deferred to MVP-1 (DEC-041); prose stays km-native.
- **Plan-rendering / plan-view UI** — separate concern.
- **The 4B open-conversation streaming panel** — shipped (DEC-085); referenced only for shared-primitive differentiation.
- **Coaching prompt *content* / voice tuning** — DEC-084 locks the register; prompt prose edits happen at spec time, not in this research.
- **Auth / registration UI** — shipped in Slice 0.
- **Internationalization / localization** — English-only for MVP-0.
