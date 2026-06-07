using System.Collections.Frozen;
using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Training.Constants;

/// <summary>
/// Single-sourced canonical metric-key strings for the open
/// <c>WorkoutLog.Metrics</c> <c>jsonb</c> bag (DEC-072). The <see cref="All"/>
/// set and the ergonomic <c>const</c> strings below are kept drift-free against
/// <see cref="WorkoutMetricKey"/> by <c>WorkoutMetricKeysTests</c>: every key is
/// the lower-camel form of its enum member name (<see cref="ToWireKey"/>), so
/// the UI metric-meta map and the LLM context formatter read
/// the same labels and cannot diverge.
/// </summary>
public static class WorkoutMetricKeys
{
    /// <summary>Rating of perceived exertion, 1–10.</summary>
    public const string Rpe = "rpe";

    /// <summary>Average heart rate, bpm.</summary>
    public const string HrAvg = "hrAvg";

    /// <summary>Maximum heart rate, bpm.</summary>
    public const string HrMax = "hrMax";

    /// <summary>Energy expenditure, kcal.</summary>
    public const string Calories = "calories";

    /// <summary>Heart-rate variability (SDNN), ms.</summary>
    public const string Hrv = "hrv";

    /// <summary>Sleep score, 0–100.</summary>
    public const string SleepScore = "sleepScore";

    /// <summary>Recovery / readiness score, 0–100.</summary>
    public const string RecoveryScore = "recoveryScore";

    /// <summary>Average cadence, full steps per minute.</summary>
    public const string Cadence = "cadence";

    /// <summary>Total elevation gain, m.</summary>
    public const string ElevationGain = "elevationGain";

    /// <summary>Average running power, W.</summary>
    public const string Power = "power";

    /// <summary>Free-text weather descriptor.</summary>
    public const string Weather = "weather";

    /// <summary>Free-text terrain descriptor.</summary>
    public const string Terrain = "terrain";

    /// <summary>Per-lap splits (array).</summary>
    public const string Splits = "splits";

    /// <summary>Reserved: vertical oscillation, cm.</summary>
    public const string VerticalOscillation = "verticalOscillation";

    /// <summary>Reserved: ground contact time, ms.</summary>
    public const string GroundContactTime = "groundContactTime";

    /// <summary>Reserved: stride length, m.</summary>
    public const string StrideLength = "strideLength";

    /// <summary>
    /// Every canonical wire key, documented and reserved, as an immutable
    /// <see cref="FrozenSet{T}"/> — tamper-proof at runtime (an
    /// <see cref="IReadOnlySet{T}"/> over a plain <see cref="HashSet{T}"/> can be
    /// downcast and mutated) and read-optimized for the membership checks that
    /// validate the metrics bag. Kept symmetric with <see cref="WorkoutMetricKey"/>
    /// by <c>WorkoutMetricKeysTests</c>.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new[]
    {
        Rpe, HrAvg, HrMax, Calories, Hrv, SleepScore, RecoveryScore,
        Cadence, ElevationGain, Power, Weather, Terrain, Splits,
        VerticalOscillation, GroundContactTime, StrideLength,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Per-metric display metadata (label + compact-display unit + coaching
    /// category) for the scalar metrics carried in the open bag. The single
    /// source the LLM-context formatter (<c>RecentLogFormatter</c>) and the
    /// frontend metric-meta map read, so prompt and UI labels cannot drift
    /// (DEC-072 / DEC-076). Keyed by wire key. Excludes <see cref="Splits"/>,
    /// which is persisted in its own typed column rather than as a scalar bag
    /// value. Kept drift-free against <see cref="All"/> by
    /// <c>WorkoutMetricMetadataTests</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, WorkoutMetricMetadata> Metadata =
        new Dictionary<string, WorkoutMetricMetadata>(StringComparer.Ordinal)
        {
            [HrAvg] = new("HR", string.Empty, MetricCategory.Effort),
            [HrMax] = new("HR max", string.Empty, MetricCategory.Effort),
            [Rpe] = new("RPE", string.Empty, MetricCategory.Effort),
            [Cadence] = new("cadence", "spm", MetricCategory.Peripheral),
            [Power] = new("power", "W", MetricCategory.Peripheral),
            [ElevationGain] = new("elev gain", "m", MetricCategory.Peripheral),
            [VerticalOscillation] = new("vert osc", "cm", MetricCategory.Peripheral),
            [GroundContactTime] = new("GCT", "ms", MetricCategory.Peripheral),
            [StrideLength] = new("stride", "m", MetricCategory.Peripheral),
            [Calories] = new("calories", "kcal", MetricCategory.Contextual),
            [Hrv] = new("HRV", "ms", MetricCategory.Contextual),
            [SleepScore] = new("sleep", string.Empty, MetricCategory.Contextual),
            [RecoveryScore] = new("recovery", string.Empty, MetricCategory.Contextual),
            [Weather] = new("weather", string.Empty, MetricCategory.Contextual),
            [Terrain] = new("terrain", string.Empty, MetricCategory.Contextual),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The fixed order in which present scalar metrics render inside a compact
    /// coaching one-liner: effort signals (HR, RPE) first, then peripheral,
    /// then contextual. Deterministic so assembled prompts and tests are
    /// stable across runs. Contains exactly the keys of <see cref="Metadata"/>.
    /// </summary>
    public static readonly ImmutableArray<string> DisplayOrder =
    [
        HrAvg, HrMax, Rpe,
        Cadence, Power, ElevationGain, VerticalOscillation, GroundContactTime, StrideLength,
        Calories, Hrv, SleepScore, RecoveryScore, Weather, Terrain,
    ];

    /// <summary>
    /// Derives the wire key for a <see cref="WorkoutMetricKey"/> by lower-casing
    /// the first character of its member name (e.g. <c>HrAvg</c> → <c>hrAvg</c>).
    /// </summary>
    /// <param name="key">The canonical metric key.</param>
    /// <returns>The lower-camel wire string used inside the metrics bag.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="key"/> is not a defined <see cref="WorkoutMetricKey"/> member
    /// (e.g. an arbitrary integer cast to the enum) — guards against leaking a
    /// non-canonical key such as <c>"999"</c> into the metrics pipeline.
    /// </exception>
    public static string ToWireKey(WorkoutMetricKey key)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown workout metric key.");
        }

        var name = key.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
