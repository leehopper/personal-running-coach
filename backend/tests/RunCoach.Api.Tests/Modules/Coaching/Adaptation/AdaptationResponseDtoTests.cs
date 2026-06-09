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
    [Fact]
    public void FromError_MapsTransientWithRetryAfter_ToRetryableErrorEnvelope()
    {
        // Arrange
        var exception = new TransientCoachingLlmException("busy", retryAfterSeconds: 30, innerException: null);

        // Act
        var envelope = AdaptationResponseDto.FromError(exception);

        // Assert
        envelope.Kind.Should().Be(AdaptationResponseKind.Error);
        envelope.Retryable.Should().BeTrue();
        envelope.RetryAfterSeconds.Should().Be(30);
        envelope.ErrorMessage.Should().Be("busy");
    }

    [Fact]
    public void FromError_MapsTransientWithoutRetryAfter_ToRetryableErrorWithNullDelay()
    {
        // Arrange
        var exception = new TransientCoachingLlmException("unreachable", retryAfterSeconds: null, innerException: null);

        // Act
        var envelope = AdaptationResponseDto.FromError(exception);

        // Assert
        envelope.Kind.Should().Be(AdaptationResponseKind.Error);
        envelope.Retryable.Should().BeTrue();
        envelope.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void FromError_MapsPermanent_ToNonRetryableErrorEnvelope()
    {
        // Arrange
        var exception = new PermanentCoachingLlmException("rejected", innerException: null);

        // Act
        var envelope = AdaptationResponseDto.FromError(exception);

        // Assert
        envelope.Kind.Should().Be(AdaptationResponseKind.Error);
        envelope.Retryable.Should().BeFalse();
        envelope.RetryAfterSeconds.Should().BeNull();
        envelope.ErrorMessage.Should().Be("rejected");
    }

    [Fact]
    public void FromError_ThrowsArgumentNullException_WhenExceptionIsNull()
    {
        var act = () => AdaptationResponseDto.FromError(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
