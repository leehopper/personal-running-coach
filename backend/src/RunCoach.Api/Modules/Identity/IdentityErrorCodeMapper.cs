using Microsoft.AspNetCore.Identity;

namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// Maps ASP.NET Core Identity error codes to a DTO property bucket. Source of
/// truth: the 22 stable <c>nameof(...)</c> codes declared in
/// <c>IdentityErrorDescriber</c> (unchanged across Identity 6–10). See DEC-052
/// for the full rationale and R-058 for the research trail. Unknown codes fall
/// through to <see cref="IdentityErrorBuckets.General"/> so non-field Identity
/// errors never silently vanish under an empty ModelState key.
/// </summary>
public static class IdentityErrorCodeMapper
{
    public static string Map(IdentityError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Code switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.PasswordRequiresDigit) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.PasswordRequiresLower) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.PasswordRequiresUpper) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.PasswordMismatch) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.UserAlreadyHasPassword) => IdentityErrorBuckets.Password,
            nameof(IdentityErrorDescriber.InvalidEmail) => IdentityErrorBuckets.Email,
            nameof(IdentityErrorDescriber.DuplicateEmail) => IdentityErrorBuckets.Email,
            nameof(IdentityErrorDescriber.InvalidUserName) => IdentityErrorBuckets.UserName,
            nameof(IdentityErrorDescriber.DuplicateUserName) => IdentityErrorBuckets.UserName,
            nameof(IdentityErrorDescriber.InvalidRoleName) => IdentityErrorBuckets.Role,
            nameof(IdentityErrorDescriber.DuplicateRoleName) => IdentityErrorBuckets.Role,
            nameof(IdentityErrorDescriber.UserAlreadyInRole) => IdentityErrorBuckets.Role,
            nameof(IdentityErrorDescriber.UserNotInRole) => IdentityErrorBuckets.Role,
            nameof(IdentityErrorDescriber.ConcurrencyFailure) => IdentityErrorBuckets.General,
            nameof(IdentityErrorDescriber.InvalidToken) => IdentityErrorBuckets.General,
            nameof(IdentityErrorDescriber.RecoveryCodeRedemptionFailed) => IdentityErrorBuckets.General,
            nameof(IdentityErrorDescriber.LoginAlreadyAssociated) => IdentityErrorBuckets.General,
            nameof(IdentityErrorDescriber.UserLockoutNotEnabled) => IdentityErrorBuckets.General,
            nameof(IdentityErrorDescriber.DefaultError) => IdentityErrorBuckets.General,
            _ => IdentityErrorBuckets.General,
        };
    }
}
