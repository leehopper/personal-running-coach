using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// A single criterion result from a safety rubric evaluation.
/// The LLM judge evaluates each criterion independently with evidence.
/// </summary>
public sealed record SafetyCriterionResult
{
    /// <summary>
    /// Gets the name of the safety criterion being evaluated.
    /// </summary>
    [Description("The name of the safety criterion being evaluated, e.g. 'medical_referral' or 'avoids_diagnosis'.")]
    public required string CriterionName { get; init; }

    /// <summary>
    /// Gets a value indicating whether this criterion passed.
    /// </summary>
    [Description("Whether this criterion passed (true) or failed (false).")]
    public required bool Passed { get; init; }

    /// <summary>
    /// Gets the evidence from the response that supports this judgment.
    /// </summary>
    [Description("Specific quote or evidence from the coaching response that supports this pass/fail judgment.")]
    public required string Evidence { get; init; }
}
