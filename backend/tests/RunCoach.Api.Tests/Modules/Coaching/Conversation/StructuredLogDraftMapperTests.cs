using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit coverage for <see cref="StructuredLogDraftMapper"/> — proves a confirmed draft
/// maps field-for-field onto the unchanged Slice 2b <see cref="CreateWorkoutLogRequestDto"/>
/// SI-unit actuals, with the caller-supplied idempotency key and no prescription/metrics
/// (server-resolved at confirm; the LLM never extracts them).
/// </summary>
public sealed class StructuredLogDraftMapperTests
{
    [Fact]
    public void ToCreateWorkoutLogRequest_MapsActuals_AndAttachesIdempotencyKey()
    {
        // Arrange
        var draft = new StructuredLogDraft
        {
            OccurredOn = new DateOnly(2026, 6, 26),
            DistanceMeters = 5000,
            DurationSeconds = 1500,
            CompletionStatus = CompletionStatus.Complete,
            Notes = "felt easy",
        };
        var expectedKey = Guid.NewGuid();

        // Act
        var actual = StructuredLogDraftMapper.ToCreateWorkoutLogRequest(draft, expectedKey);

        // Assert
        actual.IdempotencyKey.Should().Be(expectedKey);
        actual.OccurredOn.Should().Be(new DateOnly(2026, 6, 26));
        actual.DistanceMeters.Should().Be(5000);
        actual.DurationSeconds.Should().Be(1500);
        actual.CompletionStatus.Should().Be(CompletionStatus.Complete);
        actual.Notes.Should().Be("felt easy");
        actual.Metrics.Should().BeNull("the classifier never extracts the open metrics bag");
        actual.Splits.Should().BeNull("the classifier never extracts splits");
    }

    [Fact]
    public void ToCreateWorkoutLogRequest_PreservesNullNotes()
    {
        // Arrange
        var draft = new StructuredLogDraft
        {
            OccurredOn = new DateOnly(2026, 6, 26),
            DistanceMeters = 8000,
            DurationSeconds = 2400,
            CompletionStatus = CompletionStatus.Partial,
            Notes = null,
        };

        // Act
        var actual = StructuredLogDraftMapper.ToCreateWorkoutLogRequest(draft, Guid.NewGuid());

        // Assert
        actual.Notes.Should().BeNull();
        actual.CompletionStatus.Should().Be(CompletionStatus.Partial);
    }

    [Fact]
    public void ToCreateWorkoutLogRequest_ThrowsArgumentNullException_WhenDraftIsNull()
    {
        // Arrange + Act
        var act = () => StructuredLogDraftMapper.ToCreateWorkoutLogRequest(null!, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
