using Microsoft.AspNetCore.Identity;

namespace RunCoach.Api.Modules.Identity.Entities;

/// <summary>
/// Application user aggregate backed by ASP.NET Core Identity. Extension columns
/// specific to RunCoach are added here; Slice 0 carries the Identity schema only.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
}
