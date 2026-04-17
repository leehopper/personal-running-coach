namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// An integer range with inclusive lower and upper bounds.
/// Used for heart-rate zone bands expressed in whole bpm.
/// </summary>
public sealed record IntRange
{
    public IntRange(int lower, int upper)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lower);
        ArgumentOutOfRangeException.ThrowIfNegative(upper);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lower, upper);

        Lower = lower;
        Upper = upper;
    }

    public int Lower { get; }

    public int Upper { get; }
}
