# Slice 6 Design: Settings & Auth

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff §§ 5.6, 5.7 + sheets 2f/5e (Settings), 2g/5f (Auth), 5h (regenerate dialog). Settings depends on Slice 1 (tab shell); Auth depends on Slice 0 only.

## Purpose

Recompose Settings into the designed rule sections — including the app's **first sign-out affordance** — and rebuild auth as the brand-poster screens.

## Locked design decisions

- **D1 — Settings sections (§ 5.6).** `THE PLAN`: current-plan line `Generated Jun 29, 2026 · 12 weeks · <goal>` — all three values already on `GET /plan/current` (`GeneratedAt`, `Macro.TotalWeeks`, `Macro.GoalDescription`; no wire change) — + `REGENERATE PLAN` clay-outline + mono warning "REPLACES YOUR CURRENT PLAN. THE COACH STARTS FRESH FROM YOUR LOG BOOK."; the regenerate dialog keeps its 500-char intent + counter, restyled per sheet 5h, and adopts the shared BUILDING YOUR PLAN surface (built in Slice 0) while regenerating (today the dialog just closes and refetches — net-new state wiring). The dead "View previous plan" placeholder is deleted. `APPEARANCE`: DARK/LIGHT/SYSTEM segmented (same `useTheme` wiring). `UNITS`: KILOMETERS/MILES segmented (same API wiring). Footer: mono `SPLIT 0.9.0 — MVP` — a build-time constant injected from the frontend package version (DEC-089 D8; no backend endpoint).
- **D2 — ACCOUNT is net-new behavior, not a restyle.** `SIGNED IN AS <email>` (email already in the auth session state / `GET /auth/me`) + `SIGN OUT` secondary. Sign-out wires the **existing but never-called** `useLogoutMutation` (`POST /v1/auth/logout`), dispatches the logged-out action, posts the cross-tab logout broadcast (the receiver already exists), **and calls `resetApiState()` to purge per-user RTK caches** — the known cross-account cache-leak follow-up (declined PR #174, previously deferred to pre-public release) pulled forward because this slice makes sign-out reachable (cycle plan § Captured). Lands the user on login via the existing guards.
- **D3 — Auth poster (§ 5.7).** Sign-in: SPLIT/ 58px wordmark + 64px 2px rule + mono tagline `THE PLAN ADAPTS. YOU DO THE WORK.` → EMAIL / PASSWORD 48px fields with a password visibility eye toggle (net-new behavior) → `SIGN IN` → "First run here? `CREATE ACCOUNT →`". Register mirrors it: `START HERE` heading, password-rules helper rendered in mono. **Vertical room stays reserved under the primary button for the OAuth fast-follow; nothing else is built for it.** Existing schemas, error mapping, and flows unchanged.

## Functional requirements

- Every section renders per design in both themes; segmented controls post the same values as today's toggles.
- Regenerate: dialog → confirm → building surface → fresh plan rendered; cancel and failure paths intact.
- Sign out: caches purged, all tabs land on login, re-login as a different account shows zero stale data.
- Eye toggle flips input type + accessible pressed state; password managers unaffected.

## Quality requirements

- Sign-out gets real tests: mutation call, broadcast to a second tab, `resetApiState` purge (assert a previously-cached query refetches), guard redirect.
- Auth e2e (register/login journeys) realigned; form ARIA + `role="alert"` errors preserved (AX-01).
- Regenerate intent counter behavior (500-char) pinned unchanged.

## Scope: In

Settings recomposition + regenerate building-surface wiring + ACCOUNT/sign-out (with cache reset); auth poster + register + eye toggle; version footer constant; test realignment.

## Scope: Out (deferred)

OAuth (slot only); account management beyond sign-out (no email change / delete — not designed); any backend change (none needed).

## PR sketch

1. **PR-A** — Settings (sections + regenerate wiring + ACCOUNT/sign-out + tests). Depends on Slices 0 + 1.
2. **PR-B** — Auth poster + register (+ e2e realignment). Depends on Slice 0 only — independently schedulable as an early, self-contained win.

## References

- Handoff §§ 5.6, 5.7, 6; sheets 2f/5e/2g/5f/5h.
- `settings.page.tsx`, `regenerate-plan-dialog.component.tsx`, `auth.api.ts` (`useLogoutMutation`, logout broadcast), `PlanProjectionDto.cs`.
- DEC-089 D8; the cross-account cache-leak history (declined PR #174).
