namespace RunCoach.Api.Modules.Identity;

/// <summary>
/// Semantic class of an <see cref="Microsoft.AspNetCore.Identity.IdentityError"/>
/// used to pick the HTTP status when translating to a ProblemDetails response.
/// </summary>
public enum IdentityErrorKind
{
    Validation,
    Conflict,
    Unauthorized,
    Unknown,
}
