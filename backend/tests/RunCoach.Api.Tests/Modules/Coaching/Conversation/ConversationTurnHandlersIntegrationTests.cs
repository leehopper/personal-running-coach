using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Tests.Infrastructure;
using Wolverine;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Integration coverage for the two-write interactive-turn persistence handlers
/// (Slice 4B Unit 3, DEC-085) driven through the live Wolverine bus —
/// <see cref="PostUserConversationTurn"/> then
/// <see cref="PostCoachConversationTurn"/>. Proves the user turn is durable-first, the
/// coach turn appends on completion, and each write is independently idempotent on its
/// own key (client id for the user turn, server-derived id for the coach turn) — two
/// separate idempotent writes, never one transaction spanning the stream.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConversationTurnHandlersIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    [Fact]
    public async Task PostUserTurn_ThenPostCoachTurn_AsTwoSeparateWrites_MaterializeTwoTurns()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();

        // Act — two separate bus invocations (two transactions), durable-first.
        var userResponse = await PostUserTurnAsync(userId, clientMessageId, "Did my 5 easy, knee felt tight.");
        var coachResponse = await PostCoachTurnAsync(userId, clientMessageId, "Noted — ice it; we'll watch Thursday.", isErrored: false);

        // Assert — the responses carry the expected turn ids (client id / derived id).
        userResponse.TurnId.Should().Be(clientMessageId);
        coachResponse.TurnId.Should().Be(
            ConversationTurnId.DeriveCoachTurnId(clientMessageId),
            because: "the coach turn id is derived deterministically from the user turn's client id");

        // Assert — the view holds both turns in order.
        var view = await LoadConversationAsync(userId);
        view!.Turns.Should().HaveCount(2);
        view.Turns[0].Participant.Should().Be(ConversationParticipant.User);
        view.Turns[0].TurnId.Should().Be(clientMessageId);
        view.Turns[1].Participant.Should().Be(ConversationParticipant.Coach);
        view.Turns[1].Content.Should().Be("Noted — ice it; we'll watch Thursday.");
    }

    [Fact]
    public async Task PostUserTurn_Twice_IsIdempotentOnClientMessageId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();

        // Act — a retried user-turn POST (same client id).
        var first = await PostUserTurnAsync(userId, clientMessageId, "How's my week look?");
        var second = await PostUserTurnAsync(userId, clientMessageId, "How's my week look?");

        // Assert — one turn, the byte-identical prior response replayed.
        second.Should().Be(first, because: "the retried write returns the recorded response");
        var view = await LoadConversationAsync(userId);
        view!.Turns.Should().ContainSingle().Which.Participant.Should().Be(ConversationParticipant.User);
    }

    [Fact]
    public async Task PostCoachTurn_Twice_IsIdempotentOnServerDerivedId()
    {
        // Arrange — a user turn, then the coach completion delivered twice for the
        // same user turn (same client id → same derived coach id).
        var userId = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();
        await PostUserTurnAsync(userId, clientMessageId, "ping");

        // Act
        var first = await PostCoachTurnAsync(userId, clientMessageId, "first reply", isErrored: false);
        var second = await PostCoachTurnAsync(userId, clientMessageId, "second reply", isErrored: false);

        // Assert — exactly one coach turn; the duplicate completion did not double-append.
        second.Should().Be(first);
        var view = await LoadConversationAsync(userId);
        view!.Turns.Should().HaveCount(2, because: "one user turn + one coach turn — the duplicate coach write is idempotent");
        view.Turns.Single(t => t.Participant == ConversationParticipant.Coach).Content.Should().Be("first reply");
    }

    [Fact]
    public async Task PostCoachTurn_Errored_PersistsErroredMarker_WithoutPartialText()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clientMessageId = Guid.NewGuid();
        await PostUserTurnAsync(userId, clientMessageId, "How's the build going?");

        // Act — the stream died mid-flight: persist an errored marker, not the partial.
        await PostCoachTurnAsync(userId, clientMessageId, "partial advice cut off", isErrored: true);

        // Assert
        var view = await LoadConversationAsync(userId);
        var coachTurn = view!.Turns.Single(t => t.Participant == ConversationParticipant.Coach);
        coachTurn.IsErrored.Should().BeTrue();
        coachTurn.Content.Should().BeEmpty(because: "an errored marker never persists a partial as complete");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task<ConversationTurnPostedResponse> PostUserTurnAsync(Guid userId, Guid clientMessageId, string content)
    {
        using var scope = Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus
            .InvokeForTenantAsync<ConversationTurnPostedResponse>(
                userId.ToString(),
                new PostUserConversationTurn(userId, clientMessageId, content),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ConversationTurnPostedResponse> PostCoachTurnAsync(Guid userId, Guid clientMessageId, string content, bool isErrored)
    {
        using var scope = Factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus
            .InvokeForTenantAsync<ConversationTurnPostedResponse>(
                userId.ToString(),
                new PostCoachConversationTurn(userId, clientMessageId, content, isErrored, LoggedRun: null),
                TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ConversationView?> LoadConversationAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        return await session.LoadAsync<ConversationView>(userId, TestContext.Current.CancellationToken);
    }
}
