using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RunCoach.Api.Modules.Coaching.Prompts;

/// <summary>
/// Loads prompt templates from YAML files on disk and caches them
/// in a <see cref="ConcurrentDictionary{TKey, TValue}"/> for the
/// lifetime of the application.
///
/// YAML files follow the naming convention <c>{id}.v{N}.yaml</c>
/// (e.g., <c>coaching-system.v1.yaml</c>) and are stored under the
/// configured base path (defaults to <c>Prompts/</c>).
///
/// On first access, the file is loaded from disk and parsed.
/// Subsequent accesses return the cached template. At startup,
/// the store validates that all configured active versions have
/// corresponding YAML files on disk.
/// </summary>
public sealed partial class YamlPromptStore : IPromptStore
{
    private readonly ConcurrentDictionary<string, Lazy<Task<PromptTemplate>>> _cache = new();
    private readonly PromptStoreSettings _settings;
    private readonly string _basePath;
    private readonly ILogger<YamlPromptStore> _logger;

    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlPromptStore"/> class
    /// using <see cref="IWebHostEnvironment"/> for resolving the content root.
    /// Used in ASP.NET Core hosting contexts.
    /// </summary>
    /// <param name="settings">Prompt store configuration.</param>
    /// <param name="environment">Web host environment for resolving content root.</param>
    /// <param name="logger">Logger instance.</param>
    public YamlPromptStore(
        IOptions<PromptStoreSettings> settings,
        IWebHostEnvironment environment,
        ILogger<YamlPromptStore> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings.Value;
        _logger = logger;
        _basePath = Path.Combine(environment.ContentRootPath, _settings.BasePath);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlPromptStore"/> class
    /// with an explicit base path. Used for testing.
    /// </summary>
    internal YamlPromptStore(
        PromptStoreSettings settings,
        string basePath,
        ILogger<YamlPromptStore> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings;
        _basePath = basePath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PromptTemplate> GetPromptAsync(string id, string version, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var cacheKey = BuildCacheKey(id, version);
        var isNewEntry = false;
        var lazy = _cache.GetOrAdd(cacheKey, _ =>
        {
            isNewEntry = true;
            return new Lazy<Task<PromptTemplate>>(
                () => LoadAsync(id, version, CancellationToken.None));
        });

        if (!isNewEntry)
        {
            LogCacheHit(_logger, id, version);
        }

        try
        {
            return await lazy.Value.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (Exception) when (lazy.Value.IsFaulted || lazy.Value.IsCanceled)
        {
            _cache.TryRemove(cacheKey, out _);
            throw;
        }
    }

    /// <inheritdoc />
    public string GetActiveVersion(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (_settings.ActiveVersions.TryGetValue(id, out var version))
        {
            return version;
        }

        throw new KeyNotFoundException(
            $"No active version configured for prompt '{id}'. " +
            $"Add it to Prompts:ActiveVersions in appsettings.json.");
    }

    /// <summary>
    /// Validates that all configured active versions have corresponding
    /// YAML files on disk. Call during application startup to fail fast.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// Thrown when a configured active version has no corresponding YAML file.
    /// </exception>
    public void ValidateConfiguredVersions()
    {
        foreach (var (id, version) in _settings.ActiveVersions)
        {
            var filePath = BuildFilePath(id, version);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"Prompt file not found for configured active version: " +
                    $"'{id}' version '{version}'. Expected file: '{filePath}'.",
                    filePath);
            }

            LogValidatedVersion(_logger, id, version, filePath);
        }
    }

    private static string BuildCacheKey(string id, string version)
    {
        if (id.Contains("::", StringComparison.Ordinal))
        {
            throw new ArgumentException("Prompt id must not contain '::'.", nameof(id));
        }

        if (version.Contains("::", StringComparison.Ordinal))
        {
            throw new ArgumentException("Version must not contain '::'.", nameof(version));
        }

        return $"{id}::{version}";
    }

    private static PromptMetadata? MapMetadata(YamlPromptMetadata? yamlMetadata)
    {
        return yamlMetadata is null
            ? null
            : new PromptMetadata(yamlMetadata.Description, yamlMetadata.Author, yamlMetadata.CreatedAt);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Prompt cache hit for '{Id}' version '{Version}'")]
    private static partial void LogCacheHit(ILogger logger, string id, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading prompt file for '{Id}' version '{Version}' from '{FilePath}'")]
    private static partial void LogLoadingFile(ILogger logger, string id, string version, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded and cached prompt template '{Id}' version '{Version}'")]
    private static partial void LogLoadedTemplate(ILogger logger, string id, string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "Validated prompt '{Id}' version '{Version}' exists at '{FilePath}'")]
    private static partial void LogValidatedVersion(ILogger logger, string id, string version, string filePath);

    private string BuildFilePath(string id, string version)
    {
        var combined = Path.Combine(_basePath, $"{id}.{version}.yaml");
        var fullPath = Path.GetFullPath(combined);
        var normalizedBase = Path.GetFullPath(_basePath + Path.DirectorySeparatorChar);

        if (!fullPath.StartsWith(normalizedBase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved prompt path '{fullPath}' escapes the configured base directory.");
        }

        return fullPath;
    }

    private async Task<PromptTemplate> LoadAsync(
        string id,
        string version,
        CancellationToken ct)
    {
        var filePath = BuildFilePath(id, version);

        if (!File.Exists(filePath))
        {
            throw new KeyNotFoundException(
                $"Prompt template not found: '{id}' version '{version}'. " +
                $"Expected file: '{filePath}'.");
        }

        LogLoadingFile(_logger, id, version, filePath);

        var yaml = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
        var doc = _deserializer.Deserialize<YamlPromptDocument>(yaml);

        var metadata = MapMetadata(doc.Metadata);
        var template = new PromptTemplate(
            Id: id,
            Version: version,
            StaticSystemPrompt: doc.StaticSystemPrompt ?? string.Empty,
            ContextTemplate: doc.ContextTemplate ?? string.Empty,
            Metadata: metadata);

        LogLoadedTemplate(_logger, id, version);

        return template;
    }

    /// <summary>
    /// Internal YAML document model for deserialization.
    /// Maps to the YAML file structure with underscore naming convention.
    /// </summary>
    internal sealed class YamlPromptDocument
    {
        public string? StaticSystemPrompt { get; set; }

        public string? ContextTemplate { get; set; }

        public YamlPromptMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Internal YAML metadata model for deserialization.
    /// </summary>
    internal sealed class YamlPromptMetadata
    {
        public string? Description { get; set; }

        public string? Author { get; set; }

        public string? CreatedAt { get; set; }
    }
}
