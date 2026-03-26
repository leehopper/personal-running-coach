namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// Represents a pace range with minimum (faster) and maximum (slower) pace per kilometer.
/// MinPerKm is the faster end of the range (shorter time), MaxPerKm is the slower end (longer time).
/// </summary>
public sealed record PaceRange
{
    public PaceRange(TimeSpan minPerKm, TimeSpan maxPerKm)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minPerKm, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxPerKm, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minPerKm, maxPerKm);

        MinPerKm = minPerKm;
        MaxPerKm = maxPerKm;
    }

    public TimeSpan MinPerKm { get; init; }

    public TimeSpan MaxPerKm { get; init; }
}
