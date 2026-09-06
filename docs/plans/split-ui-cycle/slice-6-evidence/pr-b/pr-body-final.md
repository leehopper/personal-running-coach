**Slice 6 (Settings & Auth) PR-B recomposes sign-in and register as the SPLIT poster and adds the password visibility toggle (DEC-089 D3 of the slice design).** Its sibling, PR-A (`feat/slice-6-settings`, Settings and the first SIGN OUT), is independent: both branch from `main`, either may merge first, and the one line they share (`frontend/e2e/regenerate-plan.spec.ts`, the exact password lookup) is byte-identical in both.

Spec: `docs/specs/slice-6-settings-auth/spec.md` § 1.C, 3 PR-B, 4.2 (gitignored working-tree artifact). Committed evidence: `docs/plans/split-ui-cycle/slice-6-evidence/pr-b/` (build brief and report, host runs, both round-1 lens reports, fix list and report, e2e mutation replays, verify report and adjudication). The shared recon and red-team evidence rides PR-A.

## What's here

- **Poster.** Both pages drop the `Card` for a centered `max-w-md` column with the design's 26px rhythm. A new `AuthPosterHeader` mounts the `Wordmark` at its poster size, a 64px rule, and the mono tagline `THE PLAN ADAPTS. YOU DO THE WORK.`
- **Fields.** `Email` and `Password` keep their labels (now mono), sit at 48px, and keep every existing prop: `type`, `autoComplete` (`email`, `current-password`, `new-password`), `autoFocus`, the `FormControl` / `FormMessage` wiring and `role="alert"`. The form stack moves onto the poster rhythm (`gap-3.5`, field gap `1.5`).
- **Password visibility.** `PasswordVisibilityToggle` is a ghost icon button (`Show password` / `Hide password`, `aria-pressed`) rendered by `AuthTextField` beside the input, in a `relative` wrapper around `FormControl`, never inside it, so the native input stays the Slot's direct child. Only the input `type` flips; `autoComplete` is untouched, so password managers see the same field. The icon is small; the target is the primitive's 44px expansion.
- **Headings and actions.** Login keeps an `h1` (`Sign in`) visually hidden; register shows `START HERE`. The primary is 52px with its copy and pending copy unchanged. Under it sits an empty 52px `auth-oauth-reserve` (the OAuth fast-follow slot, nothing else). The secondary links (`First run here? CREATE ACCOUNT →`, `Already on the plan? SIGN IN →`) carry the shared 44px hit-target utility and the canonical focus ring.
- **Register helper.** The password rules render in mono: `12 characters or more, with an uppercase letter, a lowercase letter, a digit, and a symbol.`
- **Exact password lookups.** The toggle's accessible name contains the word password, and both Testing Library and Playwright match `aria-label` by substring, so `fillPassword` in both page specs and `getByLabel('Password')` in all seven Playwright journeys became `{ exact: true }` (six of those files change one line each).
- **Unchanged.** Auth schemas, the generated Zod, `parseProblem`, `form-alert`, register-then-login chaining, navigation, the session cookie, every shared primitive.

## Decisions and deviations

- Register has no artboard in the design source; its mirror of the sign-in poster, the `START HERE` heading, the helper copy, and the links are recorded as an orchestrator ruling in the red-team adjudication.
- The OAuth reserve is exactly one primary-button height; the design leaves the room unmeasured.
- The eye toggle keeps the mock's small glyph but takes the 44px target the house rule requires.
- The four `sonarjs/no-hardcoded-passwords` fixture lines in the three touched spec files take the suppression comment the sibling e2e specs already use.

## Review trail

- Two-family spec red-team (one Claude opus lens, one Codex sol lens) before the build. Three findings shaped this PR: substring password queries would throw on the toggle's name (the exact lookups above), the secondary links had no hit target or focus ring, and the form kept the old `space-y-4` rhythm.
- Round 1: mutation ledger (22 mutations red; three locked geometry contracts had no direct test) and spec conformance (35 of 39 criteria satisfied, the rest not applicable). One fix round, F1 to F5: link target and ring, form rhythm, and direct assertions for the poster frame, the toggle geometry, and the 52px primary.
- Eight e2e mutation replays from the session against the host stack, all red: every reverted exact locator hits the strict-mode violation, and removing the tagline fails the auth journey.
- Verify lens: all five fixes red under mutation; the only finding was a stale pointer in the fix brief, closed by the committed build report.

## Green

- `npm run build` clean; **1065/1065** vitest; `check-contrast` 50/50 (this PR adds no pair); `codegen:check` exit 0; eslint and prettier clean on every touched file.
- Playwright against the host stack: 14 passed; the two failures are the journeys already red on `main` before this slice (`plan-render.spec.ts:242`, `workout-logging.spec.ts:192`), outside this PR's surfaces and owned by Slice 7's suite consolidation.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01HFHnuvtPLWApRuJSDSMhsg
