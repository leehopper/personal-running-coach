using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The author/kind of a turn in the read-only "Explain-the-change" panel
/// (Slice 3 Unit 2, DEC-079). Distinguishes an adaptation explanation from a
/// deterministic safety message so the panel can render safety turns at full
/// prominence regardless of any plan-change level. Serialized by name.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConversationRole>))]
public enum ConversationRole
{
    /// <summary>An adaptation explanation projected from a <c>PlanAdaptedFromLog</c> event.</summary>
    AssistantAdaptation = 0,

    /// <summary>A deterministic safety message projected from a <c>SafetySignalRaised</c> event.</summary>
    SystemSafety = 1,
}
