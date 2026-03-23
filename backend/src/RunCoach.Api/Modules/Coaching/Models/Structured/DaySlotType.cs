using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// The type of activity assigned to a day slot within a weekly template.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DaySlotType>))]
public enum DaySlotType
{
    /// <summary>A running session day.</summary>
    Run,

    /// <summary>A complete rest day with no structured activity.</summary>
    Rest,

    /// <summary>A non-running activity day (cycling, swimming, strength, etc.).</summary>
    CrossTrain,
}
