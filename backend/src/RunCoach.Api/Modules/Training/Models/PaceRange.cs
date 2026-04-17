namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A pace range with Fast (quicker end) and Slow (slower end) bounds.
/// Fast.SecondsPerKm is always less than or equal to Slow.SecondsPerKm.
/// </summary>
public sealed record PaceRange
{
    public PaceRange(Pace fast, Pace slow)
    {
        if (fast.IsSlowerThan(slow))
        {
            throw new ArgumentException(
                "Fast pace must not be slower than Slow pace. " +
                $"Fast={fast.SecondsPerKm:F1} s/km, Slow={slow.SecondsPerKm:F1} s/km.",
                nameof(fast));
        }

        Fast = fast;
        Slow = slow;
    }

    /// <summary>Gets the faster (lower sec/km) end of the range.</summary>
    public Pace Fast { get; init; }

    /// <summary>Gets the slower (higher sec/km) end of the range.</summary>
    public Pace Slow { get; init; }
}
