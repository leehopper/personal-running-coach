using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Experiments;

namespace RunCoach.Api.Tests.Modules.Coaching.Experiments;

/// <summary>
/// Tests for <see cref="SampleConversations"/> to verify conversation
/// test data is well-formed and accessible.
/// </summary>
public class SampleConversationsTests
{
    [Fact]
    public void Empty_ReturnsEmptyArray()
    {
        // Act
        var actual = SampleConversations.Empty;

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void IntermediateGoalSetting_HasFiveTurns()
    {
        // Act
        var actual = SampleConversations.IntermediateGoalSetting;

        // Assert
        actual.Should().HaveCount(5);
    }

    [Fact]
    public void IntermediateGoalSetting_AllTurnsHaveContent()
    {
        // Act & Assert
        foreach (var turn in SampleConversations.IntermediateGoalSetting)
        {
            turn.UserMessage.Should().NotBeNullOrWhiteSpace();
            turn.CoachMessage.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void BeginnerGettingStarted_HasFiveTurns()
    {
        // Act
        var actual = SampleConversations.BeginnerGettingStarted;

        // Assert
        actual.Should().HaveCount(5);
    }

    [Fact]
    public void BeginnerGettingStarted_AllTurnsHaveContent()
    {
        // Act & Assert
        foreach (var turn in SampleConversations.BeginnerGettingStarted)
        {
            turn.UserMessage.Should().NotBeNullOrWhiteSpace();
            turn.CoachMessage.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void GetIntermediateTurns_ReturnsCorrectCount(int requested, int expected)
    {
        // Act
        var actual = SampleConversations.GetIntermediateTurns(requested);

        // Assert
        actual.Should().HaveCount(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    public void GetBeginnerTurns_ReturnsCorrectCount(int requested, int expected)
    {
        // Act
        var actual = SampleConversations.GetBeginnerTurns(requested);

        // Assert
        actual.Should().HaveCount(expected);
    }

    [Fact]
    public void GetIntermediateTurns_NegativeCount_ReturnsEmpty()
    {
        // Act
        var actual = SampleConversations.GetIntermediateTurns(-1);

        // Assert
        actual.Should().BeEmpty();
    }

    [Fact]
    public void GetBeginnerTurns_NegativeCount_ReturnsEmpty()
    {
        // Act
        var actual = SampleConversations.GetBeginnerTurns(-1);

        // Assert
        actual.Should().BeEmpty();
    }
}
