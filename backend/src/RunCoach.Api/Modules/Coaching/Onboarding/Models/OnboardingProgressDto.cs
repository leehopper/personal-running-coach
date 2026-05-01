namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Progress indicator surface for the chat UI's <c>TopicProgressIndicator</c>.
/// Property names mirror the flat counters on <see cref="OnboardingStateDto"/>
/// so the discriminated-union Zod schema on the frontend can share the same
/// shape — every progress payload on the wire reads
/// <c>{ completedTopics, totalTopics }</c>.
/// </summary>
/// <remarks>
/// Construction validates <c>0 &lt;= CompletedTopics &lt;= TotalTopics</c> and
/// <c>TotalTopics &gt;= 1</c> so the frontend's <c>completed / total</c> ratio
/// cannot collapse to NaN or a negative value, even when the wire payload is
/// reconstructed via JSON deserialization.
/// </remarks>
/// <param name="CompletedTopics">Count of topics that have a captured answer.</param>
/// <param name="TotalTopics">Total number of topics in the onboarding flow (six per DEC-047).</param>
public sealed record OnboardingProgressDto(
    int CompletedTopics,
    int TotalTopics)
{
    /// <summary>Gets count of topics that have a captured answer.</summary>
    public int CompletedTopics { get; init; } = ValidateCompleted(CompletedTopics, TotalTopics);

    /// <summary>Gets total number of topics in the onboarding flow.</summary>
    public int TotalTopics { get; init; } = ValidateTotal(TotalTopics);

    private static int ValidateTotal(int totalTopics)
    {
        if (totalTopics < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalTopics),
                totalTopics,
                "TotalTopics must be at least 1.");
        }

        return totalTopics;
    }

    private static int ValidateCompleted(int completedTopics, int totalTopics)
    {
        if (completedTopics < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedTopics),
                completedTopics,
                "CompletedTopics cannot be negative.");
        }

        if (totalTopics >= 1 && completedTopics > totalTopics)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedTopics),
                completedTopics,
                $"CompletedTopics ({completedTopics}) cannot exceed TotalTopics ({totalTopics}).");
        }

        return completedTopics;
    }
}
