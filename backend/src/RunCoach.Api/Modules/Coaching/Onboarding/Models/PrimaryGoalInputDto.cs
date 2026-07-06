using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Wire-input shape for the PrimaryGoal topic on POST /api/v1/onboarding/answers.
/// A loosened, non-throwing counterpart to <see cref="PrimaryGoalAnswer"/>: the form
/// submits primitives, the controller validates them deterministically and constructs the
/// canonical answer record. Kept separate from <see cref="PrimaryGoalAnswer"/> because that
/// record self-validates in its <c>init</c> accessors — binding it directly would surface a
/// hostile value as an uncatchable HTTP 500 instead of a clean 400.
/// </summary>
/// <param name="Goal">Categorical primary goal (validated against the closed enum server-side).</param>
/// <param name="Description">Optional runner-supplied free-text nuance for this topic.</param>
public sealed record PrimaryGoalInputDto(
    [property: JsonRequired] PrimaryGoal Goal,
    string? Description);
