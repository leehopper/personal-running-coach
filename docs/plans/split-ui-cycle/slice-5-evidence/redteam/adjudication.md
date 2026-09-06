# Slice 5 spec red-team adjudication (2026-09-05)

Two lenses read `docs/specs/slice-5-onboarding/spec.md` (luna first draft, session-edited)
against the clone at a35b9871: one Claude opus lens at xhigh through the Workflow tool
(`redteam/opus.json`) and one Codex sol lens at max through the fleet driver
(`redteam/sol.json`). A sol finding stands only when the opus lens or a repo check confirms it.

## Opus lens: REJECT (3 blockers, 5 majors, 5 minors). Every finding accepted; spec revised.

| # | Sev | Finding (short) | Disposition |
|---|---|---|---|
| 1 | BLOCKER | Empty-narrative "byte-identical to no narrative" test is circular: the view has no separate no-narrative state. | Accepted. Test renamed `ComposeForPlanGenerationAsync_EmptyNarrative_EmitsNoNarrativeBlock`; oracle = user message starts with `PROFILE SNAPSHOT (captured during onboarding):` and contains no `IN THE RUNNER'S OWN WORDS`; the full Replay run is the suite-level byte-identity oracle. |
| 2 | BLOCKER | Non-optional `narrative` on the hand-written `OnboardingStateDto` breaks `tsc -b` via the `seededState` fixture; PR-A lacked frontend gates. | Accepted. Both hand-written types get `narrative?: string | null`; PR-B keeps it optional; PR-A orchestrator runs gain `npm run build`, `npm run test`, eslint and prettier on the model file. |
| 3 | BLOCKER | The working-view test cannot observe its claim: the shared stub discards its input and is scoped per request. | Accepted. New test-infra decorator `RecordingPlanGenerationService` over the stub, registered for the one test like the `RejectOnceThenSucceed` precedent; added to PR-A's file list; the shared stub stays untouched. |
| 4 | MAJOR | The DEC-074 manifest regen that DEC-089 D7 and the cycle plan lock is dropped without a recorded deviation. | Accepted (the spec is factually right: the composer is C#). Deviation entry in spec section 9; one-line note under DEC-089 D7 and a Captured During Cycle row, both orchestrator-owned edits in PR-A. |
| 5 | MAJOR | Copy locked twice with conflicting casing; frontend rule is sentence-case source, CSS uppercase. | Accepted. Copy table declared rendered-appearance only; component map is the source of source strings; sentence-case sources enumerated; tests assert source strings. |
| 6 | MAJOR | Day-chip labels `MO..SU` change the accessible names and break two e2e clicks. | Accepted with a narrower fix: `DAY_OPTIONS` gains a `short` field for the visible chip text; `aria-label` keeps `Mon..Sun`, so the e2e clicks are unchanged. |
| 7 | MAJOR | C1-C3 cycle-plan capture has no owning PR. | Accepted. Orchestrator-owned edits committed in PR-A (cycle-plan Captured rows, Status blocks, ROADMAP). |
| 8 | MAJOR | Day chips locked at 40px under the 44px non-negotiable. | Accepted. Chips are `min-h-11`; recorded as a deviation from the mock. |
| 9 | MINOR | Write-path collapses the tri-state: omitting `narrative` clears it. | Accepted. Invariant stated in section 4; `SubmitAnswers_BlankNarrative_ClearsState` pins it; hydration re-sends the stored value. |
| 10 | MINOR | No mechanism for the textarea's accessible name while keeping FormLabel wiring. | Accepted. Visually hidden `FormLabel` with source text `In your own words`. |
| 11 | MINOR | New eval omits the trademark and voice prose guards the dated-event scenario runs. | Accepted. Both guards carried; default action on a voice-guard trip: re-record once, then deviation; guards are never dropped. |
| 12 | MINOR | `leavesNarrativeUntouchedDuringUnitReseed` is tautological against the spread-based reseed. | Accepted. Kept as a regression pin, not a scoring criterion; strengthened to assert `DISTANCE_FIELD_NAMES` is exactly the four names. |
| 13 | MINOR | Semantic-token checklist item cites the wrong HANDOFF line. | Accepted. Re-pointed to HANDOFF.md:42-46. |

Gate summary after revision: G1 (scope) now names the owner of every docs write; G2 (groundedness)
held for every checked path; G3 (testability) has a real oracle for each of the three previously
unscorable tests; G4 (wire honesty) adds the frontend gates PR-A needs; G5 (decisions) records the
D7 manifest deviation; G6 passed.

## Sol lens (max): REJECT (4 blockers, 8 majors, 4 minors). Standing only where opus or the repo confirms.

| # | Sev | Finding (short) | Disposition |
|---|---|---|---|
| 1 | BLOCKER | Additive field on `AnswerCaptured` violates `backend/REVIEW.md`'s never-modify rule and DEC-067; wants a versioned type and upcaster. | REJECTED as a blocker. DEC-089 D7 locks `AnswerCaptured (additive)`; DEC-091 and the Slice 2 `PlanGenerated` row are the decision-logged additive-nullable precedent (old JSON hydrates null, verified). The rule text does lag the practice: PR-A amends `backend/REVIEW.md` to record the exception (follow-up C4, orchestrator-owned). Spec section 1.A cites the basis. |
| 2 | BLOCKER | DEC-074 manifest regen silently reversed. | CONFIRMED by opus #4; already recorded as a deviation. Sol's remedy adopted in addition: the orchestrator runs `check-prompt-hashes.sh --write` as the D7 regen step and proves the no-op with `git diff --exit-code` on the manifest. |
| 3 | BLOCKER | "Record once more" would replay the deterministic cache, and dropping assertion (3) is self-waiving. | CONFIRMED by repo check (`EvalTestBase.cs:28,610`). A resample now deletes only the new scenario's fixture directory first; dropping (3) is an orchestrator ruling recorded in the round record and PR body, never the implementer's. |
| 4 | BLOCKER | The Replay skip lets a missing fixture pass green at completion. | CONFIRMED in principle. The skip is scaffolding for the record window; the fix round removes it after the fixture is committed so a missing fixture fails Replay. |
| 5 | MAJOR | Non-optional `narrative` on the hand-written state DTO breaks `tsc -b`. | Duplicate of opus #2; fixed (optional on both hand-written types, frontend gates added to PR-A). |
| 6 | MAJOR | Stub cannot record its input and is not in the file list. | Duplicate of opus #3; fixed (recording decorator, new file in PR-A). |
| 7 | MAJOR | Swagger criterion has no executable scorer. | CONFIRMED. An exact `jq -e` command over both schemas is the scorer. |
| 8 | MAJOR | Empty-narrative byte test is circular. | Duplicate of opus #1; fixed (oracle: starts at `PROFILE SNAPSHOT`, no header; full Replay is the suite-level oracle). |
| 9 | MAJOR | 400 ProblemDetails path only covered at mapper level. | CONFIRMED. Added `SubmitAnswers_OverlongNarrative_Returns400_NothingStaged`. |
| 10 | MAJOR | Narrative-only case scored by a test that sends no narrative. | CONFIRMED. Added `TryMap_NarrativeOnly_NoTopics_Fails`. |
| 11 | MAJOR | 44px hit-target not preserved by the geometry line; switch `h-6` reads as the control. | Duplicate of opus #8 plus a clarification: `h-6 w-11` is the track, the `Switch` root stays `h-11 w-11`; chips `min-h-11`; class assertion added. |
| 12 | MAJOR | No-upcaster conclusion needs a research prompt (compatibility question). | REJECTED. Not an unknown: the pattern is decision-logged and empirically verified (DEC-091, Slice 2 row) and locked for this field by DEC-089 D7; the legacy-JSON projection test mechanizes it. |
| 13 | MINOR | `||` where the house rule wants `??`. | CONFIRMED (`frontend/CLAUDE.md:92-93`). `narrative: values.narrative ?? null`. |
| 14 | MINOR | `DEFAULT_STATUS_LINE` is a file-level const, not a static member. | CONFIRMED; wording fixed. |
| 15 | MINOR | Adjudication line ranges shifted after the session extended A4. | CONFIRMED; line ranges replaced by file references. |
| 16 | MINOR | "Targeted" eval command runs the whole class. | CONFIRMED (`batch-8b` artifact lists `--filter-method`). Record and targeted Replay now filter to the single method. |

## Ruling

Spec revised for every confirmed finding from both lenses. Two sol findings (1, 12) are rejected
on the strength of locked decisions, with the underlying rule-text conflict turned into an
orchestrator-owned `backend/REVIEW.md` amendment in PR-A (C4). Verdict after revision: proceed to
build. The red-team is not re-run: no blocker or major survives that a repo check does not refute,
and the revisions are additive scorers, oracles, and citations rather than design changes.
