using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Integration coverage for the net-new user-scoped <c>Conversation</c> stream
/// (Slice 4B Unit 3, DEC-085) over the real <see cref="RunCoachAppFactory"/> SUT +
/// Testcontainers Postgres: appending <see cref="UserMessagePosted"/> /
/// <see cref="CoachMessagePosted"/> to the user-keyed stream materializes the inline
/// <see cref="ConversationView"/> via <see cref="InteractiveConversationProjection"/>.
/// These fail loudly if either event is unregistered or the projection is not wired
/// (<c>SkipUnknownEvents=true</c> would otherwise drop an unhandled event silently).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class InteractiveConversationProjectionIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task UserThenCoachMessage_MaterializesTwoInteractiveTurns_KeyedByUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTurnId = Guid.NewGuid();
        var coachTurnId = Guid.NewGuid();

        // Act — durable-first user turn creates the stream; the coach turn appends.
        await StartConversationAsync(userId, new UserMessagePosted(userId, userTurnId, "Did my 5 easy, knee felt tight."));
        await AppendConversationAsync(userId, new CoachMessagePosted(userId, coachTurnId, "Noted. Ice it; we'll watch Thursday.", false));

        // Assert — the view is keyed by user id and holds the two turns in order.
        var view = await LoadConversationAsync(userId);
        view!.Id.Should().Be(userId);
        view.UserId.Should().Be(userId);
        view.Turns.Should().HaveCount(2);

        var first = view.Turns[0];
        first.Participant.Should().Be(ConversationParticipant.User);
        first.TurnId.Should().Be(userTurnId);
        first.Content.Should().Be("Did my 5 easy, knee felt tight.");
        first.IsErrored.Should().BeFalse();
        first.CreatedAt.Should().NotBe(default);

        var second = view.Turns[1];
        second.Participant.Should().Be(ConversationParticipant.Coach);
        second.TurnId.Should().Be(coachTurnId);
        second.Content.Should().Be("Noted. Ice it; we'll watch Thursday.");
    }

    [Fact]
    public async Task CoachTurn_AppendedTwiceWithSameTurnId_UpsertsToOneTurn()
    {
        // Arrange — user turn, then the SAME coach turn id appended twice (two distinct
        // Marten events). The projection's per-turn-id upsert collapses them to one.
        var userId = Guid.NewGuid();
        var coachTurnId = Guid.NewGuid();
        await StartConversationAsync(userId, new UserMessagePosted(userId, Guid.NewGuid(), "ping"));

        // Act
        await AppendConversationAsync(userId, new CoachMessagePosted(userId, coachTurnId, "first", false));
        await AppendConversationAsync(userId, new CoachMessagePosted(userId, coachTurnId, "second", false));

        // Assert — one user + one coach turn; the coach turn carries the latest payload.
        var view = await LoadConversationAsync(userId);
        view!.Turns.Should().HaveCount(2, because: "the duplicate coach turn id upserts in place");
        var coachTurn = view.Turns.Single(t => t.Participant == ConversationParticipant.Coach);
        coachTurn.TurnId.Should().Be(coachTurnId);
        coachTurn.Content.Should().Be("second");
    }

    [Fact]
    public async Task ErroredCoachTurn_MaterializesErroredTurn_NeverComplete()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await StartConversationAsync(userId, new UserMessagePosted(userId, Guid.NewGuid(), "How's the build going?"));

        // Act — a mid-flight failure persists an errored marker carrying no usable text.
        await AppendConversationAsync(
            userId,
            new CoachMessagePosted(userId, Guid.NewGuid(), "partial advice cut off mid-sentence", true));

        // Assert
        var view = await LoadConversationAsync(userId);
        var coachTurn = view!.Turns.Single(t => t.Participant == ConversationParticipant.Coach);
        coachTurn.IsErrored.Should().BeTrue();
        coachTurn.Content.Should().BeEmpty(because: "an errored marker never renders partial coaching advice as complete");
        view.Turns.Should().NotContain(
            t => t.Participant == ConversationParticipant.Coach && !t.IsErrored,
            because: "no complete coach turn is materialized for the failed message");
    }

    [Fact]
    public async Task IdempotentReplay_AggregatingTheStream_YieldsExactlyOneTurnPerEvent()
    {
        // Arrange — one user + one coach turn on the stream.
        var userId = Guid.NewGuid();
        await StartConversationAsync(userId, new UserMessagePosted(userId, Guid.NewGuid(), "morning"));
        await AppendConversationAsync(userId, new CoachMessagePosted(userId, Guid.NewGuid(), "morning — easy day today", false));

        var inline = await LoadConversationAsync(userId);
        inline!.Turns.Should().HaveCount(2);

        // Act — live-aggregate the stream from scratch through the projection.
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var replayed = await session.Events.AggregateStreamAsync<ConversationView>(
            userId,
            token: TestContext.Current.CancellationToken);

        // Assert — replay yields exactly one turn per event, no duplicates.
        replayed.Should().NotBeNull();
        replayed!.Turns.Should().HaveCount(2, because: "replay produces exactly one turn per event");
        replayed.Turns.Select(t => t.TurnId).Should().OnlyHaveUniqueItems();
        replayed.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.User);
        replayed.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task StartConversationAsync(Guid userId, params object[] events)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.StartStream<ConversationView>(userId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AppendConversationAsync(Guid userId, params object[] events)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.Append(userId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ConversationView?> LoadConversationAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        return await session.LoadAsync<ConversationView>(userId, TestContext.Current.CancellationToken);
    }
}
