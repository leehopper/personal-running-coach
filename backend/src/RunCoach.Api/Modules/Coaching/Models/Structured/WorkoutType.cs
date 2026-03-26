using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Types of workouts that can appear in a training plan.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutType>))]
public enum WorkoutType
{
    /// <summary>Low-intensity aerobic run at conversational pace.</summary>
    Easy,

    /// <summary>Extended distance run for endurance development.</summary>
    LongRun,

    /// <summary>Sustained effort at lactate threshold pace.</summary>
    Tempo,

    /// <summary>Repeated efforts at VO2max intensity with recovery.</summary>
    Interval,

    /// <summary>Short, fast efforts at repetition pace for economy.</summary>
    Repetition,

    /// <summary>Very easy run for active recovery between hard sessions.</summary>
    Recovery,

    /// <summary>Non-running activity such as cycling, swimming, or strength.</summary>
    CrossTrain,
}
