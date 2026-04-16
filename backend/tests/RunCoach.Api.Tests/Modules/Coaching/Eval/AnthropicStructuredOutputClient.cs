using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that intercepts structured output requests
/// (with <see cref="ChatResponseFormat.ForJsonSchema"/>) and delegates to the native
/// Anthropic SDK's constrained decoding via <see cref="OutputConfig"/>/<see cref="JsonOutputFormat"/>.
///
/// The standard Anthropic IChatClient bridge (<c>client.AsIChatClient()</c>) does NOT
/// translate <c>ChatResponseFormat.ForJsonSchema()</c> to constrained decoding — it
/// silently ignores the schema. This wrapper fills that gap.
///
/// Unstructured requests (no schema) pass through to the inner client unchanged.
/// When placed below M.E.AI.Evaluation's caching middleware in the pipeline, cached
/// responses are returned before this code runs — no wasted API calls on replay.
/// </summary>
public sealed class AnthropicStructuredOutputClient : DelegatingChatClient
{
    private readonly IAnthropicClient _nativeClient;
    private readonly string _defaultModel;
    private readonly int _defaultMaxTokens;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicStructuredOutputClient"/> class.
    /// </summary>
    /// <param name="inner">The inner IChatClient (from <c>AnthropicClient.AsIChatClient()</c>).</param>
    /// <param name="nativeClient">The native Anthropic client for structured output calls.</param>
    /// <param name="defaultModel">Default model ID when not specified in ChatOptions.</param>
    /// <param name="defaultMaxTokens">Default max tokens when not specified in ChatOptions.</param>
    public AnthropicStructuredOutputClient(
        IChatClient inner,
        IAnthropicClient nativeClient,
        string defaultModel,
        int defaultMaxTokens = 4096)
        : base(inner)
    {
        _nativeClient = nativeClient;
        _defaultModel = defaultModel;
        _defaultMaxTokens = defaultMaxTokens;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (options?.ResponseFormat is ChatResponseFormatJson { Schema: not null } jsonFormat)
        {
            return await CallNativeStructuredAsync(messages, options, jsonFormat, cancellationToken).ConfigureAwait(false);
        }

        return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Splits M.E.AI ChatMessages into an Anthropic system prompt (separate parameter)
    /// and user/assistant MessageParam array. Anthropic's API requires system messages
    /// to be passed via the System parameter, not inline.
    /// </summary>
    /// <remarks>
    /// Non-text content parts (images, etc.) are dropped -- only <c>msg.Text</c> is extracted.
    /// This is intentional: coaching eval prompts are text-only, and the Anthropic native
    /// <see cref="MessageParam.Content"/> field expects a plain string.
    /// </remarks>
    private static (string? SystemPrompt, MessageParam[] Messages) SplitMessages(
        IEnumerable<ChatMessage> messages)
    {
        var systemParts = new List<string>();
        var messageParams = new List<MessageParam>();

        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.System)
            {
                if (!string.IsNullOrWhiteSpace(msg.Text))
                {
                    systemParts.Add(msg.Text);
                }
            }
            else
            {
                messageParams.Add(new MessageParam
                {
                    Role = msg.Role == ChatRole.Assistant ? "assistant" : "user",
                    Content = msg.Text ?? string.Empty,
                });
            }
        }

        var systemPrompt = systemParts.Count > 0 ? string.Join("\n\n", systemParts) : null;
        return (systemPrompt, messageParams.ToArray());
    }

    /// <summary>
    /// Converts a JsonElement schema (from ChatResponseFormatJson.Schema) to
    /// the Dictionary format required by Anthropic's JsonOutputFormat.Schema.
    /// </summary>
    private static Dictionary<string, JsonElement> ConvertSchema(JsonElement schemaElement)
    {
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            schemaElement.GetRawText())
            ?? throw new InvalidOperationException(
                "Schema deserialization returned null for the provided JSON schema element.");
    }

    /// <summary>
    /// Maps a native Anthropic Message response back to an M.E.AI ChatResponse.
    /// Preserves text content, model ID, stop reason, and usage data.
    /// </summary>
    private static ChatResponse MapToChatResponse(Message msg)
    {
        var text = ClaudeCoachingLlm.ExtractTextContent(msg);

        var chatMessage = new ChatMessage(ChatRole.Assistant, text);
        var response = new ChatResponse(chatMessage)
        {
            ModelId = msg.Model.ToString(),
        };

        if (msg.StopReason is { } sr)
        {
            response.FinishReason = MapFinishReason(sr);
        }

        if (msg.Usage is { } usage)
        {
            response.Usage = new UsageDetails
            {
                InputTokenCount = (int)usage.InputTokens,
                OutputTokenCount = (int)usage.OutputTokens,
                TotalTokenCount = (int)(usage.InputTokens + usage.OutputTokens),
            };
        }

        return response;
    }

    /// <summary>
    /// Maps an Anthropic <see cref="StopReason"/> to the corresponding M.E.AI
    /// <see cref="ChatFinishReason"/>. Returns <c>null</c> when the stop reason
    /// is not recognized or not set.
    /// </summary>
    private static ChatFinishReason? MapFinishReason(StopReason stopReason)
    {
        if (stopReason == StopReason.EndTurn || stopReason == StopReason.StopSequence)
        {
            return ChatFinishReason.Stop;
        }

        if (stopReason == StopReason.MaxTokens)
        {
            return ChatFinishReason.Length;
        }

        if (stopReason == StopReason.ToolUse)
        {
            return ChatFinishReason.ToolCalls;
        }

        return null;
    }

    private async Task<ChatResponse> CallNativeStructuredAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options,
        ChatResponseFormatJson jsonFormat,
        CancellationToken cancellationToken)
    {
        var (systemPrompt, userAssistantMessages) = SplitMessages(messages);
        var schemaDict = ConvertSchema(jsonFormat.Schema!.Value);

        var createParams = new MessageCreateParams
        {
            Model = options.ModelId ?? _defaultModel,
            MaxTokens = options.MaxOutputTokens ?? _defaultMaxTokens,
            System = systemPrompt ?? string.Empty,
            Messages = userAssistantMessages,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = schemaDict,
                },
            },
        };

        var nativeResponse = await _nativeClient.Messages.Create(createParams, cancellationToken).ConfigureAwait(false);

        if (nativeResponse.StopReason is { } sr && sr == StopReason.MaxTokens)
        {
            throw new InvalidOperationException(
                "LLM response was truncated (stop_reason=max_tokens). " +
                "Increase MaxTokens or reduce prompt size.");
        }

        return MapToChatResponse(nativeResponse);
    }
}
