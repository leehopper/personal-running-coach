using Microsoft.AspNetCore.Identity;

namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// Maps ASP.NET Core Identity error codes to a DTO property bucket and an
/// HTTP semantic class. Source of truth: the 22 stable <c>nameof(...)</c>
/// codes declared in <c>IdentityErrorDescriber</c> (unchanged across
/// Identity 6–10). See DEC-052 for the full rationale and R-058 for the
/// research trail. Unknown codes fall through to
/// <see cref="IdentityErrorBuckets.General"/> / <see cref="IdentityErrorKind.Unknown"/>.
/// </summary>
public static class IdentityErrorCodeMapper
{
    public static Mapping Map(IdentityError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.PasswordRequiresDigit) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.PasswordRequiresLower) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.PasswordRequiresUpper) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.PasswordMismatch) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Unauthorized),
            nameof(IdentityErrorDescriber.UserAlreadyHasPassword) =>
                new(IdentityErrorBuckets.Password, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.InvalidEmail) =>
                new(IdentityErrorBuckets.Email, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.DuplicateEmail) =>
                new(IdentityErrorBuckets.Email, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.InvalidUserName) =>
                new(IdentityErrorBuckets.UserName, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.DuplicateUserName) =>
                new(IdentityErrorBuckets.UserName, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.InvalidRoleName) =>
                new(IdentityErrorBuckets.Role, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.DuplicateRoleName) =>
                new(IdentityErrorBuckets.Role, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.UserAlreadyInRole) =>
                new(IdentityErrorBuckets.Role, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.UserNotInRole) =>
                new(IdentityErrorBuckets.Role, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.ConcurrencyFailure) =>
                new(IdentityErrorBuckets.General, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.InvalidToken) =>
                new(IdentityErrorBuckets.General, IdentityErrorKind.Validation),
            nameof(IdentityErrorDescriber.RecoveryCodeRedemptionFailed) =>
                new(IdentityErrorBuckets.General, IdentityErrorKind.Unauthorized),
            nameof(IdentityErrorDescriber.LoginAlreadyAssociated) =>
                new(IdentityErrorBuckets.General, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.UserLockoutNotEnabled) =>
                new(IdentityErrorBuckets.General, IdentityErrorKind.Conflict),
            nameof(IdentityErrorDescriber.DefaultError) =>
                new(IdentityErrorBuckets.General, IdentityErrorKind.Unknown),
            _ => new(IdentityErrorBuckets.General, IdentityErrorKind.Unknown),
        };
    }

    public readonly record struct Mapping(string PropertyName, IdentityErrorKind Kind);
}
