using FluentAssertions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Adaptation;

/// <summary>
/// The DEC-073 flat <c>Kind=Error</c> envelope. On a terminal coaching-LLM failure the
/// adaptation flow returns this over HTTP 200 (the log itself committed); the frontend reads
/// <c>retryable</c> / <c>retryAfterSeconds</c> without needing SDK types. Maps a
/// <see cref="TransientCoachingLlmException"/> to a retryable envelope (carrying the retry-after
/// hint) and a <see cref="PermanentCoachingLlmException"/> to a non-retryable one.
/// </summary>
public sealed class AdaptationResponseDtoTests
{
    [Theory]
    [InlineData(30, 30)]
    [InlineData(null, null)]
    public void FromError_MapsTransient_ToRetryableErrorEnvelope(int? retryAfterSeconds, int? expectedRetryAfterSeconds)
    {
        // Arrange
        var exception = new TransientCoachingLlmException("busy", retryAfterSeconds, innerException: null);

        // Act
        var actualEnvelope = AdaptationResponseDto.FromError(exception);

        // Assert
        actualEnvelope.Kind.Should().Be(AdaptationResponseKind.Error);
        actualEnvelope.Retryable.Should().BeTrue();
        actualEnvelope.RetryAfterSeconds.Should().Be(expectedRetryAfterSeconds);
        actualEnvelope.ErrorMessage.Should().Be("busy");
    }

    [Fact]
    public void FromError_MapsPermanent_ToNonRetryableErrorEnvelope()
    {
        // Arrange
        var exception = new PermanentCoachingLlmException("rejected", innerException: null);

        // Act
        var actualEnvelope = AdaptationResponseDto.FromError(exception);

        // Assert
        actualEnvelope.Kind.Should().Be(AdaptationResponseKind.Error);
        actualEnvelope.Retryable.Should().BeFalse();
        actualEnvelope.RetryAfterSeconds.Should().BeNull();
        actualEnvelope.ErrorMessage.Should().Be("rejected");
    }

    [Fact]
    public void FromError_ThrowsArgumentNullException_WhenExceptionIsNull()
    {
        // Arrange + Act
        var act = () => AdaptationResponseDto.FromError(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
