using Microsoft.Extensions.Options;

namespace RunCoach.Api.Modules.Training.Plan;

/// <inheritdoc />
public sealed class LocalDateProvider : ILocalDateProvider
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _appZone;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDateProvider"/> class.
    /// </summary>
    /// <param name="timeProvider">The UTC clock source.</param>
    /// <param name="settings">App clock settings supplying the IANA time-zone id.</param>
    public LocalDateProvider(TimeProvider timeProvider, IOptions<AppClockSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(settings);

        _timeProvider = timeProvider;
        _appZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Value.TimeZone);
    }

    /// <inheritdoc />
    public DateOnly Today()
    {
        var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _appZone);
        return DateOnly.FromDateTime(localNow.DateTime);
    }
}
