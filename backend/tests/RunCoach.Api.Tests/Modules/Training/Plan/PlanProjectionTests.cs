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
    private const string PromptVersion = "coaching-v1";

    private const string ModelId = "claude-sonnet-4-5";

    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid PlanId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid PreviousPlanId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Create_PlanGenerated_PopulatesProvenanceAndMacro()
    {
        // Arrange
        var expectedMacro = BuildMacro();
        var expectedPlanId = PlanId;
        var expectedUserId = UserId;
        var expectedGeneratedAt = Now;
        var expectedPromptVersion = PromptVersion;
        var expectedModelId = ModelId;
        var generated = new PlanGenerated(
            expectedPlanId,
            expectedUserId,
            expectedMacro,
            expectedGeneratedAt,
            expectedPromptVersion,
            expectedModelId,
            PreviousPlanId: null);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.PlanId.Should().Be(expectedPlanId);
        actual.UserId.Should().Be(expectedUserId);
        actual.GeneratedAt.Should().Be(expectedGeneratedAt);
        actual.PromptVersion.Should().Be(expectedPromptVersion);
        actual.ModelId.Should().Be(expectedModelId);
        actual.PreviousPlanId.Should().BeNull();
        actual.Macro.Should().BeSameAs(expectedMacro);
        actual.MesoWeeks.Should().BeEmpty();
        actual.MicroWorkoutsByWeek.Should().BeEmpty();
    }

    [Fact]
    public void Create_PlanGenerated_ThreadsPreviousPlanId_ForRegeneration()
    {
        // Arrange — Unit 5's regenerate handler threads the prior plan id onto
        // the new stream's creation event so the projection retains the audit link.
        var expectedPreviousPlanId = PreviousPlanId;
        var generated = new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PromptVersion,
            ModelId,
            PreviousPlanId: expectedPreviousPlanId);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.PreviousPlanId.Should().Be(expectedPreviousPlanId);
    }

    [Fact]
    public void Apply_MesoCycleCreated_AppendsWeekInOrder()
    {
        // Arrange
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var expectedCount = 2;
        int[] expectedWeekNumbers = [1, 2];

        // Act
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);

        // Assert
        dto.MesoWeeks.Should().HaveCount(expectedCount);
        dto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(expectedWeekNumbers);
    }

    [Fact]
    public void Apply_MesoCycleCreated_FourWeeks_PopulatesAllSlotsInWeekOrder()
    {
        // Arrange — the canonical Slice 1 four-meso sequence.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var expectedCount = 4;
        int[] expectedWeekNumbers = [1, 2, 3, 4];
        var expectedFinalIsDeload = true;
        var expectedFinalPhase = PhaseType.Build;

        // Act — append in ascending order as IPlanGenerationService emits them.
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);

        // Assert
        dto.MesoWeeks.Should().HaveCount(expectedCount);
        dto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(expectedWeekNumbers);
        dto.MesoWeeks[3].IsDeloadWeek.Should().Be(expectedFinalIsDeload);
        dto.MesoWeeks[3].PhaseType.Should().Be(expectedFinalPhase);
    }

    [Fact]
    public void Apply_MesoCycleCreated_OutOfOrderArrival_StillSortsByWeekNumber()
    {
        // Arrange — defensive: even if Marten codegen reorders applies during
        // a re-projection run, the frontend still sees ascending week order.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        int[] expectedWeekNumbers = [1, 2, 3, 4];

        // Act — deliberately apply weeks in non-ascending order.
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);

        // Assert
        dto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(expectedWeekNumbers);
    }

    [Fact]
    public void Apply_MesoCycleCreated_DuplicateWeekIndex_ReplacesEntry()
    {
        // Arrange — a re-projection that re-applies the same week index keeps
        // the slot count stable and uses the latest payload.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var expectedCount = 1;
        var expectedReplacementWeeklyTargetKm = 60;
        var originalMeso = BuildMeso(1, PhaseType.Base, isDeload: false);
        var replacementMeso = originalMeso with { WeeklyTargetKm = expectedReplacementWeeklyTargetKm };

        // Act
        PlanProjection.Apply(new MesoCycleCreated(1, originalMeso), dto);
        PlanProjection.Apply(new MesoCycleCreated(1, replacementMeso), dto);

        // Assert
        dto.MesoWeeks.Should().HaveCount(expectedCount);
        dto.MesoWeeks[0].WeeklyTargetKm.Should().Be(expectedReplacementWeeklyTargetKm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Apply_MesoCycleCreated_NonPositiveWeekIndex_Throws(int invalidWeekIndex)
    {
        // Arrange — guard against malformed events; week indices are 1-based by spec.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var meso = BuildMeso(1, PhaseType.Base, isDeload: false);

        // Act
        var act = () => PlanProjection.Apply(new MesoCycleCreated(invalidWeekIndex, meso), dto);

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*WeekIndex must be 1-based*");
    }

    [Fact]
    public void Apply_MesoCycleCreated_WeekNumberMismatch_Throws()
    {
        // Arrange — the find-or-replace logic keys by WeekIndex but writes
        // Meso payloads carrying their own WeekNumber; divergence would
        // produce a slot indexed under one number and rendered under another.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var divergentMeso = BuildMeso(2, PhaseType.Base, isDeload: false);

        // Act — event WeekIndex=1 but payload WeekNumber=2.
        var act = () => PlanProjection.Apply(new MesoCycleCreated(1, divergentMeso), dto);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*WeekIndex=1*WeekNumber=2*");
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_PopulatesWeek1Slot()
    {
        // Arrange
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var expectedMicro = BuildMicro();
        var expectedWeekKey = 1;

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(expectedMicro), dto);

        // Assert
        dto.MicroWorkoutsByWeek.Should().ContainKey(expectedWeekKey);
        dto.MicroWorkoutsByWeek[expectedWeekKey].Should().BeSameAs(expectedMicro);
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_DoesNotMutateOtherKeys()
    {
        // Arrange — a future slice may have already populated week 2, week 3, etc.
        // Re-applying the first-week event must not clobber those entries.
        var dto = PlanProjection.Create(BuildPlanGenerated());
        var expectedWeekTwoMicro = BuildMicro();
        var expectedRetainedWeekKey = 2;
        var expectedNewlyPopulatedWeekKey = 1;
        dto.MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
        {
            [expectedRetainedWeekKey] = expectedWeekTwoMicro,
        };

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), dto);

        // Assert
        dto.MicroWorkoutsByWeek.Should().ContainKeys(expectedNewlyPopulatedWeekKey, expectedRetainedWeekKey);
        dto.MicroWorkoutsByWeek[expectedRetainedWeekKey].Should().BeSameAs(expectedWeekTwoMicro);
    }

    [Fact]
    public void FullSequence_PlanGeneratedFourMesoFirstMicro_LandsCanonicalShape()
    {
        // Arrange — the canonical Slice 1 plan event order:
        // [PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated].
        var expectedPlanId = PlanId;
        var expectedUserId = UserId;
        var expectedMesoCount = 4;
        var expectedSingleMicroWeekKey = 1;
        var generated = BuildPlanGenerated();

        // Act
        var dto = PlanProjection.Create(generated);
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), dto);

        // Assert
        dto.PlanId.Should().Be(expectedPlanId);
        dto.UserId.Should().Be(expectedUserId);
        dto.Macro.Should().NotBeNull();
        dto.MesoWeeks.Should().HaveCount(expectedMesoCount);
        dto.MicroWorkoutsByWeek.Should().ContainSingle().Which.Key.Should().Be(expectedSingleMicroWeekKey);
        dto.MicroWorkoutsByWeek[expectedSingleMicroWeekKey].Workouts.Should().NotBeEmpty();
    }

    private static PlanGenerated BuildPlanGenerated()
    {
        return new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PromptVersion,
            ModelId,
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
