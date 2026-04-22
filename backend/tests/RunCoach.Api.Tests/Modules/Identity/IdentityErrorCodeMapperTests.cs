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
    [InlineData(nameof(IdentityErrorDescriber.PasswordTooShort), IdentityErrorBuckets.Password, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars), IdentityErrorBuckets.Password, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric), IdentityErrorBuckets.Password, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresDigit), IdentityErrorBuckets.Password, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresLower), IdentityErrorBuckets.Password, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordRequiresUpper), IdentityErrorBuckets.Password, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.PasswordMismatch), IdentityErrorBuckets.Password, IdentityErrorKind.Unauthorized)]
    [InlineData(nameof(IdentityErrorDescriber.UserAlreadyHasPassword), IdentityErrorBuckets.Password, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidEmail), IdentityErrorBuckets.Email, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateEmail), IdentityErrorBuckets.Email, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidUserName), IdentityErrorBuckets.UserName, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateUserName), IdentityErrorBuckets.UserName, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidRoleName), IdentityErrorBuckets.Role, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateRoleName), IdentityErrorBuckets.Role, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.UserAlreadyInRole), IdentityErrorBuckets.Role, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.UserNotInRole), IdentityErrorBuckets.Role, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.ConcurrencyFailure), IdentityErrorBuckets.General, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.InvalidToken), IdentityErrorBuckets.General, IdentityErrorKind.Validation)]
    [InlineData(nameof(IdentityErrorDescriber.RecoveryCodeRedemptionFailed), IdentityErrorBuckets.General, IdentityErrorKind.Unauthorized)]
    [InlineData(nameof(IdentityErrorDescriber.LoginAlreadyAssociated), IdentityErrorBuckets.General, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.UserLockoutNotEnabled), IdentityErrorBuckets.General, IdentityErrorKind.Conflict)]
    [InlineData(nameof(IdentityErrorDescriber.DefaultError), IdentityErrorBuckets.General, IdentityErrorKind.Unknown)]
    public void Map_ReturnsExpectedBucketAndKind_ForKnownCodes(
        string identityCode,
        string expectedBucket,
        IdentityErrorKind expectedKind)
    {
        // Arrange
        var error = new IdentityError { Code = identityCode, Description = "ignored" };

        // Act
        var actual = IdentityErrorCodeMapper.Map(error);

        // Assert
        actual.PropertyName.Should().Be(expectedBucket);
        actual.Kind.Should().Be(expectedKind);
    }

    [Fact]
    public void Map_FallsBackToGeneralUnknown_ForUnrecognizedCode()
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
        actual.PropertyName.Should().Be(IdentityErrorBuckets.General);
        actual.Kind.Should().Be(IdentityErrorKind.Unknown);
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
