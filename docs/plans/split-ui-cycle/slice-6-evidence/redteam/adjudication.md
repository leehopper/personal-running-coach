# Slice 6 spec red-team adjudication (2026-09-06)

Two lenses read `docs/specs/slice-6-settings-auth/spec.md` (luna first draft, session-edited)
against the Settings clone at ee90fcfd: one Claude opus lens at xhigh through the Workflow tool
(`redteam/opus.json`, 105 tool uses, 19 minutes) and one Codex sol lens at max through the fleet
driver (`redteam/sol.json`; the first two sol attempts timed out at 1500 s while still reading, the
third ran with a 3600 s budget). A sol finding stands only when the opus lens or a repo check
confirms it. Because the builds had already started when sol returned, its accepted code-facing
findings became round-1 fix items rather than pre-build spec changes; the spec was corrected either
way so the review lenses judge against the corrected contract.

## Opus lens: REJECT (4 blockers, 6 majors, 5 minors). Every finding accepted; spec revised before the builds.

| # | Sev | Finding (short) | Disposition |
|---|---|---|---|
| 1 | BLOCKER | `--clay-text on --secondary` measures 4.20:1 in dark mode, so the locked 56/56 gate is red by construction. | Accepted; recomputed by the session (4.201). Pair dropped; the regenerate button gets `active:text-secondary-foreground` unconditionally; PAIRS is 27 / 54; rulings B3 and D3 amended. |
| 2 | BLOCKER | The toggle's `Show password` name makes `getByLabelText(/password/i)` ambiguous in both page specs. | Accepted. `fillPassword` helpers use `getByLabelText('Password', { exact: true })`; a red line proves the toggle is not selected. |
| 3 | BLOCKER | Playwright's substring label match breaks `getByLabel('Password')` in seven e2e specs, five outside either PR's list. | Accepted; all seven confirmed by grep. PR-B owns the seven `exact: true` one-liners; PR-A repeats the identical `regenerate-plan.spec.ts:386` line inside its realignment; pr-strategy records the byte-identical shared hunk. Rulings A1 and C10 amended. |
| 4 | BLOCKER | Replacing the panel and backdrop while loading contradicts "testids remain stable" and two existing loading-phase tests. | Accepted. The two loading tests are rewritten by name; the 4.4 stability sentence now covers idle and failure phases only; the retired `Regenerating...` pending label is called out. Ruling B5 amended. |
| 5 | MAJOR | A second committed assertion pins the subprocess output string (`all 50 pairs pass`). | Accepted. The spec now names line 494 and its red line. (The literal itself was still wrong, see sol 3.) |
| 6 | MAJOR | The regenerate e2e still looks up the old textarea label at lines 420 and 427. | Accepted. Both lookups move to the stable `regenerate-plan-intent` testid. |
| 7 | MAJOR | The settings page spec has no harness for `useAuth` / `useSignOut` or the real toggles. | Accepted. Harness locked: page-scoped tree, toggles and dialog stay mocked, `auth.hooks` mocked for `useAuth` and `useSignOut`, real markup asserted. |
| 8 | MAJOR | The footer criterion interpolates the same env expression the component reads, so deleting the mechanism still passes. | Accepted. The test asserts the literal `Split 0.9.0 [EM DASH] MVP`; red line is removing the Vite define. |
| 9 | MAJOR | Two auth-hook criteria assert an empty query cache on stores that never seeded a query. | Accepted. Both tests subscribe and resolve a query first and assert non-empty to empty. |
| 10 | MAJOR | Ruling B5 ("every loading behavior stays pinned") contradicts B6 (panel replaced while loading). | Accepted. B5 amended to idle-phase tests only; section 9 records the deviation. |
| 11 | MINOR | Journey titles quoted with ASCII arrows differ from the files' U+2192 bytes. | Accepted. The spec says the title bytes do not change. |
| 12 | MINOR | The date criterion depends on the runner timezone. | Accepted in part: fixture pinned to `2026-06-29T12:00:00Z` with the local-time formatter kept (the local date is the honest display). Residual: UTC+12 to UTC+14 runners would render Jun 30 (sol 12); CI runs UTC and the maintainer's machine is UTC-5; recorded, not fixed. |
| 13 | MINOR | The Settings frame class contradicted the onboarding precedent B1 cited. | Accepted. Frame locked to the Log page's centered `max-w-md` column with the 22px gutter; B1 amended. |
| 14 | MINOR | No edge-case row for `user === null`. | Accepted. Row added (`user?.email ?? ''`, button still enabled). |
| 15 | MINOR | Three citation ranges off by a few lines. | Accepted. Corrected. |

## Sol lens (max): REJECT (5 blockers, 6 majors, 2 minors). Standing only where opus or the repo confirms.

| # | Sev | Finding (short) | Disposition |
|---|---|---|---|
| 1 | BLOCKER | "Disjoint" file lists while both PRs edit `regenerate-plan.spec.ts`; rulings A1/C10 not amended. | CONFIRMED as a documentation inconsistency (the merge mechanics were already in pr-strategy). Section 1.A now says "disjoint except one byte-identical line"; rulings A1 and C10 amended. Not a topology problem: identical hunks merge cleanly, verified by the two builds producing the same line. |
| 2 | BLOCKER | The Settings frame in the spec contradicts ruling B1 (`screen-gutter`, design-extract padding). | CONFIRMED as a contradiction between the spec and the ruling text (the spec had already moved after opus 13). Resolved by amending B1 to the spec's class: the Log page precedent with the design's 22px gutter; `screen-gutter` is unused by any screen and the shell supplies no column. |
| 3 | BLOCKER | The subprocess assertion literal `all 27 pairs` cannot pass: the script prints `results.length` (54); section 7 still said 28 / 56. | CONFIRMED by the host run (`all 54 pairs pass WCAG thresholds`) and by the one red vitest in the PR-A host record. The build wrote the spec's wrong literal: fix F1 in PR-A round 1. Spec and section 7 corrected. |
| 4 | BLOCKER | The clay outline override loses its border and fails resting contrast in dark mode: the shared outline variant adds `dark:border-input dark:bg-input/30`; clay on that composite is 3.84:1. | CONFIRMED by the session's recomputation (dark composite `#292e25`, clay 3.841; light unaffected at 5.17 on background; foreground on the composite 11.4 so SIGN OUT is fine). The built button carries no `dark:` override. Fix in PR-A round 1: `dark:border-clay-text dark:bg-background` on the regenerate button; spec 4.2 and 1.B corrected. |
| 5 | BLOCKER | The auth links have no 44px target and no focus ring. | CONFIRMED against the built `login.page.tsx` (bare clay text class). Fix in PR-B round 1: `relative hit-target-44 rounded-sm outline-none` plus the canonical `focus-visible` ring classes on both links; spec 4.2 corrected. Severity judged major, not blocker: the links work, the non-negotiable is a house rule the review round enforces. |
| 6 | MAJOR | Three red lines stay green (adding a `goto`, lowering a threshold, deleting an oracle). | CONFIRMED. All three rewritten as production mutations (remove `navigate('/')`, raise a threshold to 8.0, remove `postLogoutBroadcast()`). |
| 7 | MAJOR | The sign-out e2e needs the onboarding and plan stubs to reach `/`. | CONFIRMED in principle; the built `sign-out.spec.ts` already installs both stubs (journeys passed on the host). Spec wording added; no code change. |
| 8 | MAJOR | `npm run build` is not an unconditional gate in the spec. | CONFIRMED as a wording gap (both builders ran it, the host reran it). Spec sections 3 and 7 now list it unconditionally with exit 0 required. |
| 9 | MAJOR | The toggle's props are underspecified. | CONFIRMED as a wording gap; the built component already has `{ isVisible, onToggle, className? }` with `AuthTextField` owning the state. Spec 4.2 locks it; no code change. |
| 10 | MAJOR | The component map omits the auth form gap (3.5), the field gap (1.5), and the dialog panel column. | CONFIRMED against the built `auth-form-shell` (`space-y-4`) and `FormItem` default gap. Fix in PR-B round 1: form `flex flex-col gap-3.5`, `FormItem className="gap-1.5"`; the PR-A conformance lens judges the dialog panel column against the corrected spec. |
| 11 | MAJOR | "Password managers unaffected" needs a research prompt (compatibility question). | REJECTED. It is a functional requirement of the slice design (D3), not an unknown: the mechanism is keeping `autocomplete` and the native input as the `FormControl` child while only `type` flips, and both page specs plus the e2e journey pin it. A research prompt would not change the build. |
| 12 | MINOR | The date fixture is still timezone-dependent for UTC+12 to UTC+14. | CONFIRMED as a residual (see opus 12). Recorded; no change. |
| 13 | MINOR | Literal U+2192 and U+2026 glyphs in the spec prose. | CONFIRMED. Normalized to ASCII; the spec states the title bytes stay U+2192 in the files. |

## Ruling

Every opus finding was accepted before the builds. Of sol's thirteen, eleven are confirmed (three of
them, 4, 5 and 10, become code fixes in round 1; 3 was already caught by the host run; the rest are
spec wording), one is rejected on the design's own requirement (11), and one is a recorded residual
(12). The red-team is not re-run: the remaining changes are corrections and scorers, not design
changes, and the round-1 lenses judge the builds against the corrected spec.
