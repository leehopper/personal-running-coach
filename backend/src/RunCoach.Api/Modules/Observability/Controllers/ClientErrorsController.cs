using System.Security.Claims;
using System.Text;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Observability.Models;

namespace RunCoach.Api.Modules.Observability.Controllers;

/// <summary>
/// Endpoint that the SPA's top-level <c>AppErrorBoundary</c> fallback POSTs
/// to from <c>componentDidCatch</c> / <c>window.error</c> /
/// <c>window.unhandledrejection</c> per DEC-068. Records one
/// <see cref="ClientErrorReported"/> event per report under a Marten stream
/// keyed on the boundary-generated correlation id, so the support workflow
/// can locate the precise client-side incident by the same id the user sees
/// on the fallback card.
/// </summary>
/// <remarks>
/// <para>
/// Cookie auth gated by <see cref="AuthPolicies.CookieOrBearer"/> — the
/// authenticated user's id doubles as Marten's conjoined tenant id so the
/// row's <c>mt_events.tenant_id</c> column scopes the report to the same
/// tenant as the rest of the runner's events. Antiforgery is NOT required
/// per DEC-068 § Wire shape: the boundary may not be able to read the
/// SPA-readable XSRF cookie reliably from a partially-rendered state. The
/// rate-limit + per-session cap mitigations are pre-MVP-1 and out of scope
/// for this slice.
/// </para>
/// <para>
/// Payload bounds:
/// <list type="bullet">
/// <item>Total request body is capped at 64 KB via
///   <see cref="RequestSizeLimitAttribute"/>. The framework returns 413 on
///   exceeded — see the controller's <see cref="ProducesResponseTypeAttribute"/>
///   for the documented status code.</item>
/// <item>The <see cref="ClientErrorRequestDto.Stack"/> string is truncated
///   server-side to 16 KB before append. The truncation suffix appears
///   verbatim in the stored payload so reports that exceed the cap can be
///   spotted in the support workflow.</item>
/// </list>
/// </para>
/// </remarks>
[ApiController]
[Route("api/v1/client-errors")]
[Authorize(Policy = AuthPolicies.CookieOrBearer)]
public sealed partial class ClientErrorsController(
    IDocumentStore store,
    ILogger<ClientErrorsController> logger) : ControllerBase
{
    /// <summary>
    /// Soft cap on the request body, in bytes. Set to 64 KiB per DEC-068's
    /// bounded-payload requirement; exceeding the cap returns 413 from the
    /// framework's <see cref="Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature"/>
    /// integration before model binding runs.
    /// </summary>
    internal const int MaxRequestBodyBytes = 64 * 1024;

    /// <summary>
    /// Maximum bytes retained on <see cref="ClientErrorRequestDto.Stack"/>
    /// before server-side truncation. Long stacks (deep recursion, packed
    /// builds) cannot bloat the stored event row beyond this cap.
    /// </summary>
    internal const int MaxStackBytes = 16 * 1024;

    /// <summary>
    /// Marker suffix appended after server-side stack truncation so the
    /// truncation is visible to humans inspecting the stored row. The suffix
    /// bytes count against the <see cref="MaxStackBytes"/> cap so the final
    /// stored stack never exceeds the cap.
    /// </summary>
    internal const string StackTruncationSuffix = "\n[...truncated server-side at 16 KB]";

    private const string MissingUserType = "https://runcoach.app/problems/missing-user-claim";
    private const string InvalidKindType = "https://runcoach.app/problems/invalid-client-error-kind";

    /// <summary>POST /api/v1/client-errors — record a client-side error report.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Report(
        [FromBody] ClientErrorRequestDto request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetUserId(out var userId))
        {
            return MissingUserClaim();
        }

        // Explicit Content-Length gate before model binding's bytes are
        // consumed. The `[RequestSizeLimit]` attribute above pins the
        // Kestrel-enforced cap in production via
        // `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize`, but
        // TestServer does not run Kestrel's body-size middleware, so the
        // limit would only surface as 204 instead of 413 in the integration
        // tests. The explicit check also rejects requests with a declared
        // Content-Length over the cap before any bytes are read, which is a
        // cheaper rejection path than letting the framework throw mid-read.
        if (Request.ContentLength is { } declaredLength && declaredLength > MaxRequestBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // System.Text.Json's integer-enum binding does not range-check string
        // enums backed by `JsonStringEnumMemberName`, but the converter throws
        // when an unknown wire string is supplied — ASP.NET surfaces that as a
        // 400 from the model-binding pipeline. The defense-in-depth check here
        // catches the residual case of a recognized CLR value that is outside
        // the enum's declared range (e.g. an attacker crafting a body that
        // bypasses the converter via numeric coercion).
        if (!Enum.IsDefined(request.Kind))
        {
            return Problem(
                type: InvalidKindType,
                title: "The supplied client-error kind is not a recognized value.",
                detail: $"Kind value '{(int)request.Kind}' is outside the ClientErrorKind enum range.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var truncatedStack = TruncateStack(request.Stack);

        // Marten is configured with TenancyStyle.Conjoined; every event row
        // carries the runner's tenant id (per-user) on `mt_events.tenant_id`.
        // Open a per-request tenanted session keyed on the authenticated
        // user id — same pattern as OnboardingController.GetState.
        await using var session = store.LightweightSession(userId.ToString());

        var @event = new ClientErrorReported(
            CorrelationId: request.CorrelationId,
            OccurredAt: request.OccurredAt,
            Kind: request.Kind,
            ErrorName: request.ErrorName,
            Message: request.Message,
            Stack: truncatedStack,
            ComponentStack: request.ComponentStack,
            Url: request.Url,
            UserAgent: request.UserAgent,
            AppVersion: request.AppVersion);

        // Stream id = correlation id per DEC-068 / R-073. StartStream<T>
        // enforces "first writer wins" semantics: a duplicate POST under the
        // same correlation id surfaces ExistingStreamIdCollisionException at
        // append time, which the framework returns as a 500. The boundary's
        // crypto.randomUUID() makes a collision astronomically unlikely; the
        // failure shape is acceptable defense-in-depth.
        session.Events.StartStream<ClientErrorReported>(request.CorrelationId, @event);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        LogReportRecorded(logger, request.CorrelationId, userId, request.Kind);
        return NoContent();
    }

    internal static string TruncateStack(string stack)
    {
        if (Encoding.UTF8.GetByteCount(stack) <= MaxStackBytes)
        {
            return stack;
        }

        // Walk runes so the byte budget is measured in UTF-8 bytes, not
        // UTF-16 code units. This correctly handles multibyte characters
        // (emoji, CJK, supplementary planes) that would otherwise let the
        // stored stack exceed MaxStackBytes.
        var suffixBytes = Encoding.UTF8.GetByteCount(StackTruncationSuffix);
        var budget = MaxStackBytes - suffixBytes;
        if (budget <= 0)
        {
            return StackTruncationSuffix;
        }

        var builder = new StringBuilder();
        var used = 0;
        foreach (var rune in stack.EnumerateRunes())
        {
            var runeBytes = rune.Utf8SequenceLength;
            if (used + runeBytes > budget)
            {
                break;
            }

            builder.Append(rune);
            used += runeBytes;
        }

        builder.Append(StackTruncationSuffix);
        return builder.ToString();
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Client error recorded correlationId={CorrelationId} user={UserId} kind={Kind}")]
    private static partial void LogReportRecorded(
        ILogger logger,
        Guid correlationId,
        Guid userId,
        ClientErrorKind kind);

    private bool TryGetUserId(out Guid userId)
    {
        // Cookie auth writes ClaimTypes.NameIdentifier; raw JWTs carry the
        // subject as `sub`. Program.cs disables inbound claim mapping, so the
        // bearer-authenticated branch lands on `sub` only. Same pattern as
        // OnboardingController.TryGetUserId.
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrEmpty(raw) || !Guid.TryParse(raw, out userId))
        {
            userId = Guid.Empty;
            return false;
        }

        return true;
    }

    private ObjectResult MissingUserClaim() =>
        Problem(
            type: MissingUserType,
            title: "Authenticated user id was missing or malformed.",
            statusCode: StatusCodes.Status401Unauthorized);
}
