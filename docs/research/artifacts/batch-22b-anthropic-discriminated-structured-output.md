# batch-22b-anthropic-discriminated-structured-output.md

**Research artifact for RunCoach Slice 1 onboarding spec.**
**Author:** Research agent.
**Date stamp:** All API behaviour claims verified against Anthropic primary sources accessed **2026-04-25**.
**Inputs already integrated (not re-covered):** R-015 (M.E.AI bridge), R-016 (model IDs), R-052 (first-party `Anthropic` NuGet migration), DEC-042 (structural-not-description discipline), DEC-047 (prompt-cache mandate).

---

## TL;DR

1. **Anthropic structured outputs went GA in late 2025/early 2026.** The parameter is `output_config.format` ([the beta `output_format` + `anthropic-beta: structured-outputs-2025-11-13` header still works in a transition window](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). Constrained decoding is real grammar-level enforcement — schema-violating tokens cannot be sampled.
2. **Anthropic's grammar accepts a deliberately small JSON Schema subset.** Supported: `object`, `array`, `string`, `number`/`integer`, `boolean`, `null`, `enum`, `const`, `required`, `additionalProperties: false`, nested objects, `$ref`/`$defs`, and `type: ["X", "null"]` nullable unions. **Silently rejected at request time (HTTP 400)**: `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `minLength`, `maxLength`, `pattern`, `format`, `minItems`, `maxItems`, `uniqueItems`, `minProperties`, `maxProperties`, and the schema-composition keywords `oneOf`, `allOf`, `if/then/else`, `not`, `prefixItems` ([Vercel AI SDK issue #13355 enumerates the full list of rejected keywords](https://github.com/vercel/ai/issues/13355); [Anthropic's docs page lists supported features and notes Python/TS/Ruby/PHP SDKs strip unsupported ones automatically — C#, Go, CLI do **not**](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).
3. **`anyOf` IS supported but expensive.** Each `anyOf` branch (and each `["T","null"]` union) consumes one of the **16 union-typed-parameter** budget slots, with explicit complexity limits enforced at compile time ([Anthropic structured-outputs docs, "Schema complexity limits"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).
4. **Recommended pattern: B (single schema, six nullable typed slots + topic discriminator).** It is the only pattern that simultaneously (a) is correct under Anthropic's actual constrained-decoding feature set in 2026, (b) keeps `output_config.format` byte-stable across all six topics so the schema-grammar cache and your prompt prefix cache both hit, (c) gives type-safe C# without `object` or runtime polymorphic dispatch, and (d) is well within the 24-optional-parameter limit (your six slots = 6 unions, each used optionally = 6 unions + 6 optional ≈ 12 of the 16-union and 24-optional budget).
5. **Cache-stability verdict.** Changing the `output_config.format.schema` bytes invalidates **both** the grammar cache (a separately-cached compiled grammar artifact, 24h TTL) **and** the prompt prefix cache for that conversation thread. Anthropic's docs are explicit: *"Changing the `output_config.format` parameter will invalidate any prompt cache for that conversation thread"* ([Anthropic structured-outputs docs, "Prompt modification and token costs"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). Therefore Pattern D (per-turn schema) destroys DEC-047's cache assumption — **do not use D**.
6. **First-party `Anthropic` NuGet 12.17.0 ergonomics.** The C# SDK is Stainless-generated and exposes `output_config` as plain typed parameter classes (`OutputConfigParam` / `OutputFormat`) — there is **no** `JsonSchema.Discriminated<T>` builder, **no** automatic schema transformation (unlike Python/TS/Ruby/PHP), and **no** `[JsonPolymorphic]`-aware schema emitter. You pass a raw schema dictionary. The SDK also implements `Microsoft.Extensions.AI.IChatClient` via `client.AsIChatClient(modelId)` ([Anthropic C# SDK docs](https://platform.claude.com/docs/en/api/sdks/csharp)). `AIJsonUtilities.CreateJsonSchema` from `Microsoft.Extensions.AI` is the cleanest schema generator for the recommended pattern.

---

## 1. Anthropic constrained-decoding capability matrix (verified 2026-04-25)

### 1.1 Timeline and parameter naming

* **2025-11-14:** Anthropic released structured outputs in public beta with `output_format` parameter and `anthropic-beta: structured-outputs-2025-11-13` header ([release recap, Thomas Wiegold](https://thomas-wiegold.com/blog/claude-api-structured-output/)).
* **GA in early 2026:** Parameter renamed from `output_format` to `output_config.format`. Beta header no longer required. The old name still works during a "transition period" ([Anthropic structured-outputs docs, migration banner](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).
* **GA model coverage (2026-04-25):** Sonnet 4.5, Sonnet 4.6, Opus 4.5, Opus 4.6, Opus 4.7, Haiku 4.5, Mythos Preview ([Anthropic docs, "Structured outputs are generally available on the Claude API for…"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). Older Claude 3.x models do **not** support structured outputs; on those, the SDK ecosystem falls back to a forced-tool-call shim ([Effect-TS issue #6091 documenting the same fallback pattern](https://github.com/Effect-TS/effect/issues/6091)).

### 1.2 Feature support matrix

| JSON Schema feature | Status under `output_config.format` | Source |
|---|---|---|
| `type: object`, `array`, `string`, `number`, `integer`, `boolean`, `null` | **Supported** (core) | [Anthropic docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs); [Agent SDK docs](https://platform.claude.com/docs/en/agent-sdk/structured-outputs) |
| `enum` | **Supported** | [Anthropic docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) |
| `const` | **Supported** | [Anthropic docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs); also explicitly listed under HIPAA constraint warning ("PHI must not be in `const` values") |
| `required` | **Supported** | [Anthropic docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) |
| `additionalProperties: false` | **Supported and recommended** (SDKs auto-add it) | [Anthropic docs, "How SDK transformation works"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) |
| `$ref` / `$defs` | **Supported syntactically** but the grammar compiler inlines refs rather than reusing rules — does NOT reduce compiled grammar size ([Anthropic Python SDK issue #1185](https://github.com/anthropics/anthropic-sdk-python/issues/1185)) |
| Nullable via `type: ["X", "null"]` | **Supported** (canonical idiom; this is what System.Text.Json `JsonSchemaExporter` emits by default for `T?` properties — [.NET 9 JsonSchemaExporter docs](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema)) |
| Nullable via `nullable: true` (OpenAPI 3.0 dialect) | **Not supported** — use the type-array form |
| `anyOf` | **Supported** but counts against the **"Parameters with union types: 16"** complexity limit ([Anthropic docs, "Schema complexity limits"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). Each `anyOf` branch and each nullable union counts. |
| `oneOf` | **Rejected with HTTP 400.** Vercel AI SDK had to add a converter to rewrite `oneOf` → `anyOf` ([PR #12903 in vercel/ai](https://github.com/vercel/ai/issues/13355)). Confirmed in the same Anthropic docs section listing unsupported keywords. |
| `allOf` | **Rejected** ([Vercel AI issue #13355](https://github.com/vercel/ai/issues/13355)) |
| `if`/`then`/`else`, `not`, `prefixItems` | **Rejected** ([same](https://github.com/vercel/ai/issues/13355)) |
| `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum` | **Rejected with HTTP 400 on `output_config.format`** ([Vercel AI issue #14342](https://github.com/vercel/ai/issues/13355) and [#13355 reproduction](https://github.com/vercel/ai/issues/13355)). Note: Anthropic's docs phrase this as *"numerical constraints aren't enforced by the schema itself"* but reproductions in the wild show actual 400 rejections, not silent ignoring, when sent to `output_config.format`. The Python/TS SDKs strip these client-side and move them to the property `description`. |
| `minLength`, `maxLength`, `pattern`, `format` | **Rejected** (same — Python SDK strips and moves to description) |
| `minItems`, `maxItems`, `uniqueItems` | **Rejected** ([Vercel AI #13355](https://github.com/vercel/ai/issues/13355)) — confirms DEC-042's premise that array cardinality cannot be enforced via the schema |
| `minProperties`, `maxProperties` | **Rejected** ([same](https://github.com/vercel/ai/issues/13355)) |
| Recursive schemas | **Rejected with 400 "Too many recursive definitions"** ([Thomas Wiegold writeup](https://thomas-wiegold.com/blog/claude-api-structured-output/)) |
| Property ordering | Required properties always serialise first, in schema order; optionals follow in schema order ([Anthropic docs, "Property ordering"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). To get deterministic order, mark all properties `required`. |

### 1.3 Hard complexity limits (verified 2026-04-25)

From [Anthropic structured-outputs docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs):

| Limit | Value |
|---|---|
| Strict tools per request | 20 |
| Optional parameters across all strict schemas + JSON output schema | **24** |
| Union-typed parameters (`anyOf` or `["T", "null"]`) across all strict schemas | **16** |
| Compilation timeout | 180 seconds |
| First-request grammar compilation overhead | 100–300 ms ([Wiegold, corroborated by Anthropic docs "First request latency"](https://thomas-wiegold.com/blog/claude-api-structured-output/)) |
| Compiled grammar cache TTL | **24 hours** since last use |

### 1.4 What actually drove the recommendation

The combination of (a) `oneOf` is rejected and (b) `["T", "null"]` works but each instance counts against a 16-union budget is **the** structural fact that selects the design pattern. There is no native discriminated-union construct in Anthropic's grammar; you have to encode it as either nullable typed slots (Pattern B) or run distinct schemas per discriminator value (Pattern C/D).

---

## 2. Pattern comparison and recommendation

### 2.1 The four candidates against five evaluation criteria

| Criterion | A: `object` | **B: 6 nullable typed slots + Topic** | C: per-topic endpoint | D: dynamic per-turn schema |
|---|---|---|---|---|
| Constrained-decoding correctness | ❌ no schema on the value | ✅ each populated slot fully constrained | ✅ each schema fully constrained | ✅ |
| Prompt-cache prefix stability (DEC-047) | ✅ schema constant | ✅ **schema constant across all 6 turns** | ⚠️ 6 different prefixes (one cache per topic, per workspace) | ❌ **schema mutates each turn → cache miss** |
| Backend C# type safety | ❌ `object` casts | ✅ closed records, no casts | ✅ different generic | ✅ generic, but reflective |
| Prompt token cost (per call) | lowest | medium (one schema covers all 6 ≈ +50–80 tokens vs. C) | lowest per call | medium |
| Eval-cache fixture stability | ✅ | ✅ **single fixture covers the topic-routing matrix** | ❌ 6 fixtures | ❌ schema bytes vary, fixtures churn |
| Code-path multiplication | 1 handler | **1 handler** | 6 handlers (rejected by user) | 1 handler |
| Anthropic schema-cache (24h grammar cache) | n/a | ✅ 1 entry for all turns | 6 entries | new entry per turn |
| Hallucination blast radius | high (`object` accepts anything) | **bounded** (each slot strictly typed; only the wrong slot can be misfilled, validatable backend-side via "exactly one non-null") | bounded | bounded |

### 2.2 Recommendation: **Pattern B**

**Pattern B is the only pattern that meets every requirement in your spec.** It is:

- Correct: each populated topic slot is fully grammar-constrained.
- Cache-stable: `output_config.format.schema` is byte-identical across all 6 turns, so the schema-grammar cache (24h TTL) hits from turn 2, AND the prompt-prefix cache that DEC-047 mandates is not invalidated by `output_config` changes.
- Type-safe in C#: each slot is a strongly-typed record, no `object`.
- Within complexity budget: 6 nullable typed slots = 6 union-types (well below 16) plus 6 optional parameters at the wrapper level (well below 24). You have headroom for future topic addition.
- Minimal code: one `OnboardingTurnHandler`, one schema, one eval fixture.

The "verbose schema" downside is the single real cost — first-request payload is ~50–200 tokens larger than Pattern C ([Wiegold, "System prompt overhead adds 50-200 tokens"](https://thomas-wiegold.com/blog/claude-api-structured-output/)). At ~$3/MTok input on Sonnet 4.6 that is ~$0.0006 per call — negligible against the prompt-cache savings.

### 2.3 C# record sketch

```csharp
// One closed record. Exactly one Normalized* slot non-null per turn.
// Backend validates "exactly-one-non-null" + Topic-matches-non-null-slot post-hoc.
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
    public required OnboardingTopic Topic { get; init; } // discriminator (enum)
    public required double Confidence { get; init; }

    // Exactly one of the following six is expected to be non-null,
    // matched to Topic. Backend asserts (Pattern-B-Invariant).
    public required PrimaryGoalAnswer? NormalizedPrimaryGoal { get; init; }
    public required TargetEventAnswer? NormalizedTargetEvent { get; init; }
    public required CurrentFitnessAnswer? NormalizedCurrentFitness { get; init; }
    public required WeeklyScheduleAnswer? NormalizedWeeklySchedule { get; init; }
    public required InjuryHistoryAnswer? NormalizedInjuryHistory { get; init; }
    public required PreferencesAnswer? NormalizedPreferences { get; init; }
}

public sealed record PrimaryGoalAnswer { public required GoalType Goal { get; init; } }
public sealed record TargetEventAnswer
{
    public required StandardRace Distance { get; init; }
    public required DateOnly? Date { get; init; }
    public required string? RaceName { get; init; }
}
public sealed record CurrentFitnessAnswer
{
    public required RaceTime[] RecentRaces { get; init; }
    public required int CurrentWeeklyDistanceKm { get; init; }
    public required int RunningExperienceYears { get; init; }
}
public sealed record WeeklyScheduleAnswer
{
    public required int MaxRunDaysPerWeek { get; init; }
    public required DayOfWeek? LongRunDay { get; init; }
    public required int? AvailableTimePerRunMinutes { get; init; }
}
public sealed record InjuryHistoryAnswer { public required InjuryNote[] Injuries { get; init; } }
public sealed record PreferencesAnswer
{
    public required string[] Constraints { get; init; }
    public required UnitSystem PreferredUnits { get; init; }
}

public enum OnboardingTopic
{
    PrimaryGoal, TargetEvent, CurrentFitness, WeeklySchedule, InjuryHistory, Preferences
}
```

### 2.4 Resulting JSON Schema dictionary (abbreviated)

This is the single schema that ships in `output_config.format.schema` for **every** onboarding turn. Byte-stable.

```json
{
  "type": "object",
  "additionalProperties": false,
  "required": ["Reply", "Extracted", "NeedsClarification", "ClarificationReason", "ReadyForPlan"],
  "properties": {
    "Reply": { "type": "array", "items": { "$ref": "#/$defs/AnthropicContentBlock" } },
    "Extracted": {
      "type": ["object", "null"],
      "additionalProperties": false,
      "required": ["Topic", "Confidence",
                   "NormalizedPrimaryGoal", "NormalizedTargetEvent",
                   "NormalizedCurrentFitness", "NormalizedWeeklySchedule",
                   "NormalizedInjuryHistory", "NormalizedPreferences"],
      "properties": {
        "Topic": { "type": "string", "enum":
          ["PrimaryGoal","TargetEvent","CurrentFitness","WeeklySchedule","InjuryHistory","Preferences"] },
        "Confidence": { "type": "number" },
        "NormalizedPrimaryGoal": { "type": ["object","null"], "additionalProperties": false,
          "required": ["Goal"], "properties": { "Goal": { "$ref": "#/$defs/GoalType" } } },
        "NormalizedTargetEvent": { "type": ["object","null"], "additionalProperties": false,
          "required": ["Distance","Date","RaceName"],
          "properties": {
            "Distance": { "$ref": "#/$defs/StandardRace" },
            "Date": { "type": ["string","null"] },
            "RaceName": { "type": ["string","null"] } } },
        "NormalizedCurrentFitness": { "type": ["object","null"], "additionalProperties": false,
          "required": ["RecentRaces","CurrentWeeklyDistanceKm","RunningExperienceYears"],
          "properties": {
            "RecentRaces": { "type": "array", "items": { "$ref": "#/$defs/RaceTime" } },
            "CurrentWeeklyDistanceKm": { "type": "integer" },
            "RunningExperienceYears": { "type": "integer" } } },
        "NormalizedWeeklySchedule": { "type": ["object","null"], "additionalProperties": false,
          "required": ["MaxRunDaysPerWeek","LongRunDay","AvailableTimePerRunMinutes"],
          "properties": {
            "MaxRunDaysPerWeek": { "type": "integer" },
            "LongRunDay": { "type": ["string","null"], "enum":
              ["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday",null] },
            "AvailableTimePerRunMinutes": { "type": ["integer","null"] } } },
        "NormalizedInjuryHistory": { "type": ["object","null"], "additionalProperties": false,
          "required": ["Injuries"],
          "properties": { "Injuries": { "type": "array", "items": { "$ref": "#/$defs/InjuryNote" } } } },
        "NormalizedPreferences": { "type": ["object","null"], "additionalProperties": false,
          "required": ["Constraints","PreferredUnits"],
          "properties": {
            "Constraints": { "type": "array", "items": { "type": "string" } },
            "PreferredUnits": { "$ref": "#/$defs/UnitSystem" } } }
      }
    },
    "NeedsClarification": { "type": "boolean" },
    "ClarificationReason": { "type": ["string","null"] },
    "ReadyForPlan": { "type": "boolean" }
  },
  "$defs": { /* GoalType, StandardRace, RaceTime, InjuryNote, UnitSystem, AnthropicContentBlock */ }
}
```

**Important DEC-042-aligned notes** in this shape:
- Every `Normalized*` slot is `["object","null"]` (the supported nullable idiom) and listed in `required`. This means Claude *must* emit all six keys; five will be `null`. This is the structural guarantee that "exactly one slot" is the convention, but the **"exactly one non-null"** invariant is **NOT** enforced by the grammar (no `oneOf`, no `minProperties`). **Backend MUST validate** that exactly one slot is non-null and that it matches `Topic`. (If Anthropic later ships schema-level "exactly one of these N keys is non-null", you can switch — see Gotchas §8.4.)
- Numerical bounds (e.g., `MaxRunDaysPerWeek` 1–7, `Confidence` 0.0–1.0) are **not** representable in this schema. Encode them in the system prompt and validate post-hoc. This is exactly the same lesson as DEC-042's `minItems` issue — **do not add `minimum`/`maximum`; they will return HTTP 400** ([Vercel AI #13355](https://github.com/vercel/ai/issues/13355)).
- `Date` as `["string","null"]` (no `format: date`); document the ISO-8601 expectation in the property description and parse in C#.

---

## 3. Cache-stability verdict

### 3.1 Two distinct caches are at play

Anthropic operates **two separate caches** that interact with structured outputs:

1. **Compiled-grammar cache** (specific to structured outputs).
   - Keyed by the JSON schema bytes.
   - 24-hour TTL since last use.
   - Cached separately from message content.
   - First request with a new schema = 100–300 ms compilation overhead.
   - This cache is **organisation-isolated** today; moving to **workspace-isolated** on **Feb 5, 2026** ([Anthropic prompt-caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)).
   - **Source:** [Anthropic structured-outputs docs, "Grammar compilation and caching"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs).

2. **Prompt prefix cache** (the one DEC-047 mandates with `cache_control: { type: "ephemeral", ttl: "1h" }`).
   - Keyed by a cryptographic hash of the request prefix in the order `tools → system → messages` ([Anthropic prompt-caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching); [Spring AI blog](https://spring.io/blog/2025/10/27/spring-ai-anthropic-prompt-caching-blog/)).
   - Exact-match prefix; one byte changes anywhere in the cached prefix invalidates that level and all subsequent levels.

### 3.2 The decisive primary-source quote

> *"Changing the `output_config.format` parameter will invalidate any prompt cache for that conversation thread."*
> — [Anthropic structured-outputs docs, "Prompt modification and token costs"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)

This is unambiguous. Anthropic's docs say `output_config.format` is part of the prefix-hash inputs in the same way `tools` is. The mechanism (also documented): when structured outputs is enabled, an additional system-prompt block describing the format is auto-injected ahead of your system prompt; that block changes when the schema changes ([same source](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).

### 3.3 Implication for each pattern

| Pattern | Schema-cache hits? | Prompt-prefix-cache hits across turns? | DEC-047's 70% input-token reduction realised? |
|---|---|---|---|
| **B (recommended)** | ✅ all turns share one schema | ✅ schema bytes constant across turns | ✅ |
| C (six endpoints) | ✅ within a topic | ❌ across topics — six independent prefix caches; topic switching = full miss | partial (good when same topic asked twice; broken on first hit per topic) |
| D (dynamic schema) | ❌ each turn writes a new grammar entry | ❌ each turn invalidates the conversation cache | **0%** |

**Pattern D is therefore disqualified by DEC-047.** Pattern C realises caching only when consecutive turns hit the same topic — which onboarding does not (each turn = new topic). So C also degrades to ~0% cache utilisation in onboarding's actual call pattern.

### 3.4 Where to place `cache_control`

For Pattern B: place a single `cache_control: {type: "ephemeral", ttl: "1h"}` breakpoint at the end of the **system prompt** block (after the per-turn topic instruction). Because the cache hierarchy is `tools → system → messages`, and structured-outputs auto-inject precedes your system prompt, this caches the auto-inject + the system prompt + the schema definition. The dynamic per-turn user message and the rotating "current topic" line that goes into the user content are after the breakpoint and don't pollute the prefix.

> Concretely: keep the topic name out of the system prompt. Put it in the first user message: *"Current topic: TargetEvent. <profile snapshot>"*. The system prompt stays byte-identical.

---

## 4. First-party `Anthropic` NuGet 12.17.0 ergonomics for structured output

### 4.1 SDK status as of 2026-04-25

* The `Anthropic` NuGet at v10+ is **the official Stainless-generated Claude SDK for C#**. Versions 3.x and below were the community `tryAGI.Anthropic` package, now renamed ([Anthropic C# SDK README](https://github.com/anthropics/anthropic-sdk-csharp); [NuGet page](https://www.nuget.org/packages/Anthropic/)).
* Targets .NET Standard 2.0+ ([same](https://github.com/anthropics/anthropic-sdk-csharp)).
* The SDK is "currently in beta. APIs may change between versions" ([Anthropic C# SDK docs](https://platform.claude.com/docs/en/api/sdks/csharp)).
* Implements `Microsoft.Extensions.AI.IChatClient` via `client.AsIChatClient(modelId)` ([same](https://platform.claude.com/docs/en/api/sdks/csharp)).

### 4.2 What the C# SDK does NOT provide

This is the critical gap to plan around. Per [Anthropic's structured-outputs docs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs), the language matrix is:

> *"Python: Pydantic models with `client.messages.parse()`. TypeScript: Zod schemas with `zodOutputFormat()`. Java: Plain Java classes with automatic schema derivation via `outputConfig(Class<T>)`. Ruby: `Anthropic::BaseModel` classes. PHP: classes implementing `StructuredOutputModel`. **CLI, C#, Go: Raw JSON schemas passed via `output_config`.**"*

So in C# 12.17.0:
- **No** `outputConfig(typeof(T))` typed builder.
- **No** automatic SDK-side schema transformation that strips unsupported keywords (Python/TS/Ruby/PHP do this; C# does not — confirmed by the docs language matrix above).
- **No** `JsonSchema.Discriminated<T>(...)` or `[JsonPolymorphic]`-aware emission.
- **No** built-in `messages.parse()` typed deserialization.

That is fine — the codebase already has `JsonSchemaHelper.GenerateSchema<T>()`. Pattern B works with a plain reflection-derived schema dictionary.

### 4.3 Recommended C# usage for Pattern B

Two viable layers; we recommend Option A:

**Option A (recommended): Raw SDK call via `MessageCreateParams`.**

```csharp
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

// One schema — built once at startup, not per turn.
private static readonly IDictionary<string, JsonElement> OnboardingSchemaDict =
    BuildOnboardingSchema(); // wraps AIJsonUtilities.CreateJsonSchema<OnboardingTurnOutput>(...)

private static IDictionary<string, JsonElement> BuildOnboardingSchema()
{
    var inferenceOpts = new AIJsonSchemaCreateOptions
    {
        IncludeSchemaKeyword = false,        // Anthropic doesn't want $schema
        DisallowAdditionalProperties = true, // additionalProperties: false everywhere
        RequireAllProperties = true,         // all keys in `required` -> deterministic ordering
    };
    var serOpts = new JsonSerializerOptions(JsonSerializerOptions.Default)
    {
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };
    var schema = AIJsonUtilities.CreateJsonSchema(
        type: typeof(OnboardingTurnOutput),
        serializerOptions: serOpts,
        inferenceOptions: inferenceOpts);

    // AIJsonUtilities emits ["T","null"] for nullable -> OK for Anthropic.
    // Strip any minimum/maximum/pattern emitted by reflection (paranoia).
    return AnthropicSchemaSanitizer.Sanitize(schema);
}

// Per-turn:
var parameters = new MessageCreateParams
{
    Model = "claude-sonnet-4-6",
    MaxTokens = 2048,
    System = onboardingSystemPrompt, // byte-stable across turns; cache_control breakpoint here
    Messages = new[] { /* prior turns + current "Current topic: X" user message */ },
    OutputConfig = new OutputConfigParam
    {
        Format = new OutputFormatJsonSchema
        {
            Type = "json_schema",
            Schema = OnboardingSchemaDict,
        },
    },
};
var msg = await anthropicClient.Messages.Create(parameters, ct);
var json = msg.Content.OfType<TextBlock>().Single().Text;
var output = JsonSerializer.Deserialize<OnboardingTurnOutput>(json, serOpts);
```

The exact property names of `OutputConfigParam`/`OutputFormatJsonSchema` may differ — Anthropic's [Python SDK source](https://github.com/anthropics/anthropic-sdk-python/blob/main/src/anthropic/types/message_create_params.py) shows `OutputConfigParam` containing `format: OutputFormatParam` and `effort: …`; the Stainless-generated C# SDK mirrors this 1:1. **Verify the exact C# names against `Anthropic.Models.Messages` in your installed 12.17.0 — Stainless evolves names slightly per release** (Gotcha §8.5).

**Option B: Go through `IChatClient`.**

`Microsoft.Extensions.AI` exposes `ChatResponseFormat.ForJsonSchema(schemaElement, "OnboardingTurnOutput")` and the strongly-typed `chatClient.GetResponseAsync<OnboardingTurnOutput>(...)` extension that handles deserialization for you ([MS Learn `ChatClientStructuredOutputExtensions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatclientstructuredoutputextensions.getresponseasync?view=net-9.0-pp); [`ChatResponseFormat.ForJsonSchema`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat?view=net-9.0-pp)).

```csharp
IChatClient client = anthropicClient.AsIChatClient("claude-sonnet-4-6");
var schema = AIJsonUtilities.CreateJsonSchema(typeof(OnboardingTurnOutput), inferenceOptions: inferenceOpts);
var response = await client.GetResponseAsync<OnboardingTurnOutput>(
    messages,
    new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "OnboardingTurnOutput") },
    cancellationToken: ct);
OnboardingTurnOutput parsed = response.Result; // typed deserialize; throws on parse failure
```

The first-party Anthropic `IChatClient` adapter inside the official SDK should map `ChatResponseFormatJson { Schema = X }` to `output_config.format = { type: "json_schema", schema: X }`. **Verify empirically** (§7) — the OpenAI Microsoft.Extensions.AI client has the equivalent mapping ([dotnet/extensions OpenAIChatClient.cs](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs)) and the Anthropic adapter follows the same pattern.

### 4.4 Behaviour of `JsonSchemaExporter` / `AIJsonUtilities.CreateJsonSchema` for Pattern B

This matters because it determines whether your existing `JsonSchemaHelper.GenerateSchema<T>()` produces an Anthropic-compatible schema for the recommended record graph. Three behaviours worth knowing:

1. **Nullable typed properties (`T?`).** With `RespectNullableAnnotations` on `JsonSerializerOptions` (default in `AIJsonUtilities`), `T?` emits `"type": ["X", "null"]` — exactly what Anthropic accepts ([.NET 9 release notes for `JsonSchemaExporter`](https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/); [.NET 9 elmah.io blog](https://blog.elmah.io/whats-new-in-net-9-system-text-json-improvements/)).
2. **`object`-typed properties.** `JsonSchemaExporter` emits `{}` (the unconstrained schema — accept anything) for `object`. Anthropic's grammar accepts `{}` but it offers **zero** constraint — this is exactly why Pattern A is broken.
3. **Recurring nested types via `$ref`/`$defs`.** `AIJsonUtilities.CreateJsonSchema` does **not** consistently emit `$defs` for repeated types — it inlines them, which has been an open issue ([dotnet/runtime #113698](https://github.com/dotnet/runtime/issues/113698)). For Pattern B this is fine because no record appears more than once. If you later add recurrence (e.g., `RaceTime` referenced in two slots), expect inlining; this counts against Anthropic's grammar-size budget but does not change schema-validity.
4. **`[JsonPolymorphic]` / `[JsonDerivedType]`.** System.Text.Json's `JsonSchemaExporter` does **not** emit a JSON Schema discriminator construct from these attributes — there is no built-in mapping to `oneOf` (which Anthropic rejects anyway) or to a discriminator keyword. Even if it did, Anthropic doesn't consume them. **Conclusion: do not use `[JsonDerivedType]` for the LLM-facing schema; use Pattern B's nullable-slots encoding.**
5. **Description attributes.** Default `JsonSchemaExporter` does NOT propagate `[Description]`. `AIJsonUtilities.CreateJsonSchema` does ([Kzu's TIL on `TransformSchemaNode`](https://til.cazzulino.com/dotnet/how-to-emit-descriptions-for-exported-json-schema-using-jsonschemaexporter)). Per DEC-042 you're using descriptions for *prompting*, not invariants — they help Claude pick the right slot but you must not rely on them for correctness.
6. **Sanitizer step.** Even with `AIJsonUtilities`, when `int` properties don't carry the integer pattern guard, you can sometimes get `"pattern": "^-?(?:0|[1-9]\\d*)$"` or similar emissions ([example output in elmah.io blog](https://blog.elmah.io/whats-new-in-net-9-system-text-json-improvements/)). Run a small post-process step that strips any `pattern`, `format`, `minimum`, `maximum`, `min*`, `max*`, `exclusive*`, `uniqueItems`, `oneOf`, `allOf`, `if`/`then`/`else`, `not`, `prefixItems` — defense in depth. This is exactly the transform Anthropic's Python/TS/Ruby/PHP SDKs apply automatically and that the C# SDK does not.

---

## 5. Recommended Slice 1 spec updates

### 5.1 § Unit 1 R01.7 (AggregateHandler LLM call signature) — concrete edits

**Before** (per the spec context):
```csharp
ICoachingLlm.GenerateStructuredAsync<OnboardingTurnOutput>(
    systemPrompt, userMessage, ct);
```

**After** (no signature change, but with a contract clarification):
```csharp
// systemPrompt: byte-identical across the entire onboarding session.
//   It MUST include the cache_control: ephemeral, ttl=1h breakpoint per DEC-047,
//   and MUST NOT include the current topic name (placed in userMessage).
// userMessage: the dynamic per-turn user content.
//   First content block: "Current topic: {OnboardingTopic}\n\nProfile so far: {snapshot JSON}"
//   Subsequent content blocks: prior turn history + the user's current utterance.
// Topic discriminator validation: Topic returned in OnboardingTurnOutput.Extracted.Topic
//   MUST equal the topic the controller named in userMessage. Pattern-B-Invariant validator
//   asserts both Topic-match and exactly-one-non-null-NormalizedX-slot. On violation, retry
//   once (per Anthropic refusal/max_tokens guidance); if still wrong, escalate.

await coachingLlm.GenerateStructuredAsync<OnboardingTurnOutput>(
    systemPrompt: OnboardingSystemPromptV1,    // STABLE — never edited mid-session
    userMessage: BuildUserMessage(currentTopic, profileSnapshot, userUtterance),
    ct);
```

Add to R01.7's invariant list:
- **Inv-R01.7.a (Pattern-B-Invariant):** Exactly one of `Extracted.Normalized*` is non-null, and it matches `Extracted.Topic`. Validated by `OnboardingTurnOutputValidator` post-deserialization. Source: this batch.
- **Inv-R01.7.b (Schema stability):** The schema bytes passed to `output_config.format.schema` are computed once at startup from `typeof(OnboardingTurnOutput)` and reused across all onboarding turns. The schema MUST NOT vary per topic. Source: §3.2 of this batch (Anthropic explicitly invalidates the prompt cache when `output_config.format` changes).

### 5.2 § Unit 1 R01.12 (`onboarding-v1.yaml` structured-output schema description) — concrete edits

Replace any reference to `NormalizedValue: object` with the Pattern-B schema. Add this header block to the YAML:

```yaml
# onboarding-v1.yaml — structured output schema for Slice 1 onboarding turns
# Schema is single, byte-stable across all onboarding turns and across all 6 topics.
# This stability is REQUIRED by DEC-047 (prompt caching) and the schema-grammar cache
# behaviour documented at https://platform.claude.com/docs/en/build-with-claude/structured-outputs
#
# Discriminator pattern: nullable typed slots. The `Extracted.Topic` discriminator
# must match exactly one non-null `Normalized*` slot. This is enforced post-response
# by OnboardingTurnOutputValidator because Anthropic's constrained decoding does NOT
# support oneOf, allOf, or "exactly-one-non-null" constraints (verified 2026-04-25).
#
# Forbidden schema features (will produce HTTP 400 from Anthropic):
#   minimum, maximum, exclusiveMinimum, exclusiveMaximum, minLength, maxLength,
#   pattern, format, minItems, maxItems, uniqueItems, minProperties, maxProperties,
#   oneOf, allOf, if/then/else, not, prefixItems
# Numerical bounds and string formats MUST be encoded in the system prompt and
# validated post-response — NOT in the schema. (Same lesson as DEC-042 for arrays.)
```

### 5.3 § Technical Considerations § Anthropic prompt caching — concrete edits

Insert a subsection:

> **Schema bytes are part of the prompt-cache prefix.** Anthropic's docs explicitly state that *"Changing the `output_config.format` parameter will invalidate any prompt cache for that conversation thread"* ([source](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). The Slice 1 onboarding schema is therefore frozen at codebase level for the lifetime of the cycle: schema generation runs once at startup; the resulting schema dictionary is held in a `static readonly` field; tests assert schema-byte stability. Any deliberate schema evolution (e.g., adding a 7th topic) is a coordinated migration that resets the schema-grammar cache and the 1h prompt cache window — schedule it during a low-traffic deploy.
>
> **Two caches, both relevant.** (a) Prompt-prefix cache: 1h ephemeral via `cache_control` per DEC-047, hierarchy `tools → system → messages`. (b) Schema-grammar cache: 24h, cryptographic hash of schema bytes, organisation-isolated (workspace-isolated as of 2026-02-05 — [source](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)). Both caches hit on turns 2..N of an onboarding session under Pattern B.

---

## 6. Validation procedure (eval-suite scenario, <$0.50 budget)

### 6.1 Test plan

One scenario, three topics, 3 calls. Sonnet 4.6 input/output prices ≈ $3/$15 per MTok ([Anthropic Sonnet 4.6 announcement](https://www.anthropic.com/news/claude-sonnet-4-6)). Each call ≤ 4k input + 1k output ≈ $0.027 input + $0.015 output ≈ $0.042. Three calls ≈ $0.13 — well under budget.

The test asserts: the C# generic `GenerateStructuredAsync<OnboardingTurnOutput>` returns a parseable `OnboardingTurnOutput` where `Extracted.Topic` matches the topic instructed and the corresponding `Normalized*` slot is non-null while the other five are null.

### 6.2 Concrete cURL fixture (one of the three scenarios — `PrimaryGoal`)

Use this as the known-good integration-test fixture. The schema dictionary in the request body is the byte-frozen Pattern-B schema (abbreviated `…` for brevity below — the real fixture has all six slots fully expanded).

**Request:**

```bash
curl https://api.anthropic.com/v1/messages \
  -H "content-type: application/json" \
  -H "x-api-key: $ANTHROPIC_API_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -d '{
    "model": "claude-sonnet-4-6",
    "max_tokens": 1024,
    "system": [{
      "type": "text",
      "text": "You are RunCoach onboarding. Ask one focused question per turn for the named topic. Output JSON matching the schema. Set exactly one Normalized* slot non-null, matching Topic.",
      "cache_control": {"type": "ephemeral", "ttl": "1h"}
    }],
    "messages": [{
      "role": "user",
      "content": "Current topic: PrimaryGoal\n\nProfile so far: {}\n\nUser said: I want to run my first marathon."
    }],
    "output_config": {
      "format": {
        "type": "json_schema",
        "schema": { /* the full Pattern-B schema from §2.4 */ }
      }
    }
  }'
```

**Expected response shape** (`response.content[0].text` is JSON-parseable):

```json
{
  "Reply": [{ "type": "text", "text": "Got it — your first marathon. ..." }],
  "Extracted": {
    "Topic": "PrimaryGoal",
    "Confidence": 0.92,
    "NormalizedPrimaryGoal": { "Goal": "FirstMarathon" },
    "NormalizedTargetEvent": null,
    "NormalizedCurrentFitness": null,
    "NormalizedWeeklySchedule": null,
    "NormalizedInjuryHistory": null,
    "NormalizedPreferences": null
  },
  "NeedsClarification": false,
  "ClarificationReason": null,
  "ReadyForPlan": false
}
```

The `usage` block should show `cache_creation_input_tokens > 0` on the first call. Run an immediate identical second call (different `Current topic:`, e.g., `WeeklySchedule`) — verify `cache_read_input_tokens > 0` and `cache_creation_input_tokens` ≈ 0 except for the small delta of the new user message. This proves prefix-cache stability under Pattern B.

### 6.3 Repeat with two more topics

Same request body with the user message changed to `Current topic: WeeklySchedule` / `Current topic: InjuryHistory`. Same schema bytes — same cache hit. Total budget after 3 calls ≈ $0.13.

### 6.4 Negative test (optional, +1 call ≈ $0.04)

Send the same request with `Current topic: PrimaryGoal` but a user utterance that names a race date (`"I want to do the Berlin Marathon on Sept 27, 2026"`). Pattern-B-Invariant validator should fire because the model will be tempted to populate `NormalizedTargetEvent` instead of `NormalizedPrimaryGoal`. The retry should re-emphasise the topic. Captures the discriminator-confusion case.

---

## 7. Pattern catalog for downstream slices

The same Pattern B (single-schema, named-nullable-slots + discriminator) is the canonical answer for all upcoming RunCoach slices that share the discriminated-shape problem. Inherit verbatim:

| Slice | Discriminator domain | Pattern B encoding |
|---|---|---|
| **Slice 1 onboarding** (this batch) | 6 topic types | 6 nullable typed slots + `Topic` enum |
| **Slice 3 adaptation events** (`PlanAdaptedFromLog` payload varies) | adaptation type (e.g., reduce-volume, extend-taper, swap-workout) | one nullable slot per adaptation type + `AdaptationKind` enum |
| **Slice 4 conversation intent classification** (`{intent, payload}`) | intent space (chat, log-workout, ask-plan, modify-plan…) | one nullable typed payload slot per intent + `Intent` enum |
| **Future workout-log auto-extraction** (`{logKind, fields}`) | log type (interval, easy, long-run, race, cross-train) | one nullable typed slot per log kind + `LogKind` enum |

In each case the invariant set is identical:
- Schema bytes byte-stable across all calls in that subsystem.
- All discriminator-keyed slots `required` and `nullable`.
- Backend validates "exactly one non-null + matches discriminator" post-response.
- Each subsystem schema must stay within the 16-union and 24-optional complexity budget. For a discriminator domain larger than ~16, partition into two top-level categories first (split the schema only at category boundaries; all calls within a category share one schema).

---

## 8. Gotchas

### 8.1 API version pinning
- Send `anthropic-version: 2023-06-01` (the only currently sane value — sent automatically by the official C# SDK and the Cysharp Claudia client; [SDK source](https://github.com/Cysharp/Claudia)). Do not override this header.
- Do **not** send `anthropic-beta: structured-outputs-2025-11-13` — structured outputs are GA. Beta header is for the legacy beta path only and is being phased out ([Anthropic docs migration banner](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).

### 8.2 Model ID choice
- Use **`claude-sonnet-4-6`** as the canonical RunCoach onboarding model ([Anthropic announcement](https://www.anthropic.com/news/claude-sonnet-4-6)). It supports structured outputs (per the GA list), is the default Sonnet alias on the Anthropic API, and matches the SDK examples. Pin the snapshot date if reproducibility matters: as of 2026-04-25 the dated snapshot ID surfaces in Bedrock as `anthropic.claude-sonnet-4-6-20260217-v1:0` ([dev.to writeup](https://dev.to/jangwook_kim_e31e7291ad98/claude-sonnet-46-1m-context-300k-output-agentic-coding-397n)) but on the direct API the unversioned alias is the documented default.
- **Do NOT** use Claude 3.7 Sonnet's extended-thinking mode with structured outputs — incompatible ([Wiegold; Anthropic docs](https://thomas-wiegold.com/blog/claude-api-structured-output/)). Sonnet 4.6 with adaptive thinking is fine.
- A field-reported intermittent issue exists: Opus 4.6 sometimes returns an empty content array under `output_config` json_schema ([anthropic-sdk-typescript #913](https://github.com/anthropics/anthropic-sdk-typescript/issues/913)). Mitigation: prefer Sonnet 4.6 for onboarding; if you adopt Opus, add a "non-empty content" assertion + retry.

### 8.3 SDK version implications
- The official `Anthropic` NuGet is in **beta**: *"APIs may change between versions"* ([C# SDK docs](https://platform.claude.com/docs/en/api/sdks/csharp)). Pin to **`Anthropic 12.17.0`** (the version named in the project context) and treat any minor bump as a non-trivial review — Stainless regenerates types from the OpenAPI spec, so `OutputConfigParam`'s C# property names can shift on regeneration.
- The C# SDK does **not** strip unsupported schema keywords for you. If your `JsonSchemaHelper.GenerateSchema<T>()` ever starts emitting `pattern`, `minimum`, `format`, `oneOf`, etc. (e.g., from a new attribute or a System.Text.Json upgrade), Anthropic returns HTTP 400. **Add a sanitizer + a unit test** that asserts the produced schema dictionary contains none of the forbidden keys. Treat this as a regression guard.

### 8.4 Backward-compatibility risk if Anthropic adds `oneOf` mid-cycle
- If Anthropic ships `oneOf` support during the cycle, the *cleanest* schema would be a true discriminated union. **Don't migrate immediately.** Pattern B's nullable-slots schema is already correct, type-safe, and cache-stable — there's no functional gap. Migration to `oneOf` would invalidate every prompt cache and every grammar cache for the Slice 1, 3, 4, and auto-extraction subsystems simultaneously, plus require eval-fixture rewrites. Defer until a planned schema-revision window.
- Watch the Anthropic changelog ([release notes](https://platform.claude.com/docs/en/release-notes/overview)) and the [anthropic-sdk-csharp releases page](https://github.com/anthropics/anthropic-sdk-csharp/releases). The 2026-04-10 Vertex 0.3.0 release is the latest as of this research; there's no announced `oneOf` support.

### 8.5 Eval-cache key derivation
- Eval-cache keys for the LLM-call cassettes must include a hash of the schema dictionary bytes. The standard `JsonSerializer.SerializeToUtf8Bytes(schema, ...)` over `IDictionary<string, JsonElement>` is the cleanest input. **Caveat:** dictionary ordering matters for both Anthropic's prefix hash and your eval-cache key. `JsonNode`/`JsonObject` preserves insertion order in .NET 9+ ([elmah.io blog](https://blog.elmah.io/whats-new-in-net-9-system-text-json-improvements/)) but `Dictionary<string, JsonElement>` does NOT in older runtimes — use `OrderedDictionary<string, JsonElement>` or post-canonicalize via JSON sort. (This is also why Anthropic's docs warn that *"some languages, e.g., Swift, Go, randomize key order during JSON conversion, breaking caches"* — [Anthropic prompt-caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching). C#'s `Dictionary` is insertion-ordered in practice but not contractually; canonicalize.)
- The eval-cache key MUST also include `model`, `system`, `messages`, and `cache_control` placement. Schema is one input among several.

### 8.6 PHI / privacy
- Anthropic explicitly warns: *"Do not include PHI in schema property names, enum values, const values, or pattern regular expressions"* ([Anthropic structured-outputs docs, Data retention](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)) — schemas are cached separately from prompt content and don't get the same ZDR protections. RunCoach data isn't PHI under HIPAA, but the analogous principle holds: schema property names should be generic (`NormalizedTargetEvent`, not `Users.Hannah.NextRace`).

### 8.7 Property ordering / required-first quirk
- Anthropic emits required properties before optional in the response ([Anthropic docs, "Property ordering"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). Pattern B has every property `required` (the `Normalized*` slots are `required: true, type: ["object","null"]`), so output ordering is fully deterministic and matches schema definition order. Good.

### 8.8 `Confidence` field bounds and refusal handling
- `Confidence` cannot be schema-bounded to `[0, 1]` (no `minimum`/`maximum`). Document the bound in the property description and clamp post-deserialization. If Claude returns a value outside the range, that's a bug in the model behaviour, not a schema violation.
- Always check `stop_reason` on the response: `"refusal"` and `"max_tokens"` produce 200-OK responses but the body may not match your schema ([Anthropic docs, "Invalid outputs"](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)). Both cases need explicit handling in `GenerateStructuredAsync` (retry on `max_tokens` with bigger limit; bubble up on `refusal`).

### 8.9 Workspace-cache-isolation change on 2026-02-05
- Anthropic moved cache isolation from organisation-level to workspace-level on 2026-02-05 on the Claude API and Azure Foundry; Bedrock and Vertex remain organisation-level ([Anthropic prompt-caching docs](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)). If you're using multiple workspaces or migrating between them, expect cold-start cache misses. RunCoach probably runs in one workspace, so this is informational only.

---

## Sources

Primary (authoritative):
- Anthropic structured outputs docs: https://platform.claude.com/docs/en/build-with-claude/structured-outputs (verified 2026-04-25)
- Anthropic prompt caching docs: https://platform.claude.com/docs/en/build-with-claude/prompt-caching
- Anthropic C# SDK docs: https://platform.claude.com/docs/en/api/sdks/csharp
- Anthropic C# SDK GitHub (release branch — check `.stats.yml` and `CHANGELOG.md`): https://github.com/anthropics/anthropic-sdk-csharp
- Anthropic C# NuGet 12.17.0: https://www.nuget.org/packages/Anthropic/
- Anthropic agent-SDK structured outputs: https://platform.claude.com/docs/en/agent-sdk/structured-outputs
- Sonnet 4.6 announcement: https://www.anthropic.com/news/claude-sonnet-4-6
- Models overview: https://platform.claude.com/docs/en/about-claude/models/overview

Community / supporting:
- Vercel AI SDK issue #13355 (canonical list of unsupported keywords with reproductions): https://github.com/vercel/ai/issues/13355
- Vercel AI SDK issue #14342 (`exclusiveMinimum`, `not`, `minimum`, `maximum` rejection): https://github.com/vercel/ai/issues/14342
- Vercel AI issue #12298 (`output_format` deprecation, Bedrock requires `output_config.format`): https://github.com/vercel/ai/issues/12298
- Anthropic Python SDK issue #1185 (`$ref`/`$defs` inlining behaviour, grammar-size limits): https://github.com/anthropics/anthropic-sdk-python/issues/1185
- Anthropic TS SDK issue #913 (Opus 4.6 empty content with `output_config`): https://github.com/anthropics/anthropic-sdk-typescript/issues/913
- Effect-TS issue #6091 (provider migration from forced-tool-call shim to native structured outputs): https://github.com/Effect-TS/effect/issues/6091
- Wiegold guide (latency, error modes, beta header): https://thomas-wiegold.com/blog/claude-api-structured-output/
- Vercel AI Gateway docs (GA `output_config.format` vs. legacy `output_format`): https://vercel.com/docs/ai-gateway/sdks-and-apis/anthropic-messages-api/structured-outputs
- Spring AI prompt caching writeup (`tools→system→messages` hierarchy + invalidation): https://spring.io/blog/2025/10/27/spring-ai-anthropic-prompt-caching-blog/
- AWS Bedrock structured outputs (Anthropic on Bedrock): https://docs.aws.amazon.com/bedrock/latest/userguide/structured-output.html

.NET / Microsoft.Extensions.AI references:
- `JsonSchemaExporter` (.NET 9): https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/extract-schema
- System.Text.Json polymorphism: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism
- `AIJsonUtilities.CreateJsonSchema`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.aijsonutilities.createjsonschema
- `ChatResponseFormat`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatresponseformat
- `IChatClient.GetResponseAsync<T>`: https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatclientstructuredoutputextensions.getresponseasync
- .NET 9 `JsonSchemaExporter` blog: https://devblogs.microsoft.com/dotnet/system-text-json-in-dotnet-9/
- dotnet/runtime #113698 ($defs inlining for recurrent types): https://github.com/dotnet/runtime/issues/113698

**Date stamp on all API behaviour claims: 2026-04-25.** Re-verify before any spec freeze if Anthropic ships a model-spec, beta-header, or grammar-feature change in the meantime — particularly watch for native `oneOf` support, which would unlock a cleaner schema but require a coordinated cache reset across all subsystems listed in §7.