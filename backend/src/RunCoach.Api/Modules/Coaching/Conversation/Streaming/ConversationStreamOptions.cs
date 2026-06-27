namespace RunCoach.Api.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Tunables for the streaming Q&amp;A endpoint (Slice 4B PR4), bound from the
/// <c>Conversation</c> configuration section (all defaults sensible, so the section is
/// optional).
/// </summary>
public sealed record ConversationStreamOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Conversation";

    /// <summary>
    /// Gets the back-off (seconds) surfaced on a <b>mid-stream</b> transient failure. The
    /// adapter cannot capture a <c>Retry-After</c> header once bytes have streamed (R-084),
    /// so a mid-stream transient carries a null hint; the endpoint substitutes this
    /// configured default when it surfaces the retry affordance to the client. Default 5s.
    /// </summary>
    public int DefaultMidStreamRetryAfterSeconds { get; init; } = 5;
}
