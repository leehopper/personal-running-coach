using Microsoft.Extensions.AI;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that throws a descriptive error on any LLM call,
/// used in Replay mode to catch cache misses before they reach the real API client.
///
/// Extends <see cref="DelegatingChatClient"/> (rather than bare <see cref="IChatClient"/>)
/// so that <c>GetService</c> passes through to the inner client — this preserves model
/// metadata that the M.E.AI caching layer includes in cache key computation.
///
/// Pipeline in Replay mode:
///   DiskBasedReportingConfig (caching) → ReplayGuardChatClient → AnthropicStructuredOutputClient → dummy IChatClient
///
/// On cache hit, the caching layer returns without reaching this client.
/// On cache miss, this client throws before any outbound network I/O.
/// </summary>
public sealed class ReplayGuardChatClient : DelegatingChatClient
{
    private readonly string _clientName;

    public ReplayGuardChatClient(IChatClient innerClient, string clientName)
        : base(innerClient)
    {
        _clientName = clientName;
    }

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            $"Cache miss for '{_clientName}' client. " +
            "Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key " +
            "to regenerate the cache, then commit the updated cache files.");
    }

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            $"Cache miss for '{_clientName}' client. " +
            "Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key " +
            "to regenerate the cache, then commit the updated cache files.");
    }
}
