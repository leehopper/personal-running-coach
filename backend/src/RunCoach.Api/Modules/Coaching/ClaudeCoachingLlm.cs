using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Core;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Coaching LLM adapter backed by the Anthropic Claude API.
///
/// Uses the official Anthropic .NET SDK with built-in retry logic
/// (exponential backoff for 429 rate limits and transient errors).
/// Configuration comes from <see cref="CoachingLlmSettings"/>,
/// which reads model ID, temperature, and max tokens from the
/// IOptions pattern (overridable via appsettings or user-secrets).
///
/// The API key is never logged.
/// </summary>
public sealed partial class ClaudeCoachingLlm : ICoachingLlm, IDisposable
{
    /// <summary>
    /// JSON serializer options matching the schema generation options used by
    /// <see cref="JsonSchemaHelper"/> — snake_case naming, string enums.
    /// </summary>
    internal static readonly JsonSerializerOptions StructuredOutputSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IAnthropicClient _client;
    private readonly CoachingLlmSettings _settings;
    private readonly ILogger<ClaudeCoachingLlm> _logger;
    private readonly bool _ownsClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCoachingLlm"/> class
    /// using dependency-injected settings and logger.
    /// Creates the <see cref="AnthropicClient"/> from configuration.
    /// </summary>
    public ClaudeCoachingLlm(
        IOptions<CoachingLlmSettings> settings,
        ILogger<ClaudeCoachingLlm> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = settings.Value;
        _logger = logger;
        _ownsClient = true;

        ValidateSettings(_settings);

        _client = new AnthropicClient(new ClientOptions
        {
            ApiKey = _settings.ApiKey,
            MaxRetries = _settings.MaxRetries,
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds),
        });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCoachingLlm"/> class
    /// with an externally provided client for testing with a mock/substitute.
    /// </summary>
    internal ClaudeCoachingLlm(
        IAnthropicClient client,
        CoachingLlmSettings settings,
        ILogger<ClaudeCoachingLlm> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _settings = settings;
        _logger = logger;
        _ownsClient = false;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        LogSendingRequest(_logger, _settings.ModelId, _settings.Temperature, _settings.MaxTokens);

        var createParams = new MessageCreateParams
        {
            Model = _settings.ModelId,
            MaxTokens = _settings.MaxTokens,
            System = systemPrompt,
            Messages =
            [
                new MessageParam
                {
                    Role = "user",
                    Content = userMessage,
                },
            ],
            Temperature = _settings.Temperature,
        };

        var response = await _client.Messages.Create(createParams, ct).ConfigureAwait(false);

        var text = ExtractTextContent(response);

        var responseModel = response.Model.ToString();
        var stopReason = response.StopReason?.ToString() ?? "unknown";
        LogReceivedResponse(
            _logger,
            responseModel,
            text.Length,
            stopReason,
            response.Usage.InputTokens,
            response.Usage.OutputTokens);

        if (response.StopReason is { } sr && sr == StopReason.MaxTokens)
        {
            throw new InvalidOperationException(
                "LLM response was truncated (stop_reason=max_tokens). " +
                "Increase MaxTokens or reduce prompt size.");
        }

        return text;
    }

    /// <inheritdoc />
    public async Task<T> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        LogSendingRequest(_logger, _settings.ModelId, _settings.Temperature, _settings.MaxTokens);

        var schemaNode = JsonSchemaHelper.GenerateSchema<T>();
        var schemaDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            schemaNode.ToJsonString())!;

        var createParams = new MessageCreateParams
        {
            Model = _settings.ModelId,
            MaxTokens = _settings.MaxTokens,
            System = systemPrompt,
            Messages =
            [
                new MessageParam
                {
                    Role = "user",
                    Content = userMessage,
                },
            ],
            Temperature = _settings.Temperature,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = schemaDict,
                },
            },
        };

        var response = await _client.Messages.Create(createParams, ct).ConfigureAwait(false);

        var json = ExtractTextContent(response);

        var responseModel = response.Model.ToString();
        var stopReason = response.StopReason?.ToString() ?? "unknown";
        LogReceivedResponse(
            _logger,
            responseModel,
            json.Length,
            stopReason,
            response.Usage.InputTokens,
            response.Usage.OutputTokens);

        if (response.StopReason is { } sr && sr == StopReason.MaxTokens)
        {
            throw new InvalidOperationException(
                "LLM response was truncated (stop_reason=max_tokens). " +
                "Increase MaxTokens or reduce prompt size.");
        }

        return JsonSerializer.Deserialize<T>(json, StructuredOutputSerializerOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize structured output to {typeof(T).Name}. JSON was a null literal.");
    }

    /// <summary>
    /// Gets an <see cref="IChatClient"/> bridge for use with M.E.AI.Evaluation
    /// caching and reporting infrastructure.
    /// </summary>
    /// <returns>An IChatClient wrapping this adapter's Anthropic client.</returns>
    public IChatClient AsIChatClient()
    {
        return _client.AsIChatClient(_settings.ModelId, _settings.MaxTokens);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient && _client is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Extracts concatenated text content from the message response.
    /// Iterates through content blocks and collects all text blocks,
    /// ignoring non-text blocks (thinking, tool use, etc.).
    /// </summary>
    internal static string ExtractTextContent(Message response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Content.Count == 0)
        {
            return string.Empty;
        }

        // Fast path: single text block (most common case).
        if (response.Content.Count == 1 && response.Content[0].TryPickText(out var singleBlock))
        {
            return singleBlock.Text;
        }

        // Multiple blocks: concatenate all text blocks.
        var sb = new StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
            {
                sb.Append(textBlock.Text);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validates that required settings are present.
    /// Throws <see cref="InvalidOperationException"/> if the API key is missing.
    /// </summary>
    private static void ValidateSettings(CoachingLlmSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is not configured. " +
                "Set the 'Anthropic:ApiKey' value via user-secrets: " +
                "dotnet user-secrets set \"Anthropic:ApiKey\" \"<your-key>\"");
        }

        if (string.IsNullOrWhiteSpace(settings.ModelId))
        {
            throw new InvalidOperationException(
                "Anthropic model ID is not configured. " +
                "Set the 'Anthropic:ModelId' value in configuration.");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending coaching request to {ModelId} (temperature={Temperature}, maxTokens={MaxTokens})")]
    private static partial void LogSendingRequest(ILogger logger, string modelId, double temperature, int maxTokens);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received response from {ModelId}: {ContentLength} chars, stop_reason={StopReason}, input_tokens={InputTokens}, output_tokens={OutputTokens}")]
    private static partial void LogReceivedResponse(ILogger logger, string modelId, int contentLength, string stopReason, long inputTokens, long outputTokens);
}
