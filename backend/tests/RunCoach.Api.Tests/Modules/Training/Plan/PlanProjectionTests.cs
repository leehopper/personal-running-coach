using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Pure-function tests over the <see cref="PlanProjection"/>'s Apply methods.
/// Exercises the static <c>Create</c> and <c>Apply</c> overloads against
/// hand-built event sequences without spinning up Marten or Postgres - the
/// integration path (Marten transaction participant + inline projection
/// upsert) is covered separately by the integration tests landed alongside
/// the plan-rendering controller in T02.4.
/// </summary>
public sealed class PlanProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid PlanId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid PreviousPlanId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Create_PlanGenerated_PopulatesProvenanceAndMacro()
    {
        // Arrange
        var macro = BuildMacro();
        var generated = new PlanGenerated(
            PlanId,
            UserId,
            macro,
            Now,
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: null);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.PlanId.Should().Be(PlanId);
        actual.UserId.Should().Be(UserId);
        actual.GeneratedAt.Should().Be(Now);
        actual.PromptVersion.Should().Be("coaching-v1");
        actual.ModelId.Should().Be("claude-sonnet-4-5");
        actual.PreviousPlanId.Should().BeNull();
        actual.Macro.Should().BeSameAs(macro);
        actual.MesoWeeks.Should().BeEmpty();
        actual.MicroWorkoutsByWeek.Should().BeEmpty();
    }

    [Fact]
    public void Create_PlanGenerated_ThreadsPreviousPlanId_ForRegeneration()
    {
        // Arrange — Unit 5's regenerate handler threads the prior plan id onto
        // the new stream's creation event so the projection retains the audit link.
        var generated = new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: PreviousPlanId);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.PreviousPlanId.Should().Be(PreviousPlanId);
    }

    [Fact]
    public void Apply_MesoCycleCreated_AppendsWeekInOrder()
    {
        // Arrange
        var dto = PlanProjection.Create(BuildPlanGenerated());

        // Act
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);

        // Assert
        dto.MesoWeeks.Should().HaveCount(2);
        dto.MesoWeeks[0].WeekNumber.Should().Be(1);
        dto.MesoWeeks[1].WeekNumber.Should().Be(2);
    }

    [Fact]
    public void Apply_MesoCycleCreated_FourWeeks_PopulatesAllSlotsInWeekOrder()
    {
        // Arrange — the canonical Slice 1 four-meso sequence.
        var dto = PlanProjection.Create(BuildPlanGenerated());

        // Act — append in ascending order as IPlanGenerationService emits them.
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);

        // Assert
        dto.MesoWeeks.Should().HaveCount(4);
        dto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(1, 2, 3, 4);
        dto.MesoWeeks[3].IsDeloadWeek.Should().BeTrue();
        dto.MesoWeeks[3].PhaseType.Should().Be(PhaseType.Build);
    }

    [Fact]
    public void Apply_MesoCycleCreated_OutOfOrderArrival_StillSortsByWeekNumber()
    {
        // Arrange — defensive: even if Marten codegen reorders applies during
        // a re-projection run, the frontend still sees ascending week order.
        var dto = PlanProjection.Create(BuildPlanGenerated());

        // Act — deliberately apply weeks in non-ascending order.
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);

        // Assert
        dto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Apply_MesoCycleCreated_DuplicateWeekIndex_ReplacesEntry()
    {
        // Arrange — a re-projection that re-applies the same week index keeps
        // the slot count stable and uses the latest payload.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var originalMeso = BuildMeso(1, PhaseType.Base, isDeload: false);
        var replacementMeso = originalMeso with { WeeklyTargetKm = 60 };

        // Act
        PlanProjection.Apply(new MesoCycleCreated(1, originalMeso), dto);
        PlanProjection.Apply(new MesoCycleCreated(1, replacementMeso), dto);

        // Assert
        dto.MesoWeeks.Should().HaveCount(1);
        dto.MesoWeeks[0].WeeklyTargetKm.Should().Be(60);
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_PopulatesWeek1Slot()
    {
        // Arrange
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var micro = BuildMicro();

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(micro), dto);

        // Assert
        dto.MicroWorkoutsByWeek.Should().ContainKey(1);
        dto.MicroWorkoutsByWeek[1].Should().BeSameAs(micro);
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_DoesNotMutateOtherKeys()
    {
        // Arrange — a future slice may have already populated week 2, week 3, etc.
        // Re-applying the first-week event must not clobber those entries.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var weekTwoMicro = BuildMicro();
        dto.MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
        {
            [2] = weekTwoMicro,
        };

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), dto);

        // Assert
        dto.MicroWorkoutsByWeek.Should().ContainKeys(1, 2);
        dto.MicroWorkoutsByWeek[2].Should().BeSameAs(weekTwoMicro);
    }

    [Fact]
    public void FullSequence_PlanGeneratedFourMesoFirstMicro_LandsCanonicalShape()
    {
        // Arrange — the canonical Slice 1 plan event order:
        // [PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated].
        var generated = BuildPlanGenerated();

        // Act
        var dto = PlanProjection.Create(generated);
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), dto);

        // Assert
        dto.PlanId.Should().Be(PlanId);
        dto.UserId.Should().Be(UserId);
        dto.Macro.Should().NotBeNull();
        dto.MesoWeeks.Should().HaveCount(4);
        dto.MicroWorkoutsByWeek.Should().ContainSingle().Which.Key.Should().Be(1);
        dto.MicroWorkoutsByWeek[1].Workouts.Should().NotBeEmpty();
    }

    private static PlanGenerated BuildPlanGenerated()
    {
        return new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: null);
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
        var restSlot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = "Recovery.",
        };
        var easySlot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Run,
            WorkoutType = WorkoutType.Easy,
            Notes = "Easy aerobic.",
        };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = phase,
            WeeklyTargetKm = isDeload ? 30 : 45,
            IsDeloadWeek = isDeload,
            Sunday = easySlot,
            Monday = restSlot,
            Tuesday = easySlot,
            Wednesday = restSlot,
            Thursday = easySlot,
            Friday = restSlot,
            Saturday = easySlot,
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
}
