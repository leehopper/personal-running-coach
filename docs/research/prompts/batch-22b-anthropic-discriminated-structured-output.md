# Research Prompt: Batch 22b — R-067

# Anthropic Constrained-Decoding Structured-Output Schema for Topic-Discriminated Multi-Turn Responses (Anthropic SDK 12.x + .NET 10 + first-party `Anthropic` NuGet, 2026)

Copy the prompt below and hand to your deep research agent.

---

## PROMPT

Research Topic: For a multi-turn onboarding flow where the LLM must extract a typed answer whose **shape depends on which topic is currently being asked** (e.g., `PrimaryGoal` returns an enum, `TargetEvent` returns `{ distance, date }` or null, `CurrentFitness` returns `{ recentRaces[], weeklyDistanceKm }`), what is the 2026 canonical Anthropic structured-output schema design pattern given Anthropic constrained decoding's flat-schema constraints? Specifically: does Anthropic's `output_config.format = json` support `oneOf` / `anyOf` / discriminated unions, and if not, what's the right shape for the per-turn `OnboardingTurnOutput` record?

## Context

I'm finalizing the Slice 1 (Onboarding → Plan) spec for RunCoach, an AI running coach. The brain layer already uses Anthropic constrained decoding for plan generation via `ICoachingLlm.GenerateStructuredAsync<T>(systemPrompt, userMessage, ct)` (POC 1 — three-call macro/meso/micro chain with typed records like `MesoWeekOutput`, `MicroWorkoutListOutput`).

DEC-042 lesson learned the hard way: design invariants **structurally**, not via `[Description]` hints. `MesoWeekOutput` was originally `{ Days: MesoDaySlotOutput[] }` (failed — LLM produced 5-8 days arbitrarily); restructured to **seven named day-slot properties** (`Sunday`, `Monday`, ..., `Saturday`) so constrained decoding structurally guarantees exactly seven slots. Anthropic's constrained decoding enforces property names + types + `additionalProperties: false`, but does NOT enforce `minItems`/`maxItems` on arrays.

The new wrinkle for Slice 1 onboarding:

The per-turn handler calls `GenerateStructuredAsync<OnboardingTurnOutput>(...)` with a system prompt that names the **current topic** being asked. The expected return shape:

```csharp
public sealed record OnboardingTurnOutput
{
    public required AnthropicContentBlock[] Reply { get; init; }
    public required ExtractedAnswer? Extracted { get; init; }
    public required bool NeedsClarification { get; init; }
    public required string? ClarificationReason { get; init; }
    public required bool ReadyForPlan { get; init; }
}

public sealed record ExtractedAnswer
{
    public required OnboardingTopic Topic { get; init; }
    public required object NormalizedValue { get; init; }   // <-- the problem
    public required double Confidence { get; init; }
}
```

The `NormalizedValue` shape is **topic-dependent**:
- `PrimaryGoal` → `{ Goal: GoalType }` (enum)
- `TargetEvent` → `{ Distance: StandardRace, Date: DateOnly?, RaceName: string? }` or null
- `CurrentFitness` → `{ RecentRaces: RaceTime[], CurrentWeeklyDistanceKm: int, RunningExperienceYears: int }`
- `WeeklySchedule` → `{ MaxRunDaysPerWeek: int, LongRunDay: DayOfWeek?, AvailableTimePerRunMinutes: int? }`
- `InjuryHistory` → `{ Injuries: InjuryNote[] }`
- `Preferences` → `{ Constraints: string[], PreferredUnits: UnitSystem }`

The naive C# union via `object` is type-unsafe and fights System.Text.Json's reflection-based schema generator. We need a schema-clean shape.

The constraints Anthropic constrained decoding imposes (per Anthropic docs as of late 2025):
- Schema is JSON Schema with limited subset support.
- `oneOf`, `anyOf`, `allOf` — historically NOT supported (this is the question to verify for 2026).
- `additionalProperties: false` is enforced.
- Property names + types are enforced.
- `enum` is supported.
- `nullable: true` works via type union with `null`.

The implication: a single schema can't easily express "the `NormalizedValue` field's shape depends on the `Topic` field's value" without `oneOf`. Workarounds matter for correctness, type safety, and prompt-cache stability.

### What the existing research covers — and doesn't

- `batch-7a-ichatclient-structured-output-bridge.md` (R-015) covered the M.E.AI `IChatClient` adapter that bridges `ForJsonSchema(...)` to native Anthropic constrained decoding (the `AnthropicStructuredOutputClient`). Adapter mechanics, NOT schema design patterns.
- `batch-7b-anthropic-model-ids-versioning.md` (R-016) covered which models support structured output (Sonnet 4.6 yes; older Sonnets no). Not schema design.
- `batch-17b-anthropic-sdk-firstparty-vs-bridge.md` (R-052) recommended migrating from the DEC-037 bridge to first-party `Anthropic` NuGet 12.17.0 implementing `Microsoft.Extensions.AI.IChatClient`. Did NOT cover schema design for discriminated unions or how the Anthropic SDK 12.x exposes `output_config` shape for advanced schema features. The migration is mechanical; the schema design choice is the missing piece.
- The DEC-042 restructuring insight (named-day-slots vs Days array) confirms structural-not-description discipline but addresses a *different* shape (cardinality enforcement, not discriminated unions).

### What I've ruled out

- **Per-topic endpoint** (one HTTP endpoint per topic, dispatched server-side based on `currentTopic`) — works but multiplies code paths by 6, breaks the single-handler pattern, and complicates idempotency. Discard unless no other option.
- **`object` typed `NormalizedValue` with backend type-coercion** — loses constrained-decoding's correctness guarantee. The LLM can return any JSON shape and we type-coerce; same risk surface as untyped `GenerateAsync`. Discard.
- **Rebuilding the schema per turn based on the current topic** (one schema with a single `NormalizedValue` typed for the active topic) — works in theory; spec implementation cost is "compose the schema dynamically per turn" which the existing `JsonSchemaHelper.GenerateSchema<T>()` doesn't support out of the box. Borderline.

## Research Question

**Primary:** For Anthropic constrained-decoding (`output_config.format = json` via `Schema = JsonSchemaHelper.GenerateSchema<T>()` on the Anthropic SDK 12.x), what is the 2026 canonical schema-design pattern for "the response's typed payload shape depends on a discriminator field"? Verify against Anthropic's actual constrained-decoding constraints in late-2025 / 2026 (does `oneOf` / `anyOf` work? Are there documented updates?).

**Sub-questions** (each must be actionable):

1. **Anthropic constrained-decoding capability matrix as of 2026.** What JSON Schema features does Anthropic constrained decoding actually enforce vs silently ignore as of the latest API version? Specifically `oneOf`, `anyOf`, `allOf`, `if/then/else`, `pattern`, `format`, `minimum/maximum`, `minItems/maxItems`. Cite Anthropic docs (most recent), Anthropic's own SDK source, and any community verification (cookbooks, GitHub issues).

2. **The four candidate schema patterns** for discriminated topic-conditional output. Compare:
   - **Pattern A** — One schema with `NormalizedValue: object` (loose); backend deserializes to `ExtractedAnswer` then dispatches via topic discriminator. **Cost:** loses constrained-decoding correctness.
   - **Pattern B** — One schema with all six topic shapes as nullable typed properties (`NormalizedPrimaryGoal: PrimaryGoalAnswer?`, `NormalizedTargetEvent: TargetEventAnswer?`, etc.) + `Topic` discriminator. LLM populates exactly one. **Cost:** verbose schema, large prompt token count, but type-safe + cache-stable.
   - **Pattern C** — Per-topic endpoint with per-topic schema (six different `OnboardingTurnOutput<T>` types). LLM call site dispatches based on current topic. **Cost:** six code paths, six cached schemas, but each schema is small.
   - **Pattern D** — Dynamic schema per turn (single `OnboardingTurnOutput<TAnswer>` parameterized at runtime by the active topic; `JsonSchemaHelper` builds the schema from the closed type). **Cost:** schema instability across turns invalidates Anthropic's per-turn cache. Need to verify whether cache-control's prefix-hash mechanism cares about the schema in the request body.
   - Recommend the best pattern with justification grounded in (a) constrained-decoding correctness, (b) prompt-cache prefix stability, (c) backend type safety, (d) prompt token cost, (e) eval-cache fixture stability.

3. **Cache-stability implications.** DEC-047 mandates Anthropic prompt caching from day one (`cache_control: { type: "ephemeral", ttl: "1h" }`). The cache is prefix-hash based on the request body bytes. **Does the schema in `output_config` participate in the cache prefix?** If schema bytes change per turn (Pattern D), are calls 2+ still cache hits on the system prompt + profile prefix, or does schema mutation invalidate the entire cache window?

4. **First-party `Anthropic` NuGet 12.17.0 ergonomics.** The R-052 migration to the first-party SDK is in flight. How does the SDK expose `output_config.format` and the schema dictionary for the `Messages.Create` API? Is there a typed builder for discriminated unions (e.g., `JsonSchema.Discriminated<T>(...)`), or is everything `Dictionary<string, JsonElement>` plumbing? Verify the API surface against `github.com/anthropics/anthropic-sdk-csharp` `release/v12.x`.

5. **Confidence + retry semantics for the discriminator field.** The `Topic` discriminator must match the topic the controller asked about. What's the canonical pattern for "if the LLM returns a `Topic` mismatch, treat as `needs_clarification` + retry"? Does Anthropic's constrained decoding support per-call value constraints (e.g., "Topic MUST equal `PrimaryGoal`") or is that backend-validated only?

6. **System.Text.Json + `JsonSchemaHelper.GenerateSchema<T>()` interplay.** The existing `ClaudeCoachingLlm.cs` line 148 uses `JsonSchemaHelper.GenerateSchema<T>()` to produce the schema dictionary. How does this helper handle records with `object`-typed properties, nullable typed properties, and (crucially) records that need a discriminator? Trace the helper's behavior — does it correctly emit `nullable: true` for `T?`, and does it emit reasonable schemas for closed records of records?

7. **Empirical validation budget.** What's the cheapest way to verify which pattern works against the actual Anthropic API before committing to one in the Slice 1 spec? A throwaway eval-suite scenario? A direct curl with a known input? Document a validation procedure that costs <$0.50 of API spend and produces a deterministic answer.

## Why It Matters

- **Slice 1 ships the first user-facing LLM surface.** Wrong schema design either (a) makes the typed slots unsafe — LLM hallucinations leak into `UserProfile` JSONB columns, or (b) costs 6× more LLM calls per turn than necessary, or (c) breaks the prompt-cache assumption that DEC-047 builds on (the spec's cost model assumes ~70% input-token reduction from caching; if schema mutation invalidates the cache, that goes to 0%).
- **Locked across the cycle.** The same discriminated-shape problem will recur in:
  - Slice 3 adaptation events (`PlanAdaptedFromLog` payload shape depends on which kind of adaptation: micro-swap, meso-restructure, macro-overhaul).
  - Slice 4 conversation intent classification (the LLM returns `{ intent: "general-question" | "log-correction" | "goal-change" | "safety-concern", payload: ... }`).
  - Future workout-log auto-extraction (`Metrics` JSONB shape depends on which fields the LLM identified).
  Locking the discriminator pattern now keeps every later slice consistent; getting it wrong now means three more migrations.
- **DEC entry expected.** The cycle plan flagged ~4 new DEC entries from the Slice 1 spec session; this is one of them. The decision-log should record the chosen pattern + rationale once research lands.
- **Eval-cache fixture stability.** Multi-turn evals (R-053 multi-turn extension) depend on byte-stable cache fixtures across re-record runs. A schema-shape choice that's unstable across runs makes the cache fixture fragile and breaks CI replay.

## Deliverables

- **Definitive Anthropic constrained-decoding capability matrix as of 2026.** What's supported, what's silently ignored, what's the latest API version. Primary-source: Anthropic docs + SDK source. Secondary: Anthropic cookbooks, GitHub issues. Date-stamp the verification.
- **Recommended schema pattern (A/B/C/D) with concrete C# record sketch** for `OnboardingTurnOutput` + `ExtractedAnswer` shaped per the recommendation. Include the schema dictionary that the recommended pattern emits via `JsonSchemaHelper`.
- **Cache-stability verdict.** Direct answer: does `output_config.format.schema` participate in the prompt-cache prefix hash? Cite Anthropic docs / engineering responses, not just speculation.
- **First-party SDK ergonomics review.** How clean is the recommended pattern in Anthropic NuGet 12.17.0? Are there built-in discriminated-union helpers, or is it manual dictionary plumbing? Sample code snippet.
- **Recommended next step for the Slice 1 spec.** Concrete updates to: § Unit 1 R01.7 (the [AggregateHandler] LLM call signature), § Unit 1 R01.12 (the `onboarding-v1.yaml` structured-output schema description), § Technical Considerations § Anthropic prompt caching (cache-stability claims).
- **Validation procedure** — a single eval-suite scenario or curl-based test that proves the chosen pattern works for ≥3 of the 6 topics on the actual Anthropic API. Include expected request body + expected response body shape so the spec's first integration test has a known-good fixture.
- **Pattern catalog for downstream slices.** Bullet list of where the discriminator pattern recurs (Slice 3 / Slice 4 / future workout-log auto-extraction) so all three inherit the chosen pattern verbatim.
- **Gotchas.** Anthropic API version pinning; SDK version implications; eval-cache key derivation if the schema participates in cache hashing; backward-compatibility risk if Anthropic adds `oneOf` support mid-cycle.

## Out of scope

- POC 1's existing `MacroPlanOutput` / `MesoWeekOutput` / `MicroWorkoutListOutput` schemas — they're flat closed records with no discriminator and remain unchanged.
- Streaming responses — Slice 1 is request-response only; streaming lands in Slice 4.
- Tool use / function calling — separate Anthropic feature; not used in Slice 1.
- DEC-037 bridge migration mechanics — covered by R-052; this prompt assumes the migration has shipped.
