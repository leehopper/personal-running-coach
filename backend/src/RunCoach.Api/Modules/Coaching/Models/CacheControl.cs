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
public sealed record CacheControl(string Type, string Ttl)
{
    /// <summary>
    /// Gets anthropic <c>{ "type": "ephemeral", "ttl": "1h" }</c> breakpoint for
    /// long-prefix workloads (onboarding multi-turn, plan generation chain).
    /// </summary>
    public static CacheControl Ephemeral1h { get; } = new("ephemeral", "1h");

    /// <summary>
    /// Gets anthropic <c>{ "type": "ephemeral", "ttl": "5m" }</c> breakpoint for
    /// short-burst workloads (default).
    /// </summary>
    public static CacheControl Ephemeral5m { get; } = new("ephemeral", "5m");
}
