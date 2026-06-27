using System.Text;
using System.Text.Json;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation.Streaming;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Unit tests over <see cref="SseWriter"/> — the hand-rolled Server-Sent-Events frame
/// serializer for the streaming Q&amp;A endpoint (Slice 4B PR4). Asserts the exact wire
/// framing (<c>event: …\ndata: …\n\n</c>), the heartbeat comment shape, camelCase JSON
/// payloads, and that every frame is flushed individually (SSE buffering off).
/// </summary>
public sealed class SseWriterTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task WriteEventAsync_EmitsEventThenDataThenBlankLine()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new SseWriter(stream, WebOptions);

        // Act
        await writer.WriteEventAsync("token", new { delta = "hi" }, TestContext.Current.CancellationToken);

        // Assert
        var actual = Encoding.UTF8.GetString(stream.ToArray());
        actual.Should().Be(
            "event: token\ndata: {\"delta\":\"hi\"}\n\n",
            because: "an SSE frame is the event name, a single compact JSON data line, then a blank line terminator");
    }

    [Fact]
    public async Task WriteEventAsync_SerializesPayloadCamelCase()
    {
        // Arrange — a PascalCase property must surface camelCase on the wire (JsonSerializerDefaults.Web),
        // matching every other API response and NOT the snake_case structured-output options.
        using var stream = new MemoryStream();
        var writer = new SseWriter(stream, WebOptions);

        // Act
        await writer.WriteEventAsync(
            "error",
            new ErrorPayload("boom", Retryable: true, RetryAfterSeconds: 5),
            TestContext.Current.CancellationToken);

        // Assert
        var actual = Encoding.UTF8.GetString(stream.ToArray());
        actual.Should().Be("event: error\ndata: {\"message\":\"boom\",\"retryable\":true,\"retryAfterSeconds\":5}\n\n");
    }

    [Fact]
    public async Task WriteHeartbeatAsync_EmitsACommentLine()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new SseWriter(stream, WebOptions);

        // Act
        await writer.WriteHeartbeatAsync(TestContext.Current.CancellationToken);

        // Assert — an SSE comment line starts with ':' and carries no event/data, so it
        // keeps the connection alive without the client treating it as a message.
        var actual = Encoding.UTF8.GetString(stream.ToArray());
        actual.Should().StartWith(":", because: "a heartbeat is an SSE comment, not an event");
        actual.Should().EndWith("\n\n");
        actual.Should().NotContain("event:", because: "a heartbeat must not parse as a typed frame");
    }

    [Fact]
    public async Task EachWrite_FlushesIndividually()
    {
        // Arrange — SSE requires buffering off so each token reaches the client as it is
        // produced; assert FlushAsync fires once per frame (and once per heartbeat).
        var inner = new MemoryStream();
        await using var flushTracker = new FlushCountingStream(inner);
        var writer = new SseWriter(flushTracker, WebOptions);

        // Act
        await writer.WriteEventAsync("token", new { delta = "a" }, TestContext.Current.CancellationToken);
        await writer.WriteEventAsync("token", new { delta = "b" }, TestContext.Current.CancellationToken);
        await writer.WriteHeartbeatAsync(TestContext.Current.CancellationToken);

        // Assert
        flushTracker.FlushCount.Should().Be(3, because: "every frame and heartbeat is flushed the moment it is written");
    }

    private sealed record ErrorPayload(string Message, bool Retryable, int? RetryAfterSeconds);

    /// <summary>A pass-through stream that counts <see cref="FlushAsync(CancellationToken)"/> calls.</summary>
    private sealed class FlushCountingStream(Stream inner) : Stream
    {
        public int FlushCount { get; private set; }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCount++;
            return inner.FlushAsync(cancellationToken);
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(buffer, cancellationToken);
    }
}
