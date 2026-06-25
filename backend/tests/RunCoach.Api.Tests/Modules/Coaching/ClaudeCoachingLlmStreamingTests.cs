using Anthropic;
using Anthropic.Exceptions;
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

    [Theory]
    [InlineData("  ", "user")]
    [InlineData("system", "  ")]
    public void StreamAsync_BlankArgument_ThrowsEagerlyAtCallSite(string systemPrompt, string userMessage)
    {
        // The argument guards must run at call time — like the other (async Task) ICoachingLlm
        // methods — not be deferred to the first MoveNextAsync of the returned enumerable. A caller
        // that constructs the stream and enumerates later must still see the validation immediately.
        using var llm = BuildStreamingLlm(_ => ToAsyncStream(TextDelta("ignored")));

        // Act — calling the method (without enumerating) must throw.
        var act = () => llm.StreamAsync(systemPrompt, userMessage, TestContext.Current.CancellationToken);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task StreamAsync_ClientAbort_PropagatesCancellationUnwrapped()
    {
        // Arrange — the caller's token is cancelled and the stream surfaces an OperationCanceledException
        // (the RequestAborted shape). A genuine client abort is not-an-error: it must propagate
        // unwrapped, never reclassified into a Transient/Permanent service fault.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var llm = BuildStreamingLlm(_ => ThrowingStream(new OperationCanceledException(cts.Token)));

        // Act
        var act = async () =>
        {
            await foreach (var delta in llm.StreamAsync("system", "user", cts.Token))
            {
                _ = delta;
            }
        };

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.Should().NotBeAssignableTo<CoachingLlmException>();
    }

    [Fact]
    public async Task StreamAsync_SdkPerAttemptTimeout_TranslatesToTransient()
    {
        // Arrange — an OperationCanceledException arrives while the caller's token is NOT cancelled:
        // the SDK's per-attempt timeout fired. That is a transient service failure, not a user abort.
        using var llm = BuildStreamingLlm(_ => ThrowingStream(new OperationCanceledException()));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
    }

    [Fact]
    public async Task StreamAsync_TransportIOException_TranslatesToTransient()
    {
        // Arrange — a transport drop mid-stream (no Retry-After header to read).
        using var llm = BuildStreamingLlm(_ => ThrowingStream(
            new AnthropicIOException("connection reset", new HttpRequestException())));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert
        await act.Should().ThrowAsync<TransientCoachingLlmException>();
    }

    [Fact]
    public async Task StreamAsync_UnmodeledAnthropicException_TranslatesToPermanent()
    {
        // Arrange — any Anthropic SDK exception outside the modeled cases is terminal; the totality
        // contract still holds (only CoachingLlmException subtypes escape StreamAsync).
        using var llm = BuildStreamingLlm(_ => ThrowingStream(
            new AnthropicException("unmodeled", new InvalidOperationException())));

        // Act
        var act = async () => await CollectAsync(llm);

        // Assert
        await act.Should().ThrowAsync<PermanentCoachingLlmException>();
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

    /// <summary>
    /// A stub stream that yields the supplied updates (if any) and then throws — exercising the
    /// translator's mid-enumeration catch.
    /// </summary>
    private static async IAsyncEnumerable<ChatResponseUpdate> ThrowingStream(
        Exception toThrow,
        params ChatResponseUpdate[] before)
    {
        foreach (var update in before)
        {
            await Task.Yield();
            yield return update;
        }

        await Task.Yield();
        throw toThrow;
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
