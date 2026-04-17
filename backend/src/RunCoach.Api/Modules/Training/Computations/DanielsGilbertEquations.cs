namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Pure-math helpers implementing the Daniels–Gilbert 1979 equations from
/// <em>Oxygen Power</em> (Daniels &amp; Gilbert, 1979).
/// No state, no DI — call sites use the static methods directly.
/// </summary>
internal static class DanielsGilbertEquations
{
    /// <summary>Constant term in the oxygen-cost polynomial. Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double OxygenCostConstant = -4.60;

    /// <summary>Linear coefficient for velocity in the oxygen-cost polynomial. Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double OxygenCostLinear = 0.182258;

    /// <summary>Quadratic coefficient for velocity in the oxygen-cost polynomial. Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double OxygenCostQuadratic = 0.000104;

    /// <summary>Baseline fractional utilization at infinite duration. Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double FracUtilBaseline = 0.8;

    /// <summary>Amplitude of the fast-decay exponential term. Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double FracUtilAmplitude1 = 0.1894393;

    /// <summary>Decay rate of the fast exponential term (per minute). Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double FracUtilDecay1 = 0.012778;

    /// <summary>Amplitude of the slow-decay exponential term. Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double FracUtilAmplitude2 = 0.2989558;

    /// <summary>Decay rate of the slow exponential term (per minute). Source: Daniels &amp; Gilbert 1979.</summary>
    private static readonly double FracUtilDecay2 = 0.1932605;

    /// <summary>
    /// Oxygen cost of running at <paramref name="velocityMetersPerMinute"/>.
    /// Formula: VO₂ = −4.60 + 0.182258·v + 0.000104·v².
    /// </summary>
    public static double OxygenCost(double velocityMetersPerMinute)
    {
        var v = velocityMetersPerMinute;
        return OxygenCostConstant + (OxygenCostLinear * v) + (OxygenCostQuadratic * v * v);
    }

    /// <summary>
    /// Fractional utilization of VO₂max sustainable for <paramref name="timeMinutes"/>.
    /// Formula: %VO₂max = 0.8 + 0.1894393·e^(−0.012778·t) + 0.2989558·e^(−0.1932605·t).
    /// </summary>
    public static double FractionalUtilization(double timeMinutes)
    {
        return FracUtilBaseline
            + (FracUtilAmplitude1 * Math.Exp(-FracUtilDecay1 * timeMinutes))
            + (FracUtilAmplitude2 * Math.Exp(-FracUtilDecay2 * timeMinutes));
    }

    /// <summary>
    /// Closed-form inverse of <see cref="OxygenCost"/>: returns the velocity (m/min) at which
    /// <c>OxygenCost(v) == targetVo2</c>, via the positive root of the quadratic
    /// <c>0.000104·v² + 0.182258·v − (4.60 + targetVo2) = 0</c>.
    /// </summary>
    public static double SolveVelocityForTargetVo2(double targetVo2)
    {
        // OxygenCost(v) = targetVo2
        // OxygenCostQuadratic·v² + OxygenCostLinear·v + (OxygenCostConstant − targetVo2) = 0
        // Positive root: v = (−b + √(b² − 4·a·c_term)) / (2·a)
        var cTerm = OxygenCostConstant - targetVo2;
        var discriminant = (OxygenCostLinear * OxygenCostLinear) - (4.0 * OxygenCostQuadratic * cTerm);
        return (-OxygenCostLinear + Math.Sqrt(discriminant)) / (2.0 * OxygenCostQuadratic);
    }
}
