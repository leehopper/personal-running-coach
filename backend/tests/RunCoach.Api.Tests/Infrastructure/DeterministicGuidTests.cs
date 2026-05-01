using FluentAssertions;
using RunCoach.Api.Infrastructure;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="DeterministicGuid.From"/> covering the UUID-v5
/// shape (version + variant bits), determinism, and input validation.
/// </summary>
public sealed class DeterministicGuidTests
{
    private static readonly Guid SampleUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void From_ReturnsSameGuidForSameInputs()
    {
        // Arrange / Act
        var first = DeterministicGuid.From(SampleUserId, "onboarding");
        var second = DeterministicGuid.From(SampleUserId, "onboarding");

        // Assert
        first.Should().Be(second, because: "deterministic derivation must be repeatable");
    }

    [Fact]
    public void From_DifferentStreamPurpose_ProducesDifferentGuid()
    {
        // Arrange / Act
        var onboarding = DeterministicGuid.From(SampleUserId, "onboarding");
        var plan = DeterministicGuid.From(SampleUserId, "plan");

        // Assert
        onboarding.Should().NotBe(plan, because: "stream purpose must namespace the derivation");
    }

    [Fact]
    public void From_DifferentUserId_ProducesDifferentGuid()
    {
        // Arrange
        var otherUser = Guid.Parse("99999999-8888-7777-6666-555555555555");

        // Act
        var a = DeterministicGuid.From(SampleUserId, "onboarding");
        var b = DeterministicGuid.From(otherUser, "onboarding");

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void From_SetsUuidV5VersionAndIetfVariantBits()
    {
        // Act
        var actual = DeterministicGuid.From(SampleUserId, "onboarding");

        // Assert — RFC 4122 §4.3: the version nibble must be 0x5 and the
        // variant high bits must be 10xx (0x80..0xBF when masked with 0xC0).
        // Use ToByteArray(bigEndian: true) so byte[6] / byte[8] map to the
        // canonical version and variant positions per the spec.
        var bytes = actual.ToByteArray(bigEndian: true);

        ((bytes[6] & 0xF0) >> 4).Should().Be(0x5, because: "UUID v5 sets the version nibble to 5");
        (bytes[8] & 0xC0).Should().Be(0x80, because: "RFC 4122 IETF variant bits must be 10xx");

        // Sanity: the canonical string also surfaces the version digit at the
        // start of the third hyphen-separated group.
        actual.ToString("D")[14].Should().Be('5');
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void From_ThrowsWhenStreamPurposeIsNullOrWhitespace(string streamPurpose)
    {
        // Act
        var act = () => DeterministicGuid.From(SampleUserId, streamPurpose);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(streamPurpose));
    }

    [Fact]
    public void From_ThrowsWhenStreamPurposeIsNull()
    {
        // Act
        var act = () => DeterministicGuid.From(SampleUserId, null!);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("streamPurpose");
    }
}
