using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Pure-function tests over the <see cref="ConversationTurnView"/> factory methods
/// that map a Plan-stream event plus its Marten metadata (event id + timestamp) into
/// a persisted conversation turn. The projection wiring is covered separately by the
/// Marten integration tests.
/// </summary>
public sealed class ConversationTurnViewTests
{
    private static readonly Guid EventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid WorkoutLogId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FromAdaptation_MapsEventAndMetadata_ToAssistantAdaptationTurn()
    {
        // Arrange
        var expectedDiff = PlanAdaptationDiff.Empty;
        var data = new PlanAdaptedFromLog(
            WorkoutLogId,
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Amber,
            "We backed off your week so the niggle settles.",
            expectedDiff);

        // Act
        var actual = ConversationTurnView.FromAdaptation(EventId, CreatedAt, data);

        // Assert
        actual.TriggeringPlanEventId.Should().Be(EventId);
        actual.Role.Should().Be(ConversationRole.AssistantAdaptation);
        actual.Content.Should().Be("We backed off your week so the niggle settles.");
        actual.EscalationLevel.Should().Be(EscalationLevel.Restructure);
        actual.SafetyTier.Should().Be(SafetyTier.Amber);
        actual.ReferralCategory.Should().Be(ReferralCategory.None, because: "an adaptation turn carries no referral");
        actual.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        actual.Diff.Should().BeSameAs(expectedDiff);
        actual.TriggeringWorkoutLogId.Should().Be(WorkoutLogId);
        actual.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void FromSafety_MapsEventAndMetadata_ToSystemSafetyTurn()
    {
        // Arrange
        const string content = "988 Suicide & Crisis Lifeline. Crisis Text Line: text 741741.";
        var data = new SafetySignalRaised(
            WorkoutLogId,
            SafetyTier.Red,
            ReferralCategory.Crisis,
            content);

        // Act
        var actual = ConversationTurnView.FromSafety(EventId, CreatedAt, data);

        // Assert
        actual.TriggeringPlanEventId.Should().Be(EventId);
        actual.Role.Should().Be(ConversationRole.SystemSafety);
        actual.Content.Should().Be(content);
        actual.EscalationLevel.Should().BeNull(because: "a safety turn has no escalation level");
        actual.SafetyTier.Should().Be(SafetyTier.Red);
        actual.ReferralCategory.Should().Be(ReferralCategory.Crisis);
        actual.AdaptationKind.Should().BeNull(because: "a safety turn is not an adaptation");
        actual.Diff.Should().BeNull(because: "a safety turn carries no plan diff");
        actual.TriggeringWorkoutLogId.Should().Be(WorkoutLogId);
        actual.CreatedAt.Should().Be(CreatedAt);
    }
}
