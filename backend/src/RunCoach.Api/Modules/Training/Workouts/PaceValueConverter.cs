using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Maps a <see cref="Pace"/> value object to a <c>double</c> column of seconds
/// per kilometer (DEC-072). Used by the prescription snapshot's fast/slow pace
/// bounds inside the <c>WorkoutLog.Prescription</c> complex type.
/// </summary>
public sealed class PaceValueConverter : ValueConverter<Pace, double>
{
    public PaceValueConverter()
        : base(pace => pace.SecondsPerKm, seconds => Pace.FromSecondsPerKm(seconds))
    {
    }
}
