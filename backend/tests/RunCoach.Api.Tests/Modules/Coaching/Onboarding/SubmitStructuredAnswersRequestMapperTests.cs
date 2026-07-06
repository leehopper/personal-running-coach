using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Unit coverage for the deterministic request validator/mapper behind
/// POST /api/v1/onboarding/answers (DU-1 FR-1.8). This is the deterministic replacement for the
/// retired LLM <c>OnboardingTurnOutputValidator</c>: it must reject hostile/malformed client input
/// with a descriptive error (mapped to a 400 by the controller) rather than letting a self-validating
/// answer record throw an uncatchable exception during model binding.
/// </summary>
public sealed class SubmitStructuredAnswersRequestMapperTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Key = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void TryMap_AllSixTopics_RaceTraining_MapsEveryValidatedRecord()
    {
        // Arrange
        var request = new SubmitStructuredAnswersRequestDto(
            Key,
            new PrimaryGoalInputDto(PrimaryGoal.RaceTraining, "Sub-4 marathon"),
            new TargetEventInputDto("Berlin Marathon", 42.2, "2026-09-27", "PT3H55M0S"),
            new CurrentFitnessInputDto(45, 20, 21.1, "PT1H45M0S", "Feeling strong"),
            ValidWeeklySchedule(),
            new InjuryHistoryInputDto(false, string.Empty, "Rolled ankle 2024"),
            new PreferencesInputDto(PreferredUnits.Miles, true, true, "Prefer mornings"));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        var expectedPrimaryGoal = new PrimaryGoalAnswer { Goal = PrimaryGoal.RaceTraining, Description = "Sub-4 marathon" };
        var expectedTargetEvent = new TargetEventAnswer
        {
            EventName = "Berlin Marathon",
            DistanceKm = 42.2,
            EventDateIso = "2026-09-27",
            TargetFinishTimeIso = "PT3H55M0S",
        };
        var expectedCurrentFitness = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 45,
            LongestRecentRunKm = 20,
            RecentRaceDistanceKm = 21.1,
            RecentRaceTimeIso = "PT1H45M0S",
            Description = "Feeling strong",
        };
        var expectedWeeklySchedule = new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = 5,
            TypicalSessionMinutes = 60,
            Monday = true,
            Tuesday = false,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = true,
            Sunday = false,
            Description = "Evenings only",
        };
        var expectedInjuryHistory = new InjuryHistoryAnswer
        {
            HasActiveInjury = false,
            ActiveInjuryDescription = string.Empty,
            PastInjurySummary = "Rolled ankle 2024",
        };
        var expectedPreferences = new PreferencesAnswer
        {
            PreferredUnits = PreferredUnits.Miles,
            PreferTrail = true,
            ComfortableWithIntensity = true,
            Description = "Prefer mornings",
        };

        actual.Should().BeTrue();
        error.Should().BeNull();
        command.Should().NotBeNull();
        command!.UserId.Should().Be(UserId);
        command.IdempotencyKey.Should().Be(Key);
        command.PrimaryGoal.Should().Be(expectedPrimaryGoal);
        command.TargetEvent.Should().Be(expectedTargetEvent);
        command.CurrentFitness.Should().Be(expectedCurrentFitness);
        command.WeeklySchedule.Should().Be(expectedWeeklySchedule);
        command.InjuryHistory.Should().Be(expectedInjuryHistory);
        command.Preferences.Should().Be(expectedPreferences);
    }

    [Fact]
    public void TryMap_FitnessProfile_NoTargetEvent_MapsFiveTopics()
    {
        // Arrange — general fitness, no target event submitted.
        var request = new SubmitStructuredAnswersRequestDto(
            Key,
            new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, "Stay healthy"),
            TargetEvent: null,
            new CurrentFitnessInputDto(30, 12, null, null, "Moderate"),
            ValidWeeklySchedule(),
            new InjuryHistoryInputDto(false, string.Empty, string.Empty),
            new PreferencesInputDto(PreferredUnits.Kilometers, false, true, string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        actual.Should().BeTrue();
        error.Should().BeNull();
        command!.TargetEvent.Should().BeNull();
        command.PrimaryGoal!.Goal.Should().Be(PrimaryGoal.GeneralFitness);
        command.CurrentFitness!.RecentRaceDistanceKm.Should().BeNull();
    }

    [Fact]
    public void TryMap_PartialSubmission_SingleTopic_Succeeds()
    {
        // Arrange — a single topic is a valid (incomplete) submission; the gate decides completion.
        var request = WithTopics(primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.BuildVolume, string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        actual.Should().BeTrue();
        error.Should().BeNull();
        command!.PrimaryGoal.Should().NotBeNull();
        command.WeeklySchedule.Should().BeNull();
    }

    [Fact]
    public void TryMap_NullDescription_MapsToEmptyString()
    {
        // Arrange — a null nuance box must become the record's empty-string default, not throw.
        var request = WithTopics(primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.BuildSpeed, Description: null));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        actual.Should().BeTrue();
        error.Should().BeNull();
        command!.PrimaryGoal!.Description.Should().Be(string.Empty);
    }

    [Fact]
    public void TryMap_NoTopicsProvided_Fails()
    {
        // Arrange
        var request = new SubmitStructuredAnswersRequestDto(Key, null, null, null, null, null, null);

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_EmptyIdempotencyKey_Fails()
    {
        // Arrange
        var request = new SubmitStructuredAnswersRequestDto(
            Guid.Empty,
            new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            TargetEvent: null,
            CurrentFitness: null,
            WeeklySchedule: null,
            InjuryHistory: null,
            Preferences: null);

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_UndefinedPrimaryGoalEnum_Fails()
    {
        // Arrange — an out-of-range int-backed enum deserializes without throwing; the mapper must catch it.
        var request = WithTopics(primaryGoal: new PrimaryGoalInputDto((PrimaryGoal)99, "x"));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_UndefinedPreferredUnitsEnum_Fails()
    {
        // Arrange
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            preferences: new PreferencesInputDto((PreferredUnits)7, false, false, string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_TargetEventWithoutPrimaryGoal_Fails()
    {
        // Arrange — a target event with no primary goal in the submission cannot establish race training.
        var request = WithTopics(targetEvent: ValidTargetEvent());

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_TargetEventWithNonRacingPrimaryGoal_Fails()
    {
        // Arrange
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            targetEvent: ValidTargetEvent());

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void TryMap_NonPositiveTargetDistance_Fails(double distanceKm)
    {
        // Arrange — the `TargetEventAnswer` record enforces `DistanceKm > 0` in its init accessor.
        var request = WithTopics(
            primaryGoal: RacingGoal(),
            targetEvent: new TargetEventInputDto("Race", distanceKm, "2026-09-27", null));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryMap_BlankEventName_Fails(string eventName)
    {
        // Arrange
        var request = WithTopics(
            primaryGoal: RacingGoal(),
            targetEvent: new TargetEventInputDto(eventName, 10, "2026-09-27", null));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-13-40")]
    [InlineData("2026/09/27")]
    [InlineData("27-09-2026")]
    public void TryMap_InvalidEventDate_Fails(string eventDateIso)
    {
        // Arrange
        var request = WithTopics(
            primaryGoal: RacingGoal(),
            targetEvent: new TargetEventInputDto("Race", 10, eventDateIso, null));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_InvalidTargetFinishDuration_Fails()
    {
        // Arrange — a present-but-unparseable ISO-8601 duration is rejected.
        var request = WithTopics(
            primaryGoal: RacingGoal(),
            targetEvent: new TargetEventInputDto("Race", 10, "2026-09-27", "3 hours 55"));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData(-1, 12)]
    [InlineData(30, -1)]
    public void TryMap_NegativeFitnessDistance_Fails(double typicalWeeklyKm, double longestRecentRunKm)
    {
        // Arrange — `CurrentFitnessAnswer` enforces `>= 0` on both distances.
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            currentFitness: new CurrentFitnessInputDto(typicalWeeklyKm, longestRecentRunKm, null, null, string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_NegativeRecentRaceDistance_Fails()
    {
        // Arrange
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            currentFitness: new CurrentFitnessInputDto(30, 12, -3, null, string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void TryMap_OutOfRangeRunDays_Fails(int maxRunDaysPerWeek)
    {
        // Arrange — `WeeklyScheduleAnswer` enforces `1..7`.
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            weeklySchedule: ValidWeeklySchedule() with { MaxRunDaysPerWeek = maxRunDaysPerWeek });

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void TryMap_NonPositiveSessionMinutes_Fails(int typicalSessionMinutes)
    {
        // Arrange — `WeeklyScheduleAnswer` enforces `TypicalSessionMinutes > 0`.
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            weeklySchedule: ValidWeeklySchedule() with { TypicalSessionMinutes = typicalSessionMinutes });

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_NonFiniteTargetDistance_Fails()
    {
        // Arrange — a JSON literal overflowing double range deserializes to +Infinity, which slips past
        // the record's `<= 0` guard and would later crash serialization as a 500. The mapper must reject it.
        var request = WithTopics(
            primaryGoal: RacingGoal(),
            targetEvent: new TargetEventInputDto("Race", double.PositiveInfinity, "2026-09-27", null));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    [InlineData(500_000)]
    public void TryMap_NonFiniteOrAbsurdFitnessDistance_Fails(double typicalWeeklyKm)
    {
        // Arrange
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            currentFitness: new CurrentFitnessInputDto(typicalWeeklyKm, 12, null, null, string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Theory]
    [InlineData("-PT1H45M0S")]
    [InlineData("P100Y")]
    public void TryMap_NegativeOrAbsurdDuration_Fails(string durationIso)
    {
        // Arrange — `XmlConvert.ToTimeSpan` accepts negative and arbitrarily large durations without
        // throwing; the mapper must range-check them.
        var request = WithTopics(
            primaryGoal: RacingGoal(),
            targetEvent: new TargetEventInputDto("Race", 10, "2026-09-27", durationIso));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_OverflowDuration_Fails()
    {
        // Arrange — "P1000000000D" is syntactically valid ISO-8601 but numerically huge; `XmlConvert.ToTimeSpan`
        // throws `OverflowException` (not `FormatException`) for it. The mapper must catch both, the duration
        // twin of the 1e400→Infinity distance hole.
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, string.Empty),
            currentFitness: new CurrentFitnessInputDto(30, 12, null, "P1000000000D", string.Empty));

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        AssertMapFailed(actual, command, error);
    }

    [Fact]
    public void TryMap_UndefinedGoalWithTargetEvent_ReportsGoalError_NotCrossFieldError()
    {
        // Arrange — an undefined goal submitted with a target event must report the goal problem, not the
        // cross-field message (the enum is validated before the cross-field check).
        var request = WithTopics(
            primaryGoal: new PrimaryGoalInputDto((PrimaryGoal)99, "x"),
            targetEvent: ValidTargetEvent());

        // Act
        var actual = SubmitStructuredAnswersRequestMapper.TryMap(request, UserId, out var command, out var error);

        // Assert
        actual.Should().BeFalse();
        command.Should().BeNull();
        error.Should().Contain("recognized goal");
    }

    private static void AssertMapFailed(bool actual, SubmitStructuredAnswers? command, string? error)
    {
        actual.Should().BeFalse();
        command.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    private static PrimaryGoalInputDto RacingGoal() => new(PrimaryGoal.RaceTraining, "Goal race");

    private static TargetEventInputDto ValidTargetEvent() => new("Local 10K", 10, "2026-09-27", null);

    private static WeeklyScheduleInputDto ValidWeeklySchedule() =>
        new(5, 60, Monday: true, Tuesday: false, Wednesday: true, Thursday: false, Friday: true, Saturday: true, Sunday: false, "Evenings only");

    private static SubmitStructuredAnswersRequestDto WithTopics(
        PrimaryGoalInputDto? primaryGoal = null,
        TargetEventInputDto? targetEvent = null,
        CurrentFitnessInputDto? currentFitness = null,
        WeeklyScheduleInputDto? weeklySchedule = null,
        InjuryHistoryInputDto? injuryHistory = null,
        PreferencesInputDto? preferences = null) =>
        new(Key, primaryGoal, targetEvent, currentFitness, weeklySchedule, injuryHistory, preferences);
}
