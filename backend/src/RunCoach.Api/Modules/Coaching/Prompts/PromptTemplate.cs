namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// A versioned prompt template loaded from YAML.
/// Contains a static system prompt (cacheable coaching persona + safety rules)
/// and a context template with named tokens for dynamic user context injection.
/// </summary>
public sealed record PromptTemplate(
    string Id,
    string Version,
    string StaticSystemPrompt,
    string ContextTemplate,
    PromptMetadata? Metadata);
