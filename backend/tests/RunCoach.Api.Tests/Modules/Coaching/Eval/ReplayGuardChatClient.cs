using Microsoft.Extensions.AI;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// A no-op <see cref="IChatClient"/> that throws on any call, used as the inner client
/// in Replay mode. When the M.E.AI caching layer has a cache hit, this client is never
/// invoked. On a cache miss, this client provides a descriptive error message telling
/// the developer exactly which scenario needs re-recording.
/// </summary>
public sealed class ReplayGuardChatClient : IChatClient
{
    private readonly string _scenarioName;

    public ReplayGuardChatClient(string scenarioName)
    {
        _scenarioName = scenarioName;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            $"Cache miss for scenario '{_scenarioName}'. " +
            "Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key " +
            "to regenerate the cache, then commit the updated cache files.");
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            $"Cache miss for scenario '{_scenarioName}'. " +
            "Run eval tests locally with EVAL_CACHE_MODE=Record and a valid API key " +
            "to regenerate the cache, then commit the updated cache files.");
    }

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}
