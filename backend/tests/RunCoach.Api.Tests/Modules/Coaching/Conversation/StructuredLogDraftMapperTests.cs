using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Unit coverage for <see cref="StructuredLogDraftMapper"/> — proves a confirmed draft
/// maps onto the unchanged Slice 2b <see cref="CreateWorkoutLogRequestDto"/>, with the
/// runner-stated units converted to SI server-side (the LLM never converts), the
/// caller-supplied idempotency key, and no prescription/metrics (server-resolved at
/// confirm; the LLM never extracts them).
/// </summary>
public sealed class StructuredLogDraftMapperTests
{
    [Fact]
    public void ToCreateWorkoutLogRequest_ConvertsKilometersAndMinutes_AndAttachesIdempotencyKey()
    {
        // Arrange — runner stated "5 km in 25 minutes"; the server converts to SI.
        var draft = new StructuredLogDraft
        {
            OccurredOn = new DateOnly(2026, 6, 26),
            DistanceValue = 5,
            DistanceUnit = RunnerDistanceUnit.Kilometers,
            DurationHours = 0,
            DurationMinutes = 25,
            DurationSeconds = 0,
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
    public void ToCreateWorkoutLogRequest_ConvertsMilesAndCompoundDuration()
    {
        // Arrange — runner stated "3.1 miles in 1 hour 30 minutes 30 seconds".
        var draft = new StructuredLogDraft
        {
            OccurredOn = new DateOnly(2026, 6, 26),
            DistanceValue = 3.1,
            DistanceUnit = RunnerDistanceUnit.Miles,
            DurationHours = 1,
            DurationMinutes = 30,
            DurationSeconds = 30,
            CompletionStatus = CompletionStatus.Complete,
            Notes = null,
        };

        // Act
        var actual = StructuredLogDraftMapper.ToCreateWorkoutLogRequest(draft, Guid.NewGuid());

        // Assert — 3.1 * 1609.344 = 4988.9664 m; 1*3600 + 30*60 + 30 = 5430 s.
        actual.DistanceMeters.Should().BeApproximately(4988.9664, 1e-6);
        actual.DurationSeconds.Should().Be(5430);
    }

    [Fact]
    public void ToCreateWorkoutLogRequest_PreservesNullNotes()
    {
        // Arrange
        var draft = new StructuredLogDraft
        {
            OccurredOn = new DateOnly(2026, 6, 26),
            DistanceValue = 8000,
            DistanceUnit = RunnerDistanceUnit.Meters,
            DurationHours = 0,
            DurationMinutes = 40,
            DurationSeconds = 0,
            CompletionStatus = CompletionStatus.Partial,
            Notes = null,
        };

        // Act
        var actual = StructuredLogDraftMapper.ToCreateWorkoutLogRequest(draft, Guid.NewGuid());

        // Assert
        actual.DistanceMeters.Should().Be(8000);
        actual.DurationSeconds.Should().Be(2400);
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
