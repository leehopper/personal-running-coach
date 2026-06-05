namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// An elapsed running duration, stored canonically as <see cref="System.TimeSpan.Ticks"/>
/// (100-ns units) so it maps to a single <c>bigint</c> column via the repo's
/// <c>DurationValueConverter</c> (DEC-072). A <c>readonly record struct</c> so EF
/// snapshots and compares by value without a custom comparer. Non-negative.
/// </summary>
public readonly record struct Duration
{
    private Duration(long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        Ticks = ticks;
    }

    /// <summary>Gets the duration in <see cref="System.TimeSpan.Ticks"/> (100-ns units).</summary>
    public long Ticks { get; }

    /// <summary>Gets the duration in total seconds.</summary>
    public double TotalSeconds => TimeSpan.FromTicks(Ticks).TotalSeconds;

    /// <summary>Gets the duration in total minutes.</summary>
    public double TotalMinutes => TimeSpan.FromTicks(Ticks).TotalMinutes;

    /// <summary>Returns this duration as a <see cref="System.TimeSpan"/>.</summary>
    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(Ticks);

    /// <summary>Creates a <see cref="Duration"/> from a tick count (100-ns units).</summary>
    public static Duration FromTicks(long ticks) => new(ticks);

    /// <summary>Creates a <see cref="Duration"/> from a number of seconds.</summary>
    public static Duration FromSeconds(double seconds) => new(TimeSpan.FromSeconds(seconds).Ticks);

    /// <summary>Creates a <see cref="Duration"/> from a number of minutes.</summary>
    public static Duration FromMinutes(double minutes) => new(TimeSpan.FromMinutes(minutes).Ticks);

    /// <summary>Creates a <see cref="Duration"/> from a <see cref="System.TimeSpan"/>.</summary>
    public static Duration FromTimeSpan(TimeSpan value) => new(value.Ticks);
}
