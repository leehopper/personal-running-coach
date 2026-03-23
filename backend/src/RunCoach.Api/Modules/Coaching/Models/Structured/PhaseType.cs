using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Training periodization phase types used in macro plan output.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PhaseType>))]
public enum PhaseType
{
    /// <summary>Aerobic base building phase with low intensity.</summary>
    Base,

    /// <summary>Progressive overload phase with increasing intensity.</summary>
    Build,

    /// <summary>Race-specific sharpening phase at peak fitness.</summary>
    Peak,

    /// <summary>Pre-race volume reduction phase to optimize freshness.</summary>
    Taper,

    /// <summary>Post-race or post-cycle recovery phase.</summary>
    Recovery,
}
