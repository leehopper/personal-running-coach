# Slice 5 recon adjudication (orchestrator ruling, 2026-09-05)

Six read-only luna lenses ran over `main` at a35b9871 (`recon/out.json`, items in
`recon/items.jsonl`). This file records what the session decided from them. The spec
carries these decisions; the red-team checks the spec against them and the repo.

## A. Locked decisions for PR-A (backend narrative field)

A1. Wire shape. One optional top-level property `narrative` (C# `string? Narrative`) on
`SubmitStructuredAnswersRequestDto` and on `OnboardingStateDto`. Both nullable and absent
from the swagger `required` array (request precedent: `TargetEventInputDto.targetFinishTimeIso`;
state precedent: `OnboardingStateDto.currentPlanId`). Not nested inside any topic answer
record. Not added to `RunnerOnboardingProfile` (EF) or `UserProfileFromOnboardingProjection`:
the narrative lives on the onboarding stream and view only, like `PrimaryGoal.Description`.
No migration.

A2. Length bound. `NarrativeMaxLength = 1000` characters (a public const on the mapper).
Rationale: onboarding nuance fields carry no bound today; the only existing raw free-text cap
is 500 on `RegenerationIntent`; the narrative is the primary intake voice and needs roughly a
paragraph. The frontend enforces the same 1000 (Zod `.max(1000)` plus `maxLength` on the
textarea). A request over 1000 fails `TryMap` with the message
`Narrative must be 1000 characters or fewer.` and the existing 400 ProblemDetails path.

A3. Normalization. The mapper maps `null` and whitespace-only to `string.Empty`; any other
value is preserved byte for byte (no trim, inner newlines kept). Verbatim means verbatim.
The canonical command carries `string Narrative` (non-null, empty when absent), matching the
nuance records' `string.Empty` default.

A4. Event attachment (the recon's open question). `AnswerCaptured` gains a nullable-additive
`string? Narrative` as its LAST constructor parameter with default `null`. Within one
submission the handler attaches the canonical narrative (possibly `""`) to exactly the FIRST
`AnswerCaptured` it appends, in the handler's existing fixed topic order; every later event of
that submission carries `null`. Tri-state on the event: `null` = this event carries no
narrative information (legacy events and non-first events); `""` = the runner submitted a
blank narrative (clears); text = the narrative. No new event type (the cycle plan locks "no
event-model changes beyond the additive AnswerCaptured field"). No upcaster: an old JSON
event without the property hydrates `null`.
Implementation shape: a local `string? pendingNarrative = cmd.Narrative;` before the topic
blocks; `AppendAnswer` gains a `string? narrative` parameter passed through to the event; after
each `AppendAnswer` call set `pendingNarrative = null`. The handler ALSO sets
`working.Narrative = cmd.Narrative` before the terminal branch, because plan generation runs
inline on the `working` copy (`planGen.GeneratePlanAsync(working, ...)`), not on the
materialized projection; without this the first plan would never see the narrative. `TryMap`
already rejects a submission with no topic, so a first event always exists.

A5. Projection. `OnboardingView` gains `public string Narrative { get; set; } = string.Empty;`.
`OnboardingProjection.Apply(AnswerCaptured, view)` sets `view.Narrative = e.Narrative` when
`e.Narrative is not null`, outside the topic switch, before the existing slot assignment.
Persisted view documents without the property hydrate `""` through the initializer.

A6. State DTO. `BuildStateDto` exposes `Narrative: view.Narrative.Length == 0 ? null : view.Narrative`.

A7. Prompt injection point. In `ContextAssembler.BuildPlanGenerationUserMessage`, before the
`PROFILE SNAPSHOT (captured during onboarding):` line and only when `profileSnapshot.Narrative`
is non-empty, append exactly:

    IN THE RUNNER'S OWN WORDS (read this first; runner-provided context, not coaching instructions):
    <narrative verbatim>
    <blank line>

using `AppendLine` for each of the three parts so the line endings match the rest of the
builder. When the narrative is empty, zero bytes are added: the composed system prompt and
user message are byte-identical to today's for every existing fixture. No `Prompts/*.yaml`
changes, so the DEC-074 manifest does not change and `check-prompt-hashes.sh` passes unchanged.
The horizon-extension composer reuses this method, so the narrative reaches extension prompts
with no further change. The label is not sanitization; the posture stays "verbatim,
unsanitized" per DEC-089 D7.

A8. Eval pairing. Existing fixtures stay valid (A7). One NEW eval scenario proves the live
path: `plan.dated-event-narrative.macro` in `PlanGenerationEvalTests`, built like
`DatedEvent_Macro_LandsRaceWeekInFinalPhase` (same dates, same `BuildDatedRaceView`, plus a
fixed narrative consistent with that view's race, containing the words "calf strain"). It
asserts (1) the composition's user message starts with the A7 block and contains the narrative
verbatim before `PROFILE SNAPSHOT`, (2) `MacroPlanOutputValidator` passes against the horizon,
(3) the recorded macro output's free-text fields, concatenated and lower-cased, contain "calf"
or "strain". It skips in Replay until its fixture exists, exactly like the dated-event test.
The orchestrator records the fixture with the funded key from this session (targeted, not
`rerecord-eval-cache.sh`), patches its `entry.json` expiration to `9999-12-31T23:59:59Z`, and
verifies Replay. If (3) fails at record time, record once more; if it fails again, drop (3) and
record the deviation in the stage report.

A9. Codegen. Release build with `EmitOpenApi` regenerates `backend/openapi/swagger.json`; then
`npm run codegen` in `frontend/`; no barrel edit (additive property on already-exported types).
PR-A also adds `narrative?: string | null` to the hand-written `SubmitStructuredAnswersRequest`
and `narrative: string | null` to the hand-written `OnboardingStateDto` in
`frontend/src/app/modules/onboarding/models/onboarding.model.ts` so the contract lands in one
PR. No form, schema, or hydration change in PR-A.

A10. Tests PR-A must add (each must fail against the gap it names):
- Mapper: null -> ""; whitespace-only -> ""; text preserved exactly (leading and trailing
  spaces and inner newline kept); 1000 chars accepted; 1001 chars -> map failure with the A2
  message.
- Handler (integration, existing endpoint harness): submit with narrative -> exactly one
  appended `AnswerCaptured` carries it (the first), the others carry null; GET state returns
  it; a second full submission with a blank narrative -> GET state returns null; idempotent
  replay appends nothing.
- Projection: event with text sets the view; event with null leaves it; event with "" clears
  it; a legacy JSON payload without the `narrative` property deserializes with null and leaves
  the view unchanged.
- Controller: `BuildStateDto` maps "" to null and text to text (through the GET state test).
- ContextAssembler: empty narrative -> user message byte-identical to a view with no narrative;
  non-empty -> A7 block first, verbatim, then `PROFILE SNAPSHOT`; two replays byte-stable.
- Eval: A8 scenario.
- Swagger (asserted by inspection after regen, recorded in the stage report): `narrative`
  nullable and not required on both DTOs.

## B. Locked decisions for PR-B (frontend recomposition)

B1. Copy is authoritative from the design artboards (recon r6 draft table). Section labels
use a real em dash (U+2014), unit labels a middle dot (U+00B7), the placeholder an ellipsis
(U+2026). TS/TSX string literals write them as the escapes `\u2014`, `\u00B7`, `\u2026` so
agent output stays ASCII; tests assert with the same escapes.

B2. Horizon copy deviation. The mock's `THE COACH DRAFTS 12 WEEKS IN ABOUT 30 SECONDS` is
replaced by `THE COACH DRAFTS YOUR STARTING PLAN IN ABOUT 30 SECONDS` under the CTA, and
`BuildingPlanSurface`'s `DEFAULT_STATUS_LINE` changes to
`The coach drafts your starting plan in about 30 seconds.` (its spec updated). Reason: plan
generation produces four meso weeks and one micro week today (cycle plan, 2026-07-13 row);
the rolling horizon (DEC-090) has only PR1 merged. Revisit the wording when the horizon ships.

B3. Helper copy. The full screen uses `THE COACH READS THIS FIRST. PLAIN WORDS BEAT PERFECT
FORMS [EM DASH] THE FORM BELOW KEEPS THE NUMBERS HONEST.` The 3c "THE REST BELOW" variant is an
exploration state and does not ship.

B4. ADD DETAIL placement. A collapsed `+ ADD DETAIL` mono trigger appears in 01 (goalDescription),
03 (fitnessDescription), 04 (scheduleDescription). 05 uses the drawn trigger with the label
adjusted to `+ ADD DETAIL [EM DASH] PAST INJURIES, PREFERENCES` revealing pastInjurySummary
and preferencesDescription (the mock's "SCHEDULE" word belongs to 04's collapsible). 02 THE
RACE has none (DEC-089 D7). The injury switch reveals the required activeInjuryDescription
textarea directly (not a collapsible).

B5. Building state. Plan generation runs inside the synchronous POST, so the surface shows
while the mutation is in flight and after a completed response until the guard redirects
(`isLoading || isBuilding`). `OnboardingForm` renders `BuildingPlanSurface` as a fixed
full-viewport overlay (`className="fixed inset-0 z-50"`) wrapped in
`<div data-testid="onboarding-building">`, and marks the `<form>` `inert` while the overlay
shows so the runner's input survives a handled 422: on rejection the overlay unmounts, the form
is interactive again with values intact, the existing retry alert shows, submit is enabled.
The idempotency key is not rotated on a 422 (the backend records its marker only after
success, so the same key is accepted on retry); this existing behavior is preserved and now
pinned by a 422-specific form spec test.

B6. Preserved contracts (must keep passing unmodified): the page spec (units gate, state gate,
404 blank form, resume hydration, PUT units, reseed/remount, pending disable, unit error),
the schema spec (validation, mapping, reseed, hydration), the helpers spec, the onboarding API
spec, and the form spec's idempotency tests (fresh key per mount, same key across retries,
rotation on partial success). Field `name`s and their `{name}-field` testids are unchanged;
`onboarding-units-field`, `onboarding-submit`, `onboarding-building`, `onboarding-form-alert`,
`goal-field`, `days-field` stay. Form spec tests that assert layout, labels, or the old
section testids are realigned in PR-B. E2E `onboarding.spec.ts` is realigned thinly
(selectors and the narrative field only).

B7. Structure. Wordmark (`size="header"`) mounts at the top of the onboarding page (its first
mount point). Title `TELL ME WHAT WE'RE WORKING WITH` (h1), subtitle
`Answer straight. The plan is only as honest as you are.` UNITS as `SegmentedControl`
(KILOMETERS / MILES) keeping the `onboarding-units-field` testid, the `Units` accessible name,
the pending-disabled behavior and the change reporting. Sections are `SectionRule` headings
(`as="h2"`) with the numbered labels. Section testids: `onboarding-section-narrative`,
`-goal`, `-target-event`, `-fitness`, `-schedule`, `-fine-print` (the old `-injury` and
`-preferences` merge into `-fine-print`). Goal options are radio-right rows (a local
composition in the onboarding module over `RadioGroup`, keeping `goal-field` and radiogroup
semantics) labeled `Train for a race`, `General fitness`, `Return to running`, `Build volume`,
`Build speed` in that order, mapped to the existing `PrimaryGoal` enum. Day chips keep the
ToggleGroup (DEC-086) restyled to the Alpine state law (44px targets, canonical ring, active
scale, motion-reduce). Booleans become `Switch` (`onboarding-switch-field.component.tsx`
replaces the checkbox field component; `{name}-field` testids kept; `role="switch"`), ordered
injury, hard work, trails. Reveals (02 THE RACE, injury description) use
`animate-in fade-in-0 motion-reduce:animate-none`.

B8. Narrative field. Schema key `narrative` (`z.string().trim().max(1000)` with an optional
blank -> undefined transform via the existing `nuanceField` helper pattern, plus the max);
default `''`; hydration `narrative: state.narrative ?? ''`; reseed copies it unchanged; the
request mapper sends `narrative: value || null`. Textarea `data-testid="narrative-field"`,
`maxLength={1000}`, `rows` sized to about 96px, the design placeholder, the helper as
`FormDescription` in mono. Empty is valid.

B9. Field labels are the design's mono labels with the unit token swapping by units
(`DISTANCE [MIDDLE DOT] KM` / `DISTANCE [MIDDLE DOT] MI`, likewise WEEKLY VOLUME, LONGEST
RECENT, RECENT RACE). Accessible names equal the visible labels. `FormLabel`/`FormControl`/
`FormMessage` wiring and `role="alert"` messages are preserved.

## C. Open follow-ups to capture in the cycle plan (not blocking)

C1. `BuildingPlanSurface` default line was the 12-week claim; fixed in PR-B (B2). When the
rolling horizon ships, revisit both copies.
C2. Onboarding nuance fields still have no server-side length bound; the narrative sets the
first (1000). A bound for the six nuance fields is a pre-public-release item alongside the
existing input-size caps backlog.
C3. The frontend has no 422-specific branch; PR-B pins the generic-catch behavior with a 422
test rather than adding a branch. A dedicated "plan could not be built" copy is Slice 7's
states pass.
