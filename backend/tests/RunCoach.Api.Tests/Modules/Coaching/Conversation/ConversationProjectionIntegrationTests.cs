using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Integration coverage for the Slice 3 adaptation events (DEC-079) over the real
/// <see cref="RunCoachAppFactory"/> SUT + Testcontainers Postgres: appending
/// <see cref="PlanAdaptedFromLog"/> / <see cref="SafetySignalRaised"/> to an existing
/// Plan stream mutates the inline <see cref="PlanProjectionDto"/> read model and
/// materializes the net-new <see cref="ConversationLogView"/> via the
/// <see cref="ConversationProjection"/>. These are the proof artifacts the existing
/// composition test cannot catch — they fail loudly if either event is unregistered
/// or the projection is not wired (<c>SkipUnknownEvents=true</c> would otherwise drop
/// an unhandled event silently).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConversationProjectionIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset PlanGeneratedAt = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Restructure_PlanAdaptedFromLog_MutatesPlanProjection_AndEmitsOneAdaptationTurn()
    {
        // Arrange — canonical plan stream, then read back the seeded shapes so the
        // diff targets real current-week + upcoming-week data.
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        var seeded = await LoadPlanAsync(userId, planId);
        var beforeWorkout = seeded!.MicroWorkoutsByWeek[1].Workouts[0];
        var afterWorkout = beforeWorkout with
        {
            Title = "Restructured Easy Run",
            TargetDistanceKm = beforeWorkout.TargetDistanceKm - 2,
        };
        var beforeWeek2 = seeded.MesoWeeks.Single(w => w.WeekNumber == 2).WeeklyTargetKm;
        var expectedWeek2 = beforeWeek2 - 10;
        var triggeringLogId = Guid.NewGuid();
        var diff = new PlanAdaptationDiff(
            [new WorkoutChange(1, beforeWorkout.DayOfWeek, beforeWorkout, afterWorkout)],
            [new WeeklyTargetChange(2, beforeWeek2, expectedWeek2)]);
        var adaptation = new PlanAdaptedFromLog(
            triggeringLogId,
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Green,
            "Trimmed week 2 and swapped your easy run; we build back next week.",
            diff);

        // Act — append to the EXISTING stream (never StartStream).
        await AppendAsync(userId, planId, adaptation);

        // Assert — the plan read model mutated: current-week workout swap AND meso target.
        var plan = await LoadPlanAsync(userId, planId);
        plan!.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == beforeWorkout.DayOfWeek)
            .Should().BeEquivalentTo(afterWorkout, because: "the restructure swaps the current micro week's workout");
        plan.MesoWeeks.Single(w => w.WeekNumber == 2).WeeklyTargetKm
            .Should().Be(expectedWeek2, because: "the restructure revises the upcoming meso weekly target");

        // Assert — exactly one adaptation turn carrying the event's fields.
        var log = await LoadLogAsync(userId, planId);
        var turn = log!.Turns.Should().ContainSingle().Subject;
        turn.Role.Should().Be(ConversationRole.AssistantAdaptation);
        turn.EscalationLevel.Should().Be(EscalationLevel.Restructure);
        turn.SafetyTier.Should().Be(SafetyTier.Green);
        turn.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        turn.Content.Should().Be("Trimmed week 2 and swapped your easy run; we build back next week.");
        turn.TriggeringWorkoutLogId.Should().Be(triggeringLogId);
        turn.TriggeringPlanEventId.Should().NotBeEmpty(because: "the turn links back to its source Marten event id");
        turn.CreatedAt.Should().NotBe(default);
        turn.Diff.Should().BeEquivalentTo(diff, because: "the turn retains the exact structured diff that was appended");
    }

    [Fact]
    public async Task Nudge_PlanAdaptedFromLog_TurnRetainsBeforeAfterDiff()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        var seeded = await LoadPlanAsync(userId, planId);
        var before = seeded!.MicroWorkoutsByWeek[1].Workouts[0];
        var after = before with { Title = "Moved Easy Run", TargetDistanceKm = before.TargetDistanceKm + 1 };
        var diff = new PlanAdaptationDiff([new WorkoutChange(1, before.DayOfWeek, before, after)], []);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Shuffled your easy run so the week still works.",
            diff);

        // Act
        await AppendAsync(userId, planId, adaptation);

        // Assert — projected current micro week reflects the applied change.
        var plan = await LoadPlanAsync(userId, planId);
        plan!.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == before.DayOfWeek)
            .Should().BeEquivalentTo(after);

        // Assert — the appended event retains the before/after diff for the panel.
        var log = await LoadLogAsync(userId, planId);
        var turn = log!.Turns.Should().ContainSingle().Subject;
        turn.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        turn.Diff.Should().BeEquivalentTo(diff, because: "the turn retains the structured before/after diff payload");
    }

    [Fact]
    public async Task SafetySignalRaised_Alone_EmitsSafetyTurn_WithNoPlanMutation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        var before = await LoadPlanAsync(userId, planId);
        var triggeringLogId = Guid.NewGuid();
        const string content = "988 Suicide & Crisis Lifeline. Crisis Text Line: text 741741.";
        var signal = new SafetySignalRaised(triggeringLogId, SafetyTier.Red, ReferralCategory.Crisis, content);

        // Act
        await AppendAsync(userId, planId, signal);

        // Assert — the plan projection's micro week and meso targets are unchanged.
        var after = await LoadPlanAsync(userId, planId);
        after!.MesoWeeks.Should().BeEquivalentTo(before!.MesoWeeks, because: "a safety signal alone changes no plan state");
        after.MicroWorkoutsByWeek.Should().BeEquivalentTo(before.MicroWorkoutsByWeek);

        // Assert — exactly one system-safety turn carrying the tier + scripted content.
        var log = await LoadLogAsync(userId, planId);
        var turn = log!.Turns.Should().ContainSingle().Subject;
        turn.Role.Should().Be(ConversationRole.SystemSafety);
        turn.SafetyTier.Should().Be(SafetyTier.Red);
        turn.ReferralCategory.Should().Be(ReferralCategory.Crisis);
        turn.Content.Should().Be(content);
        turn.EscalationLevel.Should().BeNull();
        turn.AdaptationKind.Should().BeNull();
        turn.Diff.Should().BeNull();
        turn.TriggeringWorkoutLogId.Should().Be(triggeringLogId);
    }

    [Fact]
    public async Task Restructure_RevisingUpcomingMesoTarget_DoesNotSynthesizeFutureMicroDetail()
    {
        // Arrange — MicroWorkoutsByWeek holds only week 1; an upcoming-week meso
        // revision must not invent micro detail for that week.
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        var seeded = await LoadPlanAsync(userId, planId);
        var beforeWeek3 = seeded!.MesoWeeks.Single(w => w.WeekNumber == 3).WeeklyTargetKm;
        var expectedWeek3 = beforeWeek3 - 5;
        var diff = new PlanAdaptationDiff([], [new WeeklyTargetChange(3, beforeWeek3, expectedWeek3)]);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Green,
            "Trimmed week 3 to keep the build sustainable.",
            diff);

        // Act
        await AppendAsync(userId, planId, adaptation);

        // Assert
        var plan = await LoadPlanAsync(userId, planId);
        plan!.MesoWeeks.Single(w => w.WeekNumber == 3).WeeklyTargetKm.Should().Be(expectedWeek3);
        plan.MicroWorkoutsByWeek.Should().ContainSingle()
            .Which.Key.Should().Be(1, because: "the projection never synthesizes micro detail for future weeks");
    }

    [Fact]
    public async Task IdempotentReplay_AggregatingTheStream_YieldsExactlyOneTurnPerEvent()
    {
        // Arrange — one adaptation + one safety event on the stream.
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Quietly shuffled your week.",
            PlanAdaptationDiff.Empty);
        var signal = new SafetySignalRaised(
            Guid.NewGuid(),
            SafetyTier.Amber,
            ReferralCategory.Injury,
            "Let's ease back and have that niggle looked at by a physio.");
        await AppendAsync(userId, planId, adaptation, signal);

        // The inline-persisted view already has one turn per event.
        var inline = await LoadLogAsync(userId, planId);
        inline!.Turns.Should().HaveCount(2);

        // Act — replay (live-aggregate) the stream from scratch through the projection.
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var replayed = await session.Events.AggregateStreamAsync<ConversationLogView>(
            planId,
            token: TestContext.Current.CancellationToken);

        // Assert — replay yields exactly one turn per event, no duplicates.
        replayed.Should().NotBeNull();
        replayed!.Turns.Should().HaveCount(2, because: "replay produces exactly one turn per event");
        replayed.Turns.Select(t => t.TriggeringPlanEventId).Should().OnlyHaveUniqueItems();
        replayed.Turns.Should().ContainSingle(t => t.Role == ConversationRole.AssistantAdaptation);
        replayed.Turns.Should().ContainSingle(t => t.Role == ConversationRole.SystemSafety);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private async Task SeedPlanStreamAsync(Guid userId, Guid planId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var sequence = StubPlanGenerationService.BuildCanonicalSequence(
            planId,
            userId,
            goal: "Half Marathon",
            PlanGeneratedAt,
            previousPlanId: null);
        session.Events.StartStream<PlanProjectionDto>(planId, [.. sequence.ToEvents()]);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AppendAsync(Guid userId, Guid planId, params object[] events)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.Append(planId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PlanProjectionDto?> LoadPlanAsync(Guid userId, Guid planId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        return await session.LoadAsync<PlanProjectionDto>(planId, TestContext.Current.CancellationToken);
    }

    private async Task<ConversationLogView?> LoadLogAsync(Guid userId, Guid planId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        return await session.LoadAsync<ConversationLogView>(planId, TestContext.Current.CancellationToken);
    }
}
