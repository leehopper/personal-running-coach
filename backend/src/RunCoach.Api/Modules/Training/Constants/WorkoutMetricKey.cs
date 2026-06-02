namespace RunCoach.Api.Modules.Training.Constants;

/// <summary>
/// Canonical set of optional workout-metric keys (DEC-072). This enum is the
/// single source of truth surfaced to the frontend via the OpenAPI codegen
/// pipeline; the matching wire strings (the keys used inside the open
/// <c>WorkoutLog.Metrics</c> <c>jsonb</c> bag) are derived from each member's
/// name via <see cref="WorkoutMetricKeys.ToWireKey"/>. Values are explicitly
/// numbered so reordering members in source does not shift the wire encoding.
/// Names are chosen to match HealthKit / Strava / Garmin source fields so that
/// future auto-fill ingestion needs no migration.
/// </summary>
public enum WorkoutMetricKey
{
    /// <summary>Rating of perceived exertion, 1–10 scale.</summary>
    Rpe = 0,

    /// <summary>Average heart rate over the run, bpm.</summary>
    HrAvg = 1,

    /// <summary>Maximum heart rate over the run, bpm.</summary>
    HrMax = 2,

    /// <summary>Energy expenditure, kcal.</summary>
    Calories = 3,

    /// <summary>Heart-rate variability (SDNN), ms.</summary>
    Hrv = 4,

    /// <summary>Wearable sleep score, 0–100.</summary>
    SleepScore = 5,

    /// <summary>Wearable recovery / readiness score, 0–100.</summary>
    RecoveryScore = 6,

    /// <summary>Average cadence, full steps per minute.</summary>
    Cadence = 7,

    /// <summary>Total elevation gain, m.</summary>
    ElevationGain = 8,

    /// <summary>Average running power, W.</summary>
    Power = 9,

    /// <summary>Free-text weather descriptor.</summary>
    Weather = 10,

    /// <summary>Free-text terrain descriptor.</summary>
    Terrain = 11,

    /// <summary>Per-lap splits (array). Stored as the typed <c>Splits</c> column on the entity.</summary>
    Splits = 12,

    /// <summary>Reserved (DEC-072): vertical oscillation, cm. Unpopulated at MVP-0.</summary>
    VerticalOscillation = 13,

    /// <summary>Reserved (DEC-072): ground contact time, ms. Unpopulated at MVP-0.</summary>
    GroundContactTime = 14,

    /// <summary>Reserved (DEC-072): stride length, m. Unpopulated at MVP-0.</summary>
    StrideLength = 15,
}
