**Slice 6 (Settings & Auth) PR-A recomposes Settings, regeneration, Account, sign-out, version, and contrast for `feat/slice-6-settings`. Sibling PR-B #364, `feat/slice-6-auth`, carries the auth poster screens, may merge in either order, and shares one byte-identical password lookup line in `frontend/e2e/regenerate-plan.spec.ts`.**

Spec: `docs/specs/slice-6-settings-auth/spec.md` section 3 PR-A (gitignored working-tree artifact). Committed evidence: `docs/plans/split-ui-cycle/slice-6-evidence/`.

## What's here

- THE PLAN: `frontend/src/app/modules/settings/pages/settings.page.tsx` replaces Cards with Alpine rule sections. Its current-plan line formats `generatedAt`, `macro.totalWeeks`, and `macro.goalDescription`.
- It uses the fixed en-US month, day, and year formatter. Present segments join with ` \u00B7 ` in `frontend/src/app/modules/settings/pages/settings.page.tsx`.
- Blank or null plan segments and separators are omitted. Invalid dates pass through unchanged. The previous-plan placeholder and Appearance or Units helper copy are removed in `frontend/src/app/modules/settings/pages/settings.page.tsx`.
- REGENERATE PLAN: `frontend/src/app/modules/settings/pages/settings.page.tsx` adds the clay outline action and muted warning. Its dark outline overrides preserve the clay border and background.
- The overrides avoid the shared dark input composite. The active state uses `secondary-foreground` text in `frontend/src/app/modules/settings/pages/settings.page.tsx`.
- Dialog: `frontend/src/app/modules/settings/components/regenerate-plan-dialog.component.tsx` keeps the custom dialog and mounted body. The building phase mounts `BuildingPlanSurface` with its status line.
- Success closes before navigating to `/`. Failure restores the panel with its intent and idempotency key in `frontend/src/app/modules/settings/components/regenerate-plan-dialog.component.tsx`.
- Appearance and units: `frontend/src/app/modules/settings/components/theme-toggle.component.tsx` and `frontend/src/app/modules/settings/components/units-toggle.component.tsx` use `SegmentedControl` in the locked orders.
- Existing theme storage and units GET, PUT, fallback, toast, and error-report wiring remain unchanged in those components and their specs.
- ACCOUNT and SIGN OUT: `frontend/src/app/modules/settings/pages/settings.page.tsx` renders the signed-in email and the first SIGN OUT.
- `frontend/src/app/modules/auth/hooks/auth.hooks.ts` posts logout first, then runs `loggedOut()`, `resetApiState()`, and the broadcast in `finally`.
- The POST needs the live session. The auth action flips guards. The reset purges per-user cache. The broadcast informs other tabs.
- The receiver uses the same cleanup order. `frontend/src/app/modules/auth/components/require-auth.component.tsx` preserves `state.next` for re-login.
- Version footer: `frontend/src/app/modules/settings/pages/settings.page.tsx`, `frontend/vite.config.ts`, `frontend/tsconfig.node.json`, `frontend/src/vite-env.d.ts`, and the package files add `Split {version} \u2014 MVP`.
- The same files add build-time `VITE_APP_VERSION`, JSON module support, and version `0.9.0`. The value feeds telemetry keys in `frontend/src/app/error-boundary/report-client-error.ts` and `frontend/src/app/api/otel.ts`.
- Contrast and contracts: `frontend/scripts/check-contrast.ts` adds muted text on background and card, producing 27 pairs and 54 results.
- ARIA, testids, loading, error, theme, units, route-guard, and API contracts remain covered by the touched specs. No backend, wire, generated-code, migration, or prompt file changes.

## Decisions and deviations

- Muted replaces the mock's faint treatment for readable copy. SIGN OUT uses `min-h-11`, or 44px, in `frontend/src/app/modules/settings/pages/settings.page.tsx`.
- The clay-on-secondary pair was dropped after measuring 4.20:1. The dark outline overrides were added after measuring 3.84:1 on the shared dark composite.
- The custom dialog remains instead of adopting the shared primitive. Sign-out relies on `state.next` instead of explicit navigation.
- The generated-date fixture is pinned to `2026-06-29T12:00:00Z`. UTC+12 to UTC+14 can still display June 30, which remains recorded.
- `docs/specs/slice-6-settings-auth/pr-strategy.md` assigns ROADMAP, cycle-plan, and decision-log status edits to PR-A.
- `git diff main..HEAD` currently contains shared evidence and not those three files.

## Review trail

- The two-family spec red-team rejected the draft. Opus found 4 blockers, 6 majors, and 5 minors. Sol found 5 blockers, 6 majors, and 2 minors.
- Accepted findings shaped the contrast pair, exact password lookup, loading-phase dialog, dark outline, and contract assertions in `docs/plans/split-ui-cycle/slice-6-evidence/redteam/adjudication.md`.
- Round 1 conformance ran 100 checks: 85 satisfied, 12 partial, 2 missing, and 1 not applicable. Its three findings included the dialog `gap-3` omission and evidence inventory mismatch in `round1/conformance.json`.
- Round 1 mutation ran 44 mutations: 30 red, 7 green, 4 not run, and 3 e2e replays not runnable in the sandbox.
- The seven green order mutations covered section order and testid, dialog callback and failure navigation, sender order, receiver order, and broadcast-before-purge. The missing `Current plan` eyebrow was also found in `round1/mutation.json`.
- Fixes F1 through F11 corrected contrast output, the dark outline, dialog layout and assertions, the eyebrow, cleanup order pins, the placeholder contract, and thresholds in `round1/fix-list.txt`.
- Three e2e mutation replays were all red: removing the SIGN OUT handler, removing the broadcast, and rendering the panel during loading in `round1/orchestrator-runs.md`.

## Green

- Fix-round host runs recorded 1,057/1,057 Vitest tests, `check-contrast` 54/54, codegen exit 0, and a clean build with `0.9.0` in the bundle. ESLint exited 0. Prettier reported all files clean.
- Playwright recorded 16 passed. The two failures were already red on `main`: `plan-render.spec.ts:242` and `workout-logging.spec.ts:192`. Both new sign-out journeys and the realigned regenerate journey passed.
