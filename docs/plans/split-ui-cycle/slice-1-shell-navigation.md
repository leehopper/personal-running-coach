# Slice 1 Design: Shell & Navigation

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff § 4 + the tab bar as drawn on every 2x/5x screen. Depends on Slice 0.

## Purpose

Give the app its one navigation system — the fixed bottom tab bar and the tab-shell layout — and carve the `/coach` route out of the home page. Today there is **no shared nav anywhere**: navigation is scattered per-page `Link`s, and Settings has no inbound link at all (URL-only). This slice replaces all of it.

## Locked design decisions

- **D1 — Tab bar contract (handoff § 4).** Five positions: `TODAY /` · `COACH /coach` · center **LOG** action (54px clay circle, raised 26px, opens `/log`) · `LOG BOOK /history` · `SETTINGS /settings`. Fixed to the viewport bottom; labels always visible (no icon-only nav); active = clay icon + label; safe-area padding (`env(safe-area-inset-bottom)`, `viewport-fit=cover` — verify against the existing viewport meta empirically); `aria-current` on the active tab; every target ≥44px.
- **D2 — Tab-shell layout wrapper.** `/`, `/coach`, `/log`, `/history`, `/settings` render inside the shell; onboarding and auth render outside it. `RequireAuth` and `OnboardingRedirectGuard` are **unchanged** — the shell nests inside the existing guard structure (router today lives in `app.component.tsx`).
- **D3 — `/coach` is a mechanical relocation.** `CoachChat` moves off home (it currently mounts mid-home between `TodayCard` and the upcoming list) to the new route with its behavior contract intact: `TranscriptScroller` pin-to-bottom, streaming, retry, confirm flow, Edit→`/log` draft handoff. Turn-kind restyling is Slice 3, not here. The composer pins above the tab bar.
- **D4 — Scattered nav dies here.** Every ad-hoc `Link`/`useNavigate` nav affordance on home/history/settings is removed in favor of the tab bar. In-content navigation that is part of a design contract (e.g. digest → `/coach`, receipt → LOG BOOK) is per-surface work in later slices.
- **D5 — Home interim state.** After relocation, home is the existing plan furniture minus the chat panel until Slice 2 recomposes it. Acceptable mid-cycle (self+family audience).

## Functional requirements

- Tab bar renders on the five shell routes in both themes, with correct active state per route, and never on onboarding/auth.
- Center LOG action navigates to `/log` from anywhere in the shell.
- `/coach` serves the full existing chat experience; deep-linking to `/coach` works; guards redirect exactly as they do for `/` today.
- Composer stays visible above the tab bar while streaming; the transcript scroll region accounts for both fixed bars.
- Every page's bottom padding/scroll region accounts for the fixed bar (no content hidden behind it).

## Quality requirements

- Mobile-Safari keyboard + pinned composer + fixed tab bar interplay verified on a real phone-sized viewport early (the one genuinely fiddly area — cycle plan § Unknowns).
- Playwright specs realigned: navigation flows now go through the tab bar; existing `data-testid`s preserved where components survive.
- Vitest: behavior-pinning CoachChat specs pass unmodified after relocation (the move must not touch their contracts).
- A11y: tab bar is a `nav` landmark with accessible names; focus order sane across shell + content.

## Scope: In

`TabBar` component; tab-shell layout route wrapper; `/coach` route + `CoachChat` relocation + composer pinning; scattered-nav removal; scroll/padding accommodation; test realignment.

## Scope: Out (deferred)

Turn-kind restyling (Slice 3); Today recomposition + digest (Slice 2); any change to guards, streaming, or confirm contracts.

## PR sketch

1. **PR-A** — TabBar + tab-shell layout + scattered-nav removal (+ e2e nav realignment).
2. **PR-B** — `/coach` route + CoachChat relocation + composer pinning (+ its test moves). May merge into PR-A if review size allows.

## References

- Handoff § 4; every 2x/5x design frame's tab bar; § 6 focus/touch rules.
- DEC-089 (cycle decisions); `app.component.tsx` (current router + guards).
