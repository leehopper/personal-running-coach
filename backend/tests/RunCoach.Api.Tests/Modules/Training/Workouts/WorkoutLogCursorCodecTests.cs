using System.Text;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit tests for <see cref="WorkoutLogCursorCodec"/> — the opaque keyset-cursor
/// token used by the history query endpoint (slice-2b Unit 4). Both the run date
/// and the log id are load-bearing for the <c>OccurredOn DESC, WorkoutLogId DESC</c>
/// keyset (the id breaks ties within a date), so a token that drops or corrupts
/// either must be rejected rather than silently mis-page.
/// </summary>
public class WorkoutLogCursorCodecTests
{
    [Fact]
    public void Encode_ThenDecode_RoundTripsBothComponents()
    {
        // Arrange
        var expected = new WorkoutLogCursor(new DateOnly(2026, 6, 15), Guid.NewGuid());

        // Act
        var token = WorkoutLogCursorCodec.Encode(expected);
        var decoded = WorkoutLogCursorCodec.TryDecode(token, out var actual);

        // Assert
        decoded.Should().BeTrue();
        actual.Should().Be(expected);
    }

    [Fact]
    public void Encode_ProducesANonEmptyOpaqueToken()
    {
        // Arrange
        var cursor = new WorkoutLogCursor(new DateOnly(2026, 1, 2), Guid.NewGuid());

        // Act
        var token = WorkoutLogCursorCodec.Encode(cursor);

        // Assert — opaque: not the bare id the client could hand-craft.
        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain(cursor.WorkoutLogId.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64 @@@")]
    public void TryDecode_NullEmptyOrNonBase64_ReturnsFalse(string? token)
    {
        // Act
        var decoded = WorkoutLogCursorCodec.TryDecode(token, out var cursor);

        // Assert
        decoded.Should().BeFalse();
        cursor.Should().Be(default(WorkoutLogCursor));
    }

    [Fact]
    public void TryDecode_ValidBase64ButMissingSeparator_ReturnsFalse()
    {
        // Arrange — well-formed base64 whose payload has no "date|id" shape.
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("2026-06-15"));

        // Act
        var decoded = WorkoutLogCursorCodec.TryDecode(token, out _);

        // Assert
        decoded.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ValidBase64ButUnparseableDateAndGuid_ReturnsFalse()
    {
        // Arrange
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("not-a-date|not-a-guid"));

        // Act
        var decoded = WorkoutLogCursorCodec.TryDecode(token, out _);

        // Assert
        decoded.Should().BeFalse();
    }
}
