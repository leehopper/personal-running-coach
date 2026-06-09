namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Discriminator for the flat adaptation response envelope. Values are explicitly numbered for
/// stable JSON wire encoding. <see cref="Error"/> is the DEC-073 terminal-failure shape; the
/// success-path payload accompanying <see cref="Adapted"/> is owned by the orchestration layer.
/// </summary>
public enum AdaptationResponseKind
{
    /// <summary>The plan was adapted (or absorbed with no change); the success payload applies.</summary>
    Adapted = 0,

    /// <summary>A terminal coaching-LLM failure occurred (DEC-073); the error fields apply and the plan is unchanged.</summary>
    Error = 1,
}
