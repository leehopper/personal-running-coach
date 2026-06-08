using FluentAssertions;
using JasperFx.Events;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Pure-function tests over the <see cref="ConversationProjection"/> Apply methods.
/// The Marten wiring (inline projection over a real stream) is covered by
/// <see cref="ConversationProjectionIntegrationTests"/>; these unit tests exercise the
/// in-memory <c>Upsert</c> find-or-replace dedup directly — re-applying an event with
/// an already-seen id is the projection's idempotency guard, and a single-pass
/// aggregation never lands on that branch.
/// </summary>
public sealed class ConversationProjectionTests
{
    private static readonly Guid WorkoutLogId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Apply_PlanAdaptedFromLog_ReAppliesSameEventId_ReplacesTurnInPlace()
    {
        // Arrange — the find-or-replace dedup keys turns by the source event id, so
        // re-applying an event whose id is already present overwrites the single
        // existing turn instead of appending a duplicate. A projection rebuild or
        // replay re-runs the same event, and this double-apply is the only path that
        // reaches the existing-turn replace branch.
        var view = new ConversationLogView { PlanId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var eventId = Guid.NewGuid();
        var first = AdaptationEvent(eventId, version: 4, "Initial rationale.");
        var replacement = AdaptationEvent(eventId, version: 4, "Replacement rationale after re-projection.");

        // Act — apply, then re-apply the same event id with a revised payload.
        ConversationProjection.Apply(first, view);
        ConversationProjection.Apply(replacement, view);

        // Assert — one turn, carrying the replacement payload, not two.
        var turn = view.Turns.Should().ContainSingle(
            because: "re-applying an already-seen event id replaces the turn in place").Subject;
        turn.TriggeringPlanEventId.Should().Be(eventId);
        turn.Content.Should().Be("Replacement rationale after re-projection.");
    }

    [Fact]
    public void Apply_DistinctEventIds_AppendOneTurnEach()
    {
        // Arrange — distinct event ids take the append branch; ordering metadata
        // (EventVersion) is preserved per turn for the controller's tiebreak.
        var view = new ConversationLogView { PlanId = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var adaptation = AdaptationEvent(Guid.NewGuid(), version: 5, "An adjustment.");
        var safety = SafetyEvent(Guid.NewGuid(), version: 6);

        // Act
        ConversationProjection.Apply(adaptation, view);
        ConversationProjection.Apply(safety, view);

        // Assert
        view.Turns.Should().HaveCount(2);
        view.Turns.Select(t => t.TriggeringPlanEventId).Should().OnlyHaveUniqueItems();
        view.Turns.Select(t => t.EventVersion).Should().Equal(5, 6);
    }

    private static Event<PlanAdaptedFromLog> AdaptationEvent(Guid eventId, long version, string rationale) =>
        new(new PlanAdaptedFromLog(
            WorkoutLogId,
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            rationale,
            PlanAdaptationDiff.Empty))
        {
            Id = eventId,
            Version = version,
            Timestamp = CreatedAt,
        };

    private static Event<SafetySignalRaised> SafetyEvent(Guid eventId, long version) =>
        new(new SafetySignalRaised(
            WorkoutLogId,
            SafetyTier.Amber,
            ReferralCategory.Injury,
            "Let's ease back and have that niggle looked at by a physio."))
        {
            Id = eventId,
            Version = version,
            Timestamp = CreatedAt,
        };
}
