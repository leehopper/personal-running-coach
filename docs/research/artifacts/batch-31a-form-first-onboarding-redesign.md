# RunCoach Slice 4C — Form-First Hybrid Onboarding on an Event-Sourced Stack: Architecture, Event Origination, and Migration

> **Artifact for R-085 (`batch-31a`).** Prompt: `docs/research/prompts/batch-31a-form-first-onboarding-redesign.md`. Landed + integrated 2026-07-04 into the Slice 4C design doc (`docs/plans/mvp-0-cycle/slice-4c-onboarding-units.md` § "R-085 findings integrated") and DEC-086. A codebase-audit addendum at the end of this file records the ground-truth verification of the artifact's load-bearing claims (the artifact's own caveat flags that the type/field names and the `Description`-field audit must be checked against the repo — that check is done and recorded below).

## TL;DR

- **Ship a single-page, mobile-first stepped form organized as an accordion of six field groups (one per DEC-047 topic), each with an optional free-text “nuance” box; retire per-turn LLM slot extraction for the onboarding flow entirely.** The form is the primary and only elicitation path; the LLM does zero structured extraction and zero unit conversion during onboarding. *(Integration errata, 2026-07-04: not literally every section — nuance boxes reuse existing slot free-text fields only, and `TargetEventAnswer` has none, so its nuance box is **omitted by default** to keep `OnboardingSchema.Frozen` and the DEC-074 manifest untouched. See the codebase-audit addendum.)*
- **Originate events via a new `SubmitStructuredAnswers` Wolverine command that appends the *existing* `AnswerCaptured` events (whole-record-per-topic) through Marten’s `FetchForWriting`, inside the same Marten transaction that already drives `OnboardingView` and `RunnerOnboardingProfile`.** No new event type is needed; DEC-047/060 projections and the completion gate are preserved untouched, and the slot-merge loop dissolves because a form emits complete per-topic records atomically.
- **The two `slice-4-conversation.md` carry-forwards are obsoleted**: a form-first UI carries its own structured state (React Hook Form + Zod), so a server-driven `SuggestedInputType` and the `(topic, hasOutstandingClarification, isResume)` per-turn state-machine contract are no longer needed. Nuance is stored as text on the existing `Description` slot fields and rendered into later coaching prompts with **no LLM call during onboarding**.

## Key Findings

1. **The industry pattern in 2025-2026 is “form-first, conversation-as-escape-hatch,” not full conversational intake.** For known-field intake, forms remain the fastest, most reliable capture; conversation is reserved for guidance/orchestration. The design leans on progressive disclosure — the principle Jakob Nielsen introduced at Nielsen Norman Group in 1995, which NN/g’s Raluca Budiu summarizes as deferring “advanced or rarely used features to a secondary screen, making applications easier to learn and less error-prone.” RunCoach’s builder decision is aligned with this practice.
2. **A form naturally emits one complete record per topic, which is exactly what kills the slot-merge loop** — the correctness bug (days-on-one-turn, duration-on-the-next) cannot occur when both fields are submitted together in a validated form group.
3. **The clean event-origination pattern is a deterministic command handler using `FetchForWriting<OnboardingView>` that appends `AnswerCaptured` events and lets the existing inline projections do the EF write** — this satisfies DEC-060 (“form must ORIGINATE events, not write the EF row directly”) for free.
4. **Whole-record-per-topic wins decisively over per-field progressive save** for MVP-0: it matches the existing atomic `AnswerCaptured` shape, requires zero projection changes, keeps event volume low, and the completion gate logic is unchanged.
5. **Nuance text needs no LLM call during onboarding.** It is stored verbatim on the `Normalized*` slot’s existing `Description` field (already present on several) and injected into plan-gen/coaching prompts later. This keeps DEC-058’s frozen schema and DEC-074’s prompt-hash manifest completely undisturbed *if* nuance rides on existing fields.
6. **DEC-058/DEC-074/DEC-084 are only disturbed if you change the frozen schema or the prompt.** A form-first design that reuses `AnswerCaptured` and the existing `Description` fields, and that retires (does not rewrite) the `onboarding-v1` prompt, does not bust the manifest. Retiring the prompt path is a code/flag change, not a schema change. *(Integration errata, 2026-07-04: too broad — reusing existing fields avoids a **frozen-schema** bust, but deleting/flagging off the `onboarding-v1` prompt file is still a prompt-surface change that regenerates `.prompt-hashes.sha256` per §5 and DEC-074. It changes the manifest but not the schema; the two are separate.)*
7. **radix-ui’s `ToggleGroup` (already installed via the unified package) is the correct primitive for day-of-week selection**; `react-day-picker`/Calendar is NOT warranted (day-of-week is not a calendar), so DEC-041/DEC-063 deferrals hold — no new dependency.
8. **The unit-aware numeric field is solved with a React Hook Form `Controller` that stores km in form state and displays the chosen unit**, converting on display/parse with the deterministic `1609.344` factor (mirroring `WorkoutDraftUnitConverter`), keeping edits lossless by persisting the canonical km value and only formatting for display.

## Details

### 1. Form architecture — recommendation and rejected alternatives

**Recommended: a single-page, mobile-first vertical flow of six accordion/section groups (one per DEC-047 topic), with units collected first, an optional free-text nuance `Textarea` per group, and a conditional reveal for `TargetEvent`.** Rationale:

- **Why not a long flat form?** Six topics with sub-fields (WeeklySchedule alone has 7 day booleans + max-run-days + typical-session-minutes + description) is too dense for a single mobile scroll; it raises abandonment. Progressive disclosure demonstrably improves completion: Chameleon’s dataset of 15 million onboarding interactions found that linear onboarding produces an average completion rate of ~53%, while contextual, behavior-triggered disclosure raises it to ~75% with a ~30% increase in paid conversions (per digia.tech citing Chameleon). NN/g similarly reports that 18% of users abandon orders when checkout is too long or complicated — a direct analogue to a heavy intake form.
- **Why not a multi-page wizard (route-per-step)?** A wizard fragments React Hook Form state across routes and complicates the “review before submit” affordance. On mobile it adds navigation cost. The accordion/stepped-section pattern keeps all state in one `useForm` instance while still disclosing progressively.
- **Why not full-conversational (status quo)?** The builder already rejected it (heavy, and it caused the slot-merge loop). The 2025-2026 consensus: “conversation initiates, form finalizes” — use forms for known fields, chat for guidance. RunCoach’s known fields are all enumerable, so the form is primary.
- **Chat escape hatch:** NOT needed for MVP-0. The per-area nuance box *is* the escape hatch — it captures anything the structured fields cannot. This keeps onboarding decoupled from the 4B `CoachChat` streaming panel (see §13).

**Conditional `TargetEvent`:** Render the TargetEvent section only when `PrimaryGoal === RaceTraining`, using React Hook Form’s `useWatch` on `primaryGoal` (isolate re-renders by extracting the conditional section into its own child component subscribing via `useWatch`; the `watch` API re-renders the whole form and has a documented gotcha where multi-step/hidden fields can return stale/default values). This mirrors DEC-047’s “skipped unless `PrimaryGoal==RaceTraining`” exactly — and because the field is simply not rendered, no `TargetEvent` `AnswerCaptured` event is emitted, which the completion gate already tolerates (TargetEvent is conditionally-required).

**Required vs optional split:** Zod schema marks the six topic records’ required slots; nuance textareas are `.optional()`. Use a Zod discriminated union keyed on `PrimaryGoal` so TargetEvent’s required-ness is type-enforced (Zod v4 keeps `z.discriminatedUnion` and is now the installed version — note v4 moved string formats to top-level `z.email()` etc., changed `.record()` to require two args, renamed `error.errors`→`error.issues`, and `.optional().default()` semantics changed; these matter when authoring the schema).

### 2. Event origination from a deterministic form

**Recommended pattern — a new `SubmitStructuredAnswers` command that appends the existing `AnswerCaptured` events, one per completed topic, via the Wolverine + Marten aggregate handler workflow.**

```csharp
public record SubmitStructuredAnswers(
    Guid UserId,
    Guid IdempotencyKey,          // reuse the existing per-turn GUID model
    PrimaryGoalAnswer PrimaryGoal,
    TargetEventAnswer? TargetEvent,      // null unless RaceTraining
    CurrentFitnessAnswer CurrentFitness,
    WeeklyScheduleAnswer WeeklySchedule, // days + maxRunDays + typicalSessionMinutes + description
    InjuryHistoryAnswer InjuryHistory,
    PreferencesAnswer Preferences);

// Wolverine handler — deterministic, no LLM, no direct EF write
public static class SubmitStructuredAnswersHandler
{
    [AggregateHandler] // wires FetchForWriting<OnboardingView> + SaveChangesAsync + concurrency check
    public static IEnumerable<object> Handle(
        SubmitStructuredAnswers cmd, OnboardingView view)
    {
        // Idempotency: if this IdempotencyKey already recorded, emit nothing
        if (view.ProcessedKeys.Contains(cmd.IdempotencyKey))
            yield break;

        // Emit the SAME event shape the LLM path produces today, one per slot
        yield return new AnswerCaptured(cmd.UserId, Topic.PrimaryGoal,   cmd.PrimaryGoal,   cmd.IdempotencyKey);
        if (cmd.TargetEvent is not null)
            yield return new AnswerCaptured(cmd.UserId, Topic.TargetEvent, cmd.TargetEvent, cmd.IdempotencyKey);
        yield return new AnswerCaptured(cmd.UserId, Topic.CurrentFitness, cmd.CurrentFitness, cmd.IdempotencyKey);
        yield return new AnswerCaptured(cmd.UserId, Topic.WeeklySchedule, cmd.WeeklySchedule, cmd.IdempotencyKey);
        yield return new AnswerCaptured(cmd.UserId, Topic.InjuryHistory,  cmd.InjuryHistory,  cmd.IdempotencyKey);
        yield return new AnswerCaptured(cmd.UserId, Topic.Preferences,    cmd.Preferences,    cmd.IdempotencyKey);
        // OnboardingView projection recomputes next-topic + completion gate;
        // EfCoreSingleStreamProjection writes UserProfile inside the same Marten tx.
    }
}
```

Key design points, each grounded in the stack:

- **`FetchForWriting<OnboardingView>` is the Critter-Stack-recommended command entry point.** It replays the stream, hands you the current `OnboardingView`, and on `SaveChangesAsync` runs Marten’s optimistic-concurrency check and persists atomically. Because DEC-047’s `SingleStreamProjection<OnboardingView>` is inline and DEC-060’s `EfCoreSingleStreamProjection<RunnerOnboardingProfile>` writes `UserProfile` inside Marten’s transaction, appending `AnswerCaptured` is sufficient — the form never touches the EF row. This is the literal satisfaction of DEC-060.
- **Reuse `AnswerCaptured`; do NOT introduce `StructuredAnswersSubmitted`.** A new event type would require upcasting or dual projection-handling and gains nothing: the form’s per-topic records are shape-identical to what the LLM extraction emits today. Marten *does* support upcasting (`options.Events.Upcast<TOld,TNew>(...)`, on-the-fly at read time) if you ever must evolve `AnswerCaptured`, but for 4C you should not. Keeping one event type means replay, projections, and the completion gate are byte-for-byte unchanged.
- **Idempotency:** Reuse the existing per-turn idempotency GUID as the command’s `IdempotencyKey`. Two robust options: (a) track processed keys in the `OnboardingView` aggregate (shown above) so the check is replay-safe and lives in the event stream; or (b) enroll the endpoint in Wolverine’s transactional inbox (durable listener) which “will quietly reject the same message being received multiple times.” Option (a) is preferable here because it survives replay and needs no extra infrastructure. Marten’s optimistic concurrency via `FetchForWriting` additionally protects against two concurrent submits to the same stream (throws `ConcurrencyException`).
- **Partial-save / resume:** With whole-record-per-topic and a single submit, resume is trivial: `GET /state` returns the projected `OnboardingView`; the form hydrates defaults from it. If you later want section-by-section save, the same command can accept a subset (nullable topic records) and emit only the present ones — the completion gate (all required slots captured + no outstanding clarifications) is naturally satisfied incrementally.
- **Replay:** Because only `AnswerCaptured` events exist, a full projection rebuild is identical whether events originated from the LLM path or the form. This is the single biggest reason to reuse the event.

### 3. Whole-record-per-topic vs per-field slots — recommendation: whole-record-per-topic

|Dimension         |Whole-record-per-topic (recommended)|Per-field progressive                                   |
|------------------|------------------------------------|--------------------------------------------------------|
|Event shape change|None — reuses `AnswerCaptured`      |New per-field events + projection Apply changes         |
|Event volume      |~6 events per onboarding            |Potentially dozens                                      |
|Resume            |Hydrate form from `OnboardingView`  |Same, but finer-grained                                 |
|Completion gate   |Unchanged                           |Must re-evaluate per field                              |
|Slot-merge bug    |Dissolved (fields co-submitted)     |Dissolved, but reintroduces merge complexity server-side|
|Effort/risk       |Minimal                             |High, for no MVP-0 benefit                              |

Per-field save is only worth it if you need cross-device autosave of half-filled sections, which is out of scope for MVP-0. **Recommendation: whole-record-per-topic.** The form’s client-side React Hook Form state already provides in-session draft safety; you do not need to pay for per-field events.

### 4. Optional per-area free-text nuance → coaching context

**Recommendation: store nuance verbatim on the existing `Description` string field of each `Normalized*` slot record; do NOT introduce a separate note stream or a `ContextAssembler` for MVP-0; make NO LLM call during onboarding for nuance.**

- Several slot records (notably `WeeklyScheduleAnswer`) already carry a `Description` field. Route each per-area nuance textarea into the corresponding topic’s `Description`. This is pure stored text — the LLM is not invoked to parse it.
- **DEC-058 impact: none, if nuance rides on existing `Description` fields.** DEC-058’s frozen schema (`OnboardingSchema.Frozen`) already validates six nullable `Normalized*` slots + `Topic` discriminator; if `Description` already exists on those slots, storing nuance there does not edit the frozen schema and therefore does not bust the DEC-074 prompt-hash manifest or the eval cache. *If* any topic lacks a `Description` field and you must add one, that edits the frozen schema and does bust the manifest (regenerate `.prompt-hashes.sha256` + re-record the eval) — so audit the six slots first and prefer reusing existing fields.
- **Surfacing into plan-gen/coaching:** The nuance text is read from the projected profile and interpolated into the coaching/plan-gen prompt at generation time. To preserve prompt-cache byte-stability (Anthropic prefix caching requires the cached prefix to be byte-identical — the Claude Platform Docs state “Cache hits require 100% identical prompt segments, including all text and images up to and including the block marked with cache control”), place per-user nuance AFTER the cache breakpoint — i.e., in the volatile suffix of the prompt (user-message region), never in the cached system/tools prefix. This mirrors the operational rule documented in the DEV Community write-up “Anthropic Prompt Caching Saves 90%” (dev.to/gabrielanhaia): “anything that varies by request, by process, or by clock has to live after the last breakpoint.”
- **DEC-085 compliance:** Because nuance is stored text and never triggers LLM structured extraction or unit conversion during onboarding, DEC-085 PR3b (“the LLM never converts units”) is respected trivially — there is no onboarding LLM call at all in the recommended design.

### 5. Keep, shrink, or retire the conversational Pattern-B path — recommendation: retire for the form flow via feature-flag cutover, deprecate-in-place server-side

**Recommendation: retire the per-turn LLM slot-extraction call for onboarding. Keep the `onboarding-v1` prompt/handler code deprecated-in-place behind a feature flag for one release, then hard-remove.** There is no compelling role for a residual “anything else?” LLM phrasing turn in MVP-0 — the nuance textarea already captures free text, and DEC-084’s gruff-direct voice is a static concern, not a per-turn generation need.

Migration path and in-flight streams:

- **Feature flag `onboarding.formFirst`.** New streams start on the form path (`SubmitStructuredAnswers`). Because both paths emit only `AnswerCaptured`, an in-flight stream started under the conversational path can be *completed* by the form path with zero data migration — the form simply hydrates from `OnboardingView` and submits the remaining topics. This is the decisive advantage of reusing the event shape.
- **`OnboardingTurnHandler` / `OnboardingTurnOutput` / `onboarding-v1.yaml`:** deprecate-in-place. The `POST /api/v1/onboarding/turns` endpoint can remain during the flag window (answering any in-flight conversational clients), then be removed. `GET /state` is kept (the form uses it for resume). The unused `answers/revise` path can be repurposed or removed.
- **DEC-058/DEC-074/DEC-084 implications spelled out:**
  - *DEC-058 (frozen schema):* retiring the prompt does NOT change `OnboardingSchema.Frozen`. Only adding/changing a `Normalized*` field would. If the form design reuses existing fields, the schema is untouched → **no manifest bust from retirement.**
  - *DEC-074 (prompt-hash manifest):* removing/retiring the `onboarding-v1` prompt is a change to the prompt surface. Per DEC-074 “any prompt/schema change regenerates the manifest,” so **regenerate `.prompt-hashes.sha256`** when you delete or flag off the prompt, and record that the onboarding prompt surface is retired.
  - *DEC-084 (voice + eval):* since `onboarding-v1` is an enforced prose surface with a `VoiceProseGuard` and a recorded eval, retiring it means the eval for the *conversational* surface is retired/archived and the `VoiceProseGuard` no longer gates onboarding (it still gates 4B `CoachChat` and any other prose surface). Document this explicitly at spec time.

### 6. Verdict on the two `slice-4-conversation.md` carry-forwards — both OBSOLETED

- **Server-driven `SuggestedInputType` (retire client `pickInputTypeForTopic` mirror): OBSOLETED.** The form declares its own field affordances statically (a day-toggle group is a day-toggle group; a numeric input is numeric). There is no per-turn input-affordance guessing because there are no turns. The client `pickInputTypeForTopic` mirror should simply be deleted, not replaced with a server contract. A server-driven field-affordance contract would be over-engineering for a static six-topic form.
- **Explicit `(topic, hasOutstandingClarification, isResume)` state-machine contract: OBSOLETED.** The form carries structured state in React Hook Form; resume is a single `GET /state` hydrate; there is no “outstanding clarification” concept because the form validates all required fields client-side before submit (and the completion gate re-checks server-side). The state machine existed to coordinate multi-turn conversation; a form-first design has no turns to coordinate.

Both carry-forwards were premised on the conversational path continuing. It is not. **Explicitly mark them obsolete in the 4C spec.**

### 7. Schedule (days + duration) collection UI

**Recommendation: a radix `ToggleGroup` (type=“multiple”) for days-of-week + a numeric input for typical-session-minutes + a numeric (or select) for max-run-days, all inside one `<fieldset>` with a `<legend>`. No `react-day-picker`/Calendar.**

- radix `ToggleGroup` is already available via the installed unified `radix-ui` package, supports multiple pressed items, full keyboard navigation, and uses roving tabindex for focus movement. It is the right primitive for “pick several days.”
- **Accessibility caveat for `ToggleGroup`:** a Radix UI maintainer notes in GitHub Discussion #552 (radix-ui/website) that “the biggest difference between a RadioGroup and a ToggleGroup with type=‘single’ will be the fact that ToggleGroup is less suited for forms as it won’t bubble a value change” — so wire it through a React Hook Form `Controller` (controlled) rather than expecting native form submission, and wrap the group in `<fieldset><legend>Training days</legend>` so screen readers announce the group context (WCAG 1.3.1 / 3.3.2, technique H71). Provide an `aria-label` on each `ToggleGroup.Item`.
- **`react-day-picker`/Calendar is NOT warranted.** Day-of-week is a recurring weekly selection, not a calendar date; a calendar would be semantically wrong and add an unjustified dependency against DEC-041/DEC-063 deferrals. The one place a real date is needed — `TargetEvent`’s race date — can use the existing date turn input primitive (a native `<input type="date">` is accessible, mobile-friendly, and dependency-free), which the conversational flow already used. **Dependency verdict: no new dependency; existing radix + multi-select + native date input suffice.**

### 8. Units-up-front, unit-aware numeric input

**Recommendation: units are the first field (written to the sibling `UserSettings` store, out of scope here); numeric fields persist canonical km in form state and display the chosen unit via a `Controller` with display↔persist transforms using `1609.344`.**

```tsx
// Persist km in form state; display miles or km. Lossless round-trip:
// store the canonical km number, only format for display.
function UnitAwareDistance({ control, name, unit }: Props) {
  return (
    <Controller
      control={control}
      name={name}                      // form value is ALWAYS km (number | null)
      render={({ field, fieldState }) => {
        const displayValue =
          field.value == null ? "" :
          unit === "mi" ? kmToMiles(field.value) : field.value;
        return (
          <Field data-invalid={fieldState.invalid}>
            <FieldLabel htmlFor={name}>Weekly volume ({unit})</FieldLabel>
            <Input
              id={name}
              inputMode="decimal"
              value={displayValue}
              onChange={(e) => {
                const n = e.target.value === "" ? null : Number(e.target.value);
                field.onChange(
                  n == null ? null : unit === "mi" ? milesToKm(n) : n
                );
              }}
              aria-invalid={fieldState.invalid}
            />
            {fieldState.invalid && <FieldError errors={[fieldState.error]} />}
          </Field>
        );
      }}
    />
  );
}
const MILES_TO_KM = 1609.344 / 1000; // 1.609344 km per mile
const milesToKm = (mi: number) => mi * MILES_TO_KM;
const kmToMiles = (km: number) => km / MILES_TO_KM;
```

- **Lossless editing:** The canonical value in form state is km (matching the km-native persistence and DEC-085’s SI math). Displaying miles is a pure formatting concern. The known drift trap — round-tripping a rounded *display* value back into storage — is avoided by never writing the displayed/rounded miles back to state; only the user’s actual keystroke is converted once, on change. For display, round to a sensible precision (e.g., 1-2 decimals) but keep the stored km at full precision.
- **Zod:** validate the km number (`z.number().nonnegative()`), not the display string. React Hook Form’s number-input string-vs-number pitfall (inputs yield strings) is handled by the explicit `Number(...)` coercion in `onChange` rather than relying on `valueAsNumber`.
- **DEC-085 alignment:** all conversion is deterministic client/deterministic-layer math with the exact `1609.344` factor; the LLM is never involved. This mirrors `WorkoutDraftUnitConverter`.

### 9. RTK Query wiring

**Recommendation: one `submitStructuredAnswers` mutation + a `getOnboardingState` query for resume; tag invalidation ties them together; cookie + XSRF handled in the base query.**

```typescript
export const onboardingApi = createApi({
  reducerPath: "onboardingApi",
  baseQuery: fetchBaseQuery({
    baseUrl: "/api/v1",
    credentials: "include",                 // send __Host- auth cookie (HttpOnly)
    prepareHeaders: (headers) => {
      const xsrf = getCookie("XSRF-TOKEN");  // non-HttpOnly cookie, JS-readable
      if (xsrf) headers.set("X-XSRF-TOKEN", decodeURIComponent(xsrf));
      return headers;
    },
  }),
  tagTypes: ["OnboardingState"],
  endpoints: (build) => ({
    getOnboardingState: build.query<OnboardingView, void>({
      query: () => "/onboarding/state",
      providesTags: ["OnboardingState"],
    }),
    submitStructuredAnswers: build.mutation<OnboardingView, SubmitStructuredAnswersDto>({
      query: (body) => ({ url: "/onboarding/answers", method: "POST", body }),
      invalidatesTags: ["OnboardingState"],
    }),
  }),
});
```

- **Cookie + XSRF base-query contract:** ASP.NET Core’s antiforgery is the double-submit `XSRF-TOKEN` cookie / `X-XSRF-TOKEN` header pattern, configured server-side with `builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN")` (Microsoft Learn, “Prevent Cross-Site Request Forgery (XSRF/CSRF) attacks in ASP.NET Core,” aspnetcore-10.0). Per Microsoft Learn, a minimal-API endpoint writes the request token into a JS-readable cookie via `forgeryService.GetAndStoreTokens(context)` and `context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions { HttpOnly = false })`. The `__Host-` prefix on the auth cookie requires `Secure`, `Path=/`, and no `Domain` — per MDN’s Set-Cookie reference, that combination “yields a cookie that is as close as can be to treating the origin as a security boundary.” The auth cookie is HttpOnly and rides automatically on `credentials: "include"`; the XSRF token cookie must be non-HttpOnly so `prepareHeaders` can read it and echo it as the header. Critically, ASP.NET’s antiforgery request token is bound to the authenticated username (Duende, “Understanding Anti-Forgery in ASP.NET Core,” confirms validation “fail[s] when the user data embedded in the Anti-Forgery token doesn’t match the authenticated user”), so fetch/refresh the XSRF token *after* login (a common failure is a pre-auth token failing validation post-login).
- **Per-user cache-reset-on-auth gap:** RTK Query does not automatically clear cached data on login/logout — dispatch `api.util.resetApiState()` on both login and logout to prevent User A’s `OnboardingState` leaking into User B’s session. The official Redux Toolkit docs (API Slices: Utilities) describe it as “A Redux action creator that can be dispatched to manually reset the api state completely. This will immediately remove all existing cache entries, and all queries will be considered ‘uninitialized’. Note that hooks also track state in local component state and might not fully be reset by resetApiState.” Because of that caveat, also ensure onboarding components unmount on auth change; a store-level root reducer that wipes state on a `logout` action is the robust pattern.
- **Progressive/resumable save:** for MVP-0 whole-form submit, `getOnboardingState` on mount hydrates React Hook Form `defaultValues`; the single mutation submits; tag invalidation refetches state to drive the completion/redirect. If per-section save is later added, each section mutation invalidates `OnboardingState` and the query re-hydrates.

### 10. Accessibility (WCAG 2.2 / ARIA APG, 2026)

- **Grouping:** each topic section is a `<fieldset>` with a `<legend>` (WCAG 1.3.1, 3.3.2; technique H71) so screen readers announce the group name before each control. This is essential for the day-toggle group and any radio/checkbox clusters.
- **Labels & required indication:** every input has a programmatically-associated `<label>` (shadcn `FieldLabel`/`FormLabel` uses `React.useId()` and wires `htmlFor`); indicate required fields with both a visible marker and `aria-required`/`required` (WCAG 3.3.2; ARIA2).
- **Errors:** on failed submit, render an error summary and move focus to it (or to the first invalid field); associate each inline error with its input via `aria-describedby` and set `aria-invalid="true"`; announce with `role="alert"`/`aria-live="polite"`. Do not rely on color alone (WCAG 1.4.1, 3.3.1, 3.3.3). shadcn’s `FieldError`/`FormMessage` pattern applies the correct aria attributes automatically.
- **Focus management on section advance / conditional reveal:** when `TargetEvent` reveals (PrimaryGoal→RaceTraining), move focus into the newly revealed section and ensure it is announced. Keep focus order in visual order (WCAG 2.4.3). Preserve visible focus indicators — use `:focus-visible`, never remove outlines (WCAG 2.4.7, and 2.2’s 2.4.11 Focus Appearance).
- **Day-toggle group:** radix `ToggleGroup` provides roving-tabindex keyboard nav; add `aria-label` per item and the fieldset/legend wrapper for context.
- **Target size (WCAG 2.2, SC 2.5.8, Level AA, added October 2023):** the criterion requires that “the size of the target for pointer inputs is at least 24 by 24 CSS pixels, except when…” (W3C, Understanding SC 2.5.8) — with spacing between the day toggles on mobile.
- **Mobile (iOS VoiceOver / Android TalkBack):** use correct input types (`inputmode="decimal"` for numeric, `type="date"` for the race date) to summon the right virtual keyboard; handle iOS safe-area insets (`env(safe-area-inset-*)`) and viewport so the sticky submit/section controls aren’t obscured; ensure the virtual keyboard doesn’t cover the focused field.
- **Standards baseline:** build to WCAG 2.1 AA (still the procurement/DOJ Title II anchor as of 2026) and pick up 2.2 criteria (2.4.11, 2.5.8, 2.5.7 dragging) while in-code.

### 11. Eval scope

**Recommendation: retire the “multi-slot free-text merge” eval scenarios; they test a failure mode that a form-first design structurally eliminates.** What survives:

- **No merge evals needed:** the slot-merge loop cannot occur when fields are co-submitted from a validated form group, so the “multi-slot free-text merge” scenarios have no target behavior to test. Archive them with a note pointing at the 4C redesign.
- **Nuance path evals:** if (and only if) nuance text is later fed to an LLM for coaching/plan-gen, add evals at *that* surface (not onboarding) verifying the nuance is faithfully surfaced and that DEC-085 unit-non-conversion holds. During onboarding there is no LLM call, so there is nothing to eval on the onboarding turn.
- **Deterministic form evals:** the form path is deterministic, so its “eval” is really unit/integration tests (§12): event origination, projection outputs, completion gate. Under DEC-074, since the onboarding prompt is retired, the manifest records the retirement; DEC-084’s `VoiceProseGuard` no longer applies to onboarding (only to remaining prose surfaces), so no onboarding voice-eval is re-recorded — it is archived.

### 12. Testing sketch

- **Vitest + React Testing Library (component/integration):**
  - Section advance and the conditional `TargetEvent` reveal (set PrimaryGoal=RaceTraining, assert the section appears and receives focus; set another goal, assert it is absent and no TargetEvent value is submitted).
  - Unit-aware numeric: enter “10” in miles mode, assert form state holds ~16.09 km, switch display to km, assert “16.09” shows, edit and assert no drift.
  - Day-toggle group: toggle several days, assert the submitted `WeeklyScheduleAnswer` day booleans; keyboard-navigate with arrows and Space.
  - Error states: submit empty required fields, assert error summary appears, focus moves, `aria-invalid`/`aria-describedby` are set.
- **Playwright E2E (full flow → plan-generation handoff):** fill the form, submit, assert `POST /onboarding/answers` fires with the correct DTO, `GET /onboarding/state` reflects completion, and the app hands off to plan generation. **Pitfall vs the existing chat E2E:** the current E2E stubs the LLM and asserts a control-swap-per-turn contract; a form flow has no per-turn control swap, so that assertion shape must be replaced with “fill → single submit → state complete.” Do not carry the transcript/turn-stub harness into the form E2E. If any in-flight conversational streams are still supported during the flag window, keep a separate legacy E2E for that path until removal.

### 13. Differentiation from 4B `CoachChat`

- **Onboarding is a full-page, guided, terminating form; 4B `CoachChat` is an always-on streaming conversation.** They should share almost nothing at the interaction layer. Shared primitives are limited to low-level shadcn/radix building blocks (`Button`, `Textarea`, `Field`) and the RTK Query base-query (cookie+XSRF) contract.
- **Do NOT reuse the streaming/transcript machinery.** The form has no streaming, no message list, no per-turn control swap. Reusing `CoachChat` primitives would re-introduce a chat dependency onboarding does not need and would risk resurrecting the turn-based mental model that caused the slot-merge loop. Keep the onboarding surface free of the streaming panel entirely; the nuance textarea is the only free-text affordance and it is a plain controlled `Textarea`, not a chat.

## Recommendations (staged, with thresholds)

**Stage 1 — Spec & schema audit (before any code).**

1. Audit the six `Normalized*` slots for an existing `Description` field. **Threshold:** if all target topics already have `Description`, nuance adds zero frozen-schema change and no manifest bust — proceed on the “reuse existing fields” path. If any topic lacks it, decide consciously to add it (accept a DEC-074 manifest regeneration + eval re-record) or route that topic’s nuance into a shared existing field.
2. Mark both `slice-4-conversation.md` carry-forwards obsolete in the 4C spec.

**Stage 2 — Backend event origination.**
3. Add `SubmitStructuredAnswers` command + `[AggregateHandler]` using `FetchForWriting<OnboardingView>`, emitting existing `AnswerCaptured` events; reuse the per-turn GUID as `IdempotencyKey` tracked in `OnboardingView`.
4. Add `POST /api/v1/onboarding/answers`; keep `GET /state`; leave `POST /turns` behind the `onboarding.formFirst` flag.
5. Regenerate `.prompt-hashes.sha256` only if the frozen schema actually changed; record the onboarding-prompt retirement.

**Stage 3 — Frontend form.**
6. Build the six-section accordion form (React Hook Form single `useForm`, Zod v4 discriminated union on PrimaryGoal), units-first, `useWatch`-driven TargetEvent reveal, radix `ToggleGroup` schedule, unit-aware numeric `Controller`, per-area nuance `Textarea`.
7. Wire the RTK Query mutation/query with cookie+XSRF base query; dispatch `resetApiState()` on auth change.

**Stage 4 — Cutover.**
8. Ship behind `onboarding.formFirst=true` for new streams; verify in-flight conversational streams complete via the form (they can, because both emit `AnswerCaptured`). **Threshold to hard-remove the conversational path:** zero in-flight conversational streams for one full onboarding-completion window (and one release), then delete `OnboardingTurnHandler`/`OnboardingTurnOutput`/`onboarding-v1.yaml`/`POST /turns`, archive the merge evals and the onboarding voice eval, and regenerate the manifest.

**Rejected alternatives (summary):** full-conversational (rejected by builder; caused the bug); form-only with no nuance (loses coaching context the builder wants); new `StructuredAnswersSubmitted` event (needless upcasting/projection churn); per-field progressive save (event-volume + projection cost with no MVP-0 benefit); `react-day-picker`/Calendar (semantically wrong for day-of-week; violates deferrals); a residual “anything else?” LLM turn (nuance textarea already covers it; adds prompt-cache and eval surface for no gain).

## Caveats

- **The internal DEC identifiers, event/type names (`AnswerCaptured`, `OnboardingView`, `RunnerOnboardingProfile`, `WorkoutDraftUnitConverter`, `OnboardingSchema.Frozen`), and endpoint paths are taken from the task brief, not independently verifiable on the public web.** The recommendations are engineered to match those contracts as stated; validate field-level details (especially which slots already carry `Description`) against the actual codebase before implementation.
- **Whether storing nuance busts the frozen schema hinges entirely on the `Description`-field audit (Stage 1.1).** I have flagged both branches; the “no manifest bust” claim holds only if existing fields are reused.
- **Anthropic structured-outputs / constrained-decoding note:** DEC-058 states the decoder rejects `minimum`/`maximum`/`oneOf`/`pattern`/`format`; independent 2026 sources (Claude API docs; Towards Data Science) confirm Anthropic’s constrained decoding compiles a schema grammar and that a common pattern is to send a simplified schema to the model while enforcing full constraints (ranges, formats) in your own validation layer — consistent with DEC-058’s runtime-validation approach. This is a corroborating note, not a change to the locked decision. (One arXiv benchmark, “ExtractBench,” reports that provider structured-output modes can *reduce* extraction accuracy vs prompt-mode on complex documents — a reason the form-first design, which avoids LLM extraction entirely, is the safer bet.)
- **`.NET 8 → 10` antiforgery for SPAs had at least one reported regression** (GitHub dotnet/aspnetcore #59319: the antiforgery response cookie not being set for React SPAs where it worked in 6.0). Verify the XSRF cookie is actually issued on your target .NET 10 build before relying on the base-query pattern. Also note PortSwigger’s “Cookie Chaos” research documents `__Host-`/`__Secure-` prefix-enforcement bypasses via cookie-parsing quirks in some frameworks including ASP.NET — treat the prefix as defense-in-depth, not an absolute guarantee.
- **`resetApiState()` does not fully reset component-local hook state**; mounted onboarding components may refetch on auth change. Ensure onboarding routes unmount on logout, or wipe store state at the root reducer on the logout action.
- **The Zod v4 migration** (installed) changes several APIs (`error.issues`, top-level string formats, `.record()` two-arg, `.optional().default()` semantics, deprecated `.merge()`/`.superRefine()`); author the onboarding schema against v4 idioms to avoid silent behavior changes.
- **Sources are weighted toward official docs** (shadcn/ui, React Hook Form, Zod, radix-ui, Redux Toolkit, Marten/Wolverine, W3C WCAG, Microsoft Learn, Anthropic/Claude platform docs). UX-pattern claims draw on 2025-2026 engineering/design write-ups and vendor datasets (e.g., Chameleon via digia.tech), which are directional rather than authoritative; the architecture/code recommendations rest on the primary docs.

---

## Integration addendum — RunCoach codebase audit (2026-07-04)

The artifact's own caveats flag that its type/field names and (above all) the `Description`-field audit must be checked against the repo before implementation. That check was run at integration time. Result: **every load-bearing claim verifies against `backend/src` and `frontend/src`, with one refinement that changes the "zero manifest bust" headline.**

**Verified true (ground truth):**

- `AnswerCaptured` is a closed-shape event (`Topic`, `JsonDocument NormalizedPayload`, `Confidence`, `CapturedAt`) — `Modules/Coaching/Onboarding/Events/AnswerCaptured.cs`. The payload is a serialized `JsonDocument`, so the artifact's illustrative handler passing a typed answer record is a sketch; the real command serializes each answer record into the payload, exactly as the LLM path does.
- `OnboardingView` (inline `SingleStreamProjection`) + `UserProfileFromOnboardingProjection` (`EfCoreSingleStreamProjection<RunnerOnboardingProfile>`, table `UserProfile`) exist and match DEC-047/060.
- `FetchForWriting` / `[AggregateHandler]` is **already the codebase's command idiom**, including inside the onboarding module (`SubmitUserTurn`, `OnboardingTurnHandler`) and in `RegeneratePlanHandler` / `EvaluateAdaptationHandler`. The recommended `SubmitStructuredAnswers` pattern is idiomatic here, not novel.
- `OnboardingSchema.Frozen` exists (`Modules/Coaching/Onboarding/OnboardingSchema.cs`).
- `WorkoutDraftUnitConverter.MetersPerMile = 1609.344d` (exact) is confirmed; the frontend already has the mirror `METERS_PER_MILE = 1609.344` in the shipped 4C-units `modules/common/utils/unit-format.helpers.ts` (with a test asserting `.toString() === '1609.344'`). The unit-aware numeric field consumes this existing module — no parallel converter.
- Routes are exactly `POST /api/v1/onboarding/turns`, `GET /api/v1/onboarding/state`, `POST /api/v1/onboarding/answers/revise` (`OnboardingController.cs`).
- `pickInputTypeForTopic` lives in `frontend/src/app/modules/onboarding/pages/onboarding.page.tsx` — the client mirror the artifact recommends deleting.

**The one refinement — the `Description`-field audit (artifact Stage 1.1) is NOT uniform:**

| Topic | Free-text field in the record | Manifest-safe nuance box? |
|---|---|---|
| PrimaryGoal | `Description` | ✅ reuse |
| CurrentFitness | `Description` | ✅ reuse |
| WeeklySchedule | `Description` | ✅ reuse |
| Preferences | `Description` | ✅ reuse |
| InjuryHistory | `ActiveInjuryDescription` + `PastInjurySummary` (no literal `Description`) | ✅ reuse `PastInjurySummary` (free-text already the substantive answer) |
| **TargetEvent** | **none** (`EventName`, `DistanceKm`, `EventDateIso`, `TargetFinishTimeIso` only) | ❌ **a nuance box here adds a field → edits `OnboardingSchema.Frozen` → busts the DEC-074 manifest** |

So the artifact's "reuse existing `Description` fields → no manifest bust" holds for **5 of 6 topics**. **TargetEvent is the sole exception.** The 4C-onboarding spec must consciously choose one of: (a) omit a nuance box on the TargetEvent section (its four structured fields are likely complete enough — recommended default); (b) route event nuance into an adjacent existing field; or (c) add a `Description` field to `TargetEventAnswer` and accept the DEC-058 frozen-schema edit + DEC-074 manifest regen + onboarding-eval re-record. This is the artifact's Stage-1 threshold, resolved to a concrete per-topic answer.

**How this integrated:** design doc `slice-4c-onboarding-units.md` § "R-085 findings integrated" (the how, at the what/which-pattern level), DEC-086 § "Research integration", research-queue R-085 → Integrated, cycle-plan Status + a Captured-During-Cycle row. The artifact's code sketches (§2, §8, §9) are the reference for the 4C-onboarding spec written fresh at build time; they are not copied into the design doc (which stays at the requirements level).
