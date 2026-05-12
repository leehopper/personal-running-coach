using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Observability.Models;

/// <summary>
/// Wire shape for <c>POST /api/v1/client-errors</c> per DEC-068. The SPA's
/// top-level <c>AppErrorBoundary</c> fallback (and the <c>useGlobalErrorReporter</c>
/// hook for <c>window.error</c> / <c>window.unhandledrejection</c>) POST a
/// JSON body of this shape after a render-time exception or unhandled
/// rejection.
/// </summary>
/// <param name="CorrelationId">
/// Client-generated <c>crypto.randomUUID()</c>. Used directly as the Marten
/// stream id of the one-event-per-stream shape so the support workflow can
/// locate a report by the same 8-char prefix shown on the fallback card.
/// </param>
/// <param name="OccurredAt">Client-clock instant the error was captured.</param>
/// <param name="Kind">
/// Capture-site classification — wire strings are <c>"render"</c>,
/// <c>"window-error"</c>, or <c>"unhandled-rejection"</c> per
/// <see cref="ClientErrorKind"/>'s <see cref="JsonStringEnumMemberNameAttribute"/>
/// values.
/// </param>
/// <param name="ErrorName">The <c>Error.name</c> JS string.</param>
/// <param name="Message">The <c>Error.message</c> JS string.</param>
/// <param name="Stack">
/// The <c>Error.stack</c> JS string. Truncated server-side to 16 KB before
/// the event is appended; pathological frame counts cannot bloat the
/// stored row.
/// </param>
/// <param name="ComponentStack">
/// React's <c>componentDidCatch</c> component stack. Nullable — only the
/// <see cref="ClientErrorKind.Render"/> capture path carries one.
/// </param>
/// <param name="Url">Document URL at capture time (<c>location.href</c>).</param>
/// <param name="UserAgent">Browser user-agent string.</param>
/// <param name="AppVersion">
/// SPA's build version (<c>import.meta.env.VITE_APP_VERSION</c>). Pinning
/// this on the report makes it trivial to ignore reports from a stale
/// cached bundle after a deploy.
/// </param>
public sealed record ClientErrorRequestDto(
    [property: JsonRequired] Guid CorrelationId,
    [property: JsonRequired] DateTimeOffset OccurredAt,
    [property: JsonRequired] ClientErrorKind Kind,
    [property: JsonRequired] string ErrorName,
    [property: JsonRequired] string Message,
    [property: JsonRequired] string Stack,
    string? ComponentStack,
    [property: JsonRequired] string Url,
    [property: JsonRequired] string UserAgent,
    [property: JsonRequired] string AppVersion);
