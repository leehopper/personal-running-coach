using System.ComponentModel.DataAnnotations;

namespace RunCoach.Api.Modules.Identity.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/auth/register</c>. DataAnnotations enforce
/// format + basic length so the common "too short" / malformed-email path
/// short-circuits through <c>[ApiController]</c> auto-400 before reaching
/// Identity (DEC-052). Identity remains source-of-truth for character-class
/// rules and uniqueness.
/// </summary>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)]
    string Email,
    [Required, MinLength(12), MaxLength(128)]
    string Password);
