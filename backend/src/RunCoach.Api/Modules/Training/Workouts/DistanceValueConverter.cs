using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Maps a <see cref="Distance"/> value object to a <c>double</c> column of meters
/// (DEC-072). The repo's first EF <c>ValueConverter</c>; reused by both the
/// entity's own distance and the prescription snapshot's prescribed distance.
/// </summary>
public sealed class DistanceValueConverter : ValueConverter<Distance, double>
{
    public DistanceValueConverter()
        : base(distance => distance.Meters, meters => Distance.FromMeters(meters))
    {
    }
}
