namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// Canonical DTO-property bucket names used as keys in
/// <see cref="Microsoft.AspNetCore.Mvc.ValidationProblemDetails.Errors"/>.
/// Aligned with the frontend Zod schema keys so React Hook Form binds field
/// errors without an extra translation layer.
/// </summary>
public static class IdentityErrorBuckets
{
    public const string Password = "password";
    public const string Email = "email";
    public const string UserName = "username";
    public const string Role = "role";
    public const string General = "general";
}
