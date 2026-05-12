# Batch-24a: OpenAPI → TypeScript + Zod Codegen Pipeline for RunCoach

**Status:** Recommendation locked. Default + fallback identified. Migration plan for Slice 1B specified.
**Stack target:** ASP.NET Core 10 / Swashbuckle 10 / OpenAPI 3.0 → Vite 6 / React 19 / TS 5.x strict / RTK Query / Zod v4.

---

## TL;DR

- **Use a two-generator pipeline driven from a committed `backend/openapi/swagger.json` artifact:** `@rtk-query/codegen-openapi` produces the typed RTK Query slice (types + hooks + `injectEndpoints` shape) and **Orval v8 in `client: 'zod'` mode** produces runtime Zod v4 schemas. Both read the same spec file. This is the only 2026 combination that gives you (a) first-class RTK Query output, (b) Zod v4 runtime schemas, and (c) actively maintained, MIT-licensed tooling. The single-tool dream (one codegen for both) does not exist for RTK Query in 2026 — Orval's RTK Query client target was never built, and `@rtk-query/codegen-openapi` deliberately stays runtime-validation-free.
- **Drift gate is a three-line CI step:** `dotnet swagger tofile` → `npm run codegen` → `git diff --exit-code` on the generated directories. Fails loudly when backend DTOs and committed generated files disagree. Add `oasdiff` against `origin/main:backend/openapi/swagger.json` for breaking-change reporting on the spec itself.
- **Migrate endpoint-by-endpoint, not all at once.** Keep the existing hand-maintained Zod schemas in `frontend/src/app/api/**` until each is replaced by an import from the generated barrel. Land the `JsonDocument`-in-DTO antipattern fix (Slice 4) **before** cutover for any endpoint that touches `OnboardingTurnResponseDto.AssistantBlocks/.UserBlocks`; until then those properties surface as `Record<string, unknown>` and the runtime Zod gate does not protect them — exactly the Slice 1 bug #1 root cause, deferred not solved.

---

## Key Findings

### 1. The 2026 tool landscape, scored against your requirements

| Tool | Static types | Runtime Zod | RTK Query | Zod v4 | Maintained | License | Verdict for RunCoach |
|------|---|---|---|---|---|---|---|
| `@rtk-query/codegen-openapi` | ✅ | ❌ | ✅ first-class | n/a | ✅ (in Redux Toolkit monorepo) | MIT | **Half of the pipeline.** Produces the slice. Does not emit Zod; this is by design — see redux-toolkit Discussion #2576 where the recommended pattern is `transformResponse: schema.parse`. |
| **Orval v8** (`@orval/zod` 8.6.x) | ✅ | ✅ | ❌ (no `rtk-query` client target — only `react-query`, `swr`, `vue-query`, `svelte-query`, `angular`, `hono`, `fetch`, `zod`) | ✅ auto-detected from installed version; uses `z.strictObject`/`z.email` on v4 | ✅ active (v8.x March 2026, DeepWiki last indexed 7 March 2026 at orval-labs/orval) | MIT | **Other half of the pipeline.** Run with `client: 'zod'` standalone — generates pure schema file consumed by RTK Query's `transformResponse`. |
| `openapi-typescript` | ✅ | ❌ | ❌ (designed for `openapi-fetch`) | n/a | ✅ (v7 stable, ~11k stars) | MIT | Rejected: types-only violates "Out of scope" — RTK Query needs runtime parse to catch wire drift. |
| `openapi-fetch` | ✅ (via openapi-typescript) | ❌ | ❌ | n/a | ✅ | MIT | Same exclusion. |
| **Kubb** (`@kubb/plugin-zod` 4.x) | ✅ | ✅ (zod v3/v4/mini explicit `version` knob; `mini: true` emits functional API for tree-shaking) | ❌ no RTK plugin (TanStack Query, SWR, Hono, MSW only) | ✅ first-class | ✅ active | MIT (mostly; some AGPL-3.0 components — verify before commercial use) | **Fallback default.** Use if Orval bugs on a specific schema (e.g. enum + minLength regression #3024). Slightly more flexible plugin model. |
| `@hey-api/openapi-ts` | ✅ | ✅ (Zod v3, v4, mini, Valibot, Arktype) | ❌ (TanStack Query plugin is the state layer) | ✅ | ✅ very active (v0.96 Jan 2026, used by Vercel/OpenCode/PayPal) | MIT | **Pivot fallback** if the project later abandons RTK Query for TanStack Query. Best-in-class otherwise. |
| `@7nohe/openapi-react-query-codegen` | ✅ (via Hey API) | ❌ | ❌ TanStack Query only | n/a | ⚠️ slowing — v2.0.0 published, last release ~a month ago per npm | MIT | Out — wrong state layer. |
| `swagger-typescript-api` | ✅ | ❌ | ❌ | n/a | ⚠️ | MIT | Out — no Zod. |
| `kiota` (Microsoft) | ✅ | ❌ | ❌ (generates standalone clients) | n/a | ✅ | MIT | Out — clients-not-slices, no Zod, alien shape to RTK. |
| `NSwag` | ✅ | ❌ | ❌ | n/a | ✅ | MS-PL | Out — C#-centric output paradigm. |
| `openapi-zod-client` | ❌ partial (Zodios types) | ✅ | ❌ (Zodios) | partial | ⚠️ last release ~a year ago at v1.18.3 per npm | MIT | Out — couples you to Zodios runtime. |

**Conclusion:** No single 2026 tool emits both an RTK Query slice and Zod v4 schemas. The canonical pipeline is two tools sharing one spec file. The `transformResponse: schema.parse` pattern (Redux Toolkit discussion #2576) is the documented bridge.

### 2. OpenAPI source: committed build-time artifact, not live URL

**Recommendation: `dotnet swagger tofile` writes `backend/openapi/swagger.json` as a backend post-build target; the file is committed.**

Rationale:
- **Reproducibility.** CI does not need to spin up Kestrel + a DB just to read the schema. The frontend codegen step takes ~2 s instead of waiting on backend startup + healthcheck. This matters for a solo dev on 30–45 min sessions.
- **Reviewability.** Spec changes show up in the PR diff with the C# DTO changes that produced them. CodeRabbit and reviewers see "you added `WorkoutLog.Metrics` and here's the wire shape it ships." Drift becomes a code-review topic, not a runtime mystery.
- **Drift detection.** A live URL gives you "is the running backend in sync?"; the committed artifact gives you "is the *committed* backend in sync with the committed frontend?" The latter is the gate that catches Slice 1 bug #4 (`OnboardingProgressDto.Completed` / `.Total` drift) before merge, not after deploy.
- **Swashbuckle 10 specifics.** With ASP.NET Core 10, `Swashbuckle.AspNetCore` v10 supports .NET 10 and OpenAPI 3.1 (opt-in via `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1`). Default remains 3.0. **Stay on 3.0 for now** — Orval 7.5+ has known regressions on 3.1 nullable forms (issues #1817, #2249, #3269), and openapi-typescript has had nullable-on-3.1 misses through v7.6 (#2144). 3.0 with `nullable: true` is the better-supported wire dialect across the codegen ecosystem in 2026.
- **CLI quirk.** `Swashbuckle.AspNetCore.Cli` historically pinned to older runtimes. For .NET 10 use Swashbuckle.AspNetCore.Cli v10.x; if you must pin an older CLI, set `DOTNET_ROLL_FORWARD=LatestMajor` as an env var on the `<Exec>` MSBuild target (Alex Sikilinda's documented workaround).

The trade-off: pulling from `http://localhost:5xxx/swagger/v1/swagger.json` is fine for *local dev iteration* (`npm run codegen:dev` mode) but should never be the CI source. Use the committed file as the source of truth; let dev mode optionally hit the live URL if convenient.

### 3. Anthropic structured-output / Pattern B (DEC-058) compat

Your discriminator-with-nullable-slots shape (`Microsoft.Extensions.AI.AIJsonUtilities.CreateJsonSchema` output) must round-trip without property collapse, drop, or rename. Findings:

- **`nullable: true` on `$ref` properties** (the common Swashbuckle shape) — Orval issue #1530 documents a known bad-output bug here, fixed in 7.5+. Verify on a representative DTO before merge. Orval 8.x inlines `oneOf`/`anyOf`/`allOf` by default rather than producing extra named types, which keeps your discriminator shape closer to the C# source.
- **`additionalProperties: false`** — Swashbuckle emits this on records; both Orval and Kubb's Zod output respect it via `z.strictObject` (Zod v4) when `override.zod.strict = true`. **Set this explicitly** in your Orval config; otherwise objects default to `additionalProperties: true` and you lose the "extra fields fail parse" gate that surfaces wire drift.
- **Discriminator (`discriminator.propertyName`)** — openapi-typescript handles 1-level discriminators well (Cat/Dog/Pet); 2-level inheritance (Poodle → Dog → Pet) is broken (openapi-typescript #1158). Orval inlines unions, which sidesteps the problem at the cost of slightly noisier type names. Keep Anthropic-output DTOs flat — no 2-level inheritance — and you avoid the bug class entirely.
- **C# nullable reference types ↔ OpenAPI `required`** — Sebastian Chwastek's well-known pattern: in `AddSwaggerGen` call `options.SupportNonNullableReferenceTypes()` plus a `RequireNonNullablePropertiesSchemaFilter` that promotes non-nullable C# properties into the `required` array. Without this, every C# property emits as `nullable: true` and Zod generates `.nullish()` everywhere — making contract drift invisible because everything's optional.

### 4. CI drift detection: the canonical 2026 pattern

Three layers, in order of strictness:

```yaml
# .github/workflows/contract-drift.yml (sketch)
- name: Build backend & emit OpenAPI
  run: |
    dotnet build backend/RunCoach.Api.sln -c Release
    dotnet swagger tofile \
      --output backend/openapi/swagger.json \
      backend/src/RunCoach.Api/bin/Release/net10.0/RunCoach.Api.dll v1
  env: { DOTNET_ROLL_FORWARD: LatestMajor }

- name: Regenerate frontend contract code
  working-directory: frontend
  run: |
    npm ci
    npm run codegen   # runs rtk-query-codegen-openapi + orval

- name: Fail if generated files are out of date
  run: |
    git diff --exit-code -- \
      backend/openapi/swagger.json \
      frontend/src/api/generated/

- name: Detect breaking spec changes (advisory)
  uses: oasdiff/oasdiff-action/breaking@v0.0.46  # SHA-pin in real config
  with:
    base: 'origin/${{ github.base_ref }}:backend/openapi/swagger.json'
    revision: 'HEAD:backend/openapi/swagger.json'
    fail-on: ERR
```

- `git diff --exit-code` is the workhorse — it returns non-zero if regeneration produced any change, mirroring the pattern Orval itself uses for its snapshot tests (`bun run test:snapshots` fails on snapshot mismatch).
- `oasdiff` (450+ breaking-change rule categories, OpenAPI 3.0+3.1 supported, MIT) layers semantic awareness on top: it knows "optional → required" is breaking even when text-diff churn looks small.
- **No need for an OpenAPI-hash sentinel.** The committed `swagger.json` itself is the sentinel; if it doesn't match `dotnet swagger tofile` output, the diff step fails.

### 5. Build-script wiring (frontend `package.json`)

```jsonc
{
  "scripts": {
    "codegen:rtk":  "rtk-query-codegen-openapi openapi-config.ts",
    "codegen:zod":  "orval --config orval.config.ts",
    "codegen":      "npm run codegen:rtk && npm run codegen:zod",
    "codegen:check":"npm run codegen && git diff --exit-code -- src/api/generated",
    "prebuild":     "npm run codegen",
    "build":        "tsc -b && vite build",
    "dev":          "vite"
  }
}
```

Key design points:
- `prebuild` makes codegen a **build-time prerequisite** without slowing `dev`. Vite HMR continues to use whatever generated files exist on disk; you only regenerate when explicitly invoked or when building/CI runs.
- `npm run codegen:check` is what CI calls; pre-commit hook is optional (and probably wrong for a solo dev because it adds latency to every commit on the backend project; rely on CI instead).
- **Lefthook** (recommended over Husky for monorepos per Evil Martians, Steve Kinney 2026): add a `pre-push` hook running `codegen:check` so you don't push a PR that will fail CI:

```yaml
# lefthook.yml
pre-push:
  parallel: true
  commands:
    contract-drift:
      run: cd frontend && npm run codegen:check
```

- **Backend MSBuild target** (in `RunCoach.Api.csproj`):

```xml
<Target Name="EmitOpenApi" AfterTargets="Build" Condition="'$(Configuration)'=='Release' OR '$(EmitOpenApi)'=='true'">
  <Exec Command="dotnet tool restore" />
  <Exec Command="dotnet swagger tofile --output $(SolutionDir)..\openapi\swagger.json $(OutputPath)$(AssemblyName).dll v1"
        EnvironmentVariables="DOTNET_ROLL_FORWARD=LatestMajor"
        WorkingDirectory="$(ProjectDir)" />
</Target>
```

### 6. Migration plan: endpoint-by-endpoint, lowest-risk

You have ~12 hand-maintained Zod schemas. Whole-frontend swap is the wrong move — too many failure modes hit at once, no Playwright signal to localize them. Order:

1. **Land the build wiring without using output.** Generate into `frontend/src/api/generated/`; commit. No imports change yet. CI drift gate goes live.
2. **Pick the lowest-risk endpoint first** — `RegisterRequest` (Slice 0 deferred contract). Two-side mirror, no Anthropic shape, no `JsonDocument`. Replace the hand-rolled Zod schema with `import { registerRequestSchema } from '@/api/generated/auth.zod'`. Keep the old file in git history for one PR cycle for rollback.
3. **`OnboardingProgressDto` (Slice 1 bug #4) second.** The whole point: prove the gate catches `Completed`/`Total` vs `completedTopics`/`totalTopics` drift. After this PR, deliberately rename a field in C# in a throwaway commit and watch CI fail. Document the failure mode.
4. **`SuggestedInputType` (Slice 1 bug #3)** — the fallback rule needs encoding somewhere both sides see. Generated Zod handles the server-driven half; the client-derived half goes into a thin wrapper that imports the generated enum:

```ts
import { suggestedInputTypeSchema } from '@/api/generated/onboarding.zod';
export const resolveInputType = (server: SuggestedInputType | null, ctx: TurnCtx) =>
  server ?? deriveFallback(ctx); // single source of truth, both halves visible
```

5. **`OnboardingTurnResponseDto` last**, and only after Slice 4's `JsonDocument` antipattern fix lands. Reason: until those `JsonDocument` fields become typed records, codegen emits them as `Record<string, unknown>` and you've lost the parse-time gate on exactly the property that caused Slice 1 bug #1 in the first place. Shipping codegen here without the fix is performance-art compliance — it looks like a contract gate but isn't.
6. **Slice 2 (Workout Logging) endpoints are codegen-only from day one.** Six new DTOs, zero hand-rolled Zod. This is the payoff.

**Tool-supported diff-of-old-vs-new pre-flight:** there isn't one out of the box, but a trivial pre-flight is: keep the old hand-rolled schema, parse a fixture with both, log differences. Three lines of Vitest, ~10 minutes per endpoint.

**Rollback path:** generated files are checked in; revert the import-swap commit, restore the hand-rolled schema from git. No tool state to unwind.

### 7. Generated-file ergonomics — what RTK Query slices look like after cutover

Orval's Zod output names schemas by operationId — `getOnboardingTurnResponse` — which is verbose. Use the `output.namingConvention` and barrel re-exports to clean it up:

```ts
// frontend/src/api/generated/index.ts (hand-maintained barrel)
export {
  getOnboardingTurnResponse as onboardingTurnResponseSchema,
  postOnboardingTurnBody    as onboardingTurnRequestSchema,
} from './onboarding.zod';
export type { OnboardingTurnResponseDto } from './rtk/onboarding'; // RTK slice's exported type
```

The RTK slice after cutover, using `injectEndpoints`:

```ts
// frontend/src/app/modules/onboarding/api.ts
import { generatedApi as onboardingApi } from '@/api/generated/rtk/onboarding';
import { onboardingTurnResponseSchema } from '@/api/generated';

export const enhancedOnboardingApi = onboardingApi
  .enhanceEndpoints({
    addTagTypes: ['OnboardingTurn'],
    endpoints: {
      postOnboardingTurn: {
        providesTags: ['OnboardingTurn'],
        transformResponse: (raw) => onboardingTurnResponseSchema.parse(raw),
        // Slice 1 bug #2 fix lives here, NOT in invalidatesTags
      },
    },
  });

export const { usePostOnboardingTurnMutation } = enhancedOnboardingApi;
```

`enhanceEndpoints` is the documented RTK Query pattern (Redux Toolkit codegen docs) for adding tags, `transformResponse`, and overrides on top of generated endpoints — you do not edit the generated file. `injectEndpoints` is reserved for module-scoped *additions* that aren't in the spec at all.

### 8. Bundle size — qualitative; exact numbers depend on tree-shaking

- **openapi-typescript (rejected):** 0 runtime bytes. Pure types.
- **Orval Zod output (recommended):** each generated schema is roughly `1.5×` the C# DTO line count in source bytes. A 20-field DTO ≈ 700–1500 bytes minified per schema before gzip. **Tree-shaking works per-export** because each schema is an individually exported `const` — unused schemas drop out cleanly when imported from per-file outputs (use `mode: 'tags-split'`).
- **Zod v4 runtime:** ~13 KB gzipped for the full library; **`zod/mini` ~1.9 KB gzipped** (InfoQ Aug 2025 benchmarks; Zod v4 announcement). Kubb has a `mini: true` flag that emits functional API (`z.optional(z.string())` instead of chains). Orval auto-detects the Zod major version but does not yet have a `mini` flag (orval#3111 tracks workspace migration to v4-as-default). **For Slice 1B, use `zod` not `zod/mini`** — chainable API is easier to read in code review; switch to `zod/mini` only if bundle audit at MVP-1 shows the delta matters.
- **Net delta for a representative endpoint (~20 fields, 1 schema):** ~1 KB minified + the shared Zod runtime (one-time, already in `node_modules` for your existing schemas). Effectively zero marginal cost per added endpoint after the first.

### 9. Reference precedents

**Honest answer:** I could not find a 2026 GitHub monorepo demonstrating the exact stack — ASP.NET Core 10 + React 19 + RTK Query + Vite 6 + Orval-Zod + `@rtk-query/codegen-openapi` — end-to-end. The closest references are:
- The Redux Toolkit codegen tutorial (Padovani, Medium) — RTK Query + Swashbuckle (ThronesAPI), no Zod.
- Orval's `samples/react-query/basic` directory — React Query, not RTK Query.
- Sebastian Chwastek's chwastek.eu post — ASP.NET Core + openapi-ts, types only.
- Khalid Abuhakmeh's "Generate ASP.NET Core OpenAPI Spec At Build Time" — MSBuild target pattern, .NET 5-era but the technique still applies with `DOTNET_ROLL_FORWARD`.

**This is not a 2026 well-trodden path for this specific stack.** Solo dev expectation: budget 1–2 sessions to land the wiring before the migration plan starts; the integration is well-documented per-piece but the assembly is novel.

### 10. ASP.NET Core 10 + Swashbuckle 10 gotchas

- `Microsoft.AspNetCore.OpenApi` is now the .NET 10 native generator and produces OpenAPI **3.1 by default**; the .NET 10 web API templates no longer include Swashbuckle. You explicitly opted to keep Swashbuckle — that decision is sound for now because Swashbuckle 10 supports .NET 10, defaults to OpenAPI 3.0 output (Microsoft.OpenApi v2-backed), and the codegen ecosystem is more tested against 3.0. Revisit at MVP-1.
- Swashbuckle 10 depends on Microsoft.OpenApi v2.3+, which has breaking API surface changes. Upgrade to Swashbuckle 9.0.6 first, then 10, per the official migration guide.
- `WithOpenApi()` is deprecated in .NET 10 (`ASPDEPR002`); use `AddOpenApiOperationTransformer` if you mix native + Swashbuckle. With pure-Swashbuckle you can ignore this.
- **Camel-case discipline:** the `JsonDocument`-bypass-formatter bug (Slice 1 #1) recurs anywhere C# emits raw JSON into the response pipeline. The codegen *cannot* save you here — the OpenAPI document lies about the casing because the property is `type: object` with no children. The Slice 4 fix (strongly-typed records replacing `JsonDocument`) is the real solution; codegen amplifies the cost of *not* doing that fix.
- **React 19 + TS strict + RTK Query:** `injectEndpoints` returns a new api instance — type inference on the merged shape is fine in TS 5.x, but if you split generated endpoints across multiple files (Orval `mode: 'tags-split'`) and merge later, declare an explicit return type on the merge wrapper to avoid TS portability blowups. Documented gotcha on redux-toolkit issue #2836.

---

## Details — Specific Versions Known to Work

| Package | Version | Notes |
|---|---|---|
| `@reduxjs/toolkit` | latest 2.x | RTK Query is bundled |
| `@rtk-query/codegen-openapi` | latest (currently ~1.x, in redux-toolkit monorepo) | Run via `tsx` or `ts-node` for `.ts` config |
| `orval` | `^8.6.2` | v8 is ESM-only, fetch as default client, Zod v3/v4 auto-detect |
| `@orval/zod` | `^8.6.2` | Pulled in transitively |
| `zod` | `^4.0.17` or later | Avoid mixing 3 and 4 in one tree |
| `swashbuckle.aspnetcore` | `10.x` | OpenAPI 3.0 default; opt-in 3.1 via `OpenApiVersion` |
| `swashbuckle.aspnetcore.cli` | `10.x` | Use `DOTNET_ROLL_FORWARD=LatestMajor` as fallback |
| `oasdiff/oasdiff-action` | `v0.0.46` (SHA-pinned in CI) | Free `breaking` action, no token needed |
| `lefthook` | `^2.x` | Replaces Husky + lint-staged |

**Fallback if Orval-Zod hits a discriminator/nullable bug on a specific RunCoach DTO:**
1. Swap that one schema to **Kubb** (`@kubb/plugin-zod` 4.x with explicit `version: '4'`). Kubb's plugin model lets you generate only the offending schema and re-export via the barrel.
2. If Kubb also fails, hand-write the schema for that endpoint (you already have ~12 such), commit it under `frontend/src/api/manual/`, and exclude it from codegen via `filterEndpoints` regex. Drift gate still works for everything else.

**Fallback if RTK Query becomes an active liability** (unlikely in Slice 1B, but document it): the cleanest migration is to `@hey-api/openapi-ts` + `@tanstack/react-query` — single tool, zod plugin first-class, used in production at Vercel/PayPal. Note this is the "one config, two outputs" dream you wish you had today, available only by changing your state-management library.

---

## Recommendations (decision-ready)

### Stage 1 — This Slice (1B)
1. Add the MSBuild target + `swashbuckle.aspnetcore.cli` 10.x tool manifest to the backend. Commit a working `backend/openapi/swagger.json`.
2. Add `@rtk-query/codegen-openapi`, `orval`, `@orval/zod` as frontend dev deps.
3. Wire `npm run codegen` + `prebuild` + GitHub Actions drift gate as shown in §4 and §5.
4. Migrate exactly **two** endpoints — `RegisterRequest` (proves the wire works) and `OnboardingProgressDto` (proves the gate catches the kind of drift that bit you).
5. Land a DEC entry recording: tools chosen (`@rtk-query/codegen-openapi` + Orval v8), OpenAPI dialect (3.0 with `nullable: true`), Zod (v4 chainable, not mini), drift gate (git diff + oasdiff breaking).

### Stage 2 — Before Slice 2
1. Land the Slice 4 `JsonDocument` antipattern fix for `OnboardingTurnResponseDto` *first*. Convert `AssistantBlocks`/`UserBlocks` to strongly-typed records or `JsonElement` with explicit converter that emits proper OpenAPI schema.
2. Migrate `OnboardingTurnResponseDto` and `SuggestedInputType`.
3. Author all Slice 2 (Workout Logging) DTOs as codegen-only from day one — zero hand-rolled Zod.

### Stage 3 — MVP-1 readiness
1. Re-evaluate `zod/mini` if bundle-size telemetry says it matters. Switch via Kubb's `mini: true` if you've already pivoted, or wait for Orval's tracked migration (orval#3111).
2. Re-evaluate `Microsoft.AspNetCore.OpenApi` (native, OpenAPI 3.1 default, AOT-compatible) as a backend swap target. Trigger conditions: Swashbuckle 10 falls behind .NET 11, or you need AOT.
3. Re-evaluate moving from RTK Query to TanStack Query + Hey API if codegen-source pain compounds. Trigger condition: more than two endpoints/quarter need bespoke `transformResponse` workarounds because the two-tool seam is leaking.

### Benchmarks that change these recommendations
- **Switch the default to Kubb** if Orval cannot generate a faithful Zod schema for any one of the first 3 migrated endpoints (it has a regression-heavy track record on enum + nullable edge cases — see orval issues #2249, #3024, #3269).
- **Switch to native ASP.NET Core OpenAPI** if you adopt Native AOT or if Swashbuckle 10 misses .NET 11.
- **Switch to TanStack Query + Hey API** if you find yourself wrapping more than ~30% of RTK Query endpoints with bespoke `transformResponse` / `enhanceEndpoints` code (signal that the slice-codegen / Zod-codegen seam is poorly fitted to your domain).

---

## Caveats

- **No "official" Microsoft / Redux blessed end-to-end recipe for this stack in 2026.** Microsoft's guidance now favors `Microsoft.AspNetCore.OpenApi` + native templates; Redux Toolkit's codegen treats runtime validation as user responsibility. The pipeline recommended here is correct but is assembled from three independent docs (Redux Toolkit codegen, Orval v8 release notes, Swashbuckle 10 migration guide).
- **OpenAPI 3.1 is the future, but 2026 codegen tooling is not uniformly ready.** Specific 3.1-nullable regressions: openapi-typescript #2144, orval #1817 (fixed 7.5.0), orval #3269 (regression in 8.8.1 — verify your installed minor version). Use 3.0 + `nullable: true` until at least mid-2026.
- **Orval has no first-class RTK Query target.** This is by design — Orval treats RTK Query as outside its TanStack Query/SWR/Hono ecosystem. The two-tool pipeline is the accepted workaround, not a temporary hack.
- **`@rtk-query/codegen-openapi` is officially labeled "early preview"** in the Redux Toolkit docs ("We have early previews of code generation capabilities available as separate tools"). It is widely used (~200k weekly downloads per CodeSandbox stats) and the redux-toolkit monorepo treats RTKQ-Codegen issues as first-class, but the API surface is unstable enough that you should pin a minor version and update deliberately.
- **The `JsonDocument`-in-DTO antipattern is not solved by codegen.** It is hidden by codegen. Until Slice 4 lands, the runtime Zod gate cannot protect `OnboardingTurnResponseDto.AssistantBlocks/.UserBlocks` — that's the lesson of Slice 1 bug #1 and you'll re-learn it as a Slice 1B integration test failure if you forget.
- **Solo-dev velocity caveat.** The first session lands the wiring; the second migrates two endpoints. Budget 2–3 hours for the first endpoint (proving the gate) and ~15 minutes per endpoint thereafter. The asymptote is the win; the activation energy is real.
- **Kubb mixed-license note.** Kubb's repository is "Most of this repository is licensed under the MIT License … Some components are licensed under AGPL-3.0-or-later" — verify the specific plugins you consume are MIT before shipping commercially. As of March 2026 `@kubb/plugin-zod` and `@kubb/swagger-zod` are MIT on npm.
- **No external example repo.** I could not locate a 2026 monorepo demonstrating the exact recommended toolchain end-to-end on ASP.NET Core 10 + React 19 + RTK Query + Vite 6. Treat the first integration as net-new prior art. Once it works, consider open-sourcing it as RunCoach's contribution to the gap.