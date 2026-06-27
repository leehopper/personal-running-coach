using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit coverage for <see cref="MessageIntentOutputValidator"/> — the post-deserialization
/// guard that enforces "the populated slot matches the discriminator" (Anthropic
/// constrained decoding cannot express it). Mirrors <c>PlanAdaptationOutputValidatorTests</c>.
/// </summary>
public sealed class MessageIntentOutputValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_ForQuestion_WithNoDraft()
    {
        // Arrange
        var output = new MessageIntentOutput { Intent = MessageIntent.Question, WorkoutLog = null };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.None);
    }

    [Fact]
    public void Validate_ReturnsValid_ForWorkoutLog_WithDraft()
    {
        // Arrange
        var output = new MessageIntentOutput { Intent = MessageIntent.WorkoutLog, WorkoutLog = SampleDraft() };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.None);
    }

    [Fact]
    public void Validate_ReturnsValid_ForAmbiguous_WithNoDraft()
    {
        // Arrange
        var output = new MessageIntentOutput { Intent = MessageIntent.Ambiguous, WorkoutLog = null };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsSlotIntentMismatch_WhenQuestionHasDraft()
    {
        // Arrange — a non-WorkoutLog intent must not carry a draft.
        var output = new MessageIntentOutput { Intent = MessageIntent.Question, WorkoutLog = SampleDraft() };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.SlotIntentMismatch);
    }

    [Fact]
    public void Validate_ReturnsSlotIntentMismatch_WhenWorkoutLogHasNoDraft()
    {
        // Arrange — the WorkoutLog intent must carry a draft.
        var output = new MessageIntentOutput { Intent = MessageIntent.WorkoutLog, WorkoutLog = null };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.SlotIntentMismatch);
    }

    [Fact]
    public void Validate_ReturnsSlotIntentMismatch_WhenAmbiguousHasDraft()
    {
        // Arrange
        var output = new MessageIntentOutput { Intent = MessageIntent.Ambiguous, WorkoutLog = SampleDraft() };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.SlotIntentMismatch);
    }

    [Fact]
    public void Validate_ReturnsSlotIntentMismatch_ForUnknownIntent()
    {
        // Arrange — JsonStringEnumConverter can deserialize an out-of-range integer,
        // so the default switch arm must fail closed.
        var output = new MessageIntentOutput { Intent = (MessageIntent)99, WorkoutLog = null };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.SlotIntentMismatch);
    }

    [Fact]
    public void Validate_ThrowsArgumentNullException_WhenOutputIsNull()
    {
        // Arrange + Act
        var act = () => MessageIntentOutputValidator.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static StructuredLogDraft SampleDraft() => new()
    {
        OccurredOn = new DateOnly(2026, 6, 26),
        DistanceValue = 5,
        DistanceUnit = RunnerDistanceUnit.Kilometers,
        DurationHours = 0,
        DurationMinutes = 25,
        DurationSeconds = 0,
        CompletionStatus = CompletionStatus.Complete,
        Notes = null,
    };
}
