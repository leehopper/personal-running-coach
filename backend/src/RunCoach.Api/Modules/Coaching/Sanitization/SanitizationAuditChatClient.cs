using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Outer audit-only <see cref="DelegatingChatClient"/> that runs in the
/// Microsoft.Extensions.AI pipeline AFTER <c>ContextAssembler</c> has finished
/// assembling the prompt. Per Slice 1 § Unit 6 / R-068 § 4, this client does
/// NOT re-sanitize — that's done per-section inside the assembler. It exists
/// to emit a rollup span carrying the OpenInference
/// <c>openinference.span.kind = "GUARDRAIL"</c> attribute so Phoenix's
/// guardrail dashboard view picks up sanitization activity at the LLM-call
/// boundary.
/// </summary>
/// <remarks>
/// <para>
/// PII discipline: this client never reads message content into span
/// attributes. It only counts messages and stamps the policy version, so the
/// span payload is safe for any OTel backend (Phoenix Cloud, hosted vendors).
/// </para>
/// <para>
/// Registered in the M.E.AI pipeline via the
/// <c>UseSanitizationAudit()</c> extension once the pipeline is wired up
/// (T01.5 / DEC-058). Today it remains DI-resolvable as a building block.
/// </para>
/// </remarks>
public sealed class SanitizationAuditChatClient : DelegatingChatClient
{
    /// <summary>Name of the rollup span emitted around the inner client call.</summary>
    internal const string AuditSpanName = "runcoach.llm.sanitization.audit";

    private static readonly ActivitySource Source = new(LayeredPromptSanitizer.ActivitySourceName);

    /// <summary>
    /// Initializes a new instance of the <see cref="SanitizationAuditChatClient"/> class.
    /// Initializes a new instance wrapping <paramref name="innerClient"/>.
    /// </summary>
    /// <param name="innerClient">The inner chat client this audit layer wraps.</param>
    public SanitizationAuditChatClient(IChatClient innerClient)
        : base(innerClient)
    {
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        using var activity = Source.StartActivity(AuditSpanName, ActivityKind.Internal);
        StampGuardrailAttributes(activity, messageList);

        return await base.GetResponseAsync(messageList, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageList = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();

        // Audit-only span: records initiation attributes and policy version.
        // Duration reflects span start to stream-kickoff, not stream completion,
        // because holding the activity open across an IAsyncEnumerable yields
        // would require an iterator wrapper that adds complexity without benefit
        // for a guardrail audit signal. Duration measurement is not a goal here.
        using var activity = Source.StartActivity(AuditSpanName, ActivityKind.Internal);
        StampGuardrailAttributes(activity, messageList);

        return base.GetStreamingResponseAsync(messageList, options, cancellationToken);
    }

    private static void StampGuardrailAttributes(
        Activity? activity,
        IReadOnlyCollection<ChatMessage> messages)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("openinference.span.kind", "GUARDRAIL");
        activity.SetTag("runcoach.sanitization.policy_version", PatternCatalog.PolicyVersion);
        activity.SetTag("runcoach.sanitization.audit.message_count", messages.Count);
    }
}
