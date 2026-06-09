namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Captures the raw <c>Retry-After</c> header value (in seconds) seen by
/// <see cref="RetryAfterCaptureHandler"/> during one logical coaching-LLM call, so the
/// DEC-073 translation in <see cref="ClaudeCoachingLlm"/> can attach it to a
/// <see cref="TransientCoachingLlmException"/>. The Anthropic SDK 12.24.1 surfaces no header
/// accessor on its exceptions, so this is the only path to the value.
/// </summary>
/// <remarks>
/// The <see cref="AsyncLocal{T}"/> holds a single-element <c>int?[]</c> rather than an
/// <c>int?</c> directly: the HTTP handler runs in a child async flow, and a child's write to an
/// <see cref="AsyncLocal{T}"/> value is not visible to its parent. The parent allocates a fresh
/// mutable array in <see cref="BeginScope"/>; the child mutates that same array reference in
/// <see cref="Record"/>; the parent then reads it in <see cref="CurrentSeconds"/>.
/// </remarks>
internal static class RetryAfterCapture
{
    private static readonly AsyncLocal<int?[]?> Slot = new();

    /// <summary>
    /// Gets the retry-after value captured during the active scope, or <see langword="null"/>
    /// when no scope is active or no <c>Retry-After</c> header was seen.
    /// </summary>
    public static int? CurrentSeconds => Slot.Value?[0];

    /// <summary>
    /// Begins a per-call capture scope. Dispose the returned scope (via <c>using</c>) to clear it.
    /// </summary>
    public static RetryAfterCaptureScope BeginScope()
    {
        Slot.Value = new int?[1];
        return default;
    }

    /// <summary>Records the most recently observed retry-after value for the active scope.</summary>
    /// <param name="retryAfterSeconds">The retry-after delay in seconds read from the raw header.</param>
    public static void Record(int retryAfterSeconds)
    {
        var slot = Slot.Value;
        if (slot is not null)
        {
            slot[0] = retryAfterSeconds;
        }
    }

    /// <summary>Clears the active scope. Called by <see cref="RetryAfterCaptureScope.Dispose"/>.</summary>
    internal static void ClearScope() => Slot.Value = null;
}
