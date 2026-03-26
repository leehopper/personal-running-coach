namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A single day's slot within a weekly training template,
/// specifying the type of session and optional emphasis.
/// </summary>
public sealed record DaySlot(
    DayOfWeek DayOfWeek,
    string SlotType,
    string? Emphasis);
