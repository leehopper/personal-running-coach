**Slice 6 (Settings & Auth) PR-A recomposes Settings onto the Alpine rule sections, restyles the regenerate dialog around the plan-building surface, adds ACCOUNT with the app's first SIGN OUT (and the pulled-forward cross-account cache reset), and lands the version footer (DEC-089 D1, D2 and D8 of the slice design).** Its sibling PR-B (#364, `feat/slice-6-auth`, the auth poster screens) is independent: both branch from `main`, either may merge first, and the one line they share (`frontend/e2e/regenerate-plan.spec.ts`, the exact password lookup) is byte-identical in both. This PR also carries the shared Slice 6 evidence (recon, two-family red-team) and the ROADMAP, cycle-plan and decision-log status edits; Slice 6 is complete when both PRs merge.

Spec: `docs/specs/slice-6-settings-auth/spec.md` § 1.B, 3 PR-A, 4.3 to 4.6 (gitignored working-tree artifact). Committed evidence: `docs/plans/split-ui-cycle/slice-6-evidence/` (recon lenses and rulings, both red-team reports and their adjudication, build brief and report, host runs, both round-1 lens reports, fix list and report, e2e mutation replays, verify report and adjudication).

## What's here

- **Screen.** `Settings` as a `t-screen-title` over a 2px rule, then four `SectionRule` sections (`THE PLAN`, `APPEARANCE`, `UNITS`, `ACCOUNT`) in the Log page's centered column; the `Card`s and the two helper sentences are gone.
- **THE PLAN.** A `Current plan` eyebrow and the line `Generated Jun 29, 2026 · 12 weeks · <goal>` composed from the loaded plan (`generatedAt`, `macro.totalWeeks`, `macro.goalDescription`); a missing or blank segment drops with its separator, an unparseable date passes through unchanged with its raw `dateTime`. `REGENERATE PLAN` is a clay outline with the mono warning beneath it. The dead `View previous plan` placeholder is deleted.
- **Regenerate.** The dialog keeps its custom modal (ARIA and testids unchanged) and is restyled to sheet 5h: 350px panel, mono `ANYTHING I SHOULD KNOW? — OPTIONAL` label, `{n} LEFT` counter, 44px actions. Its body stays mounted through the flow, so while the synchronous POST runs the panel gives way to the `BuildingPlanSurface` overlay (`Reworking your plan from the log book.`); success closes and navigates to Today, where the invalidated plan refetches; failure returns the panel with the same intent and idempotency key.
- **APPEARANCE and UNITS.** Both toggles rewritten in place onto `SegmentedControl` (DARK / LIGHT / SYSTEM, KILOMETERS / MILES) over the unchanged theme context and units API; every existing toggle, hook and API assertion still passes.
- **ACCOUNT and SIGN OUT.** `SIGNED IN AS <email>` from the auth state and a `Sign out` outline button wired to a new `useSignOut`. Order: the logout POST first (it needs the live cookie and XSRF header, and a cache reset aborts in-flight requests), then in `finally` `loggedOut` (flips the guards), `apiSlice.util.resetApiState()` (the purge, DEC-089 D8), and the cross-tab broadcast; a rejected POST is reported through `reportClientError` and the client still signs out. The receiver now purges too, so a second tab drops its per-user caches.
- **Version footer.** `SPLIT 0.9.0 — MVP` from `import.meta.env.VITE_APP_VERSION`, now defined in `vite.config.ts` from `package.json` (bumped to 0.9.0, lockfile root fields moved) with an environment override. Side effect: error telemetry and the OpenTelemetry service version stop reporting `unknown` / `0.0.0-dev`.
- **Contrast.** `check-contrast` gains `--muted-foreground on --background` and `--muted-foreground on --card` (27 pairs, 54 results); the committed spec's counts and the subprocess literal move with it.
- **Unchanged.** No backend, wire, generated, prompt or primitive file; `codegen:check` exit 0.

## Decisions and deviations

- Muted text replaces the mock's faint ramp for the eyebrows, the warning, the counter and the footer (faint is decorative-only and fails AA); SIGN OUT is 44px, not the mock's 40px.
- The `--clay-text on --secondary` pair the spec first planned measured 4.20:1 in dark mode, so it was dropped and the regenerate button's pressed text moves onto `secondary-foreground`. The shared outline variant's dark fill (`bg-input/30`) under clay text measured 3.84:1 and would also hide the clay border, so the button carries `dark:border-clay-text dark:bg-background`.
- The regenerate dialog stays a custom modal rather than migrating to the shared Dialog primitive (deferred to Slice 7's states pass). Sign-out relies on the guard's redirect, which records `state.next` as `/settings`.
- The current-plan test fixture is a mid-day UTC instant with the local-time formatter kept; a UTC+12 to +14 runner would render the next day (recorded residual).

## Review trail

- Six recon lenses over `main` (five over the code, one over the design source) and the orchestrator's rulings (`recon/adjudication.md`).
- Two-family spec red-team (one Claude opus lens, one Codex sol lens), both REJECT before the build; every accepted finding is in `redteam/adjudication.md`. The ones that shaped this PR: the failing clay-on-secondary pair, the loading-phase contradiction in the dialog contract, the circular footer test, the settings-page harness, the dark-mode outline fill, the frame class.
- Round 1: mutation ledger (44 mutations: 30 red, 7 green on untested order, 4 without a test, 3 e2e replays deferred to the session) and spec conformance (100 checks, 85 satisfied). The green mutations and the missing `Current plan` eyebrow became fixes F1 to F11: the contrast literal, the dark overrides, the panel column, the eyebrow, and assertions pinning section order, sign-out event order, dialog callback order and ARIA, the retired placeholder, the pair thresholds, and the placeholder bytes.
- Three e2e mutation replays from the session against the host stack, all red: removing the SIGN OUT handler fails both sign-out journeys; removing the broadcast fails only the second-tab journey; rendering the panel instead of the overlay fails the regenerate journey.
- Verify lens over the fix commit: every code fix red under mutation; its three remaining findings were a baseline mix-up on the placeholder bytes (the fix matches `main` exactly), a stale pointer in the fix brief, and trailing whitespace in the orchestrator's own record (stripped). Adjudication in `round2/adjudication.md`.

## Green

- `npm run build` clean with `0.9.0` in the bundle; **1057/1057** vitest; `check-contrast` 54/54; `codegen:check` exit 0; eslint and prettier clean on every touched file.
- Playwright against the host stack: 16 passed, including both new `sign-out.spec.ts` journeys and the realigned regenerate journey; the two failures are the journeys already red on `main` before this slice (`plan-render.spec.ts:242`, `workout-logging.spec.ts:192`), owned by Slice 7's suite consolidation.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01HFHnuvtPLWApRuJSDSMhsg
