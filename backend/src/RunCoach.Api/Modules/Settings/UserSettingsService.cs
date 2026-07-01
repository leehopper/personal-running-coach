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

        var existing = await _db.UserSettings
            .SingleOrDefaultAsync(s => s.UserId == userId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.PreferredUnits = preferredUnits;
            existing.ModifiedOn = now;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var inserted = new UserSettings
        {
            UserId = userId,
            PreferredUnits = preferredUnits,
            CreatedOn = now,
            ModifiedOn = now,
        };
        _db.UserSettings.Add(inserted);

        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent first write inserted the row first (unique PK on UserId).
            // Detach the failed insert, reload the winner, and apply our value so the
            // documented last-write-wins semantics hold under the race rather than
            // surfacing a 500. A genuine non-conflict fault re-throws when the reload
            // finds no row.
            _db.Entry(inserted).State = EntityState.Detached;
            var winner = await _db.UserSettings
                .SingleAsync(s => s.UserId == userId, ct)
                .ConfigureAwait(false);
            winner.PreferredUnits = preferredUnits;
            winner.ModifiedOn = now;
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
