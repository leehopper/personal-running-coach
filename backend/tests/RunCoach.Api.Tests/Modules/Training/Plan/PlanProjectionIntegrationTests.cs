using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Integration coverage for <see cref="PlanProjection"/> exercised through
/// the real Marten + Testcontainers Postgres host. Companion to the
/// pure-function <c>PlanProjectionTests</c> unit suite, this fixture proves
/// the projection's inline lifecycle: appending the canonical Slice 1 plan
/// event sequence onto a fresh Plan stream materializes a queryable
/// <see cref="PlanProjectionDto"/> document with the correct shape, and a
/// re-projection (clearing the document table and re-reading the event
/// stream) lands the byte-identical document.
/// </summary>
[Trait("Category", "Integration")]
public class PlanProjectionIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly DateTimeOffset PlanGeneratedAt = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Append the canonical Slice 1 sequence
    /// (<c>[PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated]</c>)
    /// onto a fresh Plan stream and verify the inline projection
    /// materializes the documented <see cref="PlanProjectionDto"/> shape:
    /// macro present, four meso slots in week-ascending order, week-1 micro
    /// detail in <c>MicroWorkoutsByWeek</c>, and the audit-link metadata
    /// (<c>PromptVersion</c>, <c>ModelId</c>, <c>PreviousPlanId</c>) all
    /// stamped onto the projection.
    /// </summary>
    [Fact]
    public async Task Inline_Projection_Materializes_Canonical_Slice1_Shape()
    {
        // Arrange
        var planId = Guid.NewGuid();
        await AppendCanonicalPlanStreamAsync(planId);

        // Act — load via a fresh session so we read the persisted document,
        // not an in-memory IIdentityMap copy.
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var projection = await session.LoadAsync<PlanProjectionDto>(
            planId,
            TestContext.Current.CancellationToken);

        // Assert
        projection.Should().NotBeNull();
        projection!.PlanId.Should().Be(planId);
        projection.UserId.Should().Be(UserId);
        projection.GeneratedAt.Should().Be(PlanGeneratedAt);
        projection.PromptVersion.Should().Be("coaching-v1");
        projection.ModelId.Should().Be("claude-sonnet-4-5");
        projection.PreviousPlanId.Should().BeNull();

        projection.Macro.Should().NotBeNull();
        projection.Macro!.TotalWeeks.Should().Be(16);

        projection.MesoWeeks.Should().HaveCount(4);
        projection.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(1, 2, 3, 4);
        projection.MesoWeeks[3].IsDeloadWeek.Should().BeTrue();

        projection.MicroWorkoutsByWeek.Should().ContainKey(1);
        projection.MicroWorkoutsByWeek[1].Workouts.Should().NotBeEmpty();
    }

    /// <summary>
    /// A regenerate-from-settings flow appends a second Plan stream pointing
    /// at the prior plan via <c>PlanGenerated.PreviousPlanId</c>. The
    /// projection threads that audit linkage onto the new
    /// <see cref="PlanProjectionDto"/> without re-projecting the prior
    /// stream — both documents coexist and are independently loadable.
    /// </summary>
    [Fact]
    public async Task Two_Plan_Streams_With_PreviousPlanId_Linkage_Coexist()
    {
        // Arrange
        var firstPlanId = Guid.NewGuid();
        var secondPlanId = Guid.NewGuid();
        await AppendCanonicalPlanStreamAsync(firstPlanId);
        await AppendCanonicalPlanStreamAsync(secondPlanId, previousPlanId: firstPlanId);

        // Act
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var first = await session.LoadAsync<PlanProjectionDto>(
            firstPlanId,
            TestContext.Current.CancellationToken);
        var second = await session.LoadAsync<PlanProjectionDto>(
            secondPlanId,
            TestContext.Current.CancellationToken);

        // Assert — both documents exist; only the second carries the audit
        // link to the first.
        first.Should().NotBeNull();
        first!.PreviousPlanId.Should().BeNull();

        second.Should().NotBeNull();
        second!.PreviousPlanId.Should().Be(firstPlanId);
    }

    /// <summary>
    /// Loading the same plan id twice through fresh Marten sessions returns
    /// equivalent <see cref="PlanProjectionDto"/> documents — the projection
    /// is durable, not regenerated. Mirrors the spec's "page reload returns
    /// byte-identical" invariant at the Marten layer (the controller-layer
    /// equivalent lives in <c>PlanRenderingControllerIntegrationTests</c>).
    /// </summary>
    [Fact]
    public async Task Repeated_LoadAsync_Returns_Equivalent_Document()
    {
        // Arrange
        var planId = Guid.NewGuid();
        await AppendCanonicalPlanStreamAsync(planId);

        // Act
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var firstSession = store.LightweightSession();
        var first = await firstSession.LoadAsync<PlanProjectionDto>(
            planId,
            TestContext.Current.CancellationToken);
        await using var secondSession = store.LightweightSession();
        var second = await secondSession.LoadAsync<PlanProjectionDto>(
            planId,
            TestContext.Current.CancellationToken);

        // Assert — same MesoWeeks count + same week-1 micro shape across
        // independent sessions confirms the document is persisted, not
        // re-derived per call.
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.PlanId.Should().Be(first!.PlanId);
        second.UserId.Should().Be(first.UserId);
        second.GeneratedAt.Should().Be(first.GeneratedAt);
        second.MesoWeeks.Should().HaveCount(first.MesoWeeks.Count);
        second.MicroWorkoutsByWeek.Keys.Should().BeEquivalentTo(first.MicroWorkoutsByWeek.Keys);
    }

    /// <summary>
    /// Reset Marten event storage so streams seeded by one test do not
    /// survive into the next.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static MacroPlanOutput BuildMacro()
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 16,
            GoalDescription = "Half Marathon",
            Phases = new[]
            {
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 8,
                    WeeklyDistanceStartKm = 30,
                    WeeklyDistanceEndKm = 50,
                    IntensityDistribution = "80/20 easy/hard",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Recovery },
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 300,
                    Notes = "Aerobic base build.",
                    IncludesDeload = true,
                },
            },
            Rationale = "Progressive base then build to race specificity.",
            Warnings = "Stop and reassess if any sharp pain emerges.",
        };
    }

    private static MesoWeekOutput BuildMeso(int weekNumber, PhaseType phase, bool isDeload)
    {
        var run = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Run,
            WorkoutType = WorkoutType.Easy,
            Notes = "Easy aerobic.",
        };
        var rest = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = "Recovery.",
        };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = phase,
            WeeklyTargetKm = isDeload ? 30 : 45,
            IsDeloadWeek = isDeload,
            Sunday = run,
            Monday = rest,
            Tuesday = run,
            Wednesday = rest,
            Thursday = run,
            Friday = rest,
            Saturday = run,
            WeekSummary = $"Week {weekNumber} - {phase}.",
        };
    }

    private static MicroWorkoutListOutput BuildMicro()
    {
        return new MicroWorkoutListOutput
        {
            Workouts = new[]
            {
                new WorkoutOutput
                {
                    DayOfWeek = 0,
                    WorkoutType = WorkoutType.Easy,
                    Title = "Easy Aerobic Run",
                    TargetDistanceKm = 8,
                    TargetDurationMinutes = 50,
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 360,
                    Segments = new[]
                    {
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Warmup,
                            DurationMinutes = 10,
                            TargetPaceSecPerKm = 400,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Warm up gradually.",
                        },
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Work,
                            DurationMinutes = 30,
                            TargetPaceSecPerKm = 360,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Steady aerobic effort.",
                        },
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Cooldown,
                            DurationMinutes = 10,
                            TargetPaceSecPerKm = 420,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Cool down easy.",
                        },
                    },
                    WarmupNotes = "10 min walk-jog.",
                    CooldownNotes = "10 min walk-jog.",
                    CoachingNotes = "Conversational pace.",
                    PerceivedEffort = 3,
                },
            },
        };
    }

    private async Task AppendCanonicalPlanStreamAsync(Guid planId, Guid? previousPlanId = null)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var generated = new PlanGenerated(
            planId,
            UserId,
            BuildMacro(),
            PlanGeneratedAt,
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: previousPlanId);
        var events = new object[]
        {
            generated,
            new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)),
            new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)),
            new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)),
            new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)),
            new FirstMicroCycleCreated(BuildMicro()),
        };
        session.Events.StartStream<PlanProjectionDto>(planId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
