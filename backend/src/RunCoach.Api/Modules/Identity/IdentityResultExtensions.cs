using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RunCoach.Api.Modules.Identity.Contracts;

namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// Translates a failed <see cref="IdentityResult"/> into the RunCoach error
/// contract per DEC-052. Uniqueness conflicts (<c>DuplicateEmail</c> /
/// <c>DuplicateUserName</c>) short-circuit to HTTP 409 plain
/// <see cref="ProblemDetails"/> — their <c>title</c> is intentionally generic
/// to preserve the enumeration-resistance posture. All other failures route
/// through <c>ModelState.AddModelError(propertyKey, description)</c> +
/// <c>ValidationProblem(ModelState)</c>, which flows through
/// <c>ProblemDetailsFactory.CreateValidationProblemDetails</c> and emits a
/// DTO-property-keyed <see cref="ValidationProblemDetails"/> with the same
/// shape as <c>[ApiController]</c>'s auto-400.
/// </summary>
public static class IdentityResultExtensions
{
    private const string RegistrationConflictType =
        "https://runcoach.app/problems/registration-conflict";

    /// <summary>
    /// Translates a failed <see cref="IdentityResult"/> from
    /// <c>POST /api/v1/auth/register</c> into an <see cref="IActionResult"/>.
    /// </summary>
    public static IActionResult ToRegistrationActionResult(
        this IdentityResult result,
        ControllerBase controller,
        RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(request);

        if (result.Succeeded)
        {
            throw new InvalidOperationException(
                "ToRegistrationActionResult called on a successful IdentityResult.");
        }

        var hasConflict = result.Errors.Any(e =>
            e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                   or nameof(IdentityErrorDescriber.DuplicateUserName));

        if (hasConflict)
        {
            return controller.Problem(
                type: RegistrationConflictType,
                title: "The account could not be created.",
                statusCode: StatusCodes.Status409Conflict);
        }

        foreach (var error in result.Errors)
        {
            var key = IdentityErrorCodeMapper.Map(error).PropertyName switch
            {
                IdentityErrorBuckets.Password => nameof(RegisterRequest.Password).ToCamelCase(),
                IdentityErrorBuckets.Email => nameof(RegisterRequest.Email).ToCamelCase(),
                IdentityErrorBuckets.UserName => nameof(RegisterRequest.Email).ToCamelCase(),
                _ => string.Empty,
            };
            controller.ModelState.AddModelError(key, error.Description);
        }

        return controller.ValidationProblem(controller.ModelState);
    }

    private static string ToCamelCase(this string value) =>
        value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
