using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Wire-input shape for the TargetEvent topic on POST /api/v1/onboarding/answers. A loosened,
/// non-throwing counterpart to <see cref="TargetEventAnswer"/> (see
/// <see cref="PrimaryGoalInputDto"/> for why the wire shape is separated from the self-validating
/// answer record). Only submitted when the primary goal is race training.
/// </summary>
/// <param name="EventName">Name of the goal race or event.</param>
/// <param name="DistanceKm">Target distance in kilometers (validated &gt; 0 server-side).</param>
/// <param name="EventDateIso">Target event date in ISO-8601 calendar form (<c>yyyy-MM-dd</c>), validated server-side.</param>
/// <param name="TargetFinishTimeIso">Optional target finishing time as an ISO-8601 duration (e.g. <c>PT1H45M30S</c>).</param>
public sealed record TargetEventInputDto(
    [property: JsonRequired] string EventName,
    [property: JsonRequired] double DistanceKm,
    [property: JsonRequired] string EventDateIso,
    string? TargetFinishTimeIso);
