using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Request body for <c>POST /api/v1/plan/regenerate</c> per Slice 1 § Unit 5
/// R05.1. The wire contract carries the client-generated idempotency key plus
/// an optional regeneration intent block whose free-text is capped at
/// <see cref="Coaching.Models.RegenerationIntent.RawMaxFreeTextLength"/>
/// characters BEFORE sanitization.
/// </summary>
/// <param name="IdempotencyKey">
/// Client-generated idempotency key (typically <c>crypto.randomUUID()</c>).
/// The handler short-circuits duplicate submissions with the same key.
/// Marked <see cref="JsonRequiredAttribute"/> so System.Text.Json refuses to
/// under-post a default <see cref="Guid"/> when the field is omitted.
/// </param>
/// <param name="Intent">
/// Optional regeneration intent supplied by the runner via Settings -> Plan.
/// Null when the runner did not provide a note.
/// </param>
public sealed record RegeneratePlanRequestDto(
    [property: JsonRequired] Guid IdempotencyKey,
    RegenerationIntentRequestDto? Intent);
