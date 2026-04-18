using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Models.Structured;

public class StructuredOutputTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public void MacroPlanOutput_RoundTrips_ThroughJsonSerialization()
    {
        // Arrange
        var expected = CreateSampleMacroPlan();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MacroPlanOutput>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MacroPlanOutput_SerializesEnums_AsStrings()
    {
        // Arrange
        var plan = CreateSampleMacroPlan();

        // Act
        var json = JsonSerializer.Serialize(plan, JsonOptions);

        // Assert
        json.Should().Contain("\"Base\"");
        json.Should().Contain("\"Build\"");
        json.Should().NotMatchRegex("\"phase_type\"\\s*:\\s*\\d");
    }

    [Fact]
    public void MacroPlanOutput_PreservesPhases_AfterRoundTrip()
    {
        // Arrange
        var expected = CreateSampleMacroPlan();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MacroPlanOutput>(json, JsonOptions);

        // Assert
        actual!.Phases.Should().HaveCount(expected.Phases.Length);
        actual.Phases[0].PhaseType.Should().Be(PhaseType.Base);
        actual.Phases[1].PhaseType.Should().Be(PhaseType.Build);
    }

    [Fact]
    public void MacroPlanOutput_PreservesPaceValues_AsIntegers()
    {
        // Arrange
        var expected = CreateSampleMacroPlan();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MacroPlanOutput>(json, JsonOptions);

        // Assert
        actual!.Phases[0].TargetPaceEasySecPerKm.Should().Be(330);
        actual.Phases[0].TargetPaceFastSecPerKm.Should().Be(270);
    }

    [Fact]
    public void MesoWeekOutput_RoundTrips_WithAllSevenDaySlots()
    {
        // Arrange
        var expected = CreateSampleMesoWeek();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MesoWeekOutput>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual!.EnumerateDays().Should().HaveCount(7);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MesoWeekOutput_PreservesDaySlotTypes_AfterRoundTrip()
    {
        // Arrange
        var expected = CreateSampleMesoWeek();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MesoWeekOutput>(json, JsonOptions);

        // Assert
        actual!.Sunday.SlotType.Should().Be(DaySlotType.Rest);
        actual.Monday.SlotType.Should().Be(DaySlotType.Run);
        actual.Wednesday.SlotType.Should().Be(DaySlotType.CrossTrain);
    }

    [Fact]
    public void MesoWeekOutput_SerializesEnums_AsStrings()
    {
        // Arrange
        var week = CreateSampleMesoWeek();

        // Act
        var json = JsonSerializer.Serialize(week, JsonOptions);

        // Assert
        json.Should().Contain("\"Run\"");
        json.Should().Contain("\"Rest\"");
        json.Should().Contain("\"CrossTrain\"");
    }

    [Fact]
    public void MicroWorkoutListOutput_RoundTrips_WithNestedSegments()
    {
        // Arrange
        var expected = CreateSampleMicroWorkoutList();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MicroWorkoutListOutput>(json, JsonOptions);

        // Assert
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void MicroWorkoutListOutput_PreservesSegmentTypes_AfterRoundTrip()
    {
        // Arrange
        var expected = CreateSampleMicroWorkoutList();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MicroWorkoutListOutput>(json, JsonOptions);

        // Assert
        var segments = actual!.Workouts[0].Segments;
        segments[0].SegmentType.Should().Be(SegmentType.Warmup);
        segments[1].SegmentType.Should().Be(SegmentType.Work);
        segments[2].SegmentType.Should().Be(SegmentType.Cooldown);
    }

    [Fact]
    public void MicroWorkoutListOutput_PreservesIntensityProfiles_AfterRoundTrip()
    {
        // Arrange
        var expected = CreateSampleMicroWorkoutList();

        // Act
        var json = JsonSerializer.Serialize(expected, JsonOptions);
        var actual = JsonSerializer.Deserialize<MicroWorkoutListOutput>(json, JsonOptions);

        // Assert
        var segments = actual!.Workouts[0].Segments;
        segments[0].Intensity.Should().Be(IntensityProfile.Easy);
        segments[1].Intensity.Should().Be(IntensityProfile.Threshold);
        segments[2].Intensity.Should().Be(IntensityProfile.Easy);
    }

    [Fact]
    public void MicroWorkoutListOutput_SerializesEnums_AsStrings()
    {
        // Arrange
        var workouts = CreateSampleMicroWorkoutList();

        // Act
        var json = JsonSerializer.Serialize(workouts, JsonOptions);

        // Assert
        json.Should().Contain("\"Warmup\"");
        json.Should().Contain("\"Work\"");
        json.Should().Contain("\"Cooldown\"");
        json.Should().Contain("\"Threshold\"");
        json.Should().Contain("\"Tempo\"");
    }

    private static MacroPlanOutput CreateSampleMacroPlan() => new()
    {
        TotalWeeks = 12,
        GoalDescription = "Half Marathon",
        Rationale = "Progressive build toward race fitness.",
        Warnings = "Consult a doctor before starting.",
        Phases =
        [
            new PlanPhaseOutput
            {
                PhaseType = PhaseType.Base,
                Weeks = 4,
                WeeklyDistanceStartKm = 30,
                WeeklyDistanceEndKm = 40,
                IntensityDistribution = "80/20 easy/hard",
                AllowedWorkoutTypes = [WorkoutType.Easy, WorkoutType.LongRun],
                TargetPaceEasySecPerKm = 330,
                TargetPaceFastSecPerKm = 270,
                Notes = "Build aerobic base with easy running.",
                IncludesDeload = true,
            },
            new PlanPhaseOutput
            {
                PhaseType = PhaseType.Build,
                Weeks = 4,
                WeeklyDistanceStartKm = 40,
                WeeklyDistanceEndKm = 50,
                IntensityDistribution = "75/25 easy/hard",
                AllowedWorkoutTypes = [WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Tempo],
                TargetPaceEasySecPerKm = 325,
                TargetPaceFastSecPerKm = 260,
                Notes = "Introduce tempo work for lactate threshold.",
                IncludesDeload = true,
            },
        ],
    };

    private static MesoWeekOutput CreateSampleMesoWeek() => new()
    {
        WeekNumber = 1,
        PhaseType = PhaseType.Base,
        WeeklyTargetKm = 35,
        IsDeloadWeek = false,
        WeekSummary = "First week of base building.",
        Sunday = new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = "Full rest day." },
        Monday = new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy aerobic run." },
        Tuesday = new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Easy, Notes = "Easy aerobic run." },
        Wednesday = new MesoDaySlotOutput { SlotType = DaySlotType.CrossTrain, WorkoutType = null, Notes = "Cross-training day." },
        Thursday = new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.Tempo, Notes = "Tempo run." },
        Friday = new MesoDaySlotOutput { SlotType = DaySlotType.Rest, WorkoutType = null, Notes = "Rest day." },
        Saturday = new MesoDaySlotOutput { SlotType = DaySlotType.Run, WorkoutType = WorkoutType.LongRun, Notes = "Long run." },
    };

    private static MicroWorkoutListOutput CreateSampleMicroWorkoutList() => new()
    {
        Workouts =
        [
            new WorkoutOutput
            {
                DayOfWeek = 1,
                WorkoutType = WorkoutType.Tempo,
                Title = "Tempo Run",
                TargetDistanceKm = 10,
                TargetDurationMinutes = 50,
                TargetPaceEasySecPerKm = 330,
                TargetPaceFastSecPerKm = 270,
                WarmupNotes = "10 min easy jog.",
                CooldownNotes = "10 min easy jog with stretching.",
                CoachingNotes = "Focus on consistent effort at tempo pace.",
                PerceivedEffort = 7,
                Segments =
                [
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Warmup,
                        DurationMinutes = 10,
                        TargetPaceSecPerKm = 360,
                        Intensity = IntensityProfile.Easy,
                        Repetitions = 1,
                        Notes = "Gradual warmup.",
                    },
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Work,
                        DurationMinutes = 30,
                        TargetPaceSecPerKm = 270,
                        Intensity = IntensityProfile.Threshold,
                        Repetitions = 1,
                        Notes = "Sustained tempo effort.",
                    },
                    new WorkoutSegmentOutput
                    {
                        SegmentType = SegmentType.Cooldown,
                        DurationMinutes = 10,
                        TargetPaceSecPerKm = 360,
                        Intensity = IntensityProfile.Easy,
                        Repetitions = 1,
                        Notes = "Easy cooldown jog.",
                    },
                ],
            },
        ],
    };
}
