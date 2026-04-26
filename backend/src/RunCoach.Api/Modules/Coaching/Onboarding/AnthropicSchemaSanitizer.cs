using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Defense-in-depth post-processor that strips JSON Schema keywords Anthropic
/// rejects with HTTP 400 from a generated schema tree. Run on every schema
/// before it ships to the API so future <c>System.Text.Json.JsonSchemaExporter</c>
/// upgrades — or stray <c>[Description]</c>/attribute additions — cannot
/// silently regress to an unsupported shape.
/// </summary>
/// <remarks>
/// <para>
/// Per Slice 1 § Technical Considerations § Anthropic structured-output schema
/// design (R-067 / DEC-058): Anthropic's constrained-decoding pipeline ignores
/// the OpenAPI/JSON-Schema validation keywords below. The Anthropic SDK in
/// Python/TS/Ruby/PHP auto-strips them; the C# SDK 12.16+ does NOT. We strip
/// them ourselves so the workflow stays portable across SDK upgrades.
/// </para>
/// <para>
/// Stripped keywords: <c>pattern</c>, <c>format</c>, <c>minimum</c>, <c>maximum</c>,
/// <c>exclusiveMinimum</c>, <c>exclusiveMaximum</c>, <c>minLength</c>,
/// <c>maxLength</c>, <c>minItems</c>, <c>maxItems</c>, <c>uniqueItems</c>,
/// <c>minProperties</c>, <c>maxProperties</c>, <c>oneOf</c>, <c>allOf</c>,
/// <c>if</c>, <c>then</c>, <c>else</c>, <c>not</c>, <c>prefixItems</c>.
/// </para>
/// <para>
/// The sanitizer is idempotent: running it twice on the same input is a no-op
/// after the first pass.
/// </para>
/// </remarks>
public static class AnthropicSchemaSanitizer
{
    /// <summary>
    /// Gets jSON Schema keywords Anthropic rejects. Anthropic constrained decoding
    /// returns HTTP 400 with <c>invalid_request_error</c> when any of these
    /// appear in <c>output_config.format.schema</c>.
    /// </summary>
    public static IReadOnlySet<string> ForbiddenKeywords { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "pattern",
        "format",
        "minimum",
        "maximum",
        "exclusiveMinimum",
        "exclusiveMaximum",
        "minLength",
        "maxLength",
        "minItems",
        "maxItems",
        "uniqueItems",
        "minProperties",
        "maxProperties",
        "oneOf",
        "allOf",
        "if",
        "then",
        "else",
        "not",
        "prefixItems",
    };

    /// <summary>
    /// Recursively strips forbidden keywords from a parsed schema node tree.
    /// Mutates the node in place. Returns the same node for fluent chaining.
    /// </summary>
    /// <param name="node">The root schema node.</param>
    /// <returns>The sanitized node (same reference as <paramref name="node"/>).</returns>
    public static JsonNode? Sanitize(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            // Snapshot keys first so we can mutate the dictionary while
            // iterating. Forbidden keys are removed regardless of their value
            // shape; they are validation-only and never structural.
            var keysToRemove = obj
                .Where(kvp => ForbiddenKeywords.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                obj.Remove(key);
            }

            // Recurse into surviving children.
            foreach (var kvp in obj)
            {
                Sanitize(kvp.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                Sanitize(item);
            }
        }

        return node;
    }

    /// <summary>
    /// Materializes a sanitized <see cref="IReadOnlyDictionary{TKey, TValue}"/>
    /// of <see cref="JsonElement"/> values from a schema node tree. The
    /// returned dictionary is the shape Anthropic <c>output_config.format.schema</c>
    /// accepts directly.
    /// </summary>
    /// <param name="node">The schema node (typically a <see cref="JsonObject"/> root).</param>
    /// <returns>A dictionary representation of the sanitized schema.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sanitized node cannot be materialized as a dictionary
    /// (e.g. the root is a primitive instead of an object).
    /// </exception>
    public static IReadOnlyDictionary<string, JsonElement> ToDictionary(JsonNode? node)
    {
        Sanitize(node);

        if (node is null)
        {
            throw new InvalidOperationException("Cannot materialize a null schema node to a dictionary.");
        }

        var json = node.ToJsonString();
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? throw new InvalidOperationException("Failed to deserialize sanitized schema to a dictionary.");
    }
}
