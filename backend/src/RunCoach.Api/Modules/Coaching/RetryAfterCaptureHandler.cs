using System.Net.Http.Headers;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Outermost <see cref="DelegatingHandler"/> in the Anthropic SDK's HTTP pipeline. It reads the
/// raw <c>Retry-After</c> response header off each attempt and stashes it via
/// <see cref="RetryAfterCapture"/> so the DEC-073 translation in <see cref="ClaudeCoachingLlm"/>
/// can attach it to a <see cref="TransientCoachingLlmException"/> — the SDK 12.24.1 exposes no
/// header accessor on its exceptions, so this transport seam is the only source.
/// </summary>
internal sealed class RetryAfterCaptureHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (ReadRetryAfterSeconds(response.Headers.RetryAfter) is { } seconds)
        {
            RetryAfterCapture.Record(seconds);
        }

        return response;
    }

    /// <summary>
    /// Reads the retry-after delay in whole seconds from a parsed <c>Retry-After</c> header.
    /// Anthropic emits the numeric (delta-seconds) form; the HTTP-date form is intentionally not
    /// surfaced as a hint at MVP-0 to keep this transport handler free of a clock dependency.
    /// </summary>
    private static int? ReadRetryAfterSeconds(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta)
        {
            var seconds = Math.Ceiling(delta.TotalSeconds);
            return seconds < 0 ? 0 : (int)seconds;
        }

        return null;
    }
}
