namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Event recorded when a runner posts an interactive chat message to their coach
/// (Slice 4B Unit 3, DEC-085). Appended to the user-scoped <c>Conversation</c>
/// stream (stream id = <see cref="UserId"/>) <b>durably first</b>, before the
/// streaming reply opens, so a crash mid-stream still leaves a recoverable record
/// of what the runner asked. The interactive stream survives plan regeneration —
/// it is keyed by the user, not the plan (unlike the proactive
/// <see cref="ConversationLogView"/>).
/// </summary>
/// <param name="UserId">
/// The runner's user id — also the <c>Conversation</c> stream id. Carried on the
/// event (mirroring <c>OnboardingStarted</c> / <c>PlanGenerated</c>) so the
/// projection's <c>Create</c> can seed <see cref="ConversationView.Id"/> without
/// reading Marten stream metadata.
/// </param>
/// <param name="TurnId">
/// The client-generated message id (a GUID) that uniquely identifies this user
/// turn. Doubles as the idempotency key for the user-turn write so a retried POST
/// never double-appends.
/// </param>
/// <param name="Content">The runner's message text (sanitized at the prompt boundary, not here).</param>
public sealed record UserMessagePosted(
    Guid UserId,
    Guid TurnId,
    string Content);
