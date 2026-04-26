using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the WeeklySchedule topic. Captures the maximum weekly run-day count,
/// typical session duration, and the runner's preferred-day flags as named day slots.
/// </summary>
/// <remarks>
/// Day slots are named boolean properties (Monday..Sunday) instead of an array so Anthropic
/// constrained decoding structurally guarantees exactly seven slots without using
/// <c>minItems</c>/<c>maxItems</c> (which Anthropic rejects with HTTP 400).
/// </remarks>
public sealed record WeeklyScheduleAnswer
{
    /// <summary>
    /// Gets the maximum number of run days per week the runner can commit to (1-7).
    /// </summary>
    [Description("Maximum number of run days per week the runner can commit to. Valid range 1 through 7.")]
    public required int MaxRunDaysPerWeek { get; init; }

    /// <summary>
    /// Gets the typical session duration in minutes the runner has available per training day.
    /// </summary>
    [Description("Typical session duration in minutes the runner has available per training day.")]
    public required int TypicalSessionMinutes { get; init; }

    /// <summary>Gets a value indicating whether Monday is an available run day.</summary>
    [Description("Whether Monday is an available run day.")]
    public required bool Monday { get; init; }

    /// <summary>Gets a value indicating whether Tuesday is an available run day.</summary>
    [Description("Whether Tuesday is an available run day.")]
    public required bool Tuesday { get; init; }

    /// <summary>Gets a value indicating whether Wednesday is an available run day.</summary>
    [Description("Whether Wednesday is an available run day.")]
    public required bool Wednesday { get; init; }

    /// <summary>Gets a value indicating whether Thursday is an available run day.</summary>
    [Description("Whether Thursday is an available run day.")]
    public required bool Thursday { get; init; }

    /// <summary>Gets a value indicating whether Friday is an available run day.</summary>
    [Description("Whether Friday is an available run day.")]
    public required bool Friday { get; init; }

    /// <summary>Gets a value indicating whether Saturday is an available run day.</summary>
    [Description("Whether Saturday is an available run day.")]
    public required bool Saturday { get; init; }

    /// <summary>Gets a value indicating whether Sunday is an available run day.</summary>
    [Description("Whether Sunday is an available run day.")]
    public required bool Sunday { get; init; }

    /// <summary>
    /// Gets the runner-supplied free-text description of any constraints not captured by the day flags.
    /// </summary>
    [Description("Runner-supplied free-text description of constraints not captured by the day flags (e.g. 'no early mornings').")]
    public required string Description { get; init; }
}
