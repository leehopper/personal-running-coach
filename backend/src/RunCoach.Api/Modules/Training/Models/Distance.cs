namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// An immutable distance value stored internally in meters.
/// </summary>
public readonly record struct Distance
{
    private const double MetersPerKilometer = 1000.0;
    private const double MetersPerMile = 1609.344;

    private Distance(double meters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(meters);
        Meters = meters;
    }

    /// <summary>Gets distance in meters.</summary>
    public double Meters { get; }

    /// <summary>Gets distance in kilometers.</summary>
    public double Kilometers => Meters / MetersPerKilometer;

    /// <summary>Gets distance in miles.</summary>
    public double Miles => Meters / MetersPerMile;

    /// <summary>Creates a Distance from a value in meters.</summary>
    public static Distance FromMeters(double meters) => new(meters);

    /// <summary>Creates a Distance from a value in kilometers.</summary>
    public static Distance FromKilometers(double kilometers) => new(kilometers * MetersPerKilometer);

    /// <summary>Creates a Distance from a value in miles.</summary>
    public static Distance FromMiles(double miles) => new(miles * MetersPerMile);
}
