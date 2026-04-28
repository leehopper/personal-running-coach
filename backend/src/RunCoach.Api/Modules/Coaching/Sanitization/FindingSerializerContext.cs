using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Source-generated JSON context for the <c>runcoach.sanitization.findings</c>
/// span attribute payload. Source-gen avoids reflection at runtime and keeps
/// the trim warnings clean.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LayeredPromptSanitizer.FindingSummary))]
[JsonSerializable(typeof(List<LayeredPromptSanitizer.FindingSummary>))]
internal sealed partial class FindingSerializerContext : JsonSerializerContext;
