using Anthropic;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Slice 4B Unit 1: <see cref="ClaudeCoachingLlm.StreamAsync"/> streams free-text
/// deltas and translates the Anthropic SDK streaming failure surface (R-084) into
/// the DEC-073 totality contract — only
/// <see cref="TransientCoachingLlmException"/> /
/// <see cref="PermanentCoachingLlmException"/> /
/// <see cref="IncompleteCoachingLlmException"/> escape, with a client abort
/// (<see cref="OperationCanceledException"/>) propagating unwrapped as not-an-error.
/// These unit tests drive a controllable stub <see cref="IChatClient"/> injected
/// through the streaming-client factory seam; the SSE wire behavior (whether
/// <c>AnthropicSseException</c> exposes the structured error type) is pinned by the
/// companion integration test through the real SDK.
/// </summary>
public sealed class ClaudeCoachingLlmStreamingTests
{
    private static readonly CoachingLlmSettings Settings = new()
    {
        ApiKey = "test-key",
        ModelId = "claude-sonnet-4-6",
        MaxTokens = 1024,
    };

    [Fact]
    public async Task StreamAsync_YieldsEachTextDeltaInArrivalOrder()
    {
        // Arrange — a clean stream of three text deltas.
        using var llm = BuildStreamingLlm(_ => ToAsyncStream(
            TextDelta("Easy "),
            TextDelta("does "),
            TextDelta("it.")));

        // Act
        var deltas = await CollectAsync(llm);

        // Assert
        deltas.Should().Equal("Easy ", "does ", "it.");
    }

    private static async Task<List<string>> CollectAsync(ClaudeCoachingLlm llm)
    {
        var deltas = new List<string>();
        await foreach (var delta in llm.StreamAsync("system", "user", TestContext.Current.CancellationToken))
        {
            deltas.Add(delta);
        }

        return deltas;
    }

    private static ChatResponseUpdate TextDelta(string text) => new(ChatRole.Assistant, text);

    private static async IAsyncEnumerable<ChatResponseUpdate> ToAsyncStream(params ChatResponseUpdate[] updates)
    {
        foreach (var update in updates)
        {
            await Task.Yield();
            yield return update;
        }
    }

    /// <summary>
    /// Builds an adapter whose streaming path is fed by the supplied stub stream.
    /// The substitute <see cref="IAnthropicClient"/> only satisfies the constructor;
    /// the injected factory short-circuits <see cref="ClaudeCoachingLlm.AsIChatClient"/>
    /// so no live transport is touched.
    /// </summary>
    private static ClaudeCoachingLlm BuildStreamingLlm(
        Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> stream)
    {
        var anthropic = Substitute.For<IAnthropicClient>();
        return new ClaudeCoachingLlm(
            anthropic,
            Settings,
            NullLogger<ClaudeCoachingLlm>.Instance,
            () => new StubStreamingChatClient(stream));
    }

    private sealed class StubStreamingChatClient(
        Func<CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> stream) : IChatClient
    {
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => stream(cancellationToken);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Streaming tests use GetStreamingResponseAsync only.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
