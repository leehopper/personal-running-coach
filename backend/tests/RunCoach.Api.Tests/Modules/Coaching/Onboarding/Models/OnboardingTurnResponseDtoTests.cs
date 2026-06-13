using System.Text.Json;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Unit tests for the <see cref="OnboardingTurnResponseDto"/> static
/// factories that encode the discriminator invariants the positional record
/// cannot enforce on its own.
/// </summary>
public sealed class OnboardingTurnResponseDtoTests
{
    private static readonly OnboardingProgressDto SampleProgress = new(0, 5);

    [Fact]
    public void Ask_ConstructsValidAskResponse()
    {
        // Arrange
        var blocks = JsonSerializer.SerializeToElement(new[] { new { type = "text", text = "hi" } });

        // Act
        var actual = OnboardingTurnResponseDto.Ask(
            assistantBlocks: blocks,
            topic: OnboardingTopic.PrimaryGoal,
            suggestedInputType: SuggestedInputType.SingleSelect,
            progress: SampleProgress);

        // Assert
        actual.Kind.Should().Be(OnboardingTurnKind.Ask);
        actual.Topic.Should().Be(OnboardingTopic.PrimaryGoal);
        actual.SuggestedInputType.Should().Be(SuggestedInputType.SingleSelect);
        actual.Progress.Should().Be(SampleProgress);
        actual.PlanId.Should().BeNull();
    }

    [Fact]
    public void Ask_NullProgress_Throws()
    {
        // Arrange
        var blocks = JsonSerializer.SerializeToElement(Array.Empty<object>());

        // Act
        var act = () => OnboardingTurnResponseDto.Ask(
            assistantBlocks: blocks,
            topic: OnboardingTopic.PrimaryGoal,
            suggestedInputType: SuggestedInputType.SingleSelect,
            progress: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("progress");
    }

    [Fact]
    public void Complete_ConstructsValidCompleteResponse()
    {
        // Arrange
        var blocks = JsonSerializer.SerializeToElement(new[] { new { type = "text", text = "all done" } });
        var planId = Guid.Parse("99999999-1111-2222-3333-444444444444");

        // Act
        var actual = OnboardingTurnResponseDto.Complete(
            assistantBlocks: blocks,
            progress: SampleProgress,
            planId: planId);

        // Assert
        actual.Kind.Should().Be(OnboardingTurnKind.Complete);
        actual.Topic.Should().BeNull();
        actual.SuggestedInputType.Should().BeNull();
        actual.Progress.Should().Be(SampleProgress);
        actual.PlanId.Should().Be(planId);
    }

    [Fact]
    public void Complete_NullProgress_Throws()
    {
        // Arrange
        var blocks = JsonSerializer.SerializeToElement(Array.Empty<object>());

        // Act
        var act = () => OnboardingTurnResponseDto.Complete(
            assistantBlocks: blocks,
            progress: null!,
            planId: Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("progress");
    }

    [Fact]
    public void Complete_EmptyPlanId_Throws()
    {
        // Arrange
        var blocks = JsonSerializer.SerializeToElement(Array.Empty<object>());

        // Act
        var act = () => OnboardingTurnResponseDto.Complete(
            assistantBlocks: blocks,
            progress: SampleProgress,
            planId: Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("planId");
    }

    [Fact]
    public void Error_BuildsErrorKindWithMessage()
    {
        // Arrange
        const string expectedMessage = "We couldn't fit a plan to your event date. Please try again.";

        // Act
        var actual = OnboardingTurnResponseDto.Error(expectedMessage);

        // Assert -- full Error contract: message + every per-Kind field the factory zeroes.
        actual.Kind.Should().Be(OnboardingTurnKind.Error);
        actual.ErrorMessage.Should().Be(expectedMessage);
        actual.PlanId.Should().BeNull();
        actual.Topic.Should().BeNull();
        actual.SuggestedInputType.Should().BeNull();
        actual.Progress.Should().Be(new OnboardingProgressDto(0, 1));
    }
}
