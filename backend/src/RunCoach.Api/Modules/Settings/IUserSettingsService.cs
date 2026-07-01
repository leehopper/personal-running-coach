using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Settings;

/// <summary>
/// Read/write for a runner's <see cref="UserSettings"/> (Slice 4C-units). Scoped —
/// shares the request <c>RunCoachDbContext</c>. Pure persistence: no projection,
/// no Wolverine, no LLM.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Returns <paramref name="userId"/>'s preferred units, defaulting to
    /// <see cref="PreferredUnits.Kilometers"/> when the runner has no settings row
    /// yet — a row is created lazily on the first write.
    /// </summary>
    Task<PreferredUnits> GetPreferredUnitsAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Upserts <paramref name="userId"/>'s preferred units, last-write-wins.
    /// </summary>
    Task SetPreferredUnitsAsync(Guid userId, PreferredUnits preferredUnits, CancellationToken ct);
}
