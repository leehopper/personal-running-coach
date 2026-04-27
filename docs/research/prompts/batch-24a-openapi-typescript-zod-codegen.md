# Research Prompt: Batch 24a — R-071

# OpenAPI → TypeScript + Zod Codegen for an ASP.NET Core 10 + React 19 + RTK Query Stack (Swashbuckle producer, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a 2026 monorepo with an ASP.NET Core 10 backend (Swashbuckle-generated OpenAPI 3.x document at `/swagger/v1/swagger.json`) and a React 19 + TypeScript-strict + Vite + RTK Query frontend, what is the canonical contract-codegen pipeline that produces (a) TypeScript types and (b) runtime Zod schemas from the live OpenAPI document, with (c) committed output for PR-reviewability and (d) a CI drift-check that fails if generated files are out of date?

Compare the leading 2026 toolchains across the criteria below; pick a default plus one fallback. Recommend exact package names, versions known to work on Vite 6 + React 19 + TS 5.x, and the build-script wiring that makes generation a `npm run build` prerequisite without slowing dev-mode `npm run dev`.

### Sub-questions the artifact must answer

1. **Tool space.** Among `openapi-typescript`, `openapi-zod-client`, `orval`, `kiota`, `NSwag`, `swagger-typescript-api`, `openapi-fetch`, `@7nohe/openapi-react-query-codegen`, and any 2026 entrants — which combination produces both static types AND runtime Zod schemas, with first-class RTK Query compatibility, and is actively maintained?
2. **OpenAPI source.** Should the frontend pull the spec from a running backend (`http://localhost:5xxx/swagger/v1/swagger.json`) or from a checked-in file generated at backend build time (Swashbuckle CLI: `dotnet swagger tofile`)? What's the trade-off in CI ergonomics, monorepo coupling, and deploy-time drift detection?
3. **Anthropic structured-output schema compat.** Pattern B (DEC-058) emits Anthropic constrained-decoding schemas via `Microsoft.Extensions.AI.AIJsonUtilities.CreateJsonSchema`. The discriminator-with-nullable-slots shape must round-trip through OpenAPI → Zod cleanly; no codegen tool can collapse, drop, or rename properties. Which tools handle nullable unions and `additionalProperties: false` faithfully?
4. **Drift detection.** What's the canonical 2026 pattern for CI to fail when the committed generated files don't match the live spec? Compare `npm run codegen --check`-style diff scripts, `git diff --exit-code` post-regen, OpenAPI-spec-hash sentinels, and tool-native drift detectors.
5. **Migration path.** Slice 1 ships ~12 hand-maintained Zod schemas under `frontend/src/app/api/**` and `frontend/src/app/modules/**/models/**`. What's the lowest-risk migration order — endpoint-by-endpoint cutover, or whole-frontend swap behind a feature flag? Is there a tool-supported "diff old vs new schema" pre-flight?
6. **Generated-file ergonomics.** Generated Zod often has noisy type names (`schemas.OnboardingTurnResponseDto` → reflected as a long deep path). What's the 2026 conventional structure — barrel exports, namespaced re-exports, scoped renames? Show what a typical RTK Query slice looks like AFTER cutover.
7. **Anthropic / structured-output edge cases.** The `JsonDocument`-typed properties in `OnboardingTurnResponseDto.AssistantBlocks`/`.UserBlocks` (Slice 1's known antipattern, deferred to Slice 4) currently land in OpenAPI as untyped `object`. How should the codegen tool handle that today? Is there a pragma / OpenAPI extension that produces a usable typed shape until the JsonDocument-in-DTO antipattern gets fixed?
8. **Bundle size.** RTK Query slices currently use raw `fetch`. The Zod runtime parse adds bytes. Quantify expected delta on a representative endpoint (~20 fields). Which tools tree-shake unused schemas?
9. **Pre-existing precedents.** What 2026 example monorepos demonstrate the recommended toolchain end-to-end with ASP.NET Core 10 + React 19 + RTK Query + Vite 6? GitHub stars / activity recency.

## Context

I'm planning Slice 1B of the MVP-0 cycle for RunCoach, an AI running coach. Slice 1 (Onboarding → Plan) shipped four end-to-end bugs caught only by Playwright debugging:

1. `JsonDocument` embedded in a DTO bypassed the controller's camelCase formatter, shipping PascalCase to the frontend and breaking Zod parses.
2. RTK Query mutation tag invalidation wiped the live transcript on every successful turn (state-machine race; not contract).
3. `OnboardingTurnHandler.SuggestedInputType` is partly server-driven, partly client-derived — an outstanding clarification needed both sides to mirror a fallback rule.
4. `OnboardingProgressDto.Completed` / `.Total` (C# record) drifted from the frontend's `completedTopics` / `totalTopics` Zod schema. No source of truth.

Bugs 1, 3, and 4 are different surfaces of the same root cause: backend and frontend hand-maintain mirrored type definitions with no compile-time, build-time, or CI gate that catches drift. Slice 0's deferred follow-up "Frontend Zod schemas must mirror `RegisterRequest` DataAnnotations; contract-test in shared-contracts" is the same gap surfacing in a different module.

Slice 2 (Workout Logging) introduces a `WorkoutLog.Metrics` JSONB column and several new endpoints. Without this codegen pipeline, those endpoints will produce another generation of hand-maintained Zod schemas, and the next "obvious" wire-format bug will ship in production.

## Why It Matters

The hand-maintenance cost compounds linearly with every new endpoint and quadratically when an endpoint's shape changes (both ends update independently). Slice 2 alone adds ~6 new DTOs. Slice 3 and Slice 4 each add similar counts. By MVP-1 the count is well into double digits, and every drift produces user-visible bugs that only Playwright (or a real user) catches.

The structural fix is industry-standard 2026 practice but the .NET-on-the-backend + React-on-the-frontend variant is less commonly documented than Node-everywhere. Picking the wrong tool here means re-doing the integration in Slice 4 or pre-public-release.

## Deliverables

- **Recommended toolchain** with rationale: exact package names, versions, license posture, maintenance signal (last release date, GitHub activity, who maintains it). One default + one fallback.
- **Build-script wiring** sketch: how `npm run build` invokes codegen, where the OpenAPI spec comes from, what the lockstep with `dotnet build` looks like in CI, what hooks are needed in lefthook (if any).
- **Drift-check pattern** with a runnable shell-snippet shape — what command does CI run, what does it check, how does it fail loudly.
- **Migration plan** for the ~12 existing schemas: order, risk mitigation, rollback path if codegen produces an unworkable shape for a specific endpoint.
- **Edge-case handling** for the `JsonDocument`-typed Pattern-B fields, with an explicit note on whether the antipattern fix should land before or after the codegen cutover.
- **Bundle-size delta** estimate (or an acknowledgement that exact numbers depend on tree-shaking config — at minimum, qualitative comparison of the two recommended tools).
- **Reference precedent** — at least one 2026 monorepo on GitHub demonstrating the recommended pipeline end-to-end on this stack.
- **Gotchas** specific to ASP.NET Core 10's Swashbuckle output, React 19's TS-strict surface, and RTK Query's `injectEndpoints` pattern.

## Out of scope

- Tools that produce only types (no runtime validation) — RTK Query's runtime layer needs Zod to surface contract drift at parse time, not just at compile time.
- Tools that require a custom transformer for every endpoint — the goal is "add an endpoint, get a typed client and Zod schema for free."
- Backend-side OpenAPI generation alternatives (Microsoft.AspNetCore.OpenApi vs Swashbuckle) — the project has Swashbuckle wired and that decision is locked unless the artifact surfaces a hard incompatibility.
- Whether OpenAPI is the right interchange format vs a custom IDL (gRPC, etc.) — out of scope; OpenAPI is locked.

The artifact lands at `docs/research/artifacts/batch-24a-openapi-typescript-zod-codegen.md` and integrates into `slice-1b-hardening.md` plus a new DEC entry locking the tooling choice. The Marten event upcasting question is queued separately as R-072 (or as part of this batch, if the agent prefers a sister artifact `batch-24b`).
