using System.Text;
using System.Text.Json;

namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Hand-rolled Server-Sent-Events frame writer for the streaming Q&amp;A endpoint
/// (Slice 4B PR4, <c>POST /api/v1/conversation/messages</c>). Serializes each frame as
/// <c>event: {name}\ndata: {json}\n\n</c> and flushes immediately, so a token reaches
/// the client the moment it is produced (response buffering is disabled on the SSE
/// path). Heartbeats are SSE comment lines (<c>: …\n\n</c>) that keep an idle connection
/// alive without the client parsing them as a message. No SSE dependency — the framing
/// is ~15 lines per the spec.
/// </summary>
/// <remarks>
/// Payloads serialize with the API's configured <see cref="JsonSerializerOptions"/>
/// (camelCase web defaults) so frames match every other API response — NOT the
/// snake_case structured-output options used for LLM constrained decoding.
/// </remarks>
public sealed class SseWriter(Stream output, JsonSerializerOptions jsonOptions)
{
    private static readonly byte[] HeartbeatBytes = ": hb\n\n"u8.ToArray();

    private readonly Stream _output = output;
    private readonly JsonSerializerOptions _jsonOptions = jsonOptions;

    /// <summary>Writes one typed SSE frame and flushes it.</summary>
    /// <param name="eventName">The SSE event name (e.g. <c>token</c>, <c>done</c>, <c>error</c>).</param>
    /// <param name="data">The frame payload; serialized to a single compact JSON data line.</param>
    /// <param name="ct">Cancellation token (the request-aborted token on the live path).</param>
    public async Task WriteEventAsync(string eventName, object data, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(data);

        // Compact (no indentation) so the JSON occupies a single `data:` line — a frame
        // is terminated by the blank line, so an embedded newline would split the frame.
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var frame = Encoding.UTF8.GetBytes($"event: {eventName}\ndata: {json}\n\n");

        await _output.WriteAsync(frame, ct).ConfigureAwait(false);
        await _output.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Writes an SSE heartbeat comment line and flushes it.</summary>
    /// <param name="ct">Cancellation token (the request-aborted token on the live path).</param>
    public async Task WriteHeartbeatAsync(CancellationToken ct)
    {
        await _output.WriteAsync(HeartbeatBytes, ct).ConfigureAwait(false);
        await _output.FlushAsync(ct).ConfigureAwait(false);
    }
}
