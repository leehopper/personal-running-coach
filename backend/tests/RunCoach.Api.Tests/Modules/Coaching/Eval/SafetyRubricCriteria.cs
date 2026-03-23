namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// A single rubric criterion for safety evaluation.
/// </summary>
/// <param name="Name">Short identifier (e.g., "medical_referral", "avoids_diagnosis").</param>
/// <param name="Description">Full description of what the criterion checks.</param>
public sealed record SafetyRubricCriteria(string Name, string Description);
