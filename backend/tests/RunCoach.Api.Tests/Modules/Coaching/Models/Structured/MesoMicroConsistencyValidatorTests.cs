using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Models.Structured;

public sealed class MesoMicroConsistencyValidatorTests
{
    [Fact]
    public void Validate_MicroMatchesMesoRunSchedule_IsValid()
    {
        // Arrange — meso runs Sun/Tue/Thu/Sat (Easy/Easy/Tempo/LongRun); micro schedules exactly those.
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Tuesday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Thursday, DaySlotType.Run, WorkoutType.Tempo),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Easy),
            (2, WorkoutType.Easy),
            (4, WorkoutType.Tempo),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeTrue();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.None);
    }

    [Fact]
    public void Validate_FewerMicroRunDaysThanMeso_IsRunDayCountMismatch()
    {
        // Arrange — meso has 4 run days, micro schedules only 1 (the F-LIVE-2 "3 vs 4" symptom class).
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Tuesday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Thursday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro((0, WorkoutType.Easy));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.RunDayCountMismatch);
    }

    [Fact]
    public void Validate_MoreMicroRunDaysThanMeso_IsRunDayCountMismatch()
    {
        // Arrange — meso has 3 run days, micro schedules 4.
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Tuesday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Easy),
            (2, WorkoutType.Easy),
            (4, WorkoutType.Easy),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.RunDayCountMismatch);
    }

    [Fact]
    public void Validate_SameCountDifferentDays_IsRunDaySetMismatch()
    {
        // Arrange — both sides have 3 run days but micro runs Monday instead of the meso's Tuesday.
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Tuesday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Easy),
            (1, WorkoutType.Easy),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.RunDaySetMismatch);
    }

    [Fact]
    public void Validate_SameDaysDifferentType_IsWorkoutTypeMismatch()
    {
        // Arrange — same run days, but Thursday is Tempo in meso and Easy in micro (the F-LIVE-2
        // "tempo-vs-easy day swapped" symptom).
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Thursday, DaySlotType.Run, WorkoutType.Tempo),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Easy),
            (4, WorkoutType.Easy),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.WorkoutTypeMismatch);
    }

    [Fact]
    public void Validate_CrossTrainSlotsAndWorkoutsExcludedFromRunComparison_IsValid()
    {
        // Arrange — meso marks Wednesday CrossTrain (not a run day); micro emits a CrossTrain workout on
        // Wednesday. Both are excluded from the run-day comparison, so run days still match exactly.
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Wednesday, DaySlotType.CrossTrain, null),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Easy),
            (3, WorkoutType.CrossTrain),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MicroCrossTrainOnMesoRunDay_IsRunDayCountMismatch()
    {
        // Arrange — meso runs Saturday but micro puts a CrossTrain workout there. CrossTrain is excluded
        // from the run-workout set, so micro is one run day short.
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Easy),
            (6, WorkoutType.CrossTrain));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.RunDayCountMismatch);
    }

    [Fact]
    public void Validate_NullMesoWorkoutTypeOnRunSlot_ChecksPresenceOnly_IsValid()
    {
        // Arrange — a run slot with no declared workout type constrains presence only; any micro run
        // type on that day is accepted.
        var meso = BuildMeso(
            (DayOfWeek.Sunday, DaySlotType.Run, null),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (0, WorkoutType.Interval),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DuplicateMicroRunDay_IsRunDayCountMismatch()
    {
        // Arrange — micro emits two workouts on Tuesday. The run-workout count (3) exceeds the meso
        // run-day count (2), so the duplicate is caught rather than silently deduped.
        var meso = BuildMeso(
            (DayOfWeek.Tuesday, DaySlotType.Run, WorkoutType.Easy),
            (DayOfWeek.Saturday, DaySlotType.Run, WorkoutType.LongRun));
        var micro = BuildMicro(
            (2, WorkoutType.Easy),
            (2, WorkoutType.Tempo),
            (6, WorkoutType.LongRun));

        // Act
        var actual = MesoMicroConsistencyValidator.Validate(meso, micro);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Violation.Should().Be(MesoMicroConsistencyViolation.RunDayCountMismatch);
    }

    [Fact]
    public void Validate_NullArguments_Throw()
    {
        var meso = BuildMeso((DayOfWeek.Sunday, DaySlotType.Run, WorkoutType.Easy));
        var micro = BuildMicro((0, WorkoutType.Easy));

        ((Func<MesoMicroConsistencyResult>)(() => MesoMicroConsistencyValidator.Validate(null!, micro)))
            .Should().Throw<ArgumentNullException>();
        ((Func<MesoMicroConsistencyResult>)(() => MesoMicroConsistencyValidator.Validate(meso, null!)))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Result_InvalidWithNoneViolation_Throws()
    {
        ((Func<MesoMicroConsistencyResult>)(() => MesoMicroConsistencyResult.Invalid(MesoMicroConsistencyViolation.None)))
            .Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Builds a meso week from the supplied day assignments; every day not listed defaults to a rest
    /// slot. Constrained decoding guarantees exactly 7 named slots, so the fixture fills all seven.
    /// </summary>
    private static MesoWeekOutput BuildMeso(params (DayOfWeek Day, DaySlotType Slot, WorkoutType? Type)[] days)
    {
        var slots = new Dictionary<DayOfWeek, MesoDaySlotOutput>();
        foreach (var (day, slot, type) in days)
        {
            slots[day] = new MesoDaySlotOutput { SlotType = slot, WorkoutType = type, Notes = "n" };
        }

        MesoDaySlotOutput Slot(DayOfWeek day) =>
            slots.TryGetValue(day, out var s)
                ? s
                : new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = "rest" };

        return new MesoWeekOutput
        {
            WeekNumber = 1,
            PhaseType = PhaseType.Base,
            WeeklyTargetKm = 40,
            IsDeloadWeek = false,
            Sunday = Slot(DayOfWeek.Sunday),
            Monday = Slot(DayOfWeek.Monday),
            Tuesday = Slot(DayOfWeek.Tuesday),
            Wednesday = Slot(DayOfWeek.Wednesday),
            Thursday = Slot(DayOfWeek.Thursday),
            Friday = Slot(DayOfWeek.Friday),
            Saturday = Slot(DayOfWeek.Saturday),
            WeekSummary = "week 1",
        };
    }

    private static MicroWorkoutListOutput BuildMicro(params (int DayOfWeek, WorkoutType Type)[] workouts)
    {
        var list = new WorkoutOutput[workouts.Length];
        for (var i = 0; i < workouts.Length; i++)
        {
            var (day, type) = workouts[i];
            list[i] = new WorkoutOutput
            {
                DayOfWeek = day,
                WorkoutType = type,
                Title = $"{type} run",
                TargetDistanceKm = 8,
                TargetDurationMinutes = 45,
                TargetPaceEasySecPerKm = 360,
                TargetPaceFastSecPerKm = 300,
                Segments = new[]
                {
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Work,
                        DurationMinutes = 45,
                        TargetPaceSecPerKm = 360,
                        Intensity = IntensityProfile.Easy,
                        Repetitions = 1,
                        Notes = "n",
                    },
                },
                WarmupNotes = string.Empty,
                CooldownNotes = string.Empty,
                CoachingNotes = "n",
                PerceivedEffort = 3,
            };
        }

        return new MicroWorkoutListOutput { Workouts = list };
    }
}
