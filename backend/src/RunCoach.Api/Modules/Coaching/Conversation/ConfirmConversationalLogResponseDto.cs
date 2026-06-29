using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Response for <c>POST /api/v1/conversation/logs/confirm</c> (Slice 4B PR5). Mirrors the
/// form-logged create response: the committed (or, on a replayed confirm, the original) log id
/// plus the adaptation envelope (DEC-073). The acknowledgment turn and any proactive
/// adaptation/safety turns surface via the timeline + plan read models, which the frontend
/// refetches by invalidating its query tags after a successful confirm — no response coupling.
/// </summary>
/// <param name="WorkoutLogId">The committed (or replayed) workout log id.</param>
/// <param name="Adaptation">The adaptation envelope: <c>Kind=Adapted</c> with the resolved kind, or <c>Kind=Error</c> (the save still succeeded).</param>
public sealed record ConfirmConversationalLogResponseDto(
    Guid WorkoutLogId,
    AdaptationResponseDto Adaptation);
