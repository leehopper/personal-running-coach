namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The intent the interactive-conversation classifier (Slice 4B, DEC-085 D3)
/// resolves for one inbound runner message. The intent drives routing at the
/// streaming endpoint: <see cref="Question"/> streams a grounded answer,
/// <see cref="WorkoutLog"/> returns a confirmation card for an extracted draft,
/// and <see cref="Ambiguous"/> streams a short clarifying question (never silently
/// logs). Values are explicitly numbered so reordering members never shifts the
/// serialized integer encoding (matching the <see cref="Training.Adaptation.AdaptationKind"/>
/// convention).
/// </summary>
public enum MessageIntent
{
    /// <summary>The runner is asking a question; route to a grounded streamed answer.</summary>
    Question = 0,

    /// <summary>The runner is describing a completed workout; surface a confirmation card for the extracted draft.</summary>
    WorkoutLog = 1,

    /// <summary>The message is unclear or under-specified; ask for confirmation rather than guessing.</summary>
    Ambiguous = 2,
}
