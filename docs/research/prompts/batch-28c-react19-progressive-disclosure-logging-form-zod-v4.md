# Research Prompt: Batch 28c — R-079

# React 19 + RHF 7 + Zod v4 progressive-disclosure logging form (many optional metrics) and heterogeneous-metric history rendering on shadcn/ui (Vite SPA, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a React 19.2 + Vite 8 SPA using React Hook Form 7.76 + Zod 4.4 + shadcn/ui (Radix-based, Tailwind v4) — what is the canonical 2026 pattern for (a) a **workout-logging form** with a small set of required core fields plus a large set of *all-optional* metric fields hidden behind a "More details" progressive-disclosure section, where the optional-field Zod schema is **derived from the OpenAPI-generated request schema** rather than hand-written, and (b) a **history list** that renders a *sparse, heterogeneous* set of metrics per entry (each log has a different subset of `rpe`, `hrAvg`, `hrMax`, `calories`, `hrv`, `sleepScore`, `recoveryScore`, `weather`, `terrain`, plus an optional `splits` array) accessibly and compactly?

Deliver a recommendation with: the form architecture (RHF + shadcn `Form`/`FormField` vs React 19 Actions), the Zod-v4 optional/coercion strategy for numeric and duration inputs, the progressive-disclosure accessibility pattern, the heterogeneous-metric + splits rendering pattern, and the create-then-appear-in-history data-flow (optimistic vs pessimistic) on RTK Query.

### Sub-questions the artifact must answer

1. **Form idiom: RHF vs React 19 Actions.** The repo standardizes on RHF + Zod + shadcn `Form` (`zodResolver`). React 19 ships `useActionState`/form `action`/`useFormStatus`. For a form with conditional sections and a derived Zod schema, is there any 2026 reason to move to Actions, or is staying on RHF the right call? State it and give the canonical RHF + shadcn `Form`/`FormField`/`FormControl`/`FormMessage` skeleton for this form (core fields + a `Collapsible`-wrapped optional section).
2. **Zod v4 optional/default/coercion for a wide optional form.** Lock the rules for Zod 4.4.x: `.optional()` vs `.nullish()` vs `.optional().default()` interactions; how to represent "user left it blank" (undefined) vs "explicitly zero"; and the **numeric-input coercion problem** — RHF `valueAsNumber` yields `NaN` for an empty `<input type="number">`, which fails a naive `z.number().optional()`. Compare `z.coerce.number()`, a preprocess/`z.transform` empty-string→undefined step, and RHF's `setValueAs`. Give the exact pattern that makes an empty optional numeric field validate as "absent," not "NaN." Note any Zod v4 breaking changes vs v3 that bite here (`.default()` placement, error map, `z.coerce` behavior).
3. **Deriving the form schema from generated code.** The frontend has Orval-generated Zod v4 schemas (`src/app/api/generated/zod/...`) re-exported through a barrel. Show how the log-form schema should be built **from** the generated request-body schema (`.extend()`/`.pick()`/`.partial()` / `z.infer`) so the canonical field/metric set isn't hand-maintained — and how to layer *frontend-only* refinements (e.g., min/max, "blank→undefined" coercion) without diverging from the generated source of truth. Address the open-ended `metrics` map case: if the generated schema types metrics as an open record, how does the form bind a known canonical subset of keys while staying tolerant of unknown keys on read?
4. **Progressive disclosure accessibility.** Using shadcn `Collapsible` (Radix) for "More details": correct `aria-expanded`/`aria-controls`, focus management on expand, and — critically — **do collapsed-but-filled fields still submit?** (RHF keeps unmounted vs mounted field values differently depending on `shouldUnregister`.) Specify the `shouldUnregister` setting and mount strategy so a user who fills a metric, collapses the section, then submits still sends that value. Should the section auto-expand if any optional field has a value (e.g., on edit)? Treat default-open-vs-closed as a bounded UX choice and give the accessible default with rationale, not an open-ended taste prompt.
5. **Accessible numeric & duration inputs.** For distance and the numeric metrics: `<input type="number" inputMode="decimal">` vs text+pattern; locale/decimal concerns. For **duration** specifically: a single numeric "minutes" field vs an `mm:ss` masked/segmented input vs separate hour/min/sec fields — pick the lowest-friction accessible pattern for runners and show the RHF binding (the repo's existing `numeric-turn-input` uses `Controller` + `valueAsNumber`).
6. **Heterogeneous sparse-metric rendering in history.** Each history item shows only the metrics that are present. Compare layouts (definition list `<dl>`, labeled pill/badge grid, two-column key/value) for accessibility (screen-reader semantics, no empty-cell noise) and compactness. How to label canonical keys human-readably (`hrAvg` → "Avg HR") from a single source shared with the form, and how to render units consistently with the user's unit preference.
7. **Splits rendering.** A `splits` array (per-lap distance/duration/pace, possibly 10+ rows) should not bloat the list item. Compare: a nested `Collapsible` sub-row, a compact inline sparkline/summary with details-on-demand, a small table inside an expandable, or omit-from-list/show-in-detail. Recommend one. Is splits even *user-entered* in MVP-0 (manual entry of 10 laps is heavy) or display-only from future wearable import? If user-entered, show the RHF `useFieldArray` pattern; if display-only, say so and keep the form simpler.
8. **Create → appears-in-history data flow.** On submit, the new log must appear in the history list. Compare RTK Query **pessimistic** (await success, invalidate the history tag, refetch — the repo's only existing pattern, e.g., regenerate-plan) vs **optimistic** (`onQueryStarted` + `updateQueryData` insert, rollback on error). Recommend for a single-user MVP-0, considering the idempotency-key + "try again" error contract.
9. **Compliance with the Slice 2a design system.** Any new form/list must use semantic tokens only (no hardcoded colors), pass the `check-contrast` WCAG gate, support class-based dark mode, and honor DEC-063's reduced-motion contract (every `transition-*`/`animate-*` paired with a `motion-reduce:` variant). Note anything in the recommended components/patterns that needs explicit `motion-reduce:` pairing or a new semantic token (which would need contrast verification).

## Context

Slice 2b (Workout Logging) of the MVP-0 cycle for **RunCoach** adds an `app/modules/logging/` module: a "Log" action on the existing today's-workout card (`TodayCard` on `home.page.tsx`), a log form (required: distance, duration, completion status, notes; optional behind "More details": RPE, HR avg/max, calories, splits, HRV, sleep score, recovery score, weather, terrain), and a history list rendering whatever metrics are present.

**Verified frontend state (post-Slice-2a):**
- React 19.2.6, Vite 8, RTK Query (`@reduxjs/toolkit` 2.12), `react-hook-form` 7.76.1, `zod` 4.4.3, `react-router-dom` 7.16, Tailwind v4.2.
- shadcn/ui installed (`new-york`); primitives present and ready: `Button`, `Input`, `Label`, `Textarea`, `Form`/`FormField`/`FormItem`/`FormControl`/`FormMessage` (thin RHF+Zod wrapper; `FormControl` injects `aria-invalid`/`aria-describedby`), `Collapsible`/`CollapsibleTrigger`/`CollapsibleContent`, `RadioGroup`, `Dialog`, `Card`, `Badge`, `ScrollArea`. Unified `radix-ui` 1.4.3 package.
- Canonical form pattern in repo: RHF `useForm({ resolver: zodResolver(schema) })` + shadcn `Form`. Existing `numeric-turn-input` uses `Controller` + `valueAsNumber`. The regenerate-plan dialog uses a pessimistic RTK mutation + manual `Textarea`.
- Codegen (DEC-066): Orval 8.12.1 emits Zod v4 schemas to `src/app/api/generated/zod/...`, re-exported via a hand-maintained barrel (`generated/index.ts`); components import the generated schema + `z.infer` type. The drift gate is `git diff --exit-code`.
- Design system (DEC-070): two-tier Catppuccin Latte/Mocha semantic tokens, class-based dark mode via `ThemeProvider`, a `check-contrast` WCAG gate in pre-commit + CI, `tw-animate-css` (CSS-only). DEC-063 reduced-motion pairing is enforced by review (no lint rule yet).
- Deferred, non-blocking: #560 (migrate onboarding turn-inputs to `FormField`/`FormControl`/`FormMessage` — note `FormMessage` carries no `role="alert"`, the hand-rolled inputs do), #561 (shared single-field shell). The log form may use either the manual `Controller` pattern or the full `FormField` stack.
- A **calendar/date-picker is explicitly out of Slice 2a scope and not installed**; logging targets "today's workout," so assume no historical back-dating UI for MVP-0 unless the research shows it's trivial and necessary.

Prior frontend research (`10a-frontend-latest-practices` and `batch-26a` design-system integration) covered React 19 idioms and the token system but **did not** address a progressive-disclosure many-optional-field form, the Zod-v4 empty-numeric coercion problem, heterogeneous sparse-metric rendering, or splits display.

## Why It Matters

The log form is the highest-friction surface in the product — an athlete logs after every run, and the whole design bet is "bare-minimum logs and rich logs both feel easy." Get the optional-field Zod coercion wrong and empty numerics throw validation errors that block a minimum-payload save; get progressive disclosure wrong and filled-then-collapsed metrics silently drop on submit; get the generated-schema derivation wrong and the frontend hand-maintains a metric list that drifts from the backend (the exact failure Slice 1B's codegen was built to prevent). The history-rendering and splits questions determine whether a screen of mixed-richness logs reads cleanly or becomes empty-cell noise. These are the patterns every later logging/adaptation surface inherits.

## Deliverables

- **Form architecture recommendation** (RHF + shadcn `Form` vs Actions) with the canonical skeleton for core + collapsible-optional sections.
- **Zod v4 rule set** for optional/default/coercion, with the exact empty-numeric→undefined pattern and the splits-array handling, plus the generated-schema derivation recipe (`.extend`/`.pick`/`.partial`).
- **Progressive-disclosure spec**: `shouldUnregister`/mount strategy guaranteeing filled-then-collapsed fields submit, accessibility attributes, focus behavior, and the auto-expand-if-filled rule.
- **Accessible numeric + duration input** pattern with RHF bindings.
- **Heterogeneous sparse-metric rendering** pattern (chosen layout) + a single shared key→label/units mapping.
- **Splits rendering** recommendation, and a clear MVP-0 verdict on user-entered vs display-only (with `useFieldArray` sketch only if user-entered).
- **RTK Query create→history data-flow** recommendation (optimistic vs pessimistic) consistent with the idempotency + error contract.
- **Design-system compliance checklist** (tokens, contrast, dark mode, `motion-reduce:` pairings) for the new components.

## Out of scope

- Backend persistence of metrics / JSONB shape (separate prompt, R-077).
- The LLM-failure error envelope / retry (separate prompt, R-078) — this prompt only consumes its `retryable` flag for the "try again" affordance.
- Which metrics exist / their coaching meaning (`batch-3c`).
- Editing/deleting a logged workout (explicitly deferred from Slice 2b).
- Natural-language "log my run" parsing into the form (explicitly de-prioritized).
- The design-token architecture itself (settled by DEC-070 / `batch-26a`).

The artifact lands at `docs/research/artifacts/batch-28c-react19-progressive-disclosure-logging-form-zod-v4.md` and integrates into the Slice 2b spec (frontend section); it may surface a DEC entry if the Zod-coercion or progressive-disclosure pattern becomes a reusable project convention.
