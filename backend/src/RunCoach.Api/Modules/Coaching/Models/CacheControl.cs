namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Anthropic prompt-cache breakpoint marker per DEC-047. Maps onto the
/// Anthropic SDK's <c>CacheControlEphemeral</c> at the adapter boundary.
/// The breakpoint is placed at the end of a content block to signal where
/// the cacheable prefix ends; everything before the breakpoint is hashed
/// for cache lookup, everything after is regenerated per call.
/// </summary>
/// <param name="Type">
/// The cache-control type discriminator. Anthropic accepts only
/// <c>"ephemeral"</c> at the time of writing (April 2026); reserved for
/// future cache-control variants.
/// </param>
/// <param name="Ttl">
/// The cache time-to-live. Anthropic accepts the literal strings
/// <c>"5m"</c> (default) and <c>"1h"</c>. The 1h tier costs 2x cache-write
/// tokens but pays back on workloads that exceed five minutes between calls.
/// </param>
/// <remarks>
/// <para>
/// This is a wire-shape-agnostic record so callers in <c>RunCoach.Api</c>
/// do not take a hard dependency on Anthropic SDK types. The
/// <see cref="ClaudeCoachingLlm"/> adapter translates this into the SDK's
/// <c>CacheControlEphemeral</c> when assembling the request.
/// </para>
/// <para>
/// Per Slice 1 § Unit 1 R01.7 / R01.11 / DEC-047, onboarding sets
/// <see cref="Ephemeral1h"/> on the system block so the prefix cache hits
/// from turn 2 onward. The current topic name MUST be placed in the user
/// message, NOT the system prompt — keeping the system prompt byte-stable
/// across topics is what makes the prefix cache useful in the first place.
/// </para>
/// </remarks>
public sealed record CacheControl
{
    /// <summary>The only Anthropic-supported cache-control type discriminator.</summary>
    private const string EphemeralType = "ephemeral";

    /// <summary>The supported Anthropic cache-control TTL values.</summary>
    private static readonly string[] SupportedTtls = ["5m", "1h"];

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheControl"/> record.
    /// Validates that <paramref name="type"/> and <paramref name="ttl"/> match the
    /// values Anthropic constrained decoding accepts; throws otherwise so the
    /// adapter never silently sends unsupported cache-control to the API.
    /// Prefer the <see cref="Ephemeral1h"/> / <see cref="Ephemeral5m"/> static
    /// factories over direct construction.
    /// </summary>
    /// <param name="type">The cache-control type discriminator. Must be <c>"ephemeral"</c>.</param>
    /// <param name="ttl">The cache TTL. Must be <c>"5m"</c> or <c>"1h"</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="type"/> or <paramref name="ttl"/> is not a value
    /// Anthropic accepts. The Anthropic API would reject unsupported values with HTTP 400;
    /// failing at construction surfaces the misconfiguration at the call site instead.
    /// </exception>
    public CacheControl(string type, string ttl)
    {
        if (!string.Equals(type, EphemeralType, StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                $"Anthropic cache_control.type must be \"{EphemeralType}\".");
        }

        if (Array.IndexOf(SupportedTtls, ttl) < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ttl),
                ttl,
                $"Anthropic cache_control.ttl must be one of: {string.Join(", ", SupportedTtls)}.");
        }

        Type = type;
        Ttl = ttl;
    }

    /// <summary>
    /// Gets the Anthropic <c>{ "type": "ephemeral", "ttl": "1h" }</c> breakpoint for
    /// long-prefix workloads (onboarding multi-turn, plan generation chain).
    /// </summary>
    public static CacheControl Ephemeral1h { get; } = new(EphemeralType, "1h");

    /// <summary>
    /// Gets the Anthropic <c>{ "type": "ephemeral", "ttl": "5m" }</c> breakpoint for
    /// short-burst workloads (default).
    /// </summary>
    public static CacheControl Ephemeral5m { get; } = new(EphemeralType, "5m");

    /// <summary>Gets the cache-control type discriminator. Always <c>"ephemeral"</c>.</summary>
    public string Type { get; init; }

    /// <summary>Gets the cache time-to-live: <c>"5m"</c> or <c>"1h"</c>.</summary>
    public string Ttl { get; init; }
}
