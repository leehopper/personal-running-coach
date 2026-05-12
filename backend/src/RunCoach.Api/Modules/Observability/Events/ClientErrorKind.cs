using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Observability;

/// <summary>
/// Capture-site classification on a <see cref="ClientErrorReported"/> event.
/// Three values, matched 1:1 to the SPA reporter's three call sites in
/// DEC-068: the <c>react-error-boundary</c> fallback (<see cref="Render"/>),
/// the top-level <c>window.error</c> listener (<see cref="WindowError"/>),
/// and the top-level <c>window.unhandledrejection</c> listener
/// (<see cref="UnhandledRejection"/>).
/// </summary>
/// <remarks>
/// Wire-format strings are hyphenated: <c>"render"</c>, <c>"window-error"</c>,
/// <c>"unhandled-rejection"</c> — pinned via
/// <see cref="JsonStringEnumMemberNameAttribute"/> so the enum's CLR identifiers
/// stay PascalCase while the on-the-wire format matches the SPA reporter's
/// kebab-case contract from DEC-068. The enum is serialized as a string via the
/// <see cref="JsonConverterAttribute"/> on the type so consumers do not need to
/// register the converter globally.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ClientErrorKind>))]
public enum ClientErrorKind
{
    /// <summary>
    /// React render-phase error caught by the <c>react-error-boundary</c>
    /// fallback. Carries a non-null component stack.
    /// </summary>
    [JsonStringEnumMemberName("render")]
    Render = 0,

    /// <summary>
    /// Top-level <c>window.addEventListener('error', ...)</c> capture.
    /// </summary>
    [JsonStringEnumMemberName("window-error")]
    WindowError = 1,

    /// <summary>
    /// Top-level <c>window.addEventListener('unhandledrejection', ...)</c> capture.
    /// </summary>
    [JsonStringEnumMemberName("unhandled-rejection")]
    UnhandledRejection = 2,
}
