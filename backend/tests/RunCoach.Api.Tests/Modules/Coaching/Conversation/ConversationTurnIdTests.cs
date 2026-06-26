using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Pure-function tests over <see cref="ConversationTurnId.DeriveCoachTurnId"/> — the
/// deterministic server-side coach-turn id derived from the user turn's client
/// message id (Slice 4B Unit 3, DEC-085). The derivation must be stable (a duplicate
/// completion re-derives the same idempotency key), distinct from the client id (the
/// user turn already consumed it), and collision-free across distinct client ids.
/// </summary>
public sealed class ConversationTurnIdTests
{
    private static readonly Guid ClientMessageId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void DeriveCoachTurnId_IsDeterministic_SameInputSameOutput()
    {
        // Act
        var first = ConversationTurnId.DeriveCoachTurnId(ClientMessageId);
        var second = ConversationTurnId.DeriveCoachTurnId(ClientMessageId);

        // Assert
        second.Should().Be(
            first,
            because: "a duplicate completion must re-derive the same idempotency key so the coach turn is never double-appended");
    }

    [Fact]
    public void DeriveCoachTurnId_DiffersFromClientMessageId()
    {
        // Act
        var actual = ConversationTurnId.DeriveCoachTurnId(ClientMessageId);

        // Assert
        actual.Should().NotBe(
            ClientMessageId,
            because: "the user turn already keyed its idempotency marker on the client id; the coach turn needs a distinct key");
        actual.Should().NotBe(Guid.Empty, because: "the derived id must be a usable, non-empty GUID");
    }

    [Fact]
    public void DeriveCoachTurnId_DistinctClientIds_ProduceDistinctCoachIds()
    {
        // Arrange
        var otherClientId = new Guid("22222222-2222-2222-2222-222222222222");

        // Act
        var first = ConversationTurnId.DeriveCoachTurnId(ClientMessageId);
        var second = ConversationTurnId.DeriveCoachTurnId(otherClientId);

        // Assert
        second.Should().NotBe(
            first,
            because: "a re-send with a fresh client id must derive a separate coach turn, not collide with the original");
    }
}
