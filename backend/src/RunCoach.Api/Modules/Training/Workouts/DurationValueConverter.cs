using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Maps a <see cref="Duration"/> value object to a <c>bigint</c> column (DEC-072).
/// </summary>
public sealed class DurationValueConverter : ValueConverter<Duration, long>
{
    public DurationValueConverter()
        : base(duration => duration.Ticks, ticks => Duration.FromTicks(ticks))
    {
    }
}
