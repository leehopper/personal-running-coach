namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// Where user profile data is placed in the assembled prompt.
/// </summary>
public enum ProfilePlacement
{
    /// <summary>
    /// Profile in the START section (stable prefix, high attention). Default.
    /// </summary>
    Start,

    /// <summary>
    /// Profile in the MIDDLE section (variable content, low attention zone).
    /// </summary>
    Middle,

    /// <summary>
    /// Profile in the END section (conversational, recency attention).
    /// </summary>
    End,
}
