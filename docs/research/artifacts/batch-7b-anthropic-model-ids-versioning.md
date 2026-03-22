# Anthropic model IDs and structured output in .NET evals

**Your `claude-sonnet-4-20250514` structured output error is not a bug — it's expected behavior.** Claude Sonnet 4 (the original May 2025 release) predates the structured output feature entirely. Constrained decoding launched in November 2025 for Claude Sonnet 4.5 and Opus 4.1 only, and has since expanded to newer models. The fix is straightforward: upgrade to `claude-sonnet-4-5-20250929` (or better, `claude-sonnet-4-6`). Your SDK version (12.9.0) is already the latest and is not the issue — model IDs pass through as raw strings with no client-side validation.

## Which models actually support structured output

Structured outputs via constrained decoding (`output_config.format` with `json_schema` type) are supported exclusively by models released from the 4.5 generation onward. Here is the full compatibility matrix:

| Model | API ID (pinned) | Alias (floating) | Structured Output |
|-------|----------------|-------------------|-------------------|
| **Sonnet 4.6** | `claude-sonnet-4-6` | `claude-sonnet-4-6` | ✅ GA |
| **Opus 4.6** | `claude-opus-4-6` | `claude-opus-4-6` | ✅ GA |
| **Sonnet 4.5** | `claude-sonnet-4-5-20250929` | `claude-sonnet-4-5` | ✅ GA |
| **Opus 4.5** | `claude-opus-4-5-20251101` | `claude-opus-4-5` | ✅ GA |
| **Haiku 4.5** | `claude-haiku-4-5-20251001` | `claude-haiku-4-5` | ✅ GA |
| **Opus 4.1** | `claude-opus-4-1-20250805` | — | ✅ (original beta launch model) |
| **Sonnet 4** | `claude-sonnet-4-20250514` | — | ❌ Not supported |
| **Opus 4** | `claude-opus-4-20250514` | — | ❌ Not supported |
| All 3.x models | Various | — | ❌ Not supported |

Your attempted ID `claude-sonnet-4-5-20250514` returns "model not found" because there is no May 2025 snapshot of Sonnet 4.5 — **the correct dated ID is `claude-sonnet-4-5-20250929`** (September 2025 release). The same applies to `claude-sonnet-4-6-20250514`. Your Haiku ID `claude-haiku-4-5-20251001` is correct and works because Haiku 4.5 does support structured output.

The LiteLLM and Agno SDKs both maintain explicit exclusion lists for `claude-sonnet-4-20250514` and `claude-opus-4-20250514`, confirming this is a well-known limitation. Anthropic's own Models API (`GET /v1/models/{model_id}`) returns a `capabilities.structured_outputs.supported` field you can check programmatically.

## The output_format to output_config rename matters

A critical API change occurred during the structured output GA migration: **the `output_format` parameter was renamed to `output_config.format`**. The old `output_format` still works temporarily on the direct Anthropic API, but Amazon Bedrock already rejects it. Beta headers (`anthropic-beta: structured-outputs-2025-11-13`) are no longer required.

In the C# SDK, the correct property path is `OutputConfig.Format`:

```csharp
var parameters = new MessageCreateParams
{
    Model = "claude-sonnet-4-6",
    MaxTokens = 8192,
    Messages = messages,
    OutputConfig = new()
    {
        Format = new()
        {
            Type = "json_schema",
            Schema = rawJsonSchemaObject
        }
    }
};
```

Unlike the Python SDK (which has `client.messages.parse()` with Pydantic) or the TypeScript SDK (which has `zodOutputFormat()`), the **C# SDK requires raw JSON schemas** — there are no schema-generation helpers. You'll need to serialize your schema manually or use a library like `NJsonSchema` to generate schemas from C# types.

## Your SDK is current, but understand its architecture

The `Anthropic` NuGet package v12.9.0 (released March 16, 2026) is the latest version. The official SDK lives at `github.com/anthropics/anthropic-sdk-csharp` — note this is distinct from the community `Anthropic.SDK` by tghamm and the older `tryAGI.Anthropic`. The SDK is still marked as beta, meaning APIs may change between versions.

**The SDK version does not restrict model access.** Model IDs are passed as plain strings to the API with zero client-side validation. The SDK also provides typed enum constants (e.g., `Model.ClaudeSonnet4_5_20250929`) as a convenience, but you can always pass any string. New models work immediately without SDK updates. The `output_format` → `output_config.format` migration was reflected across all SDK versions starting around v12.0.0 (December 2025), so your v12.9.0 already uses the correct `OutputConfig` property.

## Aliases exist and solve the hardcoding problem

Anthropic provides **undated alias IDs** that float to the current version within a model family. For 4.x models, the pattern is simply the family name without a date suffix: `claude-sonnet-4-6`, `claude-sonnet-4-5`, `claude-haiku-4-5`. The older `-latest` suffix pattern (e.g., `claude-3-5-sonnet-latest`) existed for 3.x models but is not used for 4.x.

Key versioning facts that affect your eval suite design:

- **60 days minimum notice** before any model retirement
- **No automatic redirection** — retired model IDs return errors, not silent fallbacks
- `claude-sonnet-4-20250514` has an earliest possible retirement of **May 14, 2026**
- The Models API (`GET /v1/models`) returns a paginated list of all available models with capabilities, enabling startup validation

## Recommended config pattern for your eval suite

For your `CoachingLlmSettings` record, adopt a **dual-track strategy**: use floating aliases as defaults for development velocity, and pin dated IDs for reproducible regression baselines.

```csharp
public record CoachingLlmSettings
{
    /// <summary>
    /// Model for coaching tasks. Floating alias by default.
    /// Override with dated ID (e.g., claude-sonnet-4-5-20250929) for pinned evals.
    /// </summary>
    public string ModelId { get; init; } = "claude-sonnet-4-6";

    /// <summary>
    /// Model for LLM-as-judge scoring. Use the most capable model available.
    /// </summary>
    public string JudgeModelId { get; init; } = "claude-opus-4-6";
}
```

Structure your configuration in layers using standard .NET config binding:

```json
// appsettings.json — floating defaults
{
  "CoachingLlm": {
    "ModelId": "claude-sonnet-4-6",
    "JudgeModelId": "claude-opus-4-6"
  }
}

// appsettings.Eval.json — pinned for regression baselines
{
  "CoachingLlm": {
    "ModelId": "claude-sonnet-4-5-20250929",
    "JudgeModelId": "claude-opus-4-6"
  }
}
```

Environment variable overrides (`CoachingLlm__ModelId=claude-sonnet-4-6`) let CI pipelines inject model IDs without config file changes. For your CI pipeline, run evals against both the pinned baseline and the latest alias, compare scores, and only promote a new model when it meets your quality thresholds. Version everything: datasets, prompts, rubrics, model IDs, and judge configurations.

## Conclusion

The root cause is model generation, not SDK version or API misconfiguration. **Claude Sonnet 4 (`claude-sonnet-4-20250514`) simply does not support structured output** — upgrade to `claude-sonnet-4-6` (or at minimum `claude-sonnet-4-5-20250929`) and the error disappears. Your SDK at v12.9.0 is already current and correctly exposes `OutputConfig.Format` for the GA structured output interface. For long-term maintainability, default to undated alias IDs in config, use the Models API for startup validation, and adopt a dual-track eval strategy that pins baselines while exploring new models. The judge model should always be the most capable available — `claude-opus-4-6` is the current best choice at **$5/$25 per million tokens**, providing superior reasoning for evaluation scoring compared to Sonnet-class models.