using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Unit tests for <see cref="OnboardingProgressDto"/> construction-time
/// validation. The record makes invalid <c>(completed, total)</c> pairs
/// unrepresentable so the chat UI's progress ratio never collapses to NaN
/// or a negative value.
/// </summary>
public sealed class OnboardingProgressDtoTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(3, 5)]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    public void Construction_AcceptsValidPairs(int completed, int total)
    {
        // Act
        var actual = new OnboardingProgressDto(completed, total);

        // Assert
        actual.CompletedTopics.Should().Be(completed);
        actual.TotalTopics.Should().Be(total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Construction_RejectsTotalBelowOne(int total)
    {
        // Act
        var act = () => new OnboardingProgressDto(0, total);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("totalTopics");
    }

    [Fact]
    public void Construction_RejectsNegativeCompleted()
    {
        // Act
        var act = () => new OnboardingProgressDto(-1, 5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("completedTopics");
    }

    [Theory]
    [InlineData(6, 5)]
    [InlineData(10, 5)]
    public void Construction_RejectsCompletedExceedingTotal(int completed, int total)
    {
        // Act
        var act = () => new OnboardingProgressDto(completed, total);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("completedTopics");
    }
}
