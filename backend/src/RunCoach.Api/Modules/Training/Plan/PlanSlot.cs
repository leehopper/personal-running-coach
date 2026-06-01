namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// A resolved position within a training plan: a 1-based week index and a 0–6
/// day-of-week where 0 = Sunday, matching
/// <see cref="Coaching.Models.Structured.WorkoutOutput.DayOfWeek"/>. Produced by
/// <see cref="PlanCalendar.ResolveSlot"/> and consumed by Slice 2b's
/// server-authoritative prescription snapshot (Unit 3 / DEC-076).
/// </summary>
/// <param name="WeekNumber">1-based week index within the plan.</param>
/// <param name="DayOfWeek">Day of week, 0 = Sunday through 6 = Saturday.</param>
public readonly record struct PlanSlot(int WeekNumber, int DayOfWeek);
