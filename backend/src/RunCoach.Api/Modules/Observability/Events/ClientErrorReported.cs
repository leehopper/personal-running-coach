namespace RunCoach.Api.Modules.Observability;

/// <summary>
/// Marten event recording a single client-side error reported by the SPA's
/// top-level <c>AppErrorBoundary</c> (DEC-068) via
/// <c>POST /api/v1/client-errors</c>. Persisted as a one-event-per-stream
/// shape keyed on the boundary-generated <see cref="CorrelationId"/> so the
/// support workflow can locate the precise client-side incident by the same
/// id the user sees on the fallback card.
/// </summary>
/// <remarks>
/// <para>
/// The event is registered through
/// <c>MapEventTypeWithSchemaVersion&lt;ClientErrorReported&gt;(1)</c> at host
/// boot per DEC-067 so its <c>mt_events.type</c> column carries the
/// <c>client_error_reported_v1</c> tag from day one. A future shape change
/// lands an unambiguous V1 → current upcaster without a column-content
/// scan.
/// </para>
/// <para>
/// Multi-tenancy is enforced by Marten's <c>TenancyStyle.Conjoined</c>:
/// the per-user tenant id (the runner's user-id string) lands on the row's
/// <c>mt_events.tenant_id</c> column from the writing
/// <see cref="Marten.IDocumentSession.TenantId"/>. The event payload itself
/// does NOT carry a duplicate tenant field — the column is the canonical
/// store-of-truth and the body never disagrees with it.
/// </para>
/// <para>
/// Free-text fields (<see cref="Message"/>, <see cref="Stack"/>,
/// <see cref="ComponentStack"/>) are accepted verbatim from the caller; the
/// controller truncates <see cref="Stack"/> to 16 KB before append so
/// pathological frame counts cannot bloat the row. Sanitization of
/// prompt-injection-shaped payloads is out of scope here — the event is
/// never read by the LLM coaching pipeline.
/// </para>
/// </remarks>
/// <param name="CorrelationId">
/// Client-generated <c>crypto.randomUUID()</c> from the boundary's
/// <c>getDerivedStateFromError</c> path. Doubles as the stream id of the
/// one-event-per-stream shape — a duplicate POST under the same id is
/// rejected at append time by Marten's expected-version guard.
/// </param>
/// <param name="OccurredAt">
/// Wall-clock instant the error was captured client-side, in the client's
/// reckoning. Distinct from the row's server-side
/// <c>mt_events.timestamp</c> column (which carries Marten's
/// <c>DateTimeOffset.UtcNow</c> at append time).
/// </param>
/// <param name="Kind">
/// Capture-site classification: render (boundary fired),
/// window-error (top-level <c>error</c> listener), unhandled-rejection
/// (top-level <c>unhandledrejection</c> listener). The wire-format
/// hyphenated strings (e.g. <c>"window-error"</c>) are mapped to
/// <see cref="ClientErrorKind"/> via the JSON enum naming policy.
/// </param>
/// <param name="ErrorName">
/// The <c>Error.name</c> JS string (e.g. <c>"TypeError"</c>). Non-null,
/// caller-supplied; defense-in-depth only — the SPA's reporter writes a
/// stable string.
/// </param>
/// <param name="Message">The <c>Error.message</c> JS string.</param>
/// <param name="Stack">
/// The <c>Error.stack</c> JS string. Truncated server-side to 16 KB by
/// the controller before append; the truncation suffix appears verbatim
/// in the stored payload.
/// </param>
/// <param name="ComponentStack">
/// React's <c>componentDidCatch</c> component stack (null for non-render
/// kinds).
/// </param>
/// <param name="Url">Document URL at capture time (<c>location.href</c>).</param>
/// <param name="UserAgent">Browser user-agent string.</param>
/// <param name="AppVersion">
/// SPA's build version (<c>import.meta.env.VITE_APP_VERSION</c>). Pinning
/// this on the event makes it trivial to ignore reports from a stale
/// cached bundle after a deploy.
/// </param>
public sealed record ClientErrorReported(
    Guid CorrelationId,
    DateTimeOffset OccurredAt,
    ClientErrorKind Kind,
    string ErrorName,
    string Message,
    string Stack,
    string? ComponentStack,
    string Url,
    string UserAgent,
    string AppVersion);
