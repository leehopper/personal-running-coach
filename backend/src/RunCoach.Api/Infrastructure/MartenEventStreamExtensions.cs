using Marten;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Extension helpers for RunCoach's per-user Marten event streams.
/// </summary>
internal static class MartenEventStreamExtensions
{
    /// <summary>
    /// Appends <paramref name="firstEvent"/> to the stream at <paramref name="streamId"/>,
    /// starting the stream (tagged as <typeparamref name="TAggregate"/>) only when no physical
    /// stream exists there yet, otherwise appending. Several single-stream projections are keyed
    /// by the bare user id (e.g. the onboarding profile and the interactive conversation view),
    /// so more than one aggregate materializes from ONE physical stream at that id. Bootstrap
    /// must therefore key off physical stream existence, not any single projection's materialized
    /// document: a <c>StartStream</c> over an already-existing stream throws
    /// <c>ExistingStreamIdCollisionException</c>. The inline projection still runs its
    /// <c>Create</c> overload for the first event it recognizes (keyed on aggregate existence,
    /// not stream position), so appending a bootstrap event mid-stream still materializes the view.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate type tagging the stream when it is started.</typeparam>
    /// <param name="session">The Marten session (bracketed by Wolverine's transaction).</param>
    /// <param name="streamId">The per-user stream id.</param>
    /// <param name="firstEvent">The event to start-or-append.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task StartStreamOrAppendAsync<TAggregate>(
        this IDocumentSession session,
        Guid streamId,
        object firstEvent,
        CancellationToken ct)
        where TAggregate : class
    {
        ArgumentNullException.ThrowIfNull(session);

        var state = await session.Events.FetchStreamStateAsync(streamId, ct).ConfigureAwait(false);
        if (state is null)
        {
            session.Events.StartStream<TAggregate>(streamId, firstEvent);
        }
        else
        {
            session.Events.Append(streamId, firstEvent);
        }
    }
}
