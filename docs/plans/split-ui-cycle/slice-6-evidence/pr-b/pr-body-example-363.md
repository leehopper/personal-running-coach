**Slice 5 (Onboarding) PR-B recomposes the intake onto the Alpine system and lights up the narrative field PR-A landed.** PR-A merged as #361 (06bf8736). This replaces #362, which GitHub closed when PR-A's branch was deleted on merge and would not reopen; the branch is rebased onto `main`, and the five code-gauntlet findings from #362 are fixed here (round 3), each thread there answered and resolved.

Spec: `docs/specs/slice-5-onboarding/spec.md` § 3 PR-B (gitignored working-tree artifact). Committed evidence: `docs/plans/split-ui-cycle/slice-5-evidence/pr-b/` (build brief and report, host runs, both lens reports, fix list, verify report, adjudications).

## What's here

- **Screen.** Wordmark (its first mount point), `TELL ME WHAT WE'RE WORKING WITH` + "Answer straight. The plan is only as honest as you are.", then UNITS as a `SegmentedControl` before any distance field (DEC-086 preserved, reseed/remount unchanged), then numbered `SectionRule` sections `00 — IN YOUR OWN WORDS` through `05 — THE FINE PRINT`.
- **00 narrative.** Always-visible textarea with the design placeholder and helper, a visually hidden label for its accessible name, `maxLength` 1000. Zod `trim().max(1000)` with blank → undefined; request mapper sends `narrative ?? null`; hydration `state.narrative ?? ''` (legacy states without the property hydrate empty); unit reseed leaves it untouched.
- **01–05.** Radio-right goal rows (the radiogroup now carries its visible label as its accessible name), `02 — THE RACE` revealed by the race goal with `motion-reduce` pairing, 2×2 fitness grid with unit-aware mono labels and the reassurance line, `04` numeric pair + 7-day chips (visible `Mo..Su` via a new `short` field; `aria-label` stays `Mon..Sun`, so existing e2e clicks work; `min-h-11`), `05` three `Switch` rows (injury reveals its required description directly). `+ Add detail` collapsibles only where a nuance field exists (01, 03, 04, 05); none in 02 (DEC-089 D7). The trigger is a mono clay label with the canonical ring and 44px target; content motion comes from the shared `CollapsibleContent` primitive.
- **Building state.** Plan generation runs inside the synchronous POST, so `BuildingPlanSurface` shows as a fixed full-viewport overlay while the mutation is in flight and after a completed response until the guard redirects; the `<form>` is `inert` meanwhile and stays mounted, so a handled 422 returns it with every value intact, the retry alert, and the same idempotency key (pinned by new tests; rotation on incomplete success unchanged).
- **Copy.** `THE COACH DRAFTS YOUR STARTING PLAN IN ABOUT 30 SECONDS` under `BUILD MY PLAN`, and the same sentence as `BuildingPlanSurface`'s default line, replacing the mock's "12 weeks" (four meso weeks and one micro week exist today; DEC-090's rolling horizon has only PR1 merged). Source strings are sentence case; CSS uppercases; em dash, middle dot and ellipsis are written as escapes.
- **Removed.** `onboarding-checkbox-field`, `onboarding-injury-section`, `onboarding-preferences-section` (replaced by the switch field and the fine-print section).

## Decisions and deviations

- B3: the full-screen helper says `THE FORM BELOW`; the exploration sheet's `THE REST BELOW` does not ship.
- B4: the 05 trigger reads `+ ADD DETAIL — PAST INJURIES, PREFERENCES` (the mock's "SCHEDULE" word belongs to 04's collapsible).
- Chips are `min-h-11`, not the mock's 40px, so the 44px hit-target non-negotiable holds without an expansion pseudo-element.
- The `BuildingPlanSurface` default-line change is shared with Slice 6's regenerate flow on purpose.

## Review trail

- Round 1: two review lenses (mutation ledger, spec conformance). The ledger ran 29 probes; 24 went red and 4 stayed green, which became test fixes (units-first order, terminal-success key non-rotation, full value survival on 422, fresh key per mount). Conformance found the trigger styling gap and a thin dual-theme test.
- One 12-item fix round (accessible name on the radiogroup, trigger styling, seven regression tests, the restored `OnboardingForm` JSDoc).
- Verify round: every fix red under mutation except one, where the fixer had duplicated motion classes the shared primitive already supplies; the duplicate was removed in round 2 and the assertion kept as a regression pin.
- Round 3: the maintainer's code-gauntlet findings on #362 (restored DEC / R-NNN rationale in four JSDocs; `OnboardingNuanceSection` generalized to take children so the fine-print section composes the one shared `+ Add detail` trigger, 44px and `t-data-label`, with its stable id wired through the formerly unused prop). Verify lens: ACCEPT, both code fixes red under mutation.

## Green

- `npm run build` clean; **1037/1037** vitest; `check-contrast` 50/50 pairs; `codegen:check` exit 0; eslint and prettier clean on every touched file.
- Playwright on the host stack (Compose Postgres + Redis, host API from this branch): **14 passed**, the onboarding journey included; the 2 failures are pre-existing on `main` (see notes).
- No backend file changed; the PR-A head this stacks on passed 2055/2055 in Replay.

## Notes for review

- `plan-render.spec.ts:242` and `workout-logging.spec.ts:192` fail identically from `main`'s frontend (a header innerHTML reload comparison, and a strict-mode `getByText('8.0 km')` that matches both the ledger row and the week aggregate). Captured as a cycle-plan row for Slice 7's e2e consolidation. The Dependabot Playwright bump also needs `npx playwright install chromium` on the host.
- `npm run lint` reports only pre-existing problems in untouched files (password fixtures, react-refresh); CI does not run it.
- The close-out docs ride here: cycle-plan Status collapsed to the Slice 5 ledger row with a completion section, `ROADMAP.md` Status and Cycle History pointing at Slice 6 (Settings & Auth) as next.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01LpQ29fuAnCUqaZh9e4CkaV


<!-- This is an auto-generated comment: release notes by coderabbit.ai -->

## Summary by CodeRabbit

* **New Features**
  * Added a narrative field to onboarding, with guidance and a 1,000-character limit.
  * Added fine-print questions for injuries, training intensity, and trail preferences.
  * Added clearer numbered onboarding sections, compact day controls, units selection, and refreshed form styling.
  * Added an accessible plan-building overlay that keeps the form visible while processing.

* **Bug Fixes**
  * Improved onboarding state hydration, including saved narrative responses and legacy data.
  * Preserved submitted form state during plan building and improved retry behavior.
  * Updated onboarding copy and status messaging for greater clarity.

<!-- end of auto-generated comment: release notes by coderabbit.ai -->
