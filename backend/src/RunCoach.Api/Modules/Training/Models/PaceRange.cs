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
    /// <remarks>
    /// Declared without an init setter so <c>with</c>-expressions cannot reassign
    /// this property and bypass the constructor's Fast ≤ Slow invariant check.
    /// Matches the pattern in <see cref="IntRange"/>.
    /// </remarks>
    public Pace Fast { get; }

    /// <summary>Gets the slower (higher sec/km) end of the range.</summary>
    /// <remarks>
    /// Declared without an init setter so <c>with</c>-expressions cannot reassign
    /// this property and bypass the constructor's Fast ≤ Slow invariant check.
    /// </remarks>
    public Pace Slow { get; }
}
