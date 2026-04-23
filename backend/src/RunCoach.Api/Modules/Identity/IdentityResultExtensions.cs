using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        ControllerBase controller)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);

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
            // Mapper bucket names (password / email / username / general /
            // role) double as DTO-property JSON keys the frontend Zod schema
            // binds field errors against. `UserName` errors surface on the
            // `email` DTO property because Register sets `UserName = Email` —
            // there is no separate UserName field on the wire. `General` and
            // `role` surface under the canonical `"general"` bucket the SPA
            // renders in a non-field notice panel so non-field Identity errors
            // never silently vanish under an empty ModelState key (DEC-052).
            var key = IdentityErrorCodeMapper.Map(error) switch
            {
                IdentityErrorBuckets.Password => IdentityErrorBuckets.Password,
                IdentityErrorBuckets.Email => IdentityErrorBuckets.Email,
                IdentityErrorBuckets.UserName => IdentityErrorBuckets.Email,
                _ => IdentityErrorBuckets.General,
            };
            controller.ModelState.AddModelError(key, error.Description);
        }

        return controller.ValidationProblem(controller.ModelState);
    }
}
