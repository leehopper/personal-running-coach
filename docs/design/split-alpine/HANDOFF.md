# SPLIT — Frontend Redesign Handoff

Design source of truth: `SPLIT Alpine.dc.html` (screens 2a–2g, coach-turn system 3b, states 4b, light theme 4a).
Baseline recreation of the old UI: `Current UI (Recreation).dc.html`. Repo: `leehopper/personal-running-coach`.

This document tells implementation agents **what** to build. It maps every design decision onto the existing frontend architecture (React 19, Tailwind v4 two-tier tokens, shadcn/radix, RTK Query) without prescribing how to refactor.

---

## 1. Brand

- **Name:** SPLIT (working name — trademark search pending). Rationale: a split is the number a runner answers to; the `/` mark is lifted from pace notation (`4:12/km`).
- **Wordmark:** `SPLIT` in Barlow Condensed 800, letter-spacing 0.03–0.04em, ink/bone, followed by a clay `/` with no gap. Never letterspace the slash away from the T. Sizes used: 20–22px (headers), 58px (auth poster).
- **Voice (the coach is a hard-ass, not a jerk):**
  - Concise, imperative, specific. Numbers over adjectives. No exclamation marks, no emoji, no pep-talk.
  - Do: "This week drops 30 → 26 km. Don't miss it twice."
  - Don't: "Great job!! Let's crush next week! 💪"
  - Safety turns are the exception: plain, warm, unhurried, zero coach-persona edge.
- All-caps strings are a **presentation** concern (CSS `text-transform` / label styling) — keep source copy sentence case where feasible.

## 2. Design tokens

Keep the existing two-tier architecture (primitive tier → semantic tier → `@theme inline`). Replace Catppuccin with the Alpine ramp. **Dark becomes the `:root` default; light is the `.light` override** (inverts the current `.dark` pattern — update `theme-provider` accordingly; "System" still follows `prefers-color-scheme`).

### Primitive tier — dark (default)
| Token | Hex | Role |
|---|---|---|
| `--alp-bg` | `#10140F` | page background |
| `--alp-surface` | `#181E16` | cards, coach turn cards |
| `--alp-raised` | `#212819` | pressed/selected surfaces, user chat bubble |
| `--alp-input` | `#131812` | input fills, tab bar bg (`#131812`) |
| `--alp-hairline` | `#2A3326` | 1px borders/dividers |
| `--alp-bone` | `#EDE8DB` | primary text, 2px section rules |
| `--alp-muted` | `#A9A594` | secondary text |
| `--alp-faint` | `#6F6D5E` | decorative-only labels (fails AA — never for essential text) |
| `--alp-clay` | `#D06A3B` | accent fills, active nav, left-edge rules |
| `--alp-on-clay` | `#10140F` | text/icons on clay |
| `--alp-moss` | `#7E8F5A` | done/positive |
| `--alp-danger` | `#C4554D` | destructive, red safety tier |
| `--alp-amber` | `#D9A441` | amber safety tier |

### Primitive tier — light (`.light`)
`bg #F3F0E7 · surface #EAE5D7 · raised/segment #E7E2D2 · input #EFEBDD · hairline #D6D1C1 (btn-border #C9C3B1) · ink #1C2418 · muted #5B5C4E · faint #8B897A · clay fill #D06A3B (unchanged) · clay TEXT #A34A24 · clay current-marker border #C05A2E · moss #5C6B3F · tab bar #EDE9DC`.

### Semantic mapping (shadcn slots)
`--background→bg · --foreground→bone/ink · --card→surface · --muted→raised · --muted-foreground→muted · --primary→clay · --primary-foreground→on-clay · --border→hairline · --input→hairline · --ring→clay · --destructive→danger · --warning→amber`. Add two project slots: `--positive` (moss) and `--rule` (bone/ink — the 2px section rule color).

### Geometry
- Radius: 4 (day cells/tags) · 6 (chips) · 8 (buttons, inputs, cards-lite) · 10 (turn cards) · 999 (pills). Phone-safe: no radius > 12 inside screens.
- Rules: section openers are **2px solid `--rule`**; data dividers 1px `--border`. This replaces most card-with-shadow chrome — surfaces are flat; elevation is rare.
- Spacing: 4-base scale; screen gutter 22px; section gap 20–22px.
- Contrast (dark): bone/bg 14.2:1 · muted/bg 6.6:1 · ink-on-clay 6.9:1 · clay text on bg 4.6:1 → clay text only ≥12px semibold. `--alp-faint` decorative only.

## 3. Typography

Google Fonts: **Barlow Condensed** (500–800), **Barlow** (400–600), **IBM Plex Mono** (400–600). Self-host for production.

| Role | Spec |
|---|---|
| Display / workout title | Barlow Condensed 700, 36–40px/0.95, uppercase |
| Screen title | Barlow Condensed 700, 30px, uppercase, +0.02em |
| Section label | Barlow Condensed 600, 13px, uppercase, +0.18em, above a 2px rule |
| Big numerals (distance/pace/reps) | Barlow Condensed 700, 22–30px, `white-space: nowrap` |
| Body / chat | Barlow 400, 14–15px/1.5–1.55 |
| Row title | Barlow 500–600, 15px |
| Data label / eyebrow | IBM Plex Mono 500, 9.5–11px, uppercase, +0.06–0.1em |
| Data values in rows | IBM Plex Mono 500, 10.5–12px |
| Buttons | Barlow Condensed 600–700, 13–18px, uppercase, +0.1–0.14em |

Rule: **numbers are always Barlow Condensed; labels are always mono.** `font-variant-numeric: tabular-nums` on mono data columns.

## 4. IA & navigation

- **Bottom tab bar** (new component, fixed, replaces all scattered nav links): `TODAY /` · `COACH /coach (new route)` · center **LOG** action (54px clay circle, raised 26px; opens `/log`) · `LOG BOOK /history` · `SETTINGS /settings`. Active = clay icon+label; labels always visible; safe-area padding.
- **Coach gets its own route** `/coach`: the full timeline (currently `CoachChat` embedded mid-home) moves here. Composer is pinned above the tab bar.
- **Home (`/`) becomes "Today"**, order: header (wordmark + `WEEK N OF 12 — PHASE`) → today hero → THE WEEK (progress + 7-day grid) → FROM YOUR COACH (preview digest, taps to `/coach`) → UP NEXT (rest-of-week rows) → THE BLOCK (12-cell phase bar + upcoming week rows). Replaces `MacroPhaseStrip` + `MesoWeekBlock` card grid.
- Onboarding and auth render outside the tab shell. Existing route guards (`RequireAuth`, `OnboardingRedirectGuard`) unchanged.

## 5. Screen specs (deltas vs current code)

### 5.1 Today (`home.page.tsx` recomposition) — design 2a
- **Hero** (from `TodayCard`/`MicroWorkoutCard`): eyebrow `WEDNESDAY, JULY 8 — ON THE SCHEDULE` (clay, condensed) → title (display) → one-sentence workout summary composed from segments + coaching note (plain Barlow, muted) → 3-cell stat band separated by hairlines: distance / pace range (nowrap!) / reps or duration → `LOG RUN` (primary) + `DETAILS` (secondary; expands full segment list — reuse `MicroWorkoutSegmentRow` data).
- **Rest day variant** (`slotType !== 'Run'`): title `REST DAY`, summary "Recovery is training.", next-workout line; no LOG RUN, week grid still shown.
- **THE WEEK**: `N.N/NN.N KM` + 7 day cells: done = moss fill + check; today = 2px clay outline; planned run = raised+hairline; rest = surface flat. Data: meso day slots + week's logs.
- **Coach preview digest** — hard contract (design 3a): shows only the latest exchange; user line 1-line ellipsis; coach text `-webkit-line-clamp: 3`; if latest turn is an adaptation → one-line headline card (`PLAN ADJUSTED` + server `summary`); empty state + two suggestion chips. Whole module navigates to `/coach`. Composer stub here is a fake input that focuses the real composer on `/coach`.
- **THE BLOCK**: right-aligned goal chip (`10K — OCT 3` from target event); 12 grid cells (current = clay, this phase = raised+hairline, future phases = surface/dimmer); phase span labels under; upcoming week rows `WK NN · summary · vol KM` with `DELOAD` outline-moss tag when flagged.

### 5.2 Coach (`/coach`, from `CoachChat`) — designs 2b + 3b
Keep: `TranscriptScroller` behavior (role="log", pin-to-bottom unless scrolled), stream retry affordance, confirm mutation flow, `Edit → navigate('/log', {state:{draft}})`.
Turn kinds (**UI owns 100% of layout; model owns 0%; no markdown rendering**):
1. **User turn** — right bubble, `--alp-raised`, radius 10/10/4/10, max-w 85%, pre-wrap, break-word, no clamp. Mono meta `YOU · HH:MM` below.
2. **Coach text turn** — no bubble: mono label `COACH · HH:MM` (clay) + plain bone text. Streaming shows clay block-cursor; on stream death: "That reply didn't go through." + RETRY.
3. **Adaptation turn** (`AdaptationTurnDto`) — nudge: plain coach line, no card. Restructure: surface card, 2px clay left edge, label `PLAN ADJUSTED`, LLM explanation (pre-wrap), `WHAT CHANGED ▾` collapsed by default → diff rows rendered from `PlanAdaptationDiffDto` (never parsed from prose): mono locus (`WK JUN 29 · SATURDAY` / `… · VOLUME`) + `before → after` line with clay arrow.
4. **Safety turn** (`SafetyTurnDto`) — amber: 3px amber left edge, heading `WORTH A PROFESSIONAL LOOK`; red: 3px danger edge, tinted bg `#1C1614`, heading `STOP — GET SEEN`. Content renders **in full — never clamped, collapsed, or crowded by other CTAs**. Tier drives only accent + heading.
5. **Log draft card** (`StructuredLogDraft` via `CoachCard`) — `LOG THIS RUN?` + 2×2 condensed value grid (DISTANCE/TIME/DATE/STATUS, status in moss) + `ON-PLAN — <workout>` mono line when prescription matched + CONFIRM (primary) / EDIT (secondary) / CANCEL (ghost). States: **partial parse** (missing value renders `—` + clay `MISSING` tag, CONFIRM disabled at 35%, EDIT emphasized with clay border); **saving** (buttons locked, card dims, `SAVING…`); **success** (card collapses to persistent one-line receipt: moss check + `LOGGED — 9.2 KM · 41:00 · JUL 8` + `LOG BOOK →`); **failure** (card stays open, error toast, retry reuses idempotency key — existing DEC-077 behavior).
- Date dividers: mono `TUE JUN 30` between hairlines; `TODAY — WED JUL 8` for today.
- Composer: 48px input + 48px clay square send (arrow-up icon), pinned above tab bar. Placeholder: "Ask, or describe a run to log…".

### 5.3 Log (`/log`, `LogPage`/`LogForm`) — designs 2c + 3d
- Header `LOG RUN` + **tappable date chip** (calendar icon + `WED, JUL 8` + chevron → native date input; back-dating preserved).
- Sub: "Record what you actually ran — the plan adapts to the truth, not the intention."
- **Prescribed banner** (new): clay square marker + mono `PRESCRIBED — THRESHOLD INTERVALS · 9.0 KM · 4:00–4:30/KM`, from the plan's slot for the selected date; hidden when none.
- DISTANCE / TIME as two large numeric cells (condensed 30px, unit suffix mono, `inputMode="decimal"` — keep DEC-075 string-backed fields and unit-aware schema).
- Derived **PACE** row (display-only) once both fields parse.
- COMPLETION segmented control (was radios): COMPLETED (clay fill) / PARTIAL / SKIPPED.
- **HOW DID IT GO?** label + helper: "What actually happened — especially where it differed from the plan. The coach adapts to what you write here." Placeholder: "Cut to 3 reps, moved to the treadmill, calf felt tight on the last k…"
- MORE DETAILS collapsible row (`RPE · HR · ELEVATION` mono hint) → existing metric fields.
- `SAVE RUN` 54px primary; disabled until schema-valid; unit-preference gating and error/retry behavior unchanged.

### 5.4 Log Book (`/history`) — design 2d
- Header `LOG BOOK` + mono `EVERY SPLIT ON RECORD`.
- Week groups: condensed `WEEK OF JUL 6` + mono aggregate `15.2 KM · 2 RUNS` (client-side sum; `· 1 SKIP` when present).
- Entries as **ledger rows** (not cards), 1px hairline separated: left day numeral (condensed 26) over mono weekday; middle title + status mono tag (COMPLETED moss / PARTIAL amber / SKIPPED danger) + note snippet or metrics line; right column: `9.2 KM / 41:00 / 4:27 /KM`. Skipped rows dim to 75%, stats `—`.
- `N SPLITS ▾` inline expander (existing splits table restyled: mono, hairline rows). `LOAD OLDER` full-width secondary.

### 5.5 Onboarding (`/onboarding`) — designs 2e + 3c
- Title `TELL ME WHAT WE'RE WORKING WITH` + "Answer straight. The plan is only as honest as you are."
- UNITS segmented first (keeps DEC-086 units-before-distances).
- **`00 — IN YOUR OWN WORDS`** (new): always-visible textarea directly under units. Placeholder: "Coming back from a calf strain. 10K in October. Tuesdays are impossible, and I hate treadmills…" Helper: "The coach reads this first. Plain words beat perfect forms — the form below keeps the numbers honest." Submits with the form as the intake's narrative field (extend the answers payload/schema with one free-text field, read verbatim by prompts like the per-topic nuance fields).
- Numbered rule sections: `01 THE GOAL` (option rows, radio-right; race selection reveals `02 THE RACE`) · `02 THE RACE` (name, distance+date 2-col, goal time) · `03 WHERE YOU'RE AT` (2×2 numeric grid + mono reassurance "A recent race sharpens your pace zones. No race — no problem.") · `04 YOUR WEEK` (run days + session length, 7-day toggle chips) · `05 THE FINE PRINT` (switches: current injury → reveals required description; hard-workouts; trails) + `+ ADD DETAIL` per-section collapsibles (existing nuance fields).
- CTA `BUILD MY PLAN` + mono "The coach drafts 12 weeks in about 30 seconds." Building state: see §6.
- Keep: units-change reseed/remount flow, resume hydration, idempotency-key rotation.

### 5.6 Settings — design 2f
Rule sections: THE PLAN (current plan line `Generated Jun 29, 2026 · 12 weeks · <goal>` + `REGENERATE PLAN` clay-outline + mono warning "Replaces your current plan. The coach starts fresh from your log book."; regenerate dialog keeps 500-char intent + counter, restyled) · APPEARANCE (DARK/LIGHT/SYSTEM segmented) · UNITS (KILOMETERS/MILES segmented) · ACCOUNT (new: signed-in email + SIGN OUT secondary) · footer `SPLIT 0.9.0 — MVP` mono.

### 5.7 Auth — design 2g
Poster layout: SPLIT/ 58px + 64px rule + mono tagline `THE PLAN ADAPTS. YOU DO THE WORK.` → EMAIL / PASSWORD (48px fields, password eye toggle) → `SIGN IN` → "First run here? `CREATE ACCOUNT →`". Register mirrors it (`START HERE` heading, password-rules helper in mono). Leave vertical room under the primary button for the OAuth fast-follow; do not design against it now.

## 6. States & feedback (design 4b)
- **Buttons:** pressed = darken (`#B0532A`) + scale 0.98; disabled = 35% opacity. Secondary pressed = raised fill.
- **Inputs:** focus = clay border + 3px `rgba(208,106,59,0.22)` ring; error = danger border + mono danger message below (keep `role="alert"` FormMessage wiring).
- **Loading:** skeleton blocks (surface tones, 1.2s pulse) shaped like the incoming layout; spinners only inside buttons. **Plan building:** full surface `BUILDING YOUR PLAN` + clay progress + mono line — used on onboarding submit and regenerate.
- **Toasts:** success = moss check square + mono `RUN LOGGED — 9.2 KM`; error = danger left edge + `COULDN'T SAVE. NOTHING LOST.` + RETRY. Restyle sonner via CSS vars.
- **Failure surface** (query errors): `CAN'T REACH THE COACH` + "Your draft is safe on this device." + RETRY outline. Replaces generic "Something went wrong" pages.
- **Focus/touch:** 2px clay outline, 2px offset, all interactive elements; hit targets ≥44px.
- **Motion:** 150–200ms ease-out, transform/opacity only, every animation paired with `motion-reduce`. Line-clamps use `-webkit-line-clamp`.

## 7. Component inventory (new/changed)
`TabBar` · `SectionRule` (2px rule + condensed label + right slot) · `MonoLabel` · `StatBand`/`StatCell` (hairline-separated condensed numerals) · `SegmentedControl` (completion, units, theme) · `DayGrid` (7 cells, 4 states) · `WorkoutHero` (+ rest variant) · `WeekProgress` · `CoachPreview` (4 states) · `PhaseBlockBar` (12 cells + labels) · `UpcomingRow` / `LedgerRow` · `TurnCard` (adaptation/safety variants) · `LogDraftCard` (4 states) · `Receipt` · `Toggle` (replaces checkboxes in onboarding) · `DateChip` · `PrescribedBanner` · `Wordmark`. Existing shadcn primitives (button/input/form/collapsible/dialog) restyle via tokens + cva variants rather than rewrite.

## 8. Accessibility & non-negotiables
- Preserve: `role="log"` transcript semantics, `aria-live` status regions, form ARIA wiring, trademark-clean labels ("pace-zone index" / "Daniels-Gilbert", never the V-word), safety turns rendered in full.
- All-caps display text needs `aria-label` only where letter-spacing hurts screen-reader parsing (wordmark: label "Split").
- Contrast commitments in §2 are machine-checkable — extend `check-contrast.ts` to the Alpine pairs.

## 9. Open items
1. Trademark/domain search on SPLIT; fallbacks: THRESHOLD, CADENCE, VERST.
2. Full light-mode pass across all screens (4a establishes the mapping; Today is done).
3. App icon / favicon (clay slash on ink).
4. OAuth buttons (fast-follow slot reserved on auth).
5. Splits expander + regenerate dialog high-fi frames if implementation wants them (behavior specced above).
