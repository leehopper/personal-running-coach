# Slice 7 Design: States, Light Pass & Cycle Close

> **Design doc — requirements, not a spec.** Parent: [`./cycle-plan.md`](./cycle-plan.md). Design source: handoff § 6 + sheet 4b (states), § 8 (non-negotiables), sheets 4a/5a–5f (daylight). Depends on Slices 2–6. This is the cycle's audit-and-close slice — the backstop for everything the surface slices could not self-certify.

## Purpose

Certify the redesign as a system: every loading/error/empty state designed, both themes complete, the a11y and contrast commitments machine- or checklist-verified, the automated suites consolidated, and the cycle closed with the funded-key live pass that also discharges the outstanding MVP-0 done-gate.

## Locked design decisions

- **D1 — States coverage audit (§ 6, sheet 4b).** Sweep every route for: shaped skeletons (surface-tone blocks, 1.2s pulse, shaped like the incoming layout — spinners only inside buttons); the `CAN'T REACH THE COACH` failure surface ("Your draft is safe on this device." + RETRY outline) wherever a query can die — **no generic "Something went wrong" page may remain**; toast styles (moss success / danger-edge error + RETRY); the plan-building surface on both of its call sites. Surface slices ship their own states; this slice audits and fills gaps as findings.
- **D2 — Interaction audit.** Focus-visible 2px clay outline / 2px offset on every interactive element; hit targets ≥44px; pressed/disabled button states everywhere; every `transition-*`/`animate-*` paired with `motion-reduce` (manual sweep — the build-time lint rule remains a backlog item); line-clamps via `-webkit-line-clamp` only.
- **D3 — Full light-mode pass (handoff § 9 item 2, sheets 4a/5a–5f).** Every screen verified against the daylight designs; `check-contrast` re-verified over the final token usage in both modes; the light-specific values (clay text `#A34A24`, marker border `#C05A2E`, button-border `#C9C3B1`, tab bar `#EDE9DC`) confirmed at their designed sites.
- **D4 — A11y non-negotiables re-verified (§ 8).** `role="log"` transcript, `aria-live` regions, form ARIA, wordmark `aria-label="Split"`, safety turns rendered in full, all-caps as CSS-only. Trademark discipline re-verified across every new string ("pace-zone index" / "Daniels-Gilbert", never the V-word).
- **D5 — Suite consolidation.** Playwright: full journey set green against the redesigned UI (register → onboard → plan → log → history → converse → settings), stubs updated, selector inventory reconciled. Vitest: no orphaned specs for deleted components; new shared components covered. Codegen drift gate clean; `npm run lint` zero errors.
- **D6 — Closing live pass = cycle close + MVP-0 done-gate.** One funded-key end-to-end pass on the redesigned UI (Path B host-run stack; `Anthropic:ApiKey` in the `runcoach-api` user-secrets store, funded account): fresh account → form onboarding **with narrative** → plan render → log (form + conversational confirm) → adaptation → safety-free conversation → settings/regenerate spot-check. It explicitly verifies at the surface: (a) the redesign itself, (b) **F-LIVE-1** — a stochastic macro rejection no longer dead-ends onboarding, and (c) **F-LIVE-2** — rendered week-1 cards agree with the meso narrative (per `docs/plans/mvp-0-cycle/mvp-0-close-live-pass-fixes.md` § Verification). Findings triage into the cycle plan's Captured table; pass result recorded in `ROADMAP.md`.

## Functional requirements

- Zero unstyled loading/error/empty states remain (checklist per route × state, committed with the audit PR).
- Both themes complete on all seven screens; System mode follows OS changes live.
- All required CI checks green on the audit PR(s).

## Quality requirements

- The audit is evidence-based: the route×state checklist, the contrast run output, and the live-pass transcript/screenshots are attached to the closing PR / recorded per the live-pass convention.
- Any gap found lands as a Captured-table finding with a disposition (fix-in-slice-7 vs backlog), not silently patched.

## Scope: In

The four audits (states, interaction, light, a11y/trademark); gap-fixing PRs; suite consolidation; the closing live pass + docs/ROADMAP close-out.

## Scope: Out (deferred)

The motion-reduce build-time lint rule (backlog, unchanged); PWA work beyond shipped icons; any new feature scope discovered — captured, not built.

## PR sketch

1. **PR-A** — states/interaction/light/a11y audit fixes (split if large).
2. **PR-B** — e2e consolidation + close-out docs (ledger row, ROADMAP, cycle-plan Status collapse).

## References

- Handoff §§ 6, 8, § 9 item 2; sheets 4a/4b/5a–5f.
- `docs/plans/mvp-0-cycle/mvp-0-close-live-pass-fixes.md` § Verification (the F-LIVE done-gate being discharged here).
- DEC-063 (motion), DEC-089.
