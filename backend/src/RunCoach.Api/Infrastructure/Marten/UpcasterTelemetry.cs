using System.Diagnostics;

namespace RunCoach.Api.Infrastructure.Marten;

/// <summary>
/// Single shared <see cref="ActivitySource"/> for Marten event-upcasting telemetry.
/// Every registered upcaster wraps its transform call in <see cref="TraceUpcast{T}"/>
/// so the OTel pipeline (configured to <c>AddSource</c> the
/// <see cref="SourceName"/> value alongside <c>"Marten"</c> in
/// <see cref="Program"/>) emits an <c>upcast.&lt;event_type&gt;</c> span with
/// <c>from_type</c> / <c>to_type</c> tags. Without this seam, upcaster
/// invocations are invisible to Jaeger and silent corruption (wrong-shape
/// rows surviving a missing <c>MapEventTypeWithSchemaVersion</c> call) cannot
/// be alerted on (DEC-067 / R-072 §9).
/// </summary>
public static class UpcasterTelemetry
{
    /// <summary>
    /// The <see cref="ActivitySource.Name"/> the OTel pipeline subscribes to.
    /// Kept as a public constant so <see cref="Program"/> and any future
    /// dashboarding code reference the same literal.
    /// </summary>
    public const string SourceName = "RunCoach.Marten.Upcaster";

    /// <summary>
    /// Process-wide <see cref="ActivitySource"/> used by every registered
    /// upcaster lambda. Static so dependency injection is not required —
    /// upcaster delegates run inside Marten's deserialization path which
    /// holds no DI scope.
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName);

    /// <summary>
    /// Runs <paramref name="upcast"/>, wrapping the call in a single
    /// <c>upcast.&lt;event_type&gt;</c> span with <c>from_type</c> /
    /// <c>to_type</c> tags. The span name uses the PascalCase CLR type name
    /// of <typeparamref name="TNew"/> (e.g. <c>upcast.OnboardingStarted</c>)
    /// so trace filtering can scope to a single event family.
    /// </summary>
    /// <typeparam name="TOld">The legacy CLR record type Marten reads from the row's <c>mt_dotnet_type</c>.</typeparam>
    /// <typeparam name="TNew">The current CLR record type the projection apply path expects.</typeparam>
    /// <param name="oldEvent">The deserialized legacy event Marten hands to the upcaster.</param>
    /// <param name="upcast">Pure transformation from the legacy shape to the current shape.</param>
    /// <returns>The current-shape event the projection will apply.</returns>
    public static TNew TraceUpcast<TOld, TNew>(TOld oldEvent, Func<TOld, TNew> upcast)
    {
        var fromType = typeof(TOld).FullName ?? typeof(TOld).Name;
        var toType = typeof(TNew).FullName ?? typeof(TNew).Name;

        using var activity = Source.StartActivity(
            $"upcast.{typeof(TNew).Name}",
            ActivityKind.Internal);
        activity?.SetTag("from_type", fromType);
        activity?.SetTag("to_type", toType);

        try
        {
            var result = upcast(oldEvent);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            throw;
        }
    }
}
