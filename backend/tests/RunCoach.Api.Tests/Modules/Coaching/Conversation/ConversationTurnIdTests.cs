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

    [Fact]
    public void DeriveSafetyTurnId_IsDeterministic_SameInputSameOutput()
    {
        // Act
        var first = ConversationTurnId.DeriveSafetyTurnId(ClientMessageId);
        var second = ConversationTurnId.DeriveSafetyTurnId(ClientMessageId);

        // Assert
        second.Should().Be(
            first,
            because: "a re-send after mid-stream death must re-derive the same scripted-referral idempotency key so the Amber referral is never double-appended");
    }

    [Fact]
    public void DeriveSafetyTurnId_DiffersFromCoachTurnId_ForTheSameClientId()
    {
        // Act
        var safetyTurnId = ConversationTurnId.DeriveSafetyTurnId(ClientMessageId);
        var coachTurnId = ConversationTurnId.DeriveCoachTurnId(ClientMessageId);

        // Assert
        safetyTurnId.Should().NotBe(
            coachTurnId,
            because: "an Amber message persists BOTH a scripted referral turn and a streamed answer turn off the same client id; they must occupy distinct idempotency keys or one would overwrite the other");
    }

    [Fact]
    public void DeriveSafetyTurnId_DiffersFromClientMessageId()
    {
        // Act
        var actual = ConversationTurnId.DeriveSafetyTurnId(ClientMessageId);

        // Assert
        actual.Should().NotBe(
            ClientMessageId,
            because: "the user turn already keyed its idempotency marker on the client id; the scripted referral turn needs a distinct key");
        actual.Should().NotBe(Guid.Empty, because: "the derived id must be a usable, non-empty GUID");
    }

    [Fact]
    public void DeriveSafetyTurnId_DistinctClientIds_ProduceDistinctSafetyIds()
    {
        // Arrange
        var otherClientId = new Guid("22222222-2222-2222-2222-222222222222");

        // Act
        var first = ConversationTurnId.DeriveSafetyTurnId(ClientMessageId);
        var second = ConversationTurnId.DeriveSafetyTurnId(otherClientId);

        // Assert
        second.Should().NotBe(
            first,
            because: "two different messages each carrying an Amber signal must derive separate referral turns");
    }

    [Fact]
    public void DeriveWorkoutLogIdempotencyKey_IsDeterministic_SameInputSameOutput()
    {
        // Act
        var first = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(ClientMessageId);
        var second = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(ClientMessageId);

        // Assert
        second.Should().Be(
            first,
            because: "a double-confirm of the same card must re-derive the same EF-row idempotency key so exactly one WorkoutLog is ever committed (DEC-077 replay)");
    }

    [Fact]
    public void DeriveWorkoutLogIdempotencyKey_DiffersFromClientMessageId_AndIsNonEmpty()
    {
        // Act
        var actual = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(ClientMessageId);

        // Assert
        actual.Should().NotBe(
            ClientMessageId,
            because: "the conversation clientMessageId and the EF-row idempotency key are deliberately distinct mechanisms (the confirm path keys three separate markers)");
        actual.Should().NotBe(Guid.Empty, because: "an empty key would collapse every confirm onto one (UserId, Guid.Empty) index slot and swallow distinct logs as replays");
    }

    [Fact]
    public void DeriveWorkoutLogIdempotencyKey_DiffersFromCoachAndSafetyTurnIds_ForTheSameClientId()
    {
        // Act
        var workoutLogKey = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(ClientMessageId);
        var coachTurnId = ConversationTurnId.DeriveCoachTurnId(ClientMessageId);
        var safetyTurnId = ConversationTurnId.DeriveSafetyTurnId(ClientMessageId);

        // Assert
        workoutLogKey.Should().NotBe(
            coachTurnId,
            because: "the EF-row idempotency key and the ack coach-turn id are distinct mechanisms keyed off the same clientMessageId; sharing a namespace would conflate them");
        workoutLogKey.Should().NotBe(
            safetyTurnId,
            because: "the EF-row idempotency key must not collide with the scripted-referral turn id derived from the same clientMessageId");
    }

    [Fact]
    public void DeriveWorkoutLogIdempotencyKey_DistinctClientIds_ProduceDistinctKeys()
    {
        // Arrange
        var otherClientId = new Guid("22222222-2222-2222-2222-222222222222");

        // Act
        var first = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(ClientMessageId);
        var second = ConversationTurnId.DeriveWorkoutLogIdempotencyKey(otherClientId);

        // Assert
        second.Should().NotBe(
            first,
            because: "two distinct confirmed cards must commit two distinct logs, not collide on one EF idempotency slot");
    }
}
