using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit coverage for <see cref="WorkoutDraftUnitConverter"/> — the deterministic SI
/// conversion the intent classifier must NOT do itself (REVIEW.md Architecture:
/// distance/time conversions belong in the unit-tested computation layer).
/// </summary>
public sealed class WorkoutDraftUnitConverterTests
{
    [Theory]
    [InlineData(5, RunnerDistanceUnit.Kilometers, 5000)]
    [InlineData(10, RunnerDistanceUnit.Kilometers, 10000)]
    [InlineData(1, RunnerDistanceUnit.Miles, 1609.344)]
    [InlineData(3.1, RunnerDistanceUnit.Miles, 4988.9664)]
    [InlineData(400, RunnerDistanceUnit.Meters, 400)]
    public void DistanceToMeters_ConvertsEachUnit(double value, RunnerDistanceUnit unit, double expectedMeters)
    {
        WorkoutDraftUnitConverter.DistanceToMeters(value, unit)
            .Should().BeApproximately(expectedMeters, 1e-6);
    }

    [Fact]
    public void DistanceToMeters_UndefinedUnit_Throws()
    {
        var act = () => WorkoutDraftUnitConverter.DistanceToMeters(1, (RunnerDistanceUnit)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 25, 0, 1500)]
    [InlineData(1, 0, 0, 3600)]
    [InlineData(1, 30, 0, 5400)]
    [InlineData(0, 22, 30, 1350)]
    [InlineData(0, 0, 0, 0)]
    public void DurationToSeconds_SumsComponents(int hours, int minutes, int seconds, double expectedSeconds)
    {
        WorkoutDraftUnitConverter.DurationToSeconds(hours, minutes, seconds)
            .Should().Be(expectedSeconds);
    }
}
