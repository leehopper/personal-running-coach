using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Tests.Modules.Coaching.Sanitization;

/// <summary>
/// In-process OTel listener tests for <see cref="SanitizationAuditChatClient"/>.
/// Covers both the response and streaming paths plus eager null-arg validation
/// (sonar S4456 split), one-shot enumerable buffering, and the
/// <c>UseSanitizationAudit</c> extension wiring.
/// </summary>
public sealed class SanitizationAuditChatClientTests : IDisposable
{
    private readonly List<Activity> _captured = new();
    private readonly ActivityListener _listener;

    public SanitizationAuditChatClientTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "RunCoach.Llm",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                lock (_captured)
                {
                    _captured.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetResponseAsync_EmitsGuardrailSpanWithDocumentedAttributes()
    {
        // Arrange
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse()));

        using var sut = new SanitizationAuditChatClient(inner);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "system"),
            new(ChatRole.User, "user"),
            new(ChatRole.Assistant, "assistant"),
        };

        // Act
        _ = await sut.GetResponseAsync(messages, options: null, TestContext.Current.CancellationToken);

        // Assert — filter on the audit span name so a parallel test running
        // a different RunCoach.Llm span cannot bind these assertions to the
        // wrong activity (xUnit defaults to per-class parallelism).
        var span = _captured.FirstOrDefault(a =>
            a.OperationName == SanitizationAuditChatClient.AuditSpanName);

        span.Should().NotBeNull("audit client emits a rollup span around the inner call");

        var tags = span!.TagObjects.ToDictionary(kv => kv.Key, kv => kv.Value);
        tags.Should().ContainKey("openinference.span.kind").WhoseValue.Should().Be("GUARDRAIL");
        tags.Should().ContainKey("runcoach.sanitization.policy_version")
            .WhoseValue.Should().Be(PatternCatalog.PolicyVersion);
        tags.Should().ContainKey("runcoach.sanitization.audit.message_count")
            .WhoseValue.Should().Be(3);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_EmitsGuardrailSpanDuringIteration()
    {
        // Arrange
        var inner = Substitute.For<IChatClient>();
        inner
            .GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => FakeStream());

        using var sut = new SanitizationAuditChatClient(inner);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "u1"),
            new(ChatRole.User, "u2"),
            new(ChatRole.User, "u3"),
        };

        // Act — drive the async iterator so the span actually opens/closes.
        var count = 0;
        await foreach (var update in sut.GetStreamingResponseAsync(
            messages, options: null, TestContext.Current.CancellationToken))
        {
            update.Should().NotBeNull();
            count++;
        }

        count.Should().Be(2);

        // Assert
        var span = _captured.FirstOrDefault(a =>
            a.OperationName == SanitizationAuditChatClient.AuditSpanName);

        span.Should().NotBeNull("streaming path emits the same rollup span");

        var tags = span!.TagObjects.ToDictionary(kv => kv.Key, kv => kv.Value);
        tags.Should().ContainKey("openinference.span.kind").WhoseValue.Should().Be("GUARDRAIL");
        tags.Should().ContainKey("runcoach.sanitization.policy_version")
            .WhoseValue.Should().Be(PatternCatalog.PolicyVersion);
        tags.Should().ContainKey("runcoach.sanitization.audit.message_count")
            .WhoseValue.Should().Be(3);
    }

    [Fact]
    public async Task GetResponseAsync_NullMessages_ThrowsArgumentNullException()
    {
        // Arrange
        var inner = Substitute.For<IChatClient>();
        using var sut = new SanitizationAuditChatClient(inner);

        // Act
        var act = async () =>
            await sut.GetResponseAsync(null!, null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void GetStreamingResponseAsync_NullMessages_ThrowsArgumentNullExceptionEagerly()
    {
        // Arrange — sonar S4456: argument validation must happen at call time,
        // not lazily on first MoveNext, so the test does NOT iterate the result.
        var inner = Substitute.For<IChatClient>();
        using var sut = new SanitizationAuditChatClient(inner);

        // Act
        var act = () => sut.GetStreamingResponseAsync(
            null!, null, TestContext.Current.CancellationToken);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetResponseAsync_OneShotEnumerable_DoesNotDoubleEnumerate()
    {
        // Arrange — caller-supplied IEnumerable<ChatMessage> whose enumerator
        // throws on a second iteration. The audit client buffers via
        // `as IReadOnlyCollection<ChatMessage> ?? ToArray()` so both the tag
        // count stamp and the inner-client call see the same sequence without
        // re-enumerating.
        var inner = Substitute.For<IChatClient>();
        inner
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse()));

        using var sut = new SanitizationAuditChatClient(inner);
        var messages = new OneShotEnumerable(
            new ChatMessage(ChatRole.User, "u1"),
            new ChatMessage(ChatRole.User, "u2"));

        // Act
        var act = async () =>
            await sut.GetResponseAsync(messages, options: null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync(
            "buffering must collapse to a single iteration of the caller's sequence");

        var span = _captured.FirstOrDefault(a =>
            a.OperationName == SanitizationAuditChatClient.AuditSpanName);
        span.Should().NotBeNull();
        span!.GetTagItem("runcoach.sanitization.audit.message_count")
            .Should().Be(2);
    }

    [Fact]
    public void UseSanitizationAudit_WrapsInnerClient()
    {
        // Arrange
        var inner = Substitute.For<IChatClient>();

        // Act
        var wrapped = inner.UseSanitizationAudit();

        // Assert
        wrapped.Should().BeOfType<SanitizationAuditChatClient>();
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> FakeStream()
    {
        yield return new ChatResponseUpdate();
        yield return new ChatResponseUpdate();
        await Task.CompletedTask;
    }

    /// <summary>
    /// IEnumerable that lets exactly one enumerator run to completion and
    /// throws on any subsequent call to <see cref="GetEnumerator"/>. Detects
    /// double-enumeration without depending on side-effects in test data.
    /// </summary>
    private sealed class OneShotEnumerable : IEnumerable<ChatMessage>
    {
        private readonly ChatMessage[] _items;
        private int _enumerationCount;

        public OneShotEnumerable(params ChatMessage[] items)
        {
            _items = items;
        }

        public IEnumerator<ChatMessage> GetEnumerator()
        {
            var current = Interlocked.Increment(ref _enumerationCount);
            if (current > 1)
            {
                throw new InvalidOperationException(
                    "Sequence was iterated more than once — buffering broke.");
            }

            return ((IEnumerable<ChatMessage>)_items).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
