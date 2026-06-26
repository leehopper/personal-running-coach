using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Sanitization;
using AnthropicMessages = Anthropic.Models.Messages;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Coaching LLM adapter backed by the Anthropic Claude API.
///
/// Uses the official Anthropic .NET SDK with built-in retry logic
/// (exponential backoff for 429 rate limits and transient errors).
/// Configuration comes from <see cref="CoachingLlmSettings"/>,
/// which reads model ID and max tokens from the IOptions pattern
/// (overridable via appsettings or user-secrets).
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

    private const string BusyMessage =
        "The coaching service is busy right now. Please try again in a moment.";

    private const string UnavailableMessage =
        "The coaching service is temporarily unavailable. Please try again shortly.";

    private const string RejectedMessage =
        "The coaching request could not be completed.";

    private const string IncompleteMessage =
        "The coaching reply could not be completed.";

    private readonly IAnthropicClient _client;
    private readonly CoachingLlmSettings _settings;
    private readonly ILogger<ClaudeCoachingLlm> _logger;
    private readonly bool _ownsClient;
    private readonly HttpClient? _httpClient;

    /// <summary>
    /// Factory for the streaming <see cref="IChatClient"/> the
    /// <see cref="StreamAsync"/> path drives. Production wraps the SDK's M.E.AI
    /// bridge (<see cref="AsIChatClient"/>) in <see cref="SanitizationAuditChatClient"/>
    /// so the stream inherits the GUARDRAIL audit span; tests inject a controllable
    /// stub so the DEC-073 stream-error translation can be exercised without a live
    /// SSE transport.
    /// </summary>
    private readonly Func<IChatClient> _streamingChatClientFactory;

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

        (_client, _httpClient) = CreateClientPipeline(_settings, new SocketsHttpHandler());
        _streamingChatClientFactory = () => new SanitizationAuditChatClient(AsIChatClient());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCoachingLlm"/> class
    /// with an externally provided client for testing with a mock/substitute.
    /// </summary>
    internal ClaudeCoachingLlm(
        IAnthropicClient client,
        CoachingLlmSettings settings,
        ILogger<ClaudeCoachingLlm> logger,
        Func<IChatClient>? streamingChatClientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _settings = settings;
        _logger = logger;
        _ownsClient = false;
        _httpClient = null;
        _streamingChatClientFactory = streamingChatClientFactory
            ?? (() => new SanitizationAuditChatClient(AsIChatClient()));
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        LogSendingRequest(_logger, _settings.ModelId, _settings.MaxTokens);

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
        };

        var response = await CreateMessageAsync(createParams, ct).ConfigureAwait(false);

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

        ThrowIfTruncated(response);

        return ScrubTrademarkedProse(_logger, text, outputKind: "text");
    }

    /// <inheritdoc />
    public Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        return GenerateStructuredAsync<T>(systemPrompt, userMessage, schema: null, cacheControl: null, ct);
    }

    /// <inheritdoc />
    public Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        IReadOnlyDictionary<string, JsonElement>? schema,
        CacheControl? cacheControl,
        CancellationToken ct)
    {
        return GenerateStructuredAsync<T>(systemPrompt, userMessage, schema, cacheControl, modelOverride: null, ct);
    }

    /// <inheritdoc />
    public async Task<(T Result, AnthropicUsage Usage)> GenerateStructuredAsync<T>(
        string systemPrompt,
        string userMessage,
        IReadOnlyDictionary<string, JsonElement>? schema,
        CacheControl? cacheControl,
        string? modelOverride,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        // A per-call model override (the Haiku intent classifier) targets a second
        // binding; null falls back to the configured default. Log the effective
        // model so telemetry reflects what was actually sent, not the default.
        var effectiveModel = modelOverride ?? _settings.ModelId;
        LogSendingRequest(_logger, effectiveModel, _settings.MaxTokens);

        // Resolve schema: caller-supplied (byte-stable, e.g. OnboardingSchema.Frozen)
        // takes precedence; otherwise fall back to runtime generation.
        var schemaDict = schema is not null
            ? new Dictionary<string, JsonElement>(schema, StringComparer.Ordinal)
            : BuildSchemaDictionary<T>();

        var createParams = new MessageCreateParams
        {
            Model = effectiveModel,
            MaxTokens = _settings.MaxTokens,
            System = BuildSystemParam(systemPrompt, cacheControl),
            Messages =
            [
                new MessageParam
                {
                    Role = "user",
                    Content = userMessage,
                },
            ],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = schemaDict,
                },
            },
        };

        var response = await CreateMessageAsync(createParams, ct).ConfigureAwait(false);

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

        ThrowIfTruncated(response);

        // Slice 3B F2: scrub on decoded JSON string values rather than the raw JSON text so that
        // the word-boundary regex fires correctly when the term follows a JSON escape sequence
        // (e.g. \n encodes a newline as the two-char sequence backslash+'n'; the 'n' is a word
        // character and blocks \b if we scan the raw text instead).
        JsonNode? scrubNode;
        try
        {
            scrubNode = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new PermanentCoachingLlmException(RejectedMessage, ex);
        }

        if (scrubNode is null)
        {
            throw new PermanentCoachingLlmException(
                RejectedMessage,
                new InvalidOperationException(
                    $"Failed to parse structured output for {typeof(T).Name}. JSON was a null literal."));
        }

        var scrubHits = TrademarkScrubber.ScrubJsonStringValues(scrubNode);
        if (scrubHits > 0)
        {
            LogScrubbedTrademarkedTerm(_logger, scrubHits, typeof(T).Name);
        }

        // DEC-073 classifies malformed model output as terminal. Constrained decoding makes a
        // malformed or null payload structurally unreachable in production, but the totality
        // contract on ICoachingLlm (CoachingLlmException is the only failure surface) must hold
        // even if that invariant ever breaks.
        T? result;
        try
        {
            result = scrubNode.Deserialize<T>(StructuredOutputSerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new PermanentCoachingLlmException(RejectedMessage, ex);
        }

        if (result is null)
        {
            throw new PermanentCoachingLlmException(
                RejectedMessage,
                new InvalidOperationException(
                    $"Failed to deserialize structured output to {typeof(T).Name}. JSON was a null literal."));
        }

        var usage = ExtractUsage(response);
        return (result, usage);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<string> StreamAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        // Validate eagerly at the call site (not deferred to the first MoveNextAsync of the
        // returned iterator), matching the async-Task ICoachingLlm methods — split from the
        // iterator body per sonar S4456.
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);

        return StreamCore(systemPrompt, userMessage, ct);
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
        if (_ownsClient)
        {
            if (_client is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Builds the owned <see cref="HttpClient"/> + <see cref="AnthropicClient"/> pair around the
    /// supplied transport handler. The <see cref="RetryAfterCaptureHandler"/> sits outermost in
    /// the SDK's HTTP pipeline so it can read the raw <c>Retry-After</c> header (DEC-073) — the
    /// SDK 12.31.0 exposes no header accessor on its exceptions. The SDK's own bounded retry loop
    /// wraps this handler; <see cref="HttpClient.Timeout"/> is disabled so the SDK's per-attempt
    /// timeout (<c>ClientOptions.Timeout</c>, linked to the inbound <see cref="CancellationToken"/>)
    /// is the sole governor. Single construction seam for the production constructor and the
    /// stub-transport tests, so the wiring cannot drift between them.
    /// </summary>
    internal static (AnthropicClient Client, HttpClient HttpClient) CreateClientPipeline(
        CoachingLlmSettings settings,
        HttpMessageHandler transportHandler)
    {
        var httpClient = new HttpClient(new RetryAfterCaptureHandler { InnerHandler = transportHandler })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };

        var client = new AnthropicClient(new ClientOptions
        {
            ApiKey = settings.ApiKey,
            MaxRetries = settings.MaxRetries,
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            HttpClient = httpClient,
        });

        return (client, httpClient);
    }

    /// <summary>
    /// Maps the SDK <see cref="Anthropic.Models.Messages.Usage"/> on the
    /// Anthropic response into the public <see cref="AnthropicUsage"/> record.
    /// The cache breakdown counters
    /// (<c>cache_creation_input_tokens</c> / <c>cache_read_input_tokens</c>)
    /// are nullable on the wire (only emitted when prompt-cache is active);
    /// missing values are coerced to zero so downstream rollup arithmetic
    /// stays well-defined.
    /// </summary>
    internal static AnthropicUsage ExtractUsage(Message response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var usage = response.Usage;
        return new AnthropicUsage(
            InputTokens: usage.InputTokens,
            OutputTokens: usage.OutputTokens,
            CacheCreationInputTokens: usage.CacheCreationInputTokens ?? 0,
            CacheReadInputTokens: usage.CacheReadInputTokens ?? 0);
    }

    /// <summary>
    /// Generates a JSON schema dictionary from <typeparamref name="T"/> at
    /// runtime via <see cref="JsonSchemaHelper.GenerateSchema{T}"/>.
    /// Used as the fallback path when the caller does not supply a
    /// pre-built (byte-stable) schema.
    /// </summary>
    internal static Dictionary<string, JsonElement> BuildSchemaDictionary<T>()
    {
        var schemaNode = JsonSchemaHelper.GenerateSchema<T>();
        return schemaNode.Deserialize<Dictionary<string, JsonElement>>()
            ?? throw new InvalidOperationException(
                $"Failed to materialize JSON schema dictionary for {typeof(T).Name}.");
    }

    /// <summary>
    /// Builds the Anthropic <c>system</c> parameter from the prompt text and
    /// optional cache-control breakpoint. When <paramref name="cacheControl"/>
    /// is null the system prompt is sent as a plain string and is NOT cached.
    /// When non-null, the system prompt is sent as a content-block array
    /// with a <c>cache_control</c> marker on the trailing text block — this
    /// is how Anthropic's prompt-prefix cache identifies the cacheable
    /// boundary (per the SDK documentation for <c>TextBlockParam.CacheControl</c>).
    /// </summary>
    internal static MessageCreateParamsSystem BuildSystemParam(
        string systemPrompt,
        CacheControl? cacheControl)
    {
        if (cacheControl is null)
        {
            return systemPrompt;
        }

        var ttlEnum = cacheControl.Ttl switch
        {
            "1h" => AnthropicMessages.Ttl.Ttl1h,
            "5m" => AnthropicMessages.Ttl.Ttl5m,
            _ => throw new ArgumentOutOfRangeException(
                nameof(cacheControl),
                cacheControl.Ttl,
                "Anthropic cache_control.ttl must be \"1h\" or \"5m\"."),
        };

        var block = new TextBlockParam
        {
            Text = systemPrompt,
            CacheControl = new CacheControlEphemeral
            {
                Ttl = ttlEnum,
            },
        };

        return new MessageCreateParamsSystem(new List<TextBlockParam> { block }, element: null);
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
    /// Translates a <c>stop_reason=max_tokens</c> truncation into a
    /// <see cref="PermanentCoachingLlmException"/>. DEC-073 classifies malformed/incomplete model
    /// output as terminal: a truncated payload cannot be retried into a complete one at the same
    /// MaxTokens, and it must not escape <see cref="ICoachingLlm"/> as an untyped exception.
    /// </summary>
    private static void ThrowIfTruncated(Message response)
    {
        if (response.StopReason is { } sr && sr == StopReason.MaxTokens)
        {
            throw new PermanentCoachingLlmException(
                RejectedMessage,
                new InvalidOperationException(
                    "LLM response was truncated (stop_reason=max_tokens). " +
                    "Increase MaxTokens or reduce prompt size."));
        }
    }

    /// <summary>
    /// Scrubs the trademarked pace-index term from raw LLM output before it is
    /// deserialized or returned (Slice 3B F2). The prompt-level vocabulary rules
    /// make a hit rare; when one occurs it is replaced with the approved
    /// vocabulary and logged as a warning so prompt drift stays visible.
    /// </summary>
    private static string ScrubTrademarkedProse(ILogger logger, string text, string outputKind)
    {
        var scrubbed = TrademarkScrubber.Scrub(text, out var occurrences);
        if (occurrences > 0)
        {
            LogScrubbedTrademarkedTerm(logger, occurrences, outputKind);
        }

        return scrubbed;
    }

    /// <summary>
    /// Reads the authoritative Anthropic <c>stop_reason</c> from a streamed update's
    /// <c>message_delta</c> event, or <see langword="null"/> for any other update. Read from the
    /// raw event JSON rather than M.E.AI's <see cref="ChatResponseUpdate.FinishReason"/> because the
    /// SDK enum has no <c>model_context_window_exceeded</c> member and the bridge maps it to
    /// <c>Stop</c> — indistinguishable from a clean <c>end_turn</c> at the finish-reason layer.
    /// </summary>
    private static string? TryGetTerminalStopReason(ChatResponseUpdate update)
    {
        if (update.RawRepresentation is not RawMessageStreamEvent raw || !raw.TryPickDelta(out _))
        {
            return null;
        }

        // ValueKind guards keep JsonElement.TryGetProperty from throwing on a non-object element
        // (it throws InvalidOperationException unless the element is an Object) — defensive against
        // a malformed event, so no untyped exception can escape the totality contract.
        return raw.Json.ValueKind == JsonValueKind.Object
            && raw.Json.TryGetProperty("delta", out var delta)
            && delta.ValueKind == JsonValueKind.Object
            && delta.TryGetProperty("stop_reason", out var stopReason)
            && stopReason.ValueKind == JsonValueKind.String
            ? stopReason.GetString()
            : null;
    }

    /// <summary>
    /// Harvests token-usage telemetry from a streamed update's raw event so the streaming path logs
    /// the same cost-tracking counters as the non-streaming flow. <c>input_tokens</c> (and the model
    /// id) ride the <c>message_start</c> event; the authoritative cumulative <c>output_tokens</c>
    /// rides <c>message_delta</c>. Reads defensively from the raw JSON (M.E.AI's update surface does
    /// not expose Anthropic usage) and leaves the running totals untouched for any other event.
    /// </summary>
    private static void HarvestStreamUsage(ChatResponseUpdate update, ref StreamUsage usage)
    {
        if (update.RawRepresentation is not RawMessageStreamEvent raw || raw.Json.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // message_start: { "message": { "model": ..., "usage": { "input_tokens": .. } } }
        if (raw.Json.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
        {
            if (message.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String)
            {
                usage.Model = model.GetString();
            }

            if (message.TryGetProperty("usage", out var startUsage) && startUsage.ValueKind == JsonValueKind.Object
                && startUsage.TryGetProperty("input_tokens", out var input) && input.TryGetInt64(out var inputTokens))
            {
                usage.InputTokens = inputTokens;
            }
        }

        // message_delta: { "usage": { "output_tokens": .. } } — cumulative; last write wins.
        if (raw.Json.TryGetProperty("usage", out var deltaUsage) && deltaUsage.ValueKind == JsonValueKind.Object
            && deltaUsage.TryGetProperty("output_tokens", out var output) && output.TryGetInt64(out var outputTokens))
        {
            usage.OutputTokens = outputTokens;
        }
    }

    /// <summary>
    /// Raises the errored-turn signal (<see cref="IncompleteCoachingLlmException"/>) when a stream
    /// ended cleanly on an incomplete stop reason. <c>max_tokens</c> is retryable (a fresh turn may
    /// fit); context overflow and a refusal are not (re-sending the same input fails the same way).
    /// Clean stop reasons (<c>end_turn</c>/<c>stop_sequence</c>) and a missing reason are no-ops.
    /// </summary>
    private static void ThrowIfIncompleteFinish(string? stopReason)
    {
        IncompleteReason? reason = stopReason switch
        {
            "max_tokens" => IncompleteReason.MaxTokens,
            "model_context_window_exceeded" => IncompleteReason.ContextWindowExceeded,
            "refusal" => IncompleteReason.Refusal,
            _ => null,
        };

        if (reason is not { } incompleteReason)
        {
            return;
        }

        throw new IncompleteCoachingLlmException(IncompleteMessage, incompleteReason);
    }

    /// <summary>
    /// Translates an Anthropic SDK failure into the DEC-073 totality contract — the single classifier
    /// shared by the request/response (<see cref="CreateMessageAsync"/>) and streaming
    /// (<see cref="StreamAsync"/>) paths so they cannot drift. A mid-stream error after the HTTP 200
    /// arrives as <see cref="AnthropicSseException"/> (streaming-only) and is classified on its SSE
    /// error <c>type</c>; everything else is a pre-first-byte failure classified by HTTP status
    /// (408/409/429/5xx → Transient, other 4xx → Permanent) or a transport
    /// <see cref="AnthropicIOException"/> → Transient. Switch order mirrors the leaf-to-base hierarchy
    /// (rate-limit and 5xx derive from <see cref="AnthropicApiException"/>; 408/409 land on the
    /// non-leaf branch and are classified by status). The caller wraps the call in a
    /// <see cref="RetryAfterCapture"/> scope so a 429's raw <c>Retry-After</c> header is attached.
    /// </summary>
    private static CoachingLlmException TranslateAnthropicFailure(AnthropicException ex) => ex switch
    {
        AnthropicSseException sse => ClassifySseError(sse),
        AnthropicRateLimitException => new TransientCoachingLlmException(BusyMessage, RetryAfterCapture.CurrentSeconds, ex),
        Anthropic5xxException => new TransientCoachingLlmException(UnavailableMessage, RetryAfterCapture.CurrentSeconds, ex),
        AnthropicIOException => new TransientCoachingLlmException(UnavailableMessage, retryAfterSeconds: null, ex),
        AnthropicInvalidDataException => new PermanentCoachingLlmException(RejectedMessage, ex),
        AnthropicApiException api => ClassifyApiStatus(api),
        _ => new PermanentCoachingLlmException(RejectedMessage, ex),
    };

    /// <summary>
    /// Classifies a status-bearing <see cref="AnthropicApiException"/> by HTTP status: 408/409/429
    /// and 5xx are Transient (the SDK would have retried them); other 4xx are Permanent.
    /// </summary>
    private static CoachingLlmException ClassifyApiStatus(AnthropicApiException ex)
    {
        var status = (int)ex.StatusCode;
        var transient = status is 408 or 409 or 429 || status >= 500;
        return transient
            ? new TransientCoachingLlmException(UnavailableMessage, RetryAfterCapture.CurrentSeconds, ex)
            : new PermanentCoachingLlmException(RejectedMessage, ex);
    }

    /// <summary>
    /// Classifies a mid-stream <see cref="AnthropicSseException"/> on the SDK's strongly-typed
    /// <see cref="AnthropicServiceException.ErrorType"/> (parsed from the SSE <c>error.type</c> field
    /// by the SDK, independent of the server-controlled <c>error.message</c> text). Bad-request /
    /// auth / permission / not-found / billing types are terminal (Permanent); the retryable
    /// service-side types (rate-limit / overloaded / api / timeout) and any unrecognized type
    /// (<see langword="null"/>) default to Transient (recall-over-precision). There is no
    /// <c>Retry-After</c> header once SSE headers are flushed, so a transient mid-stream error
    /// carries no delay hint (the SSE endpoint applies a configured default).
    /// </summary>
    private static CoachingLlmException ClassifySseError(AnthropicSseException sse) => sse.ErrorType switch
    {
        ErrorType.InvalidRequestError or ErrorType.AuthenticationError or ErrorType.PermissionError
            or ErrorType.NotFoundError or ErrorType.BillingError
            => new PermanentCoachingLlmException(RejectedMessage, sse),
        _ => new TransientCoachingLlmException(UnavailableMessage, retryAfterSeconds: null, sse),
    };

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending coaching request to {ModelId} (maxTokens={MaxTokens})")]
    private static partial void LogSendingRequest(ILogger logger, string modelId, int maxTokens);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received response from {ModelId}: {ContentLength} chars, stop_reason={StopReason}, input_tokens={InputTokens}, output_tokens={OutputTokens}")]
    private static partial void LogReceivedResponse(ILogger logger, string modelId, int contentLength, string stopReason, long inputTokens, long outputTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scrubbed {Occurrences} trademarked pace-index term occurrence(s) from LLM output ({OutputKind})")]
    private static partial void LogScrubbedTrademarkedTerm(ILogger logger, int occurrences, string outputKind);

    /// <summary>
    /// The streaming iterator behind <see cref="StreamAsync"/> (split out so argument validation
    /// runs eagerly at the call site rather than on first enumeration — sonar S4456).
    /// </summary>
    private async IAsyncEnumerable<string> StreamCore(
        string systemPrompt,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Run inside a RetryAfterCapture scope so a pre-stream 429's raw Retry-After header
        // (seen by RetryAfterCaptureHandler on the owned pipeline) is attached to the translated
        // Transient exception, exactly as the non-streaming path does. NOTE: an AsyncLocal value
        // set here is only visible up to the FIRST `yield return` — after the consumer resumes the
        // iterator it runs under its own ExecutionContext, so RetryAfterCapture.CurrentSeconds is
        // reliable only in the pre-first-byte window. That is sufficient: a 429 is a pre-first-byte
        // failure, and mid-stream errors have no Retry-After header once SSE headers are flushed
        // (R-084).
        using (RetryAfterCapture.BeginScope())
        {
            using var chatClient = _streamingChatClientFactory();
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userMessage),
            };

            // R-084: the SDK throws mid-enumeration for streamed errors. Drive the enumerator
            // manually so MoveNextAsync sits inside try/catch while `yield return` stays outside it
            // (C# forbids yielding from a try that has a catch), translating the Anthropic failure
            // surface into the DEC-073 totality contract.
            await using var enumerator = chatClient
                .GetStreamingResponseAsync(messages, options: null, ct)
                .GetAsyncEnumerator(ct);

            string? terminalStopReason = null;
            var usage = default(StreamUsage);
            var scrubber = new StreamingTrademarkScrubber();
            var responseLength = 0;

            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
                {
                    // The SDK's per-attempt timeout fired (the caller did not cancel) — a transient
                    // service failure. A genuine client abort (ct cancelled) fails this filter and
                    // propagates unwrapped as not-an-error, mirroring CreateMessageAsync.
                    throw new TransientCoachingLlmException(UnavailableMessage, retryAfterSeconds: null, ex);
                }
                catch (AnthropicException ex)
                {
                    throw TranslateAnthropicFailure(ex);
                }

                HarvestStreamUsage(update, ref usage);

                // The terminal stop reason rides the message_delta update (and may repeat on
                // trailing updates — last-write-wins keeps that idempotent). Captured here, acted
                // on after the stream ends cleanly.
                terminalStopReason = TryGetTerminalStopReason(update) ?? terminalStopReason;

                // Scrub the trademarked pace-index term before any chunk reaches the consumer, the
                // same boundary GenerateAsync enforces on complete responses. The scrubber holds
                // back only a trailing run that could still complete into the term across deltas, so
                // a clean stream keeps its natural chunking.
                var safe = scrubber.Push(update.Text);
                if (safe.Length > 0)
                {
                    responseLength += safe.Length;
                    yield return safe;
                }
            }

            var tail = scrubber.Flush();
            if (tail.Length > 0)
            {
                responseLength += tail.Length;
                yield return tail;
            }

            if (scrubber.Occurrences > 0)
            {
                LogScrubbedTrademarkedTerm(_logger, scrubber.Occurrences, "stream");
            }

            // Mirror the non-streaming cost-tracking invariant: log model, response length, stop
            // reason, and token counts once the stream ends — before the errored-turn check so the
            // telemetry is captured even on an incomplete finish.
            LogReceivedResponse(
                _logger,
                usage.Model ?? _settings.ModelId,
                responseLength,
                terminalStopReason ?? "unknown",
                usage.InputTokens,
                usage.OutputTokens);

            // A clean enumeration end on an incomplete stop reason (truncation, context overflow,
            // or a refusal) is the errored-turn signal: the partial just yielded is unusable as a
            // complete turn. R-084 — these end the stream cleanly, never as an exception.
            ThrowIfIncompleteFinish(terminalStopReason);
        }
    }

    /// <summary>
    /// Issues the Anthropic <c>messages.create</c> call inside a <see cref="RetryAfterCapture"/>
    /// scope and translates the SDK failure surface into the adapter-owned
    /// <see cref="TransientCoachingLlmException"/> / <see cref="PermanentCoachingLlmException"/>
    /// (DEC-073) via the shared <see cref="TranslateAnthropicFailure"/> classifier, so callers never
    /// see an <c>Anthropic.Exceptions</c> type and this path cannot drift from
    /// <see cref="StreamAsync"/>. The SDK has already applied its own bounded retries
    /// (<see cref="CoachingLlmSettings.MaxRetries"/>) and honored <c>Retry-After</c> backoff by the
    /// time any catch fires. Genuine caller cancellation propagates unwrapped and is never
    /// reclassified; the filtered <c>OperationCanceledException</c> catch only fires when the caller's
    /// token is NOT cancelled — i.e. the SDK's per-attempt timeout (<c>ClientOptions.Timeout</c>),
    /// which surfaces as a raw <see cref="TaskCanceledException"/> from a linked CTS with no timeout
    /// exception type of its own.
    /// </summary>
    private async Task<Message> CreateMessageAsync(MessageCreateParams createParams, CancellationToken ct)
    {
        using (RetryAfterCapture.BeginScope())
        {
            try
            {
                return await _client.Messages.Create(createParams, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // The SDK's per-attempt timeout fired (the caller did not cancel): a hung or
                // overlong Anthropic call is a transient service failure, not a user cancellation.
                throw new TransientCoachingLlmException(UnavailableMessage, retryAfterSeconds: null, ex);
            }
            catch (AnthropicException ex)
            {
                // Status / leaf-type classification lives in the shared translator so this path and
                // StreamAsync cannot drift (DEC-073). Genuine caller cancellation is unaffected — it
                // surfaces as OperationCanceledException, which is not an AnthropicException.
                throw TranslateAnthropicFailure(ex);
            }
        }
    }

    /// <summary>
    /// Mutable accumulator for token-usage telemetry harvested across stream events
    /// (see <see cref="HarvestStreamUsage"/>). Nested because it is a private implementation
    /// detail of the streaming path with no meaning outside it.
    /// </summary>
    private struct StreamUsage
    {
        public string? Model;
        public long InputTokens;
        public long OutputTokens;
    }
}
