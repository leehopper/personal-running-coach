namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A weekly summary of training volume, used for Layer 2 summarization
/// in context injection.
/// </summary>
public sealed record WeekSummary(
    DateOnly WeekStartDate,
    decimal TotalDistanceKm,
    int NumberOfRuns,
    decimal? LongRunKm,
    string? Notes);
