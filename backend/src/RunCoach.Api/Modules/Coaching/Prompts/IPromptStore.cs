namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// Loads and caches versioned prompt templates.
/// Implementations read from a backing store (e.g., YAML files)
/// and cache loaded templates for the lifetime of the application.
/// </summary>
public interface IPromptStore
{
    /// <summary>
    /// Gets a prompt template by ID and version.
    /// </summary>
    /// <param name="id">The prompt template identifier (e.g., "coaching-system").</param>
    /// <param name="version">The version string (e.g., "v1").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The loaded prompt template.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no template exists for the given ID and version combination.
    /// </exception>
    Task<PromptTemplate> GetPromptAsync(string id, string version, CancellationToken ct = default);

    /// <summary>
    /// Gets the active (configured) version string for a prompt ID.
    /// </summary>
    /// <param name="id">The prompt template identifier (e.g., "coaching-system").</param>
    /// <returns>The active version string (e.g., "v1").</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no active version is configured for the given ID.
    /// </exception>
    string GetActiveVersion(string id);
}
