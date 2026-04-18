namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// An immutable pace value stored internally as seconds per kilometer.
/// Faster paces have lower SecondsPerKm values.
/// Use IsFasterThan/IsSlowerThan for comparisons — no &lt; or &gt; operators are defined
/// to avoid the "lower number = faster" confusion at call sites.
/// </summary>
public readonly record struct Pace
{
    private Pace(double secondsPerKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(secondsPerKm);
        SecondsPerKm = secondsPerKm;
    }

    /// <summary>Gets pace in seconds per kilometer.</summary>
    public double SecondsPerKm { get; }

    /// <summary>Creates a Pace from seconds per kilometer.</summary>
    public static Pace FromSecondsPerKm(double secondsPerKm) => new(secondsPerKm);

    /// <summary>Creates a Pace from a TimeSpan representing time per kilometer.</summary>
    public static Pace FromTimeSpan(TimeSpan timePerKm) => new(timePerKm.TotalSeconds);

    /// <summary>Returns this pace as a TimeSpan (time to run one kilometer).</summary>
    public TimeSpan ToTimeSpan() => TimeSpan.FromSeconds(SecondsPerKm);

    /// <summary>Returns true if this pace is faster (lower sec/km) than the other.</summary>
    public bool IsFasterThan(Pace other) => SecondsPerKm < other.SecondsPerKm;

    /// <summary>Returns true if this pace is slower (higher sec/km) than the other.</summary>
    public bool IsSlowerThan(Pace other) => SecondsPerKm > other.SecondsPerKm;
}
