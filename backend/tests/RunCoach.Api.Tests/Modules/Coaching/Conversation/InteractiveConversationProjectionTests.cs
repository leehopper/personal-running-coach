using FluentAssertions;
using JasperFx.Events;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Pure-function tests over the <see cref="InteractiveConversationProjection"/>
/// Create/Apply methods and the event-type registration (Slice 4B Unit 3, DEC-085).
/// The Marten wiring (inline projection over a real stream) is covered by
/// <see cref="InteractiveConversationProjectionIntegrationTests"/>; these exercise the
/// in-memory view mutation directly, including the per-turn-id <c>Upsert</c> dedup
/// that backstops the two-write handler idempotency.
/// </summary>
public sealed class InteractiveConversationProjectionTests
{
    private static readonly Guid UserId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_FromUserMessage_SeedsViewKeyedByUserId_WithFirstTurn()
    {
        // Arrange
        var turnId = Guid.NewGuid();
        var @event = UserEvent(turnId, "Did my 5 easy, knee felt tight.", version: 1);

        // Act
        var view = InteractiveConversationProjection.Create(@event);

        // Assert — the stream id (= user id) becomes the document identity, and the
        // first user turn is recorded.
        view.Id.Should().Be(UserId, because: "the user-scoped stream is keyed by user id");
        view.UserId.Should().Be(UserId);
        var turn = view.Turns.Should().ContainSingle().Subject;
        turn.TurnId.Should().Be(turnId);
        turn.Participant.Should().Be(ConversationParticipant.User);
        turn.Content.Should().Be("Did my 5 easy, knee felt tight.");
        turn.IsErrored.Should().BeFalse();
        turn.EventVersion.Should().Be(1);
        turn.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void Apply_UserThenCoach_AppendsBothTurnsInOrder()
    {
        // Arrange
        var userTurnId = Guid.NewGuid();
        var coachTurnId = Guid.NewGuid();
        var view = InteractiveConversationProjection.Create(UserEvent(userTurnId, "How's my week look?", version: 1));

        // Act
        InteractiveConversationProjection.Apply(
            CoachEvent(coachTurnId, "Solid. Tempo holds Thursday.", isErrored: false, version: 2),
            view);

        // Assert
        view.Turns.Should().HaveCount(2);
        view.Turns[0].Participant.Should().Be(ConversationParticipant.User);
        view.Turns[1].Participant.Should().Be(ConversationParticipant.Coach);
        view.Turns[1].TurnId.Should().Be(coachTurnId);
        view.Turns[1].Content.Should().Be("Solid. Tempo holds Thursday.");
    }

    [Fact]
    public void Apply_CoachWithSameTurnId_ReplacesInPlace_OneTurn()
    {
        // Arrange — the per-turn-id dedup overwrites a turn re-applied under the same
        // id (replay, or a duplicate coach append the handler idempotency should have
        // caught) rather than appending a duplicate.
        var coachTurnId = Guid.NewGuid();
        var view = InteractiveConversationProjection.Create(UserEvent(Guid.NewGuid(), "ping", version: 1));

        // Act
        InteractiveConversationProjection.Apply(CoachEvent(coachTurnId, "first", isErrored: false, version: 2), view);
        InteractiveConversationProjection.Apply(CoachEvent(coachTurnId, "second", isErrored: false, version: 2), view);

        // Assert — still one coach turn, carrying the replacement payload.
        view.Turns.Should().HaveCount(2, because: "the duplicate coach id replaces in place rather than appending");
        var coachTurn = view.Turns.Single(t => t.Participant == ConversationParticipant.Coach);
        coachTurn.TurnId.Should().Be(coachTurnId);
        coachTurn.Content.Should().Be("second");
    }

    [Fact]
    public void Apply_ErroredCoach_MaterializesErroredTurn_WithEmptyContent()
    {
        // Arrange
        var view = InteractiveConversationProjection.Create(UserEvent(Guid.NewGuid(), "ping", version: 1));

        // Act — a mid-flight failure persists an errored marker; its partial text must
        // never render as a complete reply.
        InteractiveConversationProjection.Apply(
            CoachEvent(Guid.NewGuid(), "partial text that must be discarded", isErrored: true, version: 2),
            view);

        // Assert
        var coachTurn = view.Turns.Single(t => t.Participant == ConversationParticipant.Coach);
        coachTurn.IsErrored.Should().BeTrue();
        coachTurn.Content.Should().BeEmpty(because: "a truncated reply is never stored as complete content");
        view.Turns.Should().NotContain(
            t => t.Participant == ConversationParticipant.Coach && !t.IsErrored,
            because: "no complete coach turn is materialized for an errored message");
    }

    [Fact]
    public void Apply_DistinctTurnIds_AppendOneTurnEach()
    {
        // Arrange
        var view = InteractiveConversationProjection.Create(UserEvent(Guid.NewGuid(), "one", version: 1));

        // Act
        InteractiveConversationProjection.Apply(CoachEvent(Guid.NewGuid(), "two", isErrored: false, version: 2), view);
        InteractiveConversationProjection.Apply(UserEvent(Guid.NewGuid(), "three", version: 3), view);

        // Assert
        view.Turns.Should().HaveCount(3);
        view.Turns.Select(t => t.TurnId).Should().OnlyHaveUniqueItems();
        view.Turns.Select(t => t.EventVersion).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void RegisteredEventTypes_IncludesInteractiveConversationEvents()
    {
        // The composition guard (MartenStoreOptionsCompositionTests) tags every
        // registered event `_v1`; this asserts the two interactive events are actually
        // on that list, so removing either fails here (DEC-067 registration hygiene).
        MartenConfiguration.RegisteredEventTypes.Should().Contain(typeof(UserMessagePosted));
        MartenConfiguration.RegisteredEventTypes.Should().Contain(typeof(CoachMessagePosted));
    }

    private static Event<UserMessagePosted> UserEvent(Guid turnId, string content, long version) =>
        new(new UserMessagePosted(UserId, turnId, content))
        {
            Id = Guid.NewGuid(),
            Version = version,
            Timestamp = CreatedAt,
        };

    private static Event<CoachMessagePosted> CoachEvent(Guid turnId, string content, bool isErrored, long version) =>
        new(new CoachMessagePosted(UserId, turnId, content, isErrored))
        {
            Id = Guid.NewGuid(),
            Version = version,
            Timestamp = CreatedAt,
        };
}
