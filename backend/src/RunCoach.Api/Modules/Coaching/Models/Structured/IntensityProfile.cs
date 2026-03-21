using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Intensity profiles for workout segments, aligned with training zone terminology.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntensityProfile>))]
public enum IntensityProfile
{
    /// <summary>Zone 1-2: conversational pace, aerobic development.</summary>
    Easy,

    /// <summary>Zone 3: moderate aerobic effort, marathon-pace range.</summary>
    Moderate,

    /// <summary>Zone 4: lactate threshold effort, tempo-pace range.</summary>
    Threshold,

    /// <summary>Zone 5: VO2max effort, interval-pace range.</summary>
    VO2Max,

    /// <summary>Zone 5+: supramaximal effort for neuromuscular economy.</summary>
    Repetition,
}
