# Slice 1B Requirements: Pre-Slice-2 Hardening

> **Requirements only — not a specification and not an implementation plan.** Captures the "what" at a level that survives implementation discoveries. The "how" is written as a spec in a fresh session at build time. Parent: `docs/plans/mvp-0-cycle/cycle-plan.md`.

## Purpose

Close out the structural fragilities surfaced during Slice 1's onboarding-flow debugging before Slice 2 builds on top of them. Slice 1's four end-to-end bugs (PascalCase wire leak, RTK tag-invalidation race, multi-select clarification dead-end, `Completed`/`Total` field-name drift) were patched at the call site, but three of the four were symptoms of the same root cause: **backend and frontend hand-maintain mirrored type definitions with no shared source of truth**, and there is no compile-time, build-time, or CI gate that catches drift before it reaches the user.

Slice 2 introduces a `WorkoutLog.Metrics` JSONB column and several new endpoints. Without contract codegen, those endpoints will produce another generation of hand-maintained Zod schemas, and the next "obvious" wire-format bug will ship in production. Slice 1B's objective is to make that class of bug structurally impossible going forward.

## Functional requirements

- TypeScript types and runtime Zod schemas consumed by RTK Query are generated from the live OpenAPI document, not hand-maintained, with the codegen wired into the frontend build so a missing regen fails CI.
- Marten event payloads have a documented evolution strategy: when an event type adds a property, old streams continue to project without manual data migration, and the strategy is exercised by at least one regression test against a synthetic "old-shape" stream.
- The React app has a top-level error boundary that catches render-time exceptions, logs them with a correlation ID, and renders a recovery affordance instead of a blank screen. The boundary's recovery path is tested via Playwright (forcing a child throw should not crash the shell).
- DI graph regression coverage extends beyond the single `ContextAssemblerDiResolutionTests` guard to the other multi-implementation interfaces wired in Slice 1 — `IIdempotencyStore`, `IPlanGenerationService`, `RegeneratePlanHandler`'s `IDocumentSession` — so a future "most-resolvable-parameters" silent regression cannot ship.
- The Slice 0 deferred follow-up "Frontend Zod schemas must mirror `RegisterRequest` DataAnnotations; contract-test in shared-contracts" closes here, subsumed by the OpenAPI codegen above (the auth schemas come along for free).

## Quality requirements

- Pre-push remains under 5 seconds wall-clock on the reference hardware. The codegen step runs at frontend `npm run build` time, not on every test invocation.
- The codegen output is committed (not a build artifact) so reviewers can see the wire shape in a PR diff. A separate `npm run codegen:check` script verifies the committed files match the live OpenAPI spec; CI fails if they drift.
- The Marten upcasting strategy must be reusable — the same pattern carries Slice 2's `WorkoutLog`, Slice 3's `PlanAdaptedFromLog`, and Slice 4's `ConversationTurnRecorded`. No bespoke per-event migration code.

## Scope: In

- OpenAPI → TypeScript types + Zod schema codegen pipeline. Tooling decision recorded in a new DEC entry. Existing hand-maintained Zod schemas under `frontend/src/app/api/` and `frontend/src/app/modules/**/models/` migrate to the generated forms; legacy file deletions are part of this slice.
- Marten event upcasting layer (one of: `IEventUpcaster<TOld, TNew>` middleware, Marten's built-in `Upcast` registration, or versioned event types with adapter projections — the research prompt below resolves which).
- Top-level React error boundary in `App.tsx` (or the router root). Logs to OTel via the existing telemetry channel. One Playwright test forcing a render-time throw asserts the boundary catches.
- Three additional DI-resolution tests covering `IIdempotencyStore`, `IPlanGenerationService`, and `RegeneratePlanHandler` constructor selection. Use the same `RunCoachAppFactory`-backed pattern as `ContextAssemblerDiResolutionTests`.

## Scope: Out (deferred)

- The `JsonDocument`-in-DTO antipattern fix (replacing `assistantBlocks`/`userBlocks` with typed discriminated records). Deferred to Slice 4 prep — the conversation panel needs typed message blocks anyway, and the rewrite is cheaper amortized across both consumers.
- Server-driven `SuggestedInputType` redesign (eliminating the client-side `pickInputTypeForTopic` mirror). Deferred to Slice 4 prep — the chat-panel state machine will redesign this surface anyway.
- Build-time trademark analyzer (Roslyn rule that flags "VDOT" in `Prompts/*` and API response paths). Deferred to pre-public-release — DEC-042's runtime guard plus the user-facing-string convention is sufficient until a non-builder contributor joins.
- Wolverine LLM-failure error policy (retry queue, structured user-facing error response). Belongs in Slice 2 prep because longer prompts there will increase timeout likelihood.
- `WorkoutLog.Metrics` canonical key constants. Belongs in Slice 2 prep — the slice owns the keys, not Slice 1B.
- Eval cache prompt-hash invalidation guard. Belongs in Slice 2 prep — Slice 2 will iterate prompts more frequently.

## Pragmatic defaults for deferred decisions

- If the codegen tooling research returns more than one viable option, pick the one that produces Zod schemas (not just types) so RTK Query's runtime parse layer has something to call. Bias toward zero-config tooling — adding two npm packages is cheaper than maintaining a custom transformer.
- If Marten's built-in upcast registration covers the common case, use it; only build a custom middleware if the built-in path can't express the projection-side fan-in (multiple stream types that share a payload shape).

## Research to consult before writing the spec

- (new prompt, this slice) `docs/research/prompts/batch-24a-openapi-typescript-zod-codegen.md` — tooling choice for OpenAPI → TS + Zod, codegen build wiring, drift-check pattern.
- (existing) `docs/research/artifacts/batch-22b-anthropic-discriminated-structured-output.md` — Pattern B locks the `OnboardingTurnOutput` schema; codegen must accept the existing JSON schema shape unchanged.
- (existing) `docs/research/artifacts/batch-15c-ef-migrations-testcontainers-xunit-v3.md` — testing pattern reused for the new DI guards.
- Marten docs on event upcasting — verify the `Upcast` registration shape on `StoreOptions` against the 8.32.x release; the cycle plan's deferred Marten-9 upgrade note is informational only.

## Open items for the spec-writing session to resolve

- Exact codegen tool (e.g., `openapi-typescript` + `openapi-zod-client`, NSwag, Kiota, custom). Lock at spec time after the research artifact lands.
- Whether the OpenAPI document is generated at build time from Swashbuckle or from the running API (decision affects CI ergonomics). Default: generate at build time from Swashbuckle's startup, no live API needed.
- Whether the React error boundary's correlation ID propagates back to the backend (request-ID middleware) or stays client-only. Default: client-only for MVP-0; backend correlation is a pre-MVP-1 follow-up.
- Marten upcasting target — does the project introduce a single shared upcast registry, or does each module register its own? Default: per-module registration mirroring the existing module-first organization.

## How this feeds the spec

The spec session inherits the per-PR commit chronology of Slice 1's split (PR #67–#71) as the regression baseline. The four bug-fix commits (`9e56b78`, `27aa4b5`, `6bb46d3`, `ad59625`) name exactly the fragility classes Slice 1B is hardening — every one is a one-line fix that the structural change would have made impossible. The spec must include a non-regression check that maps each pre-fix bug to a contract-codegen or upcaster guard.

`SanitizationOTelTests`'s ad-hoc `Clear`+`LastOrDefault` fix (task #127) is a candidate to revisit during this slice's DI-graph guard work — the broader pattern (`ActivityListener` registered globally with no test-scope filter) recurs across any future OTel test and deserves the same kind of structural fix.

## Relationship to the cycle plan

Slice 1B is a **hardening slice**, not a feature slice. It ships nothing the user can demonstrate ("I can…") because the user-visible surface is unchanged; instead, it removes an entire class of bug from the trajectory of Slice 2 / 3 / 4. Acceptance is the absence of a regression: no contract-drift bug like Slice 1's four can happen the same way again, and the next slice's DTO additions cost zero hand-maintenance.
