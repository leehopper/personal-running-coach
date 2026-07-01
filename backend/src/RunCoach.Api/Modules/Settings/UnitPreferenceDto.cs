using System.Text.Json.Serialization;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Settings;

/// <summary>
/// The runner's unit preference — the body of both <c>GET</c> and
/// <c>PUT /api/v1/settings/units</c> (Slice 4C-units / DEC-086). Frontend-display-only:
/// the value never changes stored km/SI data or the km-native plan-gen prompt
/// (DEC-010 / DEC-041). <see cref="PreferredUnits"/> serializes as its numeric
/// value (<c>0</c>=Kilometers, <c>1</c>=Miles), matching the existing generated
/// client contract.
/// </summary>
/// <param name="PreferredUnits">The runner's preferred distance units.</param>
public sealed record UnitPreferenceDto(
    [property: JsonRequired] PreferredUnits PreferredUnits);
