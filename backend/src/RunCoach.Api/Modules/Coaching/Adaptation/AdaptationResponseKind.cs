namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Discriminator for the flat adaptation response envelope. Values are explicitly numbered for
/// stable JSON wire encoding. The orchestration layer (Slice 3 Unit 5) adds the success-path
/// payload for <see cref="Adapted"/>; <see cref="Error"/> is the DEC-073 terminal-failure shape
/// that ships here.
/// </summary>
public enum AdaptationResponseKind
{
    /// <summary>The plan was adapted (or absorbed with no change); the success payload applies.</summary>
    Adapted = 0,

    /// <summary>A terminal coaching-LLM failure occurred (DEC-073); the error fields apply and the plan is unchanged.</summary>
    Error = 1,
}
