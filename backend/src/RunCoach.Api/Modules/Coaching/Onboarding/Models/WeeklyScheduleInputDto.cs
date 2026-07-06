using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Wire-input shape for the WeeklySchedule topic on POST /api/v1/onboarding/answers. A loosened,
/// non-throwing counterpart to <see cref="WeeklyScheduleAnswer"/> (see
/// <see cref="PrimaryGoalInputDto"/> for the rationale). Day availability is carried as seven
/// named booleans, matching the answer record — the whole group is co-submitted, which is what
/// structurally dissolves the turn-based slot-merge loop (DEC-086).
/// </summary>
/// <param name="MaxRunDaysPerWeek">Maximum run days per week (validated 1-7 server-side).</param>
/// <param name="TypicalSessionMinutes">Typical session duration in minutes (validated &gt; 0).</param>
/// <param name="Monday">Whether Monday is an available run day.</param>
/// <param name="Tuesday">Whether Tuesday is an available run day.</param>
/// <param name="Wednesday">Whether Wednesday is an available run day.</param>
/// <param name="Thursday">Whether Thursday is an available run day.</param>
/// <param name="Friday">Whether Friday is an available run day.</param>
/// <param name="Saturday">Whether Saturday is an available run day.</param>
/// <param name="Sunday">Whether Sunday is an available run day.</param>
/// <param name="Description">Optional runner-supplied free-text nuance for this topic.</param>
public sealed record WeeklyScheduleInputDto(
    [property: JsonRequired] int MaxRunDaysPerWeek,
    [property: JsonRequired] int TypicalSessionMinutes,
    [property: JsonRequired] bool Monday,
    [property: JsonRequired] bool Tuesday,
    [property: JsonRequired] bool Wednesday,
    [property: JsonRequired] bool Thursday,
    [property: JsonRequired] bool Friday,
    [property: JsonRequired] bool Saturday,
    [property: JsonRequired] bool Sunday,
    string? Description);
