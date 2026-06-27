using Marten;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Infrastructure.Idempotency;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Wolverine command handler for <see cref="PostScriptedSafetyTurn"/> (Slice 4B PR4) —
/// persists a scripted Amber referral as a <see cref="CoachMessagePosted"/> coach turn on
/// the user-scoped <c>Conversation</c> stream. Idempotent on the caller-supplied,
/// server-derived <see cref="PostScriptedSafetyTurn.TurnId"/>, so a re-send after a
/// mid-stream answer failure re-derives the same id and never double-appends the
/// referral. Appends to the stream the durable-first user turn already created.
/// Wolverine's transactional middleware brackets a single Marten <c>SaveChangesAsync</c>;
/// the handler never calls it.
/// </summary>
/// <remarks>
/// Reuses the existing <see cref="CoachMessagePosted"/> event (no new event type, so the
/// <c>MartenConfiguration.RegisteredEventTypes</c> registration guard is untouched): on
/// the interactive stream a safety referral is a coach turn carrying scripted copy. It is
/// never errored — the content is deterministic system copy, always complete.
/// </remarks>
public sealed partial class PostScriptedSafetyTurnHandler
{
    // See PostCoachConversationTurnHandler — Wolverine codegen needs a non-static logical
    // handler type for ILogger<T>; the private constructor blocks instantiation.
    private PostScriptedSafetyTurnHandler()
    {
    }

    /// <summary>Persists the scripted safety coach turn, idempotent on the derived turn id.</summary>
    /// <param name="cmd">The post-scripted-safety-turn command.</param>
    /// <param name="session">Marten session bracketed by Wolverine's transactional middleware.</param>
    /// <param name="idempotency">Idempotency store backed by the same <paramref name="session"/>.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The persisted safety turn id (= the supplied derived id).</returns>
    public static async Task<ConversationTurnPostedResponse> Handle(
        PostScriptedSafetyTurn cmd,
        IDocumentSession session,
        IIdempotencyStore idempotency,
        ILogger<PostScriptedSafetyTurnHandler> logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(logger);

        var prior = await idempotency
            .SeenAsync<ConversationTurnPostedResponse>(cmd.TurnId, ct)
            .ConfigureAwait(false);
        if (prior is not null)
        {
            LogIdempotentReplay(logger, cmd.TurnId, cmd.UserId);
            return prior;
        }

        // The user turn is durable-first, so the stream already exists — append. A
        // scripted referral is never an errored marker (the content is always complete).
        session.Events.Append(
            cmd.UserId,
            new CoachMessagePosted(cmd.UserId, cmd.TurnId, cmd.Content, IsErrored: false));

        var response = new ConversationTurnPostedResponse(cmd.TurnId);
        idempotency.Record(cmd.TurnId, response);
        return response;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Scripted safety referral turn idempotent replay turnId={TurnId} user={UserId}")]
    private static partial void LogIdempotentReplay(ILogger logger, Guid turnId, Guid userId);
}
