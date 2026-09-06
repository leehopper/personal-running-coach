# Slice 6 recon adjudication (orchestrator ruling, 2026-09-06)

Six read-only lenses ran over `main` at 87718f5f: five luna lenses over the code
(`recon/out.json`, items in `recon/items.jsonl`: r1 settings page, r2 regenerate and
building surface, r3 sign-out, r4 auth pages, r6 version constant, test utilities, e2e
inventory, lint traps) and one terra lens over the design source (`recon/out-design.json`,
its extract in `recon/design-extract.md`). This file records what the session decided from
them. The spec carries these decisions; the red-team checks the spec against them and the
repo. Where a lens recommendation and this file differ, this file wins.

## A. Shape of the slice

A1. Two independent PRs on two branches off `main`, built in parallel in two clones, no
stacking. PR-A `feat/slice-6-settings` (Settings recomposition, regenerate building surface,
ACCOUNT and the first SIGN OUT, version footer, the contrast-pair additions). PR-B
`feat/slice-6-auth` (auth poster screens, register mirror, password visibility toggle, e2e
realignment). Their file lists are disjoint except one byte-identical line (amended after the red-team: the
password toggle's accessible name forces `exact: true` on every password lookup, so PR-B also
changes one line in six other e2e specs, and `regenerate-plan.spec.ts:386` carries the identical
change in both PRs; identical hunks merge cleanly in either order). PR-B touches no file under
`docs/` except `docs/plans/split-ui-cycle/slice-6-evidence/pr-b/`; every roadmap, cycle-plan,
and decision-log edit rides PR-A, and PR-A's status text describes both PRs so the merge order
does not matter. Shared evidence (recon, red-team, spec-draft) is committed in PR-A only.

A2. No backend, wire, event, prompt, generated-code, or migration change in either PR. The
current-plan line reads fields already on `GET /api/v1/plan/current` (r1, r2). Sign-out uses
the existing `POST /api/v1/auth/logout` (r3). The version footer is a build-time constant
(DEC-089 D8; D below). Nothing under `backend/`, `frontend/src/app/api/generated/`, or
`backend/openapi/` changes; `npm run codegen:check` must still exit 0.

A3. Register has no artboard (terra fact 2). Its composition is the sign-in poster mirrored
per HANDOFF 5.7 with the copy locked in C6 below; this is an orchestrator ruling, not a design
inference, and the spec says so.

## B. PR-A rulings (Settings)

B1. Page frame. `settings.page.tsx` drops the three `Card`s. `<main data-testid="settings-page">`
uses the Log page's centered column with the design's 22px gutter:
`mx-auto flex min-h-full w-full max-w-md flex-col gap-5 bg-background px-[22px] py-8` (amended
after the red-team: `screen-gutter` is unused anywhere and the shell supplies no column). Header: `<h1 className="t-screen-title">Settings</h1>`
followed by a full-width 2px `bg-rule` divider (the design's title rule). Four sections in this
order, each `<section>` with the existing testid kept (`settings-plan-section`,
`settings-appearance-section`, `settings-units-section`) plus the new
`settings-account-section`, each opened by `SectionRule` as `h2` with sentence-case source
labels `The plan`, `Appearance`, `Units`, `Account` (CSS uppercases). Then the footer (B10).
The two helper sentences under Appearance and Units are deleted (the design has none, and the
Appearance one names the retired brand).

B2. THE PLAN current-plan line. `MonoLabel tone="muted"` eyebrow `Current plan`, then a
`<p data-testid="settings-plan-generated-at" className="t-body text-muted-foreground">` whose
content is the segments `Generated <time dateTime={generatedAt}>{date}</time>`, `{weeks} weeks`,
and `{goal}` joined by ` · `. Rules: `date` is `formatGeneratedAt(generatedAt)`, now
`Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', year: 'numeric' })` (renders
`Jun 29, 2026`), keeping the existing invalid-input passthrough (the page spec pins the raw
string and the `<time dateTime>` attribute for `definitely-not-a-date`). `weeks` is
`plan.macro?.totalWeeks`; `goal` is `plan.macro?.goalDescription`. A segment whose value is
null, undefined, or blank is omitted together with its separator, so a plan with `macro: null`
renders `Generated Jun 29, 2026` alone. `goalDescription` is LLM-authored prose and renders
verbatim (no truncation, no case change). Loading and error branches keep their current copy and
roles (both pinned by the page spec); the CAN'T REACH THE COACH surface is Slice 7's.
`totalWeeks` is the macro's own declared horizon, not a claim about populated weeks (the
rolling-horizon note from Slice 5 C1 does not apply to this line).

B3. REGENERATE PLAN. `Button variant="outline"` with the clay override classes
`h-12 border-clay-text text-clay-text` (semantic tokens; the shared outline variant is not
clay-specific, r2 fact 32), testid `settings-regenerate-button` kept, source string
`Regenerate plan`. Beneath it the mono warning
`<p className="t-data-label text-muted-foreground">Replaces your current plan. The coach starts
fresh from your log book.</p>` (sentence-case source, CSS uppercase; muted, not the mock's
faint, per design-extract deviation 1). Pressed state: the shared primitive's secondary fill with
`active:text-secondary-foreground` unconditionally (amended after the red-team measured clay text
on the secondary fill at 4.20:1 in dark mode, below AA; no clay-on-secondary pair is added).

B4. The `View previous plan` placeholder, its `previousPlanId` prop on `PlanSummary`, the
`settings-previous-plan-link` testid, and the two page-spec assertions on it are deleted.

B5. Regenerate dialog: keep the custom dialog, restyle, add the building surface. The existing
component (a custom positioned div with `role="dialog"`, `aria-modal`, labelledby, describedby,
the polite counter region, the alert error, testids `regenerate-plan-backdrop`,
`regenerate-plan-dialog`, `regenerate-plan-intent`, `regenerate-plan-submit`) is NOT migrated to
the shared Dialog primitive in this slice (that churns preserved ARIA and testids for no design
gain; deferred, see F2). Its body stays mounted for the whole flow so `intent`, the idempotency
key, and the error survive the building phase. Restyle per design-extract 5h: panel
`w-[calc(100%-40px)] max-w-[350px] rounded-xl border border-border bg-card p-[18px]` with a
`gap-3` column; title source `Regenerate plan` (condensed section-title treatment);
description `This replaces your current plan. The coach starts fresh from your log book —
nothing you've logged is lost.`; textarea label `Anything I should know? — optional`
(mono `t-data-label`, CSS uppercase); textarea `min-h-[88px] rounded-md px-[14px] py-[11px]`
with `maxLength={500}` and the calf-strain placeholder unchanged; the counter becomes
`{remaining} left` in mono, `self-end`, keeping its `aria-live="polite"` region and the 500 /
250 / 0 values the component spec pins (the spec's expected strings change from `N characters
remaining` to `N left`; the numbers and the live region do not); actions row
`flex items-center justify-end gap-2.5` with CANCEL (`variant="ghost"`, `min-h-11`, gains
testid `regenerate-plan-cancel`) and REGENERATE (`variant="default"`, `min-h-11`, testid kept).
Every current idle-phase behavior in r2 facts 9 to 13 (trim, empty-intent omission, one UUID per
mounted body reused across retries, failure keeps the dialog open with `role="alert"`) is
preserved and stays pinned by the existing component spec. The two loading-phase tests (submit
disabled with pending copy; backdrop click blocked) are rewritten, not preserved: while the
mutation is in flight the panel and backdrop do not exist (B6), so those tests become "shows the
building surface while regeneration is in flight" and "cannot be dismissed while the mutation is
in flight". (Amended after the red-team: the original wording claimed every loading behavior
stayed pinned, which B6 contradicts.)

B6. Building phase and success. While the mutation is in flight the panel and backdrop are
replaced (conditional render inside the still-mounted body) by
`<div data-testid="settings-regenerate-building" className="fixed inset-0 z-50">
<BuildingPlanSurface statusLine="Reworking your plan from the log book." /></div>`.
On success the dialog calls `onClose()` and navigates to `/` with `useNavigate()` (Today is the
only surface that renders the plan; the Plan tag invalidation refetches it there; the existing
e2e already proves Plan B on `/`). On failure the panel returns with the intent text, the same
key, and the existing generic alert. Nothing outside the dialog is made `inert`: the overlay
covers the viewport, Escape and pointer dismissal are already blocked while loading, and the
surface announces through its `role="status"` region. The e2e `regenerate-plan.spec.ts` is
realigned: after submit expect `settings-regenerate-building` visible (the stubbed POST is
delayed by a short `route.fulfill` wait so the overlay is observable), then expect URL `/` and
Plan B without the explicit `page.goto('/')`. The 400 intent-length server rejection keeps
mapping to the generic alert (r2 risk 3; Slice 7 owns failure copy).

B7. APPEARANCE. `theme-toggle.component.tsx` is rewritten in place onto `SegmentedControl`
(radiogroup and radio semantics are what the tests select by). Items in order Dark, Light,
System with values `dark`, `light`, `system`; `aria-label="Appearance"`, root testid
`settings-theme-toggle`, item testids `theme-option-dark`, `theme-option-light`,
`theme-option-system`; `value={theme}` and `onValueChange={(v) => setTheme(v as Theme)}` on the
existing context. Every theme-toggle spec assertion (radio names, checked state from storage,
`html.dark`/`html.light` toggling, persisted literal, system resolution) is preserved unchanged.

B8. UNITS. `units-toggle.component.tsx` is rewritten in place onto `SegmentedControl`. Items
Kilometers (`"0"`) and Miles (`"1"`) from `UNIT_OPTIONS` via `String(value)` and
`parsePreferredUnits` on change (the onboarding units field is the precedent);
`aria-label="Units"`, root testid `settings-units-toggle`, item testids `units-option-0`,
`units-option-1`; the PUT on change, the Kilometers fallback while data is undefined, and the
failure path (`toast.error` once plus `reportClientError` kind `unhandled-rejection`) are
preserved and stay pinned by the existing component spec.

B9. ACCOUNT and SIGN OUT.
- Section body: a row `flex items-center justify-between gap-4`; left a column with
  `MonoLabel tone="muted"` `Signed in as` and `<p data-testid="settings-account-email"
  className="t-body text-foreground">{user.email}</p>` from `useAuth().user`; right
  `<Button variant="outline" data-testid="settings-sign-out-button" className="min-h-11">
  Sign out</Button>` (the mock's 40px is a recorded deviation), disabled while signing out.
- Hook: `useSignOut()` in `auth.hooks.ts` returning `{ signOut, isSigningOut }`. Sequence, with
  the reason each step sits where it does: (1) `await logout().unwrap()` first, because
  `resetApiState` aborts in-flight requests and the POST needs the live cookie and XSRF header;
  (2) `dispatch(loggedOut())`, which flips the guards; (3) `dispatch(apiSlice.util.resetApiState())`,
  the purge; (4) `postLogoutBroadcast()`. Steps 2 to 4 run in `finally`: a rejected logout POST
  still signs the client out (the user asked to leave, and a 401 has already dispatched
  `loggedOut` through the base query); the rejection is reported through the existing
  `reportClientError` helper the units toggle uses, never swallowed silently, and no error UI is
  added to Settings.
- Receiver: the existing cross-tab listener hook dispatches `loggedOut()` then
  `apiSlice.util.resetApiState()` (same order as the sender) so a second tab drops its per-user
  caches too.
- Guard: no explicit navigation; `RequireAuth` redirects to `/login` with `state.next` set to
  `/settings`, so a re-login returns to Settings. Recorded as a note, not changed.
- Tests (all new or extended, all must be able to go red): `auth.hooks.spec.tsx` (new, store
  built with `apiSlice.reducer`, `apiSlice.middleware`, and the auth reducer, per REVIEW.md):
  (a) `signOut` posts to `/api/v1/auth/logout` then the auth status is `unauthenticated`;
  (b) a query subscribed before sign-out has no entry under `state.api.queries` afterwards, and
  re-subscribing issues a second fetch (the purge oracle); (c) a second `BroadcastChannel('auth')`
  opened by the test receives the exact `logout` message (install a minimal in-memory
  `BroadcastChannel` fake in the spec file when jsdom lacks one; `broadcast-auth.ts` no-ops
  without the global, so the fake is what makes the assertion possible); (d) a second store
  running the listener hook receives that message and ends with `unauthenticated` auth and an
  empty `state.api.queries`; (e) a rejected logout POST still ends unauthenticated, purged, and
  broadcast, and `reportClientError` was called once. `require-auth.component.spec.tsx` gains
  the live flip: render authenticated, dispatch `loggedOut`, expect the `/login` redirect.
  `settings-page.spec.tsx` gains: email rendered from the store, clicking SIGN OUT invokes the
  hook, the button is disabled while `isSigningOut`.
- e2e: new `e2e/sign-out.spec.ts`: register, land on `/`, open Settings through the tab bar,
  assert `settings-account-email` shows the registered email, click SIGN OUT, expect URL
  `/login`, `tab-bar` not visible, and the `__Host-RunCoach` cookie absent or expired (copy the
  cookie check from `auth.spec.ts`); a second test opens two pages in one context, signs out
  from the first, and expects the second to land on `/login` (BroadcastChannel is same-origin
  within a context). The fixture password line carries the sibling specs' suppression comment.

B10. Version footer. `<footer><p data-testid="settings-version" className="t-data-label
text-center text-muted-foreground pt-2">Split {version} — MVP</p></footer>` (CSS uppercase
renders `SPLIT 0.9.0 — MVP`; muted is the recorded deviation for the mock's untokened footer
color). `version` is `import.meta.env.VITE_APP_VERSION`. See D1 for the mechanism.

B11. Specs to update in PR-A: `settings-page.spec.tsx` (replace the `theme-toggle-stub` and
placeholder assertions with real segmented-control queries and the sections' accessible names,
add the current-plan line cases: full line, `macro: null`, blank goal, invalid date passthrough;
the footer exact text `Split ${import.meta.env.VITE_APP_VERSION} — MVP` in both themes via
`renderInBothThemes` plus `expectDualThemeParity`), `theme-toggle.component.spec.tsx` and
`units-toggle.component.spec.tsx` (unchanged assertions, plus one order assertion each and the
new item testids), `regenerate-plan-dialog.component.spec.tsx` (counter strings, the building
phase, success navigation, failure return), `auth.hooks.spec.tsx` (new), the require-auth flip,
`check-contrast.spec.ts` (pair count), and the two e2e files.

B12. PR-A file list. Changed: `frontend/src/app/modules/settings/pages/settings.page.tsx`,
`frontend/src/app/modules/settings/pages/settings-page.spec.tsx`,
`frontend/src/app/modules/settings/components/theme-toggle.component.tsx`,
`frontend/src/app/modules/settings/components/theme-toggle.component.spec.tsx`,
`frontend/src/app/modules/settings/components/units-toggle.component.tsx`,
`frontend/src/app/modules/settings/components/units-toggle.component.spec.tsx`,
`frontend/src/app/modules/settings/components/regenerate-plan-dialog.component.tsx`,
`frontend/src/app/modules/settings/components/regenerate-plan-dialog.component.spec.tsx`,
`frontend/src/app/modules/auth/hooks/auth.hooks.ts`,
`frontend/src/app/modules/auth/components/require-auth.component.spec.tsx`,
`frontend/e2e/regenerate-plan.spec.ts`, `frontend/vite.config.ts`, `frontend/tsconfig.node.json`,
`frontend/src/vite-env.d.ts`, `frontend/package.json`, `frontend/package-lock.json`,
`frontend/scripts/check-contrast.ts`, `frontend/scripts/__tests__/check-contrast.spec.ts`.
New: `frontend/src/app/modules/auth/hooks/auth.hooks.spec.tsx`, `frontend/e2e/sign-out.spec.ts`.
Orchestrator-owned docs edits in PR-A: `ROADMAP.md`, `docs/plans/split-ui-cycle/cycle-plan.md`,
`docs/decisions/decision-log.md` (a one-line D8 build note only if the D1 mechanism needs one),
and the evidence directory.

## C. PR-B rulings (Auth poster)

C1. Layout. Both pages render `<main className="mx-auto flex min-h-dvh w-full max-w-md flex-col
justify-center gap-[26px] bg-background px-[26px] pb-10">` (design-extract auth geometry; the
`max-w-md` column is the onboarding precedent). The `Card` wrapper goes.

C2. `AuthPosterHeader` (new, `frontend/src/app/modules/auth/components/auth-poster-header.component.tsx`,
testid `auth-poster-header`): `Wordmark size="poster"` (58px, accessible name `Split`, both
already in the component), a rule `<div aria-hidden="true" className="h-0.5 w-16 bg-rule" />`,
and the tagline `<p className="font-mono text-[13px] font-medium uppercase tracking-[0.1em]
text-muted-foreground">The plan adapts. You do the work.</p>`, in a `flex flex-col gap-3`
column. Register renders the same header; the heading below it differs (C6).

C3. Headings. `AuthFormShell` keeps rendering `heading` as the page `h1` and gains a boolean
`headingVisuallyHidden` prop. Login passes `heading="Sign in"` hidden (`sr-only`; the poster has
no visible sign-in title, the page keeps an `h1` for assistive tech). Register passes
`heading="Start here"` visible with `t-screen-title`.

C4. Fields. Labels stay `Email` and `Password` (every spec and e2e selects by these labels) and
render through `FormLabel` with `t-data-label` (mono, CSS uppercase). `AuthTextField` passes
`className="h-12 px-[14px]"` to `Input` (visual 48px; `input.tsx` untouched). `type`,
`autoComplete` (`email`, `current-password`, `new-password`), `autoFocus`, `FormControl`,
`FormDescription`, `FormMessage` and the `role="alert"` wiring are unchanged.

C5. Password visibility toggle (new
`frontend/src/app/modules/auth/components/password-visibility-toggle.component.tsx`, r4 draft
adopted): `Button type="button" variant="ghost" size="icon"` with `aria-label` `Show password` /
`Hide password`, `aria-pressed={isVisible}`, testid `password-visibility-toggle`, lucide `Eye` /
`EyeOff` with `aria-hidden`, positioned `absolute top-1/2 right-1 -translate-y-1/2` inside a
`relative` wrapper that `AuthTextField` renders around `FormControl` and the toggle when
`type === "password"`. The wrapper sits around `FormControl`, never inside it (FormControl is a
Slot that clones ids and ARIA onto its direct child, which must stay the native input). The
input gets `pr-12` in that case and its `type` switches between `password` and `text` with local
state; `autoComplete` is untouched, so password managers see the same field. The hit target is
44px through the icon size's `before:` expansion the button primitive already supplies.

C6. Copy and actions.
- Login: primary `Sign in` (spec and e2e select `/sign in/i`), pending `Signing in…`,
  `Button` override `h-[52px] text-[17px] tracking-[0.14em]`. Below it the OAuth reserve:
  `<div aria-hidden="true" data-testid="auth-oauth-reserve" className="min-h-[52px]" />`, one
  primary-button height of empty room, nothing else (DEC-089 fast-follow slot). Then the
  secondary row `<p className="t-body text-muted-foreground">First run here? <Link
  to="/register" className="font-condensed font-bold uppercase tracking-[0.12em]
  text-clay-text">Create account →</Link></p>`.
- Register: primary `Create account` (spec and e2e select `/create account/i`), pending
  `Creating account…`, same override, same reserve, secondary row `Already on the plan?
  <Link to="/login" ...>Sign in →</Link>`. The password-rules helper stays a
  `FormDescription` (its id wiring is what `aria-describedby` references) with the new source
  text `12 characters or more, with an uppercase letter, a lowercase letter, a digit, and a
  symbol.` rendered `font-mono text-[11px] leading-relaxed text-muted-foreground` (no
  uppercase; the register spec asserts only the generated Zod message, not this helper).
- No other string changes. Register still chains login after register; both pages' error
  mapping, `parseProblem`, `form-alert`, and navigation are untouched.

C7. Lint. The three touched spec files (`e2e/auth.spec.ts`, `login.page.spec.tsx`,
`register.page.spec.tsx`) carry four pre-existing `sonarjs/no-hardcoded-passwords` errors on
their fixture constants; each gets the `// eslint-disable-next-line sonarjs/no-hardcoded-passwords`
line the sibling specs already use (`e2e/plan-render.spec.ts:29`, `shell-navigation.spec.ts:8`),
so per-file eslint is clean on every touched file.

C8. Tests. New co-located specs for `AuthPosterHeader` (wordmark accessible name, rule, tagline
text, both themes) and `PasswordVisibilityToggle` (label and pressed flip). Login and register
page specs gain: header present, hidden vs visible `h1`, the toggle flips `type` while
`autocomplete` stays, the reserve is present and empty, the secondary link text and target,
dual-theme parity with no raw hex. Existing assertions stay unmodified.

C9. e2e `auth.spec.ts` realignment: same journey and cookie assertions; add on `/login` the
wordmark `role="img"` named `Split`, the tagline text, and the eye toggle flipping the password
input to `type="text"` with `aria-pressed="true"`. Registration still fills `Email` and
`Password` and clicks `Create account`.

C10. PR-B file list. Changed: `frontend/src/app/pages/login/login.page.tsx`,
`frontend/src/app/pages/login/login-form.component.tsx`,
`frontend/src/app/pages/login/login.page.spec.tsx`,
`frontend/src/app/pages/register/register.page.tsx`,
`frontend/src/app/pages/register/register-form.component.tsx`,
`frontend/src/app/pages/register/register.page.spec.tsx`,
`frontend/src/app/modules/auth/components/auth-form-shell.component.tsx`,
`frontend/src/app/modules/auth/components/auth-text-field.component.tsx`,
`frontend/e2e/auth.spec.ts`, plus one line each in `frontend/e2e/shell-navigation.spec.ts`,
`onboarding.spec.ts`, `plan-render.spec.ts`, `workout-logging.spec.ts`,
`conversation-streaming.spec.ts`, and `regenerate-plan.spec.ts` (`exact: true` on the password
lookup; amended after the red-team). New: `auth-poster-header.component.tsx` and `.spec.tsx`,
`password-visibility-toggle.component.tsx` and `.spec.tsx` (all under
`frontend/src/app/modules/auth/components/`). Untouched contracts: `auth.schema.ts`, the
generated zod, `problem-details.helpers.ts`, `auth.hooks.ts`, `input.tsx`, `button.tsx`,
`form.tsx`, `label.tsx`.

## D. Cross-cutting rulings

D1. Version constant (DEC-089 D8). Reuse the existing `import.meta.env.VITE_APP_VERSION` that
telemetry already reads. `vite.config.ts` imports `./package.json` and adds
`define: { 'import.meta.env.VITE_APP_VERSION': JSON.stringify(process.env.VITE_APP_VERSION ??
packageJson.version) }` (an explicitly provided env value still wins; none is set today);
`tsconfig.node.json` gains `resolveJsonModule: true`; `vite-env.d.ts` declares
`readonly VITE_APP_VERSION: string` on `ImportMetaEnv`. `package.json` `version` becomes
`0.9.0` through `npm version 0.9.0 --no-git-tag-version` so `package-lock.json`'s two root
`version` fields move with it (r6 open question 1: yes, same change). Vitest shares the config,
so the footer spec asserts the exact interpolated string. Side effect worth naming in the PR
body: error telemetry and the OpenTelemetry service version stop reporting `unknown` /
`0.0.0-dev`.

D2. Focus ring. The shared primitives' canonical 3px semantic ring is the rule
(`frontend/CLAUDE.md` Alpine components); the handoff's 2px wording is a known disagreement
already resolved for Slices 0 to 5. No primitive changes.

D3. Contrast pairs. `check-contrast.ts` PAIRS gains `--muted-foreground on --background` and
`--muted-foreground on --card` at 4.5 (essential muted text now sits on both; r6 found neither
guarded); the committed spec's expected count moves from 25 pairs / 50 results to 27 / 54, and the
subprocess assertion string moves to `all 27 pairs pass WCAG thresholds`. No clay-on-secondary
pair (amended: it measures 4.20 in dark mode; B3 moves pressed text onto secondary-foreground). If
a muted pair fails, stop and report (that would be a token problem outside this slice).

D4. Copy law. Source strings are sentence case; CSS uppercases; TSX literals write the em dash,
middle dot, arrow and ellipsis as the escapes `\u2014`, `\u00B7`, `\u2192`, `\u2026`; tests
assert source strings (the specs may use the same escapes). Semantic tokens only; `--alp-faint` never on essential text; every animation keeps its
`motion-reduce` pair; every interactive target is at least 44px.

D5. Sandbox limits the builder reports as deviations: `npm run e2e`, `npm run check-contrast`,
the full `npm run test`, `dotnet test`. The orchestrator runs them from the session and records
them in `build-orchestrator-runs.md`.

## E. Rejected alternatives

E1. Lifting the whole regenerate state machine into `SettingsPage` (r2's recommendation).
Rejected: the dialog body already owns intent, key, mutation and error, and keeping it mounted
through the building phase gives the same overlay with a smaller diff and no new page state.
E2. Migrating the regenerate dialog onto the shared Dialog primitive. Deferred (F2).
E3. Explicit `navigate('/login')` on sign-out. Rejected: the design says the guards land the
user on login; the guard already does.
E4. A separate `__APP_VERSION__` global. Rejected in favor of the env key telemetry already reads.
E5. Building the register password rules into `auth.schema.ts`. Rejected: the schema file
documents the login-only client boundary on purpose; the generated register schema stays the
validator and the helper text is presentation.

## F. Captured for the cycle plan

F1. `state.next` after sign-out is `/settings`, so a re-login returns there. Harmless; noted.
F2. The regenerate dialog is a custom modal, not the shared Dialog primitive; a migration is a
Slice 7 or post-cycle cleanup once the states pass lands its focus-trap audit.
F3. The regenerate failure alert stays generic (design has no copy); Slice 7's states pass.
F4. E2E account setup is duplicated per spec file (r6); consolidate in Slice 7's e2e pass.
