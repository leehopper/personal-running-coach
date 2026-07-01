using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Settings;

/// <summary>
/// Default <see cref="IUserSettingsService"/>. A plain EF read/upsert on the
/// user-keyed <see cref="UserSettings"/> row — no projection, no Wolverine, no
/// LLM. Reads default to Kilometers for a row-less runner; writes upsert
/// last-write-wins and stamp the audit timestamps from the injected
/// <see cref="TimeProvider"/>.
/// </summary>
public sealed class UserSettingsService(
    RunCoachDbContext db,
    TimeProvider timeProvider) : IUserSettingsService
{
    private readonly RunCoachDbContext _db = db;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public async Task<PreferredUnits> GetPreferredUnitsAsync(Guid userId, CancellationToken ct)
    {
        var row = await _db.UserSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct)
            .ConfigureAwait(false);

        return row?.PreferredUnits ?? PreferredUnits.Kilometers;
    }

    /// <inheritdoc />
    public async Task SetPreferredUnitsAsync(Guid userId, PreferredUnits preferredUnits, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();

        var row = await _db.UserSettings
            .SingleOrDefaultAsync(s => s.UserId == userId, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            _db.UserSettings.Add(new UserSettings
            {
                UserId = userId,
                PreferredUnits = preferredUnits,
                CreatedOn = now,
                ModifiedOn = now,
            });
        }
        else
        {
            row.PreferredUnits = preferredUnits;
            row.ModifiedOn = now;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
