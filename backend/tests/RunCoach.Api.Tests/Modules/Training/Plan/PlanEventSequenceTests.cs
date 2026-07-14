using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Unit tests for <see cref="PlanEventSequence"/> construction invariants.
/// The wrapper enforces the canonical Slice 1 shape so callers cannot
/// accidentally drop, reorder, or mismatch element types.
/// </summary>
public sealed class PlanEventSequenceTests
{
    [Fact]
    public void Constructor_ValidSequence_FlattensInCanonicalOrder()
    {
        // Arrange
        var macro = BuildMacro();
        var mesos = new[]
        {
            new MesoCycleCreated(1, BuildMeso(1)),
            new MesoCycleCreated(2, BuildMeso(2)),
            new MesoCycleCreated(3, BuildMeso(3)),
            new MesoCycleCreated(4, BuildMeso(4)),
        };
        var micro = new FirstMicroCycleCreated(BuildMicro());

        // Act
        var sequence = new PlanEventSequence(macro, mesos, micro);
        var events = sequence.ToEvents();

        // Assert
        events.Should().HaveCount(6);
        events[0].Should().BeSameAs(macro);
        events[1].Should().BeSameAs(mesos[0]);
        events[2].Should().BeSameAs(mesos[1]);
        events[3].Should().BeSameAs(mesos[2]);
        events[4].Should().BeSameAs(mesos[3]);
        events[5].Should().BeSameAs(micro);
    }

    [Fact]
    public void Constructor_NullMacro_Throws()
    {
        // Act
        var act = () => new PlanEventSequence(null!, BuildMesos(), new FirstMicroCycleCreated(BuildMicro()));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("Macro");
    }

    [Fact]
    public void Constructor_NullMicro_Throws()
    {
        // Act
        var act = () => new PlanEventSequence(BuildMacro(), BuildMesos(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("Micro");
    }

    [Fact]
    public void Constructor_NullMesos_Throws()
    {
        // Act
        var act = () => new PlanEventSequence(BuildMacro(), null!, new FirstMicroCycleCreated(BuildMicro()));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("mesos");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Constructor_WrongMesoCount_Throws(int count)
    {
        // Arrange
        var mesos = Enumerable.Range(1, count)
            .Select(week => new MesoCycleCreated(week, BuildMeso(week)))
            .ToArray();

        // Act
        var act = () => new PlanEventSequence(BuildMacro(), mesos, new FirstMicroCycleCreated(BuildMicro()));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*Expected {PlanEventSequence.ExpectedMesoCount} meso events*")
            .Which.ParamName.Should().Be("mesos");
    }

    [Fact]
    public void Constructor_OutOfOrderMesos_Throws()
    {
        // Arrange — week 1, 3, 2, 4 (out of canonical order)
        var mesos = new[]
        {
            new MesoCycleCreated(1, BuildMeso(1)),
            new MesoCycleCreated(3, BuildMeso(3)),
            new MesoCycleCreated(2, BuildMeso(2)),
            new MesoCycleCreated(4, BuildMeso(4)),
        };

        // Act
        var act = () => new PlanEventSequence(BuildMacro(), mesos, new FirstMicroCycleCreated(BuildMicro()));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ordered for weeks 1..4*")
            .Which.ParamName.Should().Be("mesos");
    }

    [Fact]
    public void Constructor_DuplicateWeekIndex_Throws()
    {
        // Arrange — week 1, 1, 3, 4 (duplicate)
        var mesos = new[]
        {
            new MesoCycleCreated(1, BuildMeso(1)),
            new MesoCycleCreated(1, BuildMeso(1)),
            new MesoCycleCreated(3, BuildMeso(3)),
            new MesoCycleCreated(4, BuildMeso(4)),
        };

        // Act
        var act = () => new PlanEventSequence(BuildMacro(), mesos, new FirstMicroCycleCreated(BuildMicro()));

        // Assert
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("mesos");
    }

    private static MesoCycleCreated[] BuildMesos() =>
    [
        new MesoCycleCreated(1, BuildMeso(1)),
        new MesoCycleCreated(2, BuildMeso(2)),
        new MesoCycleCreated(3, BuildMeso(3)),
        new MesoCycleCreated(4, BuildMeso(4)),
    ];

    private static PlanGenerated BuildMacro() => new(
        PlanId: Guid.Parse("00000000-0000-0000-0000-000000000010"),
        UserId: Guid.Parse("00000000-0000-0000-0000-000000000020"),
        Macro: new MacroPlanOutput
        {
            TotalWeeks = 4,
            GoalDescription = "test",
            Phases = Array.Empty<PlanPhaseOutput>(),
            Rationale = string.Empty,
            Warnings = string.Empty,
        },
        GeneratedAt: new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
        PlanStartDate: PlanCalendar.StartOfTrainingWeek(new DateOnly(2026, 5, 1)),
        PromptVersion: "v1",
        ModelId: "test-model",
        PreviousPlanId: null,
        TargetEventName: null,
        TargetEventDistanceKm: null,
        TargetEventDate: null);

    private static MesoWeekOutput BuildMeso(int weekNumber)
    {
        var rest = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = "rest",
        };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = 20,
            IsDeloadWeek = false,
            Sunday = rest,
            Monday = rest,
            Tuesday = rest,
            Wednesday = rest,
            Thursday = rest,
            Friday = rest,
            Saturday = rest,
            WeekSummary = $"Week {weekNumber}",
        };
    }

    private static MicroWorkoutListOutput BuildMicro() => new()
    {
        Workouts = Array.Empty<WorkoutOutput>(),
    };
}
