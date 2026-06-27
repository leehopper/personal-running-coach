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
    public static TheoryData<StructuredLogDraft> OutOfRangeDrafts() => new()
    {
        SampleDraft() with { DistanceValue = 0 },
        SampleDraft() with { DistanceValue = -5 },
        SampleDraft() with { DistanceValue = double.NaN },
        SampleDraft() with { DistanceUnit = (RunnerDistanceUnit)99 },
        SampleDraft() with { DurationHours = -1 },
        SampleDraft() with { DurationMinutes = 60 },
        SampleDraft() with { DurationMinutes = -1 },
        SampleDraft() with { DurationSeconds = 60 },
        SampleDraft() with { DurationHours = 0, DurationMinutes = 0, DurationSeconds = 0 },
    };

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

    [Theory]
    [MemberData(nameof(OutOfRangeDrafts))]
    public void Validate_ReturnsDraftActualsOutOfRange_ForImpossibleActuals(StructuredLogDraft draft)
    {
        // Arrange — constrained decoding cannot reject these numerically, so the validator must.
        var output = new MessageIntentOutput { Intent = MessageIntent.WorkoutLog, WorkoutLog = draft };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(MessageIntentOutputValidationViolation.DraftActualsOutOfRange);
    }

    [Fact]
    public void Validate_AllowsBoundaryDurationComponents()
    {
        // Arrange — 59/59 are valid; only 60+ is out of range.
        var draft = SampleDraft() with { DurationHours = 2, DurationMinutes = 59, DurationSeconds = 59 };
        var output = new MessageIntentOutput { Intent = MessageIntent.WorkoutLog, WorkoutLog = draft };

        // Act
        var result = MessageIntentOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
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
