using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Types of segments within a structured workout.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SegmentType>))]
public enum SegmentType
{
    /// <summary>Pre-workout warmup segment at easy effort.</summary>
    Warmup,

    /// <summary>Main work segment at the prescribed intensity.</summary>
    Work,

    /// <summary>Recovery segment between work intervals.</summary>
    Recovery,

    /// <summary>Post-workout cooldown segment at easy effort.</summary>
    Cooldown,
}
