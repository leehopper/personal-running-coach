using Marten;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Infrastructure.Idempotency;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Wolverine command handler for <see cref="PostUserConversationTurn"/> (Slice 4B
/// Unit 3, DEC-085) — the durable-first user-turn write. Idempotent on the client
/// message id, so a retried POST appends nothing the second time. Bootstraps the
/// user-scoped <c>Conversation</c> stream on the runner's first message, then
/// appends to it on subsequent turns. Wolverine's transactional middleware brackets
/// a single Marten <c>SaveChangesAsync</c> around the handler; it never calls
/// <c>SaveChangesAsync</c> itself.
/// </summary>
/// <remarks>
/// Two-write persistence (DEC-085): this user-turn write and the
/// <see cref="PostCoachConversationTurnHandler"/> coach-turn write are two separate
/// idempotent transactions, because the co-transactional idempotency + single-
/// <c>SaveChanges</c> model cannot span a multi-second stream.
/// </remarks>
public sealed partial class PostUserConversationTurnHandler
{
    // Wolverine codegen emits a non-static handler stub that calls the static Handle
    // method; ILogger<PostUserConversationTurnHandler> needs a non-static type. The
    // private constructor prevents instantiation in test code.
    private PostUserConversationTurnHandler()
    {
    }

    /// <summary>Persists the user turn (durable-first), idempotent on the client message id.</summary>
    /// <param name="cmd">The post-user-turn command.</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted turn id (= the client message id).</returns>
    public static async Task<ConversationTurnPostedResponse> Handle(
        PostUserConversationTurn cmd,
        IDocumentSession session,
        IIdempotencyStore idempotency,
        ILogger<PostUserConversationTurnHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(logger);

        // Idempotency short-circuit: a retried POST returns the byte-identical prior
        // response and appends nothing.
        var prior = await idempotency
            .SeenAsync<ConversationTurnPostedResponse>(cmd.ClientMessageId, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, cmd.ClientMessageId, cmd.UserId);
            return prior;
        }

        var streamId = cmd.UserId;
        var view = await session
            .LoadAsync<ConversationView>(streamId, ct)
            .ConfigureAwait(false);

        var @event = new UserMessagePosted(cmd.UserId, cmd.ClientMessageId, cmd.Content);

        // First message bootstraps the stream; subsequent messages append. The inline
        // InteractiveConversationProjection materializes the view in the same transaction.
        if (view is null)
        {
            session.Events.StartStream<ConversationView>(streamId, @event);
        }
        else
        {
            session.Events.Append(streamId, @event);
        }

        var response = new ConversationTurnPostedResponse(cmd.ClientMessageId);
        idempotency.Record(cmd.ClientMessageId, response);
        return response;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Interactive user turn idempotent replay clientMessageId={ClientMessageId} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid clientMessageId, Guid userId);
}
