# SPLIT / Alpine UI Redesign — Build Cycle Plan

> **Status:** Approved (2026-07-07). Next cycle after the MVP-0 build/fix scope closed. Design source of truth: `docs/design/split-alpine/HANDOFF.md` + `docs/design/split-alpine/split-alpine.dc.html` (builder-produced, 2026-07-07).

## Status

- **Current Cycle:** SPLIT / Alpine UI Redesign
- **Active Slice:** Slice 4 (Log & Log Book) — next to build; no PR yet. Slice 3 (Coach) shipped complete 2026-07-15 (PRs #288 / #290 / #292 / #294, DEC-091). Slice 2 (Today) shipped 2026-07-14.
- **Slice ledger:**
  | # | Slice | Completed | PR |
  |---|---|---|---|
  | 0 | Alpine Foundation | 2026-07-08 | #275 |
  | 1 | Shell & Navigation | 2026-07-08 | #277 |
  | 2 | Today | 2026-07-14 | #285 |
  | 3 | Coach | 2026-07-15 | #288 / #290 / #292 / #294 |
- **Active Slice Spec:** Slice 4 (Log & Log Book) has **no spec yet** — write it first from `docs/plans/split-ui-cycle/slice-4-log-logbook.md` (specs are gitignored working-tree artifacts, planned per-slice at build time). Slice 3's spec remains at `docs/specs/slice-3-coach/spec.md` for reference; its narrative is captured in the Slice 3 completion section below and the `ROADMAP.md` Cycle History row.
- **Next Step:** **Begin Slice 4 (Log & Log Book)** in a fresh session — write the slice spec from `docs/plans/split-ui-cycle/slice-4-log-logbook.md`, then build one backend PR (`GET` prescribed-slot-for-date reusing `PlanCalendar.ResolveSlot` + `IsOnPlan`/nullable prescribed-title on the history-row DTO, DEC-089 D5, + codegen) and the frontend `/log` restyle + `/history` ledger recomposition. Read this plan's Status + `slice-4-log-logbook.md` first. Slices 3/4 were independent after Slice 1 and 3 is now done; numbered order is the default.
- **Blockers:** None.
- **Parallel workstream:** Rolling Plan Horizon (backend-only, DEC-090; plan `docs/plans/plan-horizon/rolling-horizon-plan.md`) is running alongside this cycle — no file overlap, no wire/codegen churn with any SPLIT slice. See § Captured During Cycle, 2026-07-13 row.

This status block is the single source of truth for "where are we?" — mirrored into `ROADMAP.md` so `/catchup` finds it. Update both whenever a slice completes or the active slice changes. **Replace, don't append:** when a slice completes, collapse its Status entry to a one-line ledger row; the narrative moves to a per-slice completion section, a `ROADMAP.md` Cycle History row, and the decision log.

---

## Captured During Cycle

Follow-ups surfaced mid-cycle land here with a date + disposition, exactly as in the MVP-0 cycle plan. Empty at cycle start except the items deliberately deferred at planning time:

| Date | Item | Disposition |
|---|---|---|
| 2026-07-07 | **Partial-parse log-draft states (design 3b·05: `—` + `MISSING` tag, CONFIRM disabled).** The backend never emits a partial `StructuredLogDraft` — every field is `JsonRequired` and the intent classifier falls back to `Ambiguous` instead of a partial draft (`StructuredLogDraft.cs`). The designed states are unreachable UI today. | Deferred with the design as the contract. Build the reachable states (saving / success-receipt / failure) in Slice 3; revisit partial extraction as a product decision post-cycle (DEC-089 D6). |
| 2026-07-07 | **SPLIT trademark/domain search** (handoff § 9 item 1; fallbacks THRESHOLD / CADENCE / VERST). | User-owned, runs in parallel with the cycle. The wordmark + brand strings are contained in the `Wordmark` component + a small string set (DEC-089 D2), so a rename is a bounded swap. |
| 2026-07-07 | **OAuth buttons on auth** (handoff § 9 item 4). | Out of scope; Slice 6 leaves the designed vertical room under the primary button and nothing else. |
| 2026-07-07 | **`RTK cache reset on auth transitions`** — Slice 6 wires the first real SIGN OUT affordance, which makes the known cross-account cache-leak follow-up (declined PR #174; previously deferred to pre-public release) newly user-reachable. | Slice 6 must call `resetApiState()` on logout as part of wiring SIGN OUT — pulled forward because the affordance now exists. |
| 2026-07-13 | **Plan generation only ever produces 4 meso weeks and ONE micro week; nothing extends them. Only week 1 of any plan has real workouts.** Surfaced while specifying Slice 2's Today header, which would have displayed a clamped, permanently-stale week. | Promoted to its own backend workstream (plan doc + DEC-090, `docs/plans/plan-horizon/rolling-horizon-plan.md`); runs parallel to the UI slices, no file overlap or codegen churn. |
| 2026-07-14 | **Slice 3 (Coach) receipt persistence forces one deliberate backend/wire delta beyond the cycle's fixed four → DEC-091.** D6's persistent structured log receipt ("`LOGGED — 9.2 KM · 41:00 · JUL 8`, survives reload, from the committed turn representation") is **not** buildable frontend-only: on confirm the backend persists only a free-text coach-ack turn (no structured distance/time/date on the timeline), so the "frontend-only / already on the wire" premise for Slice 3 was false. | **DEC-091, chosen after weighing three options (spec § 1):** the confirm-ack coach turn carries an additive-nullable structured `LoggedRunSummary { workoutLogId, distanceKm, durationSeconds, occurredOn, completionStatus }`; the frontend renders the coach turn as text **plus** a durable receipt chip. Keeps the timeline the single server-composed source of truth (rejected option B's client-side `/history` merge and option A's in-session-only receipt). Low-risk: additive-nullable field on `CoachMessagePosted`, existing turns hydrate `loggedRun: null` (no upcaster — the same pattern as the row below), no LLM/prompt change → **no eval re-record**. This is the **only** backend change in Slice 3; the spec regroups the plan's 2-PR sketch into 4 PRs (A backend, B/C/D frontend). Logged as DEC-091; ROADMAP updated. |
| 2026-07-14 | **Slice 2 PR-A appends 3 nullable fields to `PlanGenerated`, a persisted Marten event record** — read literally, § Backend after this cycle's "no event-model changes beyond the additive `AnswerCaptured` field" line reserves that carve-out for Slice 5 only. | Not blocking, interpretation recorded here per the slice-2 spec's own flag: `string? TargetEventName, double? TargetEventDistanceKm, DateOnly? TargetEventDate` land directly on `PlanGenerated` rather than on `MacroPlanOutput` (the LLM structured-output schema) — `MacroPlanOutput` carries only free-prose `GoalDescription`, and touching the LLM schema would risk hallucinated drift plus an unbudgeted eval re-record. `GET /onboarding/state` was also rejected as the carrier (DEC-089 D4): it reflects the runner's last-onboarding answers, not the active plan, and drifts the moment a plan regenerates on new answers. All 3 fields are nullable with no default value threaded through every call site explicitly; previously-stored `PlanGenerated` events hydrate with nulls on replay, verified empirically, so no upcaster is required. |

---

## Goal & Done-State

**Goal.** Replace the Catppuccin-token MVP-0 interface with the fully-designed SPLIT / Alpine visual system — new palette (dark-default), new typography (Barlow Condensed / Barlow / IBM Plex Mono), new IA (bottom tab bar, dedicated `/coach` route, home recomposed as "Today"), and the redesigned seven screens plus states — while preserving every behavioral contract the current frontend implements (streaming, confirm-then-commit, idempotency, guards, event-sourced reads, a11y semantics, trademark discipline).

**This is overwhelmingly a frontend cycle.** The gap analysis (2026-07-07, adversarially reviewed) classified the 58 handoff requirements as: 28 restyle-only, 13 frontend-behavior, 7 frontend-recomposition, 5 new components, 2 infra/tooling, 3 backend-classified items — and the plan resolves to exactly **four narrow backend/wire deltas** (a prescribed-slot GET, two display fields on the history-row DTO, structured target-event fields on the plan projection, one onboarding narrative field), of which only the narrative field touches LLM prompts.

**Done-state.**

- All seven screens (Today, Coach, Log, Log Book, Onboarding, Settings, Auth) render the Alpine design in dark and light, on the new type system, behind the tab-bar IA.
- The handoff's § 8 non-negotiables hold: `role="log"` transcript semantics, `aria-live` regions, form ARIA, trademark-clean labels, safety turns rendered in full.
- `check-contrast` gates the Alpine pairs in pre-commit + CI (the clay-text ≥12px-semibold constraint is a documented usage rule, per Slice 0 D5).
- Full frontend suite green (Vitest + Playwright realigned), backend suite green in Replay, codegen drift gate clean.
- **Closing live pass:** a fresh funded-key end-to-end pass over the redesigned UI — onboard (with narrative field) → plan → log → adapt → converse — which **doubles as the outstanding MVP-0 done-gate verification** of the F-LIVE-1/F-LIVE-2 fixes at the surface (user decision 2026-07-07: redesign first; the standalone done-gate session is superseded by this pass).

## In Scope

- The Alpine token ramp (dark `:root` default + `.light` override), semantic remap, two new slots (`--positive`, `--rule`), geometry + spacing rules.
- Self-hosted Barlow Condensed / Barlow / IBM Plex Mono + the § 3 role table (R-086 integrated — static `@fontsource/*` per-weight packages, Slice 0 D4).
- Bottom `TabBar` + tab-shell layout + `/coach` route; onboarding/auth outside the shell.
- The seven screen recompositions/restyles per handoff § 5, including the § 7 component inventory.
- States & feedback per § 6 (buttons, inputs, skeletons, plan-building surface, toasts, failure surface, focus, motion).
- The four backend/wire deltas: `GET` prescribed-slot-for-date (Slice 4), `IsOnPlan` + prescribed-title on history rows (Slice 4), target-event fields on the plan projection (Slice 2, DEC-089 D4), and the onboarding narrative field + prompt injection (Slice 5, with DEC-074 manifest + plan-gen eval re-record).
- App icon / favicon (design 5g), SPLIT wordmark (user-ratified 2026-07-07).
- `check-contrast` Alpine pair swap (+ the documented clay-text usage rule).
- E2E + Vitest realignment as each surface lands; the closing live pass.

## Out of Scope (Deferred — Designed-For)

- **OAuth / third-party auth** — fast-follow slot reserved on the auth poster only.
- **Partial-parse log-draft states** — unreachable until the backend emits partial drafts (Captured above).
- **Markdown rendering in chat** — permanently out per the design contract ("UI owns 100% of layout; model owns 0%").
- **`motion/react`** — DEC-063 stands; Tailwind utilities + `tw-animate-css` only, every animation `motion-reduce`-paired.
- **The product rename decision** — SPLIT renders now; the trademark search is a parallel user-owned track.
- **iOS app / PWA installability beyond icon assets** — icon/manifest assets land; no further PWA work.
- **Backend contract work beyond the four named deltas** — anything else the design appears to want must be derived client-side or comes back through § When Agents Encounter Unknowns.

---

## Slice Structure

Each slice ships top-to-bottom (any backend delta + codegen + frontend + tests) and leaves the app usable. Tier-3 requirements docs live alongside this file — they elaborate the *what*; specs are written fresh per-slice at build time.

| # | Name | Requirements doc | Depends on |
|---|---|---|---|
| 0 | Alpine Foundation ✅ (2026-07-08, PR #275) | [`./slice-0-alpine-foundation.md`](./slice-0-alpine-foundation.md) | — (R-086 cleared) |
| 1 | Shell & Navigation ✅ (2026-07-08, PR #277) | [`./slice-1-shell-navigation.md`](./slice-1-shell-navigation.md) | 0 |
| 2 | Today ✅ (2026-07-14, PR #285) | [`./slice-2-today.md`](./slice-2-today.md) | 1 |
| 3 | Coach | [`./slice-3-coach.md`](./slice-3-coach.md) | 1 |
| 4 | Log & Log Book | [`./slice-4-log-logbook.md`](./slice-4-log-logbook.md) | 1 |
| 5 | Onboarding | [`./slice-5-onboarding.md`](./slice-5-onboarding.md) | 0 |
| 6 | Settings & Auth | [`./slice-6-settings-auth.md`](./slice-6-settings-auth.md) | 1 (Settings), 0 (Auth) |
| 7 | States, Light Pass & Cycle Close | [`./slice-7-states-close.md`](./slice-7-states-close.md) | 2–6 |

Slices 2/3/4 are independent of each other after Slice 1; Slice 5 needs only Slice 0 (onboarding renders outside the shell). Build them in the numbered order by default — Today first makes the daily surface coherent earliest — but re-ordering 2/3/4/5 between sessions is legitimate if review load suggests it.

### Slice 0 — Alpine Foundation ✅ Complete (2026-07-08, PR #275)

**Requirements:** [`./slice-0-alpine-foundation.md`](./slice-0-alpine-foundation.md) · **Research gate:** R-086 (fonts) — **cleared 2026-07-07** (integrated with errata; architecture locked in the slice doc's D4)

**Acceptance — "I can…"**

- [x] …open any existing screen and see the Alpine palette (dark by default), with light mode via the Settings toggle and System still following `prefers-color-scheme`.
- [x] …see Barlow Condensed / Barlow / IBM Plex Mono self-hosted with the § 3 role rules applied to shared primitives (buttons, inputs, labels).
- [x] …run `npm run check-contrast` and see the Alpine pairs gated (the clay-text ≥12px-semibold constraint is a documented usage rule enforced at review — the pair ratio itself clears the standard 4.5:1 gate).
- [x] …see the SPLIT wordmark component and the clay-slash favicon in the browser tab.
- [x] …see toasts, focus rings, pressed/disabled button states, and input error states in the Alpine style on existing surfaces.

**Shipped:** Three atomic commits (tokens/polarity/contrast gate, fonts/typography, component layer/brand/sonner) across PR #275. `check-contrast` 38/38 (light+dark), 662 vitest tests, `npm run build` clean. Each part went through an adversarial multi-lens review + blind-challenge pass; 29 confirmed findings fixed pre-merge, including a WCAG regression the review caught (the pressed-clay retune to `#C56438`). Deferred out of this slice, tracked as open items: raster PNG icon set (no rasterizer in-env; ships SVG-only), the `Wordmark` mount point (waits for Slice 1's shell), and two advisory test-coverage gaps flagged by the post-merge deep-review pass (`vite.config.ts`'s `fontFallbackFaces`/`preloadFontLinks` plugins have no unit tests; `index.html`'s `matchMedia`-throws fallback branch is untested) — worth a follow-up test pass, not blocking.

**Scope.** Token-ramp swap + semantic remap + `--positive`/`--rule` slots + the § 2 geometry/spacing rhythm; dark-default inversion (index.css `@custom-variant`, theme-provider, no-flash script, theme e2e updates); fonts + type roles; `check-contrast` Alpine pair swap (rides the token PR — the gate must stay green per-commit); shadcn primitive restyles (button/input/badge/dialog/collapsible/radio-group/switch-`Toggle`/sonner) + new shared primitives (`SegmentedControl`, `SectionRule`, `MonoLabel`, `StatBand`/`StatCell`, `Wordmark`, the plan-building `BUILDING YOUR PLAN` surface consumed later by Slices 5 and 6); favicon/app-icon assets. No layout recomposition — existing screens keep their structure on the new skin.

**Key risks.** The theme e2e (`theme.spec.ts`) and the provider drift-assertion spec pin the current light-default + `.dark` mechanics — both must be updated in the same PR as the inversion. Every semantic slot must keep resolving or check-contrast fails the commit. `SegmentedControl` has no shadcn primitive — build on existing Radix radio-group/toggle patterns; flag at spec time if a new Radix package is wanted (bounded choice, not research).

### Slice 1 — Shell & Navigation ✅ Complete (2026-07-08, PR #277)

**Requirements:** [`./slice-1-shell-navigation.md`](./slice-1-shell-navigation.md)

**Acceptance — "I can…"**

- [x] …navigate between Today / Coach / Log Book / Settings from a fixed bottom tab bar with always-visible labels and a raised 54px clay LOG action opening `/log`.
- [x] …open `/coach` and use the full chat timeline (streaming, confirm, retry) with the composer pinned above the tab bar.
- [x] …see onboarding and auth render without the tab bar; guards behave exactly as before.
- [x] …reach Settings from the tab bar (it currently has no inbound link at all).

**Shipped:** `TabBar` (fixed bottom 5-col grid, `NavLink`-driven active state, `aria-current="page"`, canonical focus ring, ≥44px targets, `env(safe-area-inset-bottom)` padding) + `ShellLayout` (`Outlet` + `TabBar`, shared `TAB_BAR_CLEARANCE` padding, `min-h-dvh`). Router rewire nests `/`, `/coach`, `/log`, `/history`, `/settings` under one `RequireAuth` → `ShellLayout`, each child keeping its pre-existing guard (`/settings` stays deliberately unguarded, matching prior behavior). `/coach` route + `CoachPage`: a mechanical `CoachChat` relocation off Home onto a full-height screen with the composer pinned above the tab bar — no turn-kind restyling (Slice 3's scope), `CoachChat`'s own behavior contract (streaming, retry, confirm, Edit→`/log`) untouched. Removed every scattered chrome nav link (`home-history-link`, History/Settings "Back to plan"); Playwright realigned to navigate via the tab bar. 674 vitest tests at merge. Post-merge pass addressed 4 CodeRabbit/deep-review findings before CI went green: dropped a forbidden planning-phase forward-reference comment; migrated `min-h-screen` → `min-h-full` on shell-route fallback states (Home's loading/error/no-plan-yet, plus a new `fullScreen` prop on the shared `OnboardingRedirectGuard` so its loading/error fallback keeps full-viewport centering on the standalone `/onboarding` route while avoiding the same overflow inside the four shell routes); corrected `ShellLayout`'s doc-comment overclaim (not every nested route is onboarding-gated); added a route-table-level test rendering the real composed `App` tree to close the onboarding-guard-asymmetry coverage gap. 677/677 vitest at close.

**Scope.** `TabBar` (safe-area padding, active-clay states, aria-current), tab-shell layout route wrapper, `/coach` route + `CoachChat` relocation (mechanical move; turn restyling is Slice 3), composer pinning, removal of every scattered nav link, e2e/nav realignment.

**Key risks.** Fixed bar changes every page's scroll region and bottom padding. Pinned composer + fixed tab bar + mobile-Safari keyboard/viewport interplay is the one genuinely fiddly area — flagged in the PR as needing a real-device eyeball check before merge; not independently re-verified as part of this close-out.

### Slice 2 — Today ✅ Complete (2026-07-14, PR #285)

**Requirements:** [`./slice-2-today.md`](./slice-2-today.md)

**Acceptance — "I can…"**

- [x] …see the Today header (wordmark + `WEEK N OF M — PHASE`), the workout hero with eyebrow/title/summary/stat-band and LOG RUN + DETAILS, and the rest-day variant on rest days.
- [x] …see THE WEEK: logged-vs-target km and 7 day cells reflecting done/today/planned/rest from meso slots joined with this week's logs.
- [x] …see FROM YOUR COACH as a clamped latest-exchange digest (or a one-line PLAN ADJUSTED headline when the latest turn is an adaptation), tap through to `/coach`, and have the fake composer focus the real one.
- [x] …see UP NEXT rows and THE BLOCK (12 phase cells, phase labels, goal chip from the plan's target event, upcoming week rows with DELOAD tags).

**Shipped:** Home recomposed as **Today** — six sections in the design's locked order (header → workout hero → THE WEEK → FROM YOUR COACH → UP NEXT → THE BLOCK), deleting the old `TodayCard` / `MacroPhaseStrip` / `MesoWeekBlock` / `UpcomingList` furniture. Four atomic commits: **PR-A** target-event fields on the plan projection + codegen; **PR-B** header + workout hero + THE WEEK + shared date/week derivations; **PR-C** coach digest + adaptation-diff helper + `/coach` composer prefill/focus receiver + UP NEXT; **PR-D** THE BLOCK + furniture deletion + e2e realignment. **Backend delta:** `PlanGenerated` / `PlanProjectionDto` gained three **nullable** target-event fields (name, distance, date) sourced from the onboarding `TargetEvent` at the generation site — old-shape events hydrate all-null on replay (verified empirically), no upcaster needed; general-fitness plans render no goal chip (DEC-089 D4). **Two build-time decisions the design didn't cover:** the hero and THE WEEK now derive from a single `isDateLogged` predicate so one run can't read as done-and-not-done on the same screen; essential text (`KILOMETERS`, rest-day labels, `BASE 1–4` phase spans) migrated off the decorative-only `--alp-faint` (AA-exempt, check-contrast-invisible) onto AA-gated `--muted-foreground`, a documented deviation from the mock. **Bugs caught pre-merge:** a wrong hero date in every non-UTC timezone (mixed local `getDay()` + UTC formatter — now one notion of "today" flows through the screen); the digest's PLAN ADJUSTED headline could silently grow. Whole-diff review + full CodeRabbit addressment landed before CI green.

**Scope.** Full home recomposition replacing `TodayCard`/`MacroPhaseStrip`/`MesoWeekBlock` furniture; client-side week/phase derivations; week-grid log join; digest with client-composed deterministic adaptation summary from the typed diff (DEC-089 D3), including the **shared adaptation-diff presentation helper** (headline compose + week/day→calendar-date locus math) that Slice 3 reuses, and the **`/coach` composer prefill/focus receiver contract** the digest's chips and fake composer target (Slice 3's restyle preserves it); **backend:** target-event fields on the plan projection (DEC-089 D4) + codegen.

**Key risks.** THE WEEK re-derives calendar→slot mapping client-side (PlanCalendar semantics) — pin with unit tests against the same fixtures. Digest must obey the hard clamp contract (nothing scrolls, no transcript reuse).

### Slice 3 — Coach ✅ Complete (2026-07-15, PRs #288/#290/#292/#294)

**Requirements:** [`./slice-3-coach.md`](./slice-3-coach.md) (design doc, predates DEC-091 — the build source of truth was `docs/specs/slice-3-coach/spec.md`)

**Acceptance — "I can…"**

- [x] …see the five turn kinds render per design 3b: user bubble with mono meta, label-style coach turns with streaming block-cursor, nudge-vs-restructure adaptation rendering with a collapsed WHAT CHANGED expander over typed diff rows, amber/red safety turns in full, and the rebuilt log-draft card.
- [x] …watch a confirmed log collapse into a persistent one-line receipt with LOG BOOK → (durability backed by PR-A's persisted `loggedRun`; PR-D wired the render).
- [x] …see mono date dividers (and TODAY — prefix) between calendar days.
- [x] …recover a dead stream via "That reply didn't go through." + RETRY.

**Shipped:** The `/coach` transcript restyled into the five Alpine turn kinds, with the confirmed-log receipt made reload-durable. Four atomic PRs. **PR-A** (backend, DEC-091, PR #288) — the confirm-ack coach turn carries an additive-nullable structured `LoggedRunSummary { workoutLogId, distanceKm, durationSeconds, occurredOn, completionStatus }` + codegen; existing turns hydrate `loggedRun: null` (no upcaster), no LLM/prompt change → no eval re-record. **PR-B** (PR #290) — `UserTurn` / `CoachTextTurn` / `DateDivider` turn-kind dispatch, local-day grouping, one shared live-exchange timestamp, composer restyle (48px clay send square, no-markdown body); 888/888 vitest at merge. **PR-C** (PR #292) — the adaptation turn rebuilt as the `PLAN ADJUSTED` card with calendar-date-anchored diff loci (UTC plan-anchor math, week-index fallback), `SafetyTurn` unified to a shared `{ tier, content, role?, testId? }` core across the persisted + live paths (content always in full, AX-01, local `SafetyNotice` deleted), and the deferred `--danger-surface` / `--danger-text` tokens landed (the live retry affordance moved onto `bg-danger-surface`, resolving PR-B's interim `bg-secondary`); 912/912 vitest, check-contrast 44 pairs. **PR-D** (PR #294) — the durable `LoggedRunReceipt` (`LOGGED — 5.0 km · 25:00 · JUN 29   LOG BOOK →`, the `LOG BOOK →` link → `/history`) rendered by `CoachTextTurn` from the persisted `interactive.loggedRun` (reload-durable by construction, never from ephemeral card state), a guarded `formatReceiptDate` (an unparseable `occurredOn` omits the date fragment instead of crashing into `new Date(null)` / `JAN 1`), and the `LogConfirmationCard` Alpine restyle (2×2 grid, moss STATUS, unit-aware ON-PLAN target, saving state); 937/937 vitest, check-contrast 44 pairs. **Bugs caught pre-merge:** a stale `liveTime` on an identical-text RETRY (re-keyed off `isStreaming`'s transition, PR-B); a real AA fix moving the amber safety heading off the ungated `--warning` border-accent (~1.8:1 on the light card) onto a gated `--warning-text` pair (PR-C); an out-of-range `dayOfWeek` bounds-check degrading to the week index rather than a plausible-but-wrong calendar date (PR-C). Every PR ran deep-review + full CodeRabbit addressment before CI green.

**Scope.** Frontend turn-kind restyles + net-new behaviors (timestamp meta rendering, date-divider grouping, diff-row locus dates derived from `PlanStartDate`, draft-card saving/receipt states, composer restyle) over one backend/wire delta — DEC-091's additive-nullable `LoggedRunSummary` on the confirm-ack turn (PR-A). Preserved `TranscriptScroller`, confirm/idempotency, Edit→`/log` draft handoff. **Deferred (unreachable / out-of-scope):** partial-parse `MISSING` card states (the backend never emits a partial `StructuredLogDraft` — every field is `JsonRequired`) and the optimistic in-card receipt bridge (spec § 9 #2 — reload-durability doesn't depend on it).

**Key risks (retired).** Diff locus (`WK JUN 29 · SATURDAY`) needed the plan anchor joined client-side from `GET /plan/current` (done in PR-C). Receipt persistence across remounts had to come from the timeline's committed representation, not ephemeral state (satisfied by DEC-091's persisted `loggedRun`, PR-A + PR-D).

### Slice 4 — Log & Log Book

**Requirements:** [`./slice-4-log-logbook.md`](./slice-4-log-logbook.md)

**Acceptance — "I can…"**

- [ ] …see the prescribed banner on `/log` for the selected date (including back-dates), and no banner when nothing is prescribed.
- [ ] …log with the two large numeric cells, see derived pace once both parse, pick completion via segmented control, and expand MORE DETAILS for metrics.
- [ ] …read `/history` as week-grouped ledger rows with client-side aggregates, status tags, ON-PLAN markers, prescribed titles, right-column stats, and dimmed skipped rows.
- [ ] …expand N SPLITS inline (restyled, lazy, conditional HR column) and LOAD OLDER.

**Scope.** **Backend (one PR):** `GET` prescribed-slot-for-date reusing `PlanCalendar.ResolveSlot` + the existing prescription resolver; `IsOnPlan` + nullable prescribed-title on the history-row DTO (DEC-089 D5) + codegen. **Frontend:** `/log` restyle (DEC-075 string-backed fields and unit gating unchanged), `/history` ledger recomposition + client aggregates + derived pace.

**Key risks.** The on-plan/title exposure narrowly amends the deliberate MVP-0 snapshot-withholding — display-scoped fields only, snapshot stays private (DEC-089 D5). Unit-preference conversion must keep applying at every new render site.

### Slice 5 — Onboarding

**Requirements:** [`./slice-5-onboarding.md`](./slice-5-onboarding.md)

**Acceptance — "I can…"**

- [ ] …complete the redesigned intake: units first, `00 — IN YOUR OWN WORDS` narrative, numbered rule sections 01–05 with radio-right goal rows, conditional race section, 2×2 fitness grid, 7-day toggle chips, fine-print switches, per-section ADD DETAIL where a nuance field exists.
- [ ] …submit and watch the BUILDING YOUR PLAN surface (clay progress + mono line) until the plan lands.
- [ ] …have my narrative read verbatim by the plan-generation prompt (verified via the eval suite in Replay after re-record).
- [ ] …still resume mid-intake, rotate idempotency keys, and reseed on units change.

**Scope.** **Backend PR:** one narrative free-text field through the answers DTO → canonical record → `OnboardingView` → `AnswerCaptured` → `ContextAssembler` prompt injection; DEC-074 hash-manifest regen + targeted plan-gen fixture re-record (funded key); codegen. **Frontend PR(s):** full recomposition (field set already matches the wire — verified); adopts the Slice 0 plan-building surface on submit (regenerate wiring lands in Slice 6).

**Key risks.** The only LLM-touching change in the cycle — isolate it in its own PR so the re-record blast radius is contained. Narrative reaches the prompt unsanitized exactly like the existing nuance fields (known caveat, unchanged posture). THE RACE section gets no ADD DETAIL (no nuance field exists — DEC-089 D7).

### Slice 6 — Settings & Auth

**Requirements:** [`./slice-6-settings-auth.md`](./slice-6-settings-auth.md)

**Acceptance — "I can…"**

- [ ] …see Settings as rule sections: THE PLAN (current-plan line from existing plan data + restyled regenerate flow with the BUILDING surface), APPEARANCE and UNITS segmented controls, ACCOUNT with my email and a working SIGN OUT, and the mono version footer.
- [ ] …sign in on the poster auth screen (58px wordmark, mono tagline, eye-toggle password) and register on its START HERE mirror.
- [ ] …sign out and land on login with all per-user caches cleared, in every open tab.

**Scope.** Settings recomposition (all data already on existing endpoints) + regenerate adoption of the Slice 0 plan-building surface; first-ever SIGN OUT wiring (existing unused `useLogoutMutation` + logout broadcast + **`resetApiState()` cache clear** — pulled forward, see Captured); auth poster + register + eye toggle; version footer as a build-time constant (DEC-089 D8). The auth PR depends only on Slice 0 and may be scheduled early as a self-contained win.

**Key risks.** Sign-out is net-new user-reachable behavior, not a restyle — it needs its own tests (cross-tab broadcast, cache reset, guard redirect).

### Slice 7 — States, Light Pass & Cycle Close

**Requirements:** [`./slice-7-states-close.md`](./slice-7-states-close.md)

**Acceptance — "I can…"**

- [ ] …see shaped skeletons (1.2s pulse) on every loading surface and the CAN'T REACH THE COACH failure surface wherever a query dies — no generic "Something went wrong" pages remain.
- [ ] …tab through every screen with the 2px clay focus ring; every interactive target is ≥44px; every animation is motion-reduce-paired.
- [ ] …flip every screen to light mode and see the 4a/5a–5f daylight designs, contrast-gated.
- [ ] …see the full Playwright suite green against the redesigned UI, and the closing funded-key live pass recorded — including surface verification of the F-LIVE-1/F-LIVE-2 fixes (the MVP-0 done-gate).

**Scope.** The cross-cutting sweep the per-surface slices can't self-certify: skeleton/failure coverage audit, a11y + hit-target + motion-reduce audit, full light-mode pass, contrast re-verification, e2e suite consolidation, the closing live pass + findings triage into this file.

**Key risks.** This slice is the backstop — anything the surface slices under-delivered lands here as findings, so keep it a real audit, not a rubber stamp.

---

## Architecture Additions

### Frontend after this cycle

```
frontend/src/
  # fonts arrive via @fontsource/* static per-weight packages (R-086) — no vendored assets/fonts/ dir
  components/ui/                   # restyled shadcn primitives + segmented-control
  app/modules/
    app/                           # + tab-shell layout, TabBar, Wordmark, building-surface, skeletons, failure surface
    plan/                          # home recomposed as Today (hero, DayGrid, WeekProgress, CoachPreview, PhaseBlockBar, UpcomingRow)
    coaching/                      # CoachChat on /coach; TurnCard variants, LogDraftCard states, Receipt, date dividers
    logging/                       # /log restyle (DateChip, PrescribedBanner, derived pace); /history LedgerRow
    onboarding/                    # numbered-section recomposition + narrative field
    settings/                      # rule sections + ACCOUNT
    auth/                          # poster login/register
```

### Backend after this cycle

Four additive deltas, all in existing modules: a prescribed-slot GET in `Modules/Training` (Slice 4), two display fields on the history-row DTO (Slice 4), target-event fields on the plan projection (Slice 2), and one narrative field through the onboarding answer path + `ContextAssembler` (Slice 5). No new modules, no event-model changes beyond the additive `AnswerCaptured` field, no migration risk. Every wire change rides the full codegen chain (Release build → `swagger.json` → `npm run codegen` → barrel hand-edit → drift gate); wire-changing slices regenerate serially — never on concurrent branches, or the regenerated barrel conflicts.

---

## Testing Strategy

- **Per-slice:** Vitest specs updated in the same PR as the surface they pin; behavior-pinning specs (streaming, confirm, idempotency, guards, resume) must keep passing unmodified wherever the contract is "preserve". New client derivations (week/phase math, week-grid join, digest summary composition, derived pace, week aggregates) each get focused unit tests.
- **E2E:** per-slice realignment stays **thin** — smoke/nav-level fixes only, so the same journeys aren't rewritten three times as surfaces recompose; the full journey-suite rewrite happens once, in Slice 7's consolidation. Selectors preserved where possible (`data-testid`s stable unless a component is deleted).
- **Contrast:** `check-contrast` extended in Slice 0 gates every later PR.
- **Evals:** untouched except Slice 5's narrative-field re-record (DEC-074 manifest + targeted plan-gen fixtures, funded key, Replay-verified) — the memory/`docs` targeted re-record procedure applies.
- **Live pass:** one closing funded-key pass (Slice 7) covering the loop end-to-end on the new UI; doubles as the MVP-0 F-LIVE done-gate surface verification.

## Roadmapping Hygiene

Identical to the MVP-0 cycle plan: Tier 1 `ROADMAP.md` Status mirrors this doc's Status; Tier 2 is this doc; Tier 3 per-slice requirements live beside it; specs are gitignored working-tree artifacts; per-PR quality cycle (deep-review + full CodeRabbit addressment) applies to every PR; commit/branch/PR conventions unchanged.

## When Agents Encounter Unknowns

The baseline rule is `CLAUDE.md` § Research Protocol; the prompt template and handoff protocol are inherited verbatim from `docs/plans/mvp-0-cycle/cycle-plan.md` § When Agents Encounter Unknowns.

Pre-flagged for this cycle:

- **Self-hosted fonts (R-086 — RESOLVED 2026-07-07).** Integrated with errata (the artifact's `@fontsource-variable/*` primary recommendation does not exist on npm; its static-package fallback is adopted). Architecture locked in Slice 0 D4; artifact at `docs/research/artifacts/batch-32a-self-hosted-fonts-vite-react.md` — read the errata block before relying on its § 1/§ Recommendations.
- **Safe-area / fixed-bar viewport handling (Slice 1).** `env(safe-area-inset-bottom)` + `viewport-fit=cover` + mobile-Safari keyboard interplay with the pinned composer. Pure CSS, no package — verify empirically at spec time; escalate to a research prompt only if the empirical pass fails.
- **SegmentedControl primitive (Slices 0/4/6).** No shadcn primitive exists. Prefer composing existing Radix radio-group; adding `@radix-ui/react-toggle-group` is a bounded dependency choice an implementing session may make with a one-line justification — it is shadcn-standard, not novel. Anything beyond that (a11y pattern doubts, roving-tabindex questions) → research prompt.
- **Anything the design wants that the wire lacks** beyond the four named deltas → stop and check this plan's gap decisions (DEC-089) before inventing an endpoint; if genuinely new, research-prompt it.

## Relationship to MVP-0

MVP-0's build/fix scope closed with the F-LIVE fix PRs #271/#272 merged 2026-07-07 (see `docs/plans/mvp-0-cycle/cycle-plan.md`). The one outstanding MVP-0 item — the funded-key live-pass done-gate for DEC-087/DEC-088 — is **deliberately deferred into this cycle's closing live pass** (user decision 2026-07-07: redesign first). If anything forces an earlier live verification, that standalone gate can still run from `docs/plans/mvp-0-cycle/mvp-0-close-live-pass-fixes.md` § Verification at any time.
