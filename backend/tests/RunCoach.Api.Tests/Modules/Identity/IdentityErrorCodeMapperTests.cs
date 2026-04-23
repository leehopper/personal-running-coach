using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using RunCoach.Api.Modules.Identity;

namespace RunCoach.Api.Tests.Modules.Identity;

/// <summary>
/// Exhaustive mapping assertions over the 22 stable
/// <see cref="IdentityErrorDescriber"/> codes plus the unknown-code fallback.
/// Every case is asserted explicitly — no reflection over the describer — so
/// a future SDK that silently renames or drops a code surfaces as a specific
/// test failure rather than a coverage drift (DEC-052).
/// </summary>
[Trait("Category", "Unit")]
public sealed class IdentityErrorCodeMapperTests
{
    [Theory]
    [InlineData(nameof(IdentityErrorDescriber.PasswordTooShort), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresDigit), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresLower), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresUpper), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordMismatch), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.UserAlreadyHasPassword), IdentityErrorBuckets.Password)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidEmail), IdentityErrorBuckets.Email)]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateEmail), IdentityErrorBuckets.Email)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidUserName), IdentityErrorBuckets.UserName)]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateUserName), IdentityErrorBuckets.UserName)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidRoleName), IdentityErrorBuckets.Role)]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateRoleName), IdentityErrorBuckets.Role)]
    [InlineData(nameof(IdentityErrorDescriber.UserAlreadyInRole), IdentityErrorBuckets.Role)]
    [InlineData(nameof(IdentityErrorDescriber.UserNotInRole), IdentityErrorBuckets.Role)]
    [InlineData(nameof(IdentityErrorDescriber.ConcurrencyFailure), IdentityErrorBuckets.General)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidToken), IdentityErrorBuckets.General)]
    [InlineData(nameof(IdentityErrorDescriber.RecoveryCodeRedemptionFailed), IdentityErrorBuckets.General)]
    [InlineData(nameof(IdentityErrorDescriber.LoginAlreadyAssociated), IdentityErrorBuckets.General)]
    [InlineData(nameof(IdentityErrorDescriber.UserLockoutNotEnabled), IdentityErrorBuckets.General)]
    [InlineData(nameof(IdentityErrorDescriber.DefaultError), IdentityErrorBuckets.General)]
    public void Map_ReturnsExpectedBucket_ForKnownCodes(string identityCode, string expectedBucket)
    {
        // Arrange
        var error = new IdentityError { Code = identityCode, Description = "ignored" };

        // Act
        var actual = IdentityErrorCodeMapper.Map(error);

        // Assert
        actual.Should().Be(expectedBucket);
    }

    [Fact]
    public void Map_FallsBackToGeneral_ForUnrecognizedCode()
    {
        // Arrange — a code no version of IdentityErrorDescriber has ever
        // shipped. A silently-renamed code in a future Identity release
        // should land here and surface nowhere else.
        var error = new IdentityError
        {
            Code = "NotAnIdentityErrorCode",
            Description = "unknown",
        };

        // Act
        var actual = IdentityErrorCodeMapper.Map(error);

        // Assert
        actual.Should().Be(IdentityErrorBuckets.General);
    }

    [Fact]
    public void Map_ThrowsArgumentNullException_ForNullError()
    {
        // Arrange + Act
        var act = () => IdentityErrorCodeMapper.Map(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("error");
    }
}
