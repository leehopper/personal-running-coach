using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Training.Models;

public class UserProfileTests
{
    private static readonly UserPreferences ValidPreferences = new(
        PreferredRunDays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
        LongRunDay: DayOfWeek.Sunday,
        MaxRunDaysPerWeek: 3,
        PreferredUnits: "metric",
        AvailableTimePerRunMinutes: 60,
        Constraints: ImmutableArray<string>.Empty);

    [Fact]
    public void Constructor_ValidValues_CreatesUserProfile()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();
        var expectedName = "Alice";
        var expectedAge = 28;
        var expectedGender = "Female";

        // Act
        var actual = CreateValid(
            userId: expectedUserId,
            name: expectedName,
            age: expectedAge,
            gender: expectedGender);

        // Assert
        actual.UserId.Should().Be(expectedUserId);
        actual.Name.Should().Be(expectedName);
        actual.Age.Should().Be(expectedAge);
        actual.Gender.Should().Be(expectedGender);
    }

    [Fact]
    public void Constructor_NullOptionalFields_CreatesUserProfile()
    {
        // Act
        var actual = CreateValid(
            weightKg: null,
            heightCm: null,
            restingHeartRateAvg: null,
            maxHeartRate: null,
            currentLongRunKm: null);

        // Assert
        actual.WeightKg.Should().BeNull();
        actual.HeightCm.Should().BeNull();
        actual.RestingHeartRateAvg.Should().BeNull();
        actual.MaxHeartRate.Should().BeNull();
        actual.CurrentLongRunKm.Should().BeNull();
    }

    [Fact]
    public void Constructor_ZeroExperienceAndDistance_CreatesUserProfile()
    {
        // Act
        var actual = CreateValid(
            runningExperienceYears: 0m,
            currentWeeklyDistanceKm: 0m);

        // Assert
        actual.RunningExperienceYears.Should().Be(0m);
        actual.CurrentWeeklyDistanceKm.Should().Be(0m);
    }

    [Fact]
    public void Constructor_EmptyGuid_ThrowsArgumentException()
    {
        // Act
        var act = () => CreateValid(userId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidName_ThrowsArgumentException(string? invalidName)
    {
        // Act
        var act = () => CreateValid(name: invalidName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidGender_ThrowsArgumentException(string? invalidGender)
    {
        // Act
        var act = () => CreateValid(gender: invalidGender!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("gender");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_AgeZeroOrNegative_ThrowsArgumentOutOfRangeException(int invalidAge)
    {
        // Act
        var act = () => CreateValid(age: invalidAge);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("age");
    }

    [Fact]
    public void Constructor_AgeExceeds150_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CreateValid(age: 151);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("age");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(150)]
    public void Constructor_AgeBoundaryValues_CreatesUserProfile(int validAge)
    {
        // Act
        var actual = CreateValid(age: validAge);

        // Assert
        actual.Age.Should().Be(validAge);
    }

    [Fact]
    public void Constructor_NegativeRunningExperience_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CreateValid(runningExperienceYears: -0.1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("runningExperienceYears");
    }

    [Fact]
    public void Constructor_NegativeWeeklyDistance_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => CreateValid(currentWeeklyDistanceKm: -1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("currentWeeklyDistanceKm");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WeightKgZeroOrNegative_ThrowsArgumentOutOfRangeException(int invalidWeight)
    {
        // Act
        var act = () => CreateValid(weightKg: invalidWeight);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("weightKg.Value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_HeightCmZeroOrNegative_ThrowsArgumentOutOfRangeException(int invalidHeight)
    {
        // Act
        var act = () => CreateValid(heightCm: invalidHeight);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("heightCm.Value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RestingHeartRateZeroOrNegative_ThrowsArgumentOutOfRangeException(int invalidHr)
    {
        // Act
        var act = () => CreateValid(restingHeartRateAvg: invalidHr);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("restingHeartRateAvg.Value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_MaxHeartRateZeroOrNegative_ThrowsArgumentOutOfRangeException(int invalidHr)
    {
        // Act
        var act = () => CreateValid(maxHeartRate: invalidHr);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxHeartRate.Value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_CurrentLongRunKmZeroOrNegative_ThrowsArgumentOutOfRangeException(int invalidDistance)
    {
        // Act
        var act = () => CreateValid(currentLongRunKm: invalidDistance);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("currentLongRunKm.Value");
    }

    [Fact]
    public void Constructor_NullPreferences_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UserProfile(
            userId: Guid.NewGuid(),
            name: "Test",
            age: 30,
            gender: "Male",
            weightKg: null,
            heightCm: null,
            restingHeartRateAvg: null,
            maxHeartRate: null,
            runningExperienceYears: 0m,
            currentWeeklyDistanceKm: 0m,
            currentLongRunKm: null,
            recentRaceTimes: ImmutableArray<RaceTime>.Empty,
            injuryHistory: ImmutableArray<InjuryNote>.Empty,
            preferences: null!,
            createdOn: DateTime.UtcNow,
            modifiedOn: DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("preferences");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var a = new UserProfile(
            userId: userId,
            name: "Runner",
            age: 30,
            gender: "Male",
            weightKg: 75m,
            heightCm: 180m,
            restingHeartRateAvg: 55,
            maxHeartRate: 185,
            runningExperienceYears: 5m,
            currentWeeklyDistanceKm: 40m,
            currentLongRunKm: 14m,
            recentRaceTimes: ImmutableArray<RaceTime>.Empty,
            injuryHistory: ImmutableArray<InjuryNote>.Empty,
            preferences: ValidPreferences,
            createdOn: now,
            modifiedOn: now);

        var b = new UserProfile(
            userId: userId,
            name: "Runner",
            age: 30,
            gender: "Male",
            weightKg: 75m,
            heightCm: 180m,
            restingHeartRateAvg: 55,
            maxHeartRate: 185,
            runningExperienceYears: 5m,
            currentWeeklyDistanceKm: 40m,
            currentLongRunKm: 14m,
            recentRaceTimes: ImmutableArray<RaceTime>.Empty,
            injuryHistory: ImmutableArray<InjuryNote>.Empty,
            preferences: ValidPreferences,
            createdOn: now,
            modifiedOn: now);

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var a = new UserProfile(
            userId: Guid.NewGuid(),
            name: "Runner A",
            age: 30,
            gender: "Male",
            weightKg: 75m,
            heightCm: 180m,
            restingHeartRateAvg: 55,
            maxHeartRate: 185,
            runningExperienceYears: 5m,
            currentWeeklyDistanceKm: 40m,
            currentLongRunKm: 14m,
            recentRaceTimes: ImmutableArray<RaceTime>.Empty,
            injuryHistory: ImmutableArray<InjuryNote>.Empty,
            preferences: ValidPreferences,
            createdOn: now,
            modifiedOn: now);

        var b = new UserProfile(
            userId: Guid.NewGuid(),
            name: "Runner B",
            age: 25,
            gender: "Female",
            weightKg: 60m,
            heightCm: 165m,
            restingHeartRateAvg: 60,
            maxHeartRate: 190,
            runningExperienceYears: 3m,
            currentWeeklyDistanceKm: 30m,
            currentLongRunKm: 10m,
            recentRaceTimes: ImmutableArray<RaceTime>.Empty,
            injuryHistory: ImmutableArray<InjuryNote>.Empty,
            preferences: ValidPreferences,
            createdOn: now,
            modifiedOn: now);

        // Assert
        a.Should().NotBe(b);
    }

    private static UserProfile CreateValid(
        Guid? userId = null,
        string name = "TestRunner",
        int age = 30,
        string gender = "Male",
        decimal? weightKg = 75m,
        decimal? heightCm = 180m,
        int? restingHeartRateAvg = 55,
        int? maxHeartRate = 185,
        decimal runningExperienceYears = 5m,
        decimal currentWeeklyDistanceKm = 40m,
        decimal? currentLongRunKm = 14m,
        ImmutableArray<RaceTime>? recentRaceTimes = null,
        ImmutableArray<InjuryNote>? injuryHistory = null,
        UserPreferences? preferences = null)
    {
        return new UserProfile(
            userId: userId ?? Guid.NewGuid(),
            name: name,
            age: age,
            gender: gender,
            weightKg: weightKg,
            heightCm: heightCm,
            restingHeartRateAvg: restingHeartRateAvg,
            maxHeartRate: maxHeartRate,
            runningExperienceYears: runningExperienceYears,
            currentWeeklyDistanceKm: currentWeeklyDistanceKm,
            currentLongRunKm: currentLongRunKm,
            recentRaceTimes: recentRaceTimes ?? ImmutableArray<RaceTime>.Empty,
            injuryHistory: injuryHistory ?? ImmutableArray<InjuryNote>.Empty,
            preferences: preferences ?? ValidPreferences,
            createdOn: DateTime.UtcNow,
            modifiedOn: DateTime.UtcNow);
    }
}
