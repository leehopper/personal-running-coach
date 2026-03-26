namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A generic numeric range with minimum and maximum decimal values.
/// Used for distance ranges, volume targets, and similar bounded quantities.
/// </summary>
public sealed record DecimalRange
{
    public DecimalRange(decimal min, decimal max)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);

        Min = min;
        Max = max;
    }

    public decimal Min { get; init; }

    public decimal Max { get; init; }
}
