namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// Strongly-typed settings for the prompt store.
/// Mapped from the "Prompts" configuration section.
/// </summary>
public sealed record PromptStoreSettings
{
    /// <summary>
    /// The configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "Prompts";

    /// <summary>
    /// Gets the base directory path for YAML prompt files.
    /// Defaults to "Prompts" (relative to the application content root).
    /// </summary>
    public string BasePath { get; init; } = "Prompts";

    /// <summary>
    /// Gets the mapping of prompt IDs to their active version strings.
    /// Example: { "coaching-system": "v1" }.
    /// </summary>
    public IReadOnlyDictionary<string, string> ActiveVersions { get; init; } = new Dictionary<string, string>();
}
