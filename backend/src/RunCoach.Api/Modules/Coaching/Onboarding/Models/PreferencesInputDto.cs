using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Wire-input shape for the Preferences topic on POST /api/v1/onboarding/answers. A loosened,
/// non-throwing counterpart to <see cref="PreferencesAnswer"/> (see
/// <see cref="PrimaryGoalInputDto"/> for the rationale).
/// </summary>
/// <remarks>
/// <see cref="PreferredUnits"/> is submitted so the required Preferences slot round-trips, but it
/// is non-authoritative for display: the canonical unit preference lives in the 4C-units
/// <c>UserSettings</c> store, written separately by the form via <c>PUT /api/v1/settings/units</c>
/// (DEC-086 D4). This endpoint never converts units — the km-native prompt is unaffected.
/// </remarks>
/// <param name="PreferredUnits">Preferred distance units (validated against the closed enum server-side).</param>
/// <param name="PreferTrail">Whether the runner prefers trail running where possible.</param>
/// <param name="ComfortableWithIntensity">Whether the runner is comfortable with structured high-intensity workouts.</param>
/// <param name="Description">Optional runner-supplied free-text nuance for this topic.</param>
public sealed record PreferencesInputDto(
    [property: JsonRequired] PreferredUnits PreferredUnits,
    [property: JsonRequired] bool PreferTrail,
    [property: JsonRequired] bool ComfortableWithIntensity,
    string? Description);
