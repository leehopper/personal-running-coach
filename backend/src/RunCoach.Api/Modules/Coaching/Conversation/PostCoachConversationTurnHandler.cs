using Marten;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Infrastructure.Idempotency;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Wolverine command handler for <see cref="PostCoachConversationTurn"/> (Slice 4B
/// Unit 3, DEC-085) — the on-completion coach-turn write (or an errored-turn marker).
/// Idempotent on the server-derived turn id, so a duplicate completion for the same
/// user turn never double-appends. Appends to the user-scoped <c>Conversation</c>
/// stream the user turn already created (durable-first ordering). Wolverine's
/// transactional middleware brackets a single Marten <c>SaveChangesAsync</c>.
/// </summary>
/// <remarks>
/// Never persists a partial as complete: when <see cref="PostCoachConversationTurn.IsErrored"/>
/// is set, the appended turn carries empty content and an errored flag, so a stream
/// that died mid-flight leaves an explicit marker rather than truncated advice.
/// </remarks>
public sealed partial class PostCoachConversationTurnHandler
{
    // See PostUserConversationTurnHandler — Wolverine codegen needs a non-static
    // logical handler type; the private constructor blocks instantiation.
    private PostCoachConversationTurnHandler()
    {
    }

    /// <summary>Persists the coach turn on completion, idempotent on the server-derived turn id.</summary>
    /// <param name="cmd">The post-coach-turn command.</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted coach turn id (server-derived).</returns>
    public static async Task<ConversationTurnPostedResponse> Handle(
        PostCoachConversationTurn cmd,
        IDocumentSession session,
        IIdempotencyStore idempotency,
        ILogger<PostCoachConversationTurnHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(logger);

        // The coach turn id is derived from the user turn's client id, never random,
        // so a duplicate completion re-derives the same id and short-circuits here.
        var coachTurnId = ConversationTurnId.DeriveCoachTurnId(cmd.ClientMessageId);

        var prior = await idempotency
            .SeenAsync<ConversationTurnPostedResponse>(coachTurnId, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, coachTurnId, cmd.UserId);
            return prior;
        }

        // An errored marker never carries partial text — a truncated reply is recorded
        // as incomplete, not stored as a complete turn.
        var content = cmd.IsErrored ? string.Empty : cmd.Content;

        // The user turn is durable-first, so the stream already exists — append.
        session.Events.Append(
            cmd.UserId,
            new CoachMessagePosted(cmd.UserId, coachTurnId, content, cmd.IsErrored, cmd.LoggedRun));

        var response = new ConversationTurnPostedResponse(coachTurnId);
        idempotency.Record(coachTurnId, response);
        return response;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Interactive coach turn idempotent replay coachTurnId={CoachTurnId} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid coachTurnId, Guid userId);
}
