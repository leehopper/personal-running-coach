namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Observations from cross-validation between baseline and secondary profiles.
/// </summary>
public sealed record CrossValidationObservations
{
    /// <summary>
    /// Gets the baseline profile name.
    /// </summary>
    public required string BaselineProfile { get; init; }

    /// <summary>
    /// Gets the cross-validation profile name.
    /// </summary>
    public required string CrossValidationProfile { get; init; }

    /// <summary>
    /// Gets average token count for the baseline profile.
    /// </summary>
    public required double BaselineAverageTokens { get; init; }

    /// <summary>
    /// Gets average token count for the cross-validation profile.
    /// </summary>
    public required double CrossValidationAverageTokens { get; init; }

    /// <summary>
    /// Gets the number of runs for the baseline profile.
    /// </summary>
    public required int BaselineRunCount { get; init; }

    /// <summary>
    /// Gets the number of runs for the cross-validation profile.
    /// </summary>
    public required int CrossValidationRunCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether both profiles have results.
    /// </summary>
    public required bool BothProfilesHaveResults { get; init; }
}
