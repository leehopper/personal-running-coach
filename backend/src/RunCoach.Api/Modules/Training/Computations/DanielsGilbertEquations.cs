namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Pure-math helpers implementing the Daniels–Gilbert 1979 equations from
/// <em>Oxygen Power</em> (Daniels &amp; Gilbert, 1979).
/// No state, no DI — call sites use the static methods directly.
/// </summary>
internal static class DanielsGilbertEquations
{
    /// <summary>Hard iteration cap for Newton-Raphson; per spec.</summary>
    private const int NrMaxIterations = 10;

    /// <summary>Constant term in the oxygen-cost polynomial. Source: Daniels &amp; Gilbert 1979.</summary>
    private const double OxygenCostConstant = -4.60;

    /// <summary>Linear coefficient for velocity in the oxygen-cost polynomial. Source: Daniels &amp; Gilbert 1979.</summary>
    private const double OxygenCostLinear = 0.182258;

    /// <summary>Quadratic coefficient for velocity in the oxygen-cost polynomial. Source: Daniels &amp; Gilbert 1979.</summary>
    private const double OxygenCostQuadratic = 0.000104;

    /// <summary>Baseline fractional utilization at infinite duration. Source: Daniels &amp; Gilbert 1979.</summary>
    private const double FracUtilBaseline = 0.8;

    /// <summary>Amplitude of the fast-decay exponential term. Source: Daniels &amp; Gilbert 1979.</summary>
    private const double FracUtilAmplitude1 = 0.1894393;

    /// <summary>Decay rate of the fast exponential term (per minute). Source: Daniels &amp; Gilbert 1979.</summary>
    private const double FracUtilDecay1 = 0.012778;

    /// <summary>Amplitude of the slow-decay exponential term. Source: Daniels &amp; Gilbert 1979.</summary>
    private const double FracUtilAmplitude2 = 0.2989558;

    /// <summary>Decay rate of the slow exponential term (per minute). Source: Daniels &amp; Gilbert 1979.</summary>
    private const double FracUtilDecay2 = 0.1932605;

    /// <summary>Newton-Raphson convergence tolerance in minutes; per spec.</summary>
    private const double NrTolerance = 1e-3;

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

    /// <summary>
    /// Newton-Raphson solver: returns predicted race time (minutes) for the given pace-zone
    /// <paramref name="index"/> over <paramref name="distanceMeters"/>.
    /// Solves <c>OxygenCost(distance/t) / FractionalUtilization(t) − index = 0</c>, the
    /// Daniels–Gilbert root condition (pace-zone index = VO₂ ÷ sustainable-fraction).
    /// Initial guess: velocity at 100% VO₂max utilization from <see cref="SolveVelocityForTargetVo2"/>,
    /// equivalent to the GoldenCheetah approach (<c>t₀ = distance / (index × 0.17)</c> is the spec
    /// reference; this implementation uses the analytically identical quadratic root for stability).
    /// Tolerance: 1e-3 min; hard cap: 10 iterations.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the solver fails to converge within 10 iterations.</exception>
    public static double PredictRaceTimeMinutes(double index, double distanceMeters)
    {
        // Initial velocity at 100% VO₂max (assumes max utilization); gives t₀ ≈ 19.2 min at index 50 / 5 km
        var initialVelocity = SolveVelocityForTargetVo2(index);
        var t = distanceMeters / initialVelocity;

        for (var i = 0; i < NrMaxIterations; i++)
        {
            var v = distanceMeters / t;
            var fracUtil = FractionalUtilization(t);
            var oxygenCost = OxygenCost(v);
            var fx = (oxygenCost / fracUtil) - index;

            if (Math.Abs(fx) < NrTolerance)
            {
                return t;
            }

            // f(t) = VO₂(v(t)) / F(t) where v(t) = d/t
            // f'(t) = [F(t)·VO₂'(v)·v'(t) − VO₂·F'(t)] / F(t)²
            // with v'(t) = −d/t² and VO₂'(v) = linear + 2·quadratic·v
            var fracUtilDeriv =
                (-FracUtilAmplitude1 * FracUtilDecay1 * Math.Exp(-FracUtilDecay1 * t))
                + (-FracUtilAmplitude2 * FracUtilDecay2 * Math.Exp(-FracUtilDecay2 * t));
            var oxygenCostDeriv = OxygenCostLinear + (2.0 * OxygenCostQuadratic * v);
            var vDeriv = -distanceMeters / (t * t);
            var fxDeriv = ((fracUtil * oxygenCostDeriv * vDeriv) - (oxygenCost * fracUtilDeriv)) / (fracUtil * fracUtil);

            t -= fx / fxDeriv;
        }

        throw new InvalidOperationException(
            $"PredictRaceTimeMinutes did not converge within 10 iterations for index={index}, distance={distanceMeters}m.");
    }
}
