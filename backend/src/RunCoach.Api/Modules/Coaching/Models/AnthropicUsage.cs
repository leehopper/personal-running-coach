namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Token-usage breakdown returned alongside a structured-output LLM call.
/// Mirrors the four Anthropic `usage` fields the platform exposes per
/// <c>messages</c> response: total input tokens, total output tokens, plus the
/// two prompt-cache breakdown counters that let downstream consumers compute
/// cache effectiveness (hit-rate = <c>CacheReadInputTokens / (CacheReadInputTokens
/// + CacheCreationInputTokens + InputTokens)</c>).
/// </summary>
/// <remarks>
/// <para>
/// Slice 1 § Unit 2 R02.8 calls out <c>cache_hit_rate</c> as a required
/// telemetry attribute on the <c>runcoach.plan.generation.completed</c> OTel
/// event. The original <see cref="ICoachingLlm.GenerateStructuredAsync{T}(string, string, CancellationToken)"/>
/// signature returned only the deserialized output, dropping these counters on
/// the floor — the <c>PlanGenerationService</c> rollup span had no source for
/// the rate. Surfacing them via this record keeps the orchestration layer
/// authoritative without re-bridging through the underlying SDK span.
/// </para>
/// <para>
/// Per Anthropic's wire schema the values are non-negative integers. They are
/// modelled here as <see cref="long"/> to match the SDK property type and to
/// avoid overflow on chains that aggregate across the six-call macro / meso /
/// micro pipeline.
/// </para>
/// </remarks>
/// <param name="InputTokens">
/// Number of <em>fresh</em> input tokens consumed (i.e. tokens that were neither
/// served from cache nor used to create a new cache entry on this call).
/// </param>
/// <param name="OutputTokens">Number of output tokens emitted by the model.</param>
/// <param name="CacheCreationInputTokens">
/// Number of input tokens that were used to create a new prompt-cache entry on
/// this call. Counts towards billing at the cache-creation rate.
/// </param>
/// <param name="CacheReadInputTokens">
/// Number of input tokens that were served from a prior prompt-cache entry on
/// this call. Counts towards billing at the cache-read rate.
/// </param>
public sealed record AnthropicUsage(
    long InputTokens,
    long OutputTokens,
    long CacheCreationInputTokens,
    long CacheReadInputTokens)
{
    /// <summary>
    /// Zero-valued usage record. Used when an upstream call returns no usage
    /// information (defensive default) so downstream accumulation arithmetic
    /// stays well-defined.
    /// </summary>
    public static readonly AnthropicUsage Zero = new(0, 0, 0, 0);

    /// <summary>
    /// Returns a new usage record summing the per-field counters of this and
    /// <paramref name="other"/>. Used by orchestration layers that aggregate
    /// usage across a multi-call chain (e.g. plan generation's six-call
    /// macro / meso / micro pipeline).
    /// </summary>
    public AnthropicUsage Add(AnthropicUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AnthropicUsage(
            InputTokens + other.InputTokens,
            OutputTokens + other.OutputTokens,
            CacheCreationInputTokens + other.CacheCreationInputTokens,
            CacheReadInputTokens + other.CacheReadInputTokens);
    }
}
