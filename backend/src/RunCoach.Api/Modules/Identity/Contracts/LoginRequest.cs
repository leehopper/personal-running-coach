using System.ComponentModel.DataAnnotations;

namespace RunCoach.Api.Modules.Identity.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/auth/login</c>. Validation here is
/// deliberately permissive — format mismatches must produce the same 401
/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> as a genuinely
/// unknown user, not a 400, so an enumerator cannot distinguish the two.
/// Only <c>[Required]</c> is applied to reject missing fields via model
/// binding before the timing-mitigation path runs.
/// </summary>
public sealed record LoginRequest(
    [property: Required]
    string Email,
    [property: Required]
    string Password);
