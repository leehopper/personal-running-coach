using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RunCoach.Api.Modules.Coaching;

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
    /// Gets the JSON Schema keywords Anthropic rejects. Anthropic constrained decoding
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
    /// Resolves every local <c>$ref</c> by inlining the referenced subschema, so
    /// the shipped schema contains no references. <c>System.Text.Json.JsonSchemaExporter</c>
    /// emits a <c>$ref</c> with a JSON Pointer into <c>#/properties/...</c> for any
    /// type used more than once (e.g. <c>WorkoutOutput</c> in two adaptation slots),
    /// but Anthropic constrained decoding rejects references that are not under
    /// <c>$defs</c>/<c>definitions</c> with HTTP 400 (<c>invalid_request_error</c>).
    /// Inlining sidesteps that entirely. Returns the original node untouched when it
    /// contains no <c>$ref</c> (the common case), so ref-free schemas — and their
    /// recorded eval-cache keys — are byte-stable.
    /// </summary>
    /// <param name="root">The root schema node.</param>
    /// <returns>A reference-free schema node (the original node if it had no refs).</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a <c>$ref</c> is unresolvable or recursive (a recursive schema
    /// cannot be expressed for constrained decoding).
    /// </exception>
    public static JsonNode? ResolveReferences(JsonNode? root)
    {
        if (root is null || !ContainsRef(root))
        {
            return root;
        }

        var rootObject = root as JsonObject
            ?? throw new InvalidOperationException("Cannot resolve $ref against a non-object schema root.");

        return ResolveNode(root, rootObject, new HashSet<string>(StringComparer.Ordinal));
    }

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
        var resolved = ResolveReferences(node);
        Sanitize(resolved);

        if (resolved is null)
        {
            throw new InvalidOperationException("Cannot materialize a null schema node to a dictionary.");
        }

        return resolved.Deserialize<Dictionary<string, JsonElement>>()
            ?? throw new InvalidOperationException("Failed to deserialize sanitized schema to a dictionary.");
    }

    private static bool ContainsRef(JsonNode? node) => node switch
    {
        JsonObject obj => obj.ContainsKey("$ref") || obj.Any(kvp => ContainsRef(kvp.Value)),
        JsonArray array => array.Any(ContainsRef),
        _ => false,
    };

    private static JsonNode? ResolveNode(JsonNode? node, JsonObject root, HashSet<string> activePointers)
    {
        switch (node)
        {
            case JsonObject obj when TryGetRefPointer(obj, out var pointer):
                if (!activePointers.Add(pointer))
                {
                    throw new InvalidOperationException(
                        $"Recursive $ref '{pointer}' cannot be inlined for Anthropic constrained decoding.");
                }

                var target = ResolvePointer(root, pointer)
                    ?? throw new InvalidOperationException($"Unresolvable $ref '{pointer}' in schema.");

                // ResolveNode is a pure rebuilder — it never mutates its input —
                // so the still-attached target can be passed directly.
                var inlined = ResolveNode(target, root, activePointers);
                activePointers.Remove(pointer);

                // Carry any sibling keywords on the $ref node (e.g. an injected
                // description) onto the inlined object — siblings win.
                if (inlined is JsonObject inlinedObject)
                {
                    foreach (var sibling in obj.Where(kvp => kvp.Key != "$ref"))
                    {
                        inlinedObject[sibling.Key] = ResolveNode(sibling.Value, root, activePointers);
                    }
                }

                return inlined;

            case JsonObject obj:
                var rebuilt = new JsonObject();
                foreach (var kvp in obj)
                {
                    rebuilt[kvp.Key] = ResolveNode(kvp.Value, root, activePointers);
                }

                return rebuilt;

            case JsonArray array:
                var rebuiltArray = new JsonArray();
                foreach (var item in array)
                {
                    rebuiltArray.Add(ResolveNode(item, root, activePointers));
                }

                return rebuiltArray;

            default:
                return node?.DeepClone();
        }
    }

    private static bool TryGetRefPointer(JsonObject obj, out string pointer)
    {
        if (obj.TryGetPropertyValue("$ref", out var refNode)
            && refNode is JsonValue refValue
            && refValue.TryGetValue(out string? value)
            && value is not null)
        {
            pointer = value;
            return true;
        }

        pointer = string.Empty;
        return false;
    }

    private static JsonNode? ResolvePointer(JsonObject root, string pointer)
    {
        if (pointer is "#" or "#/")
        {
            return root;
        }

        if (!pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            // Only local pointers are supported; an external/relative ref is unresolvable here.
            return null;
        }

        JsonNode? current = root;
        foreach (var rawSegment in pointer[2..].Split('/'))
        {
            // JSON Pointer unescaping: ~1 -> /, ~0 -> ~ (order matters).
            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            switch (current)
            {
                case JsonObject obj when obj.TryGetPropertyValue(segment, out var child):
                    current = child;
                    break;
                case JsonArray array when int.TryParse(segment, out var index) && index >= 0 && index < array.Count:
                    current = array[index];
                    break;
                default:
                    return null;
            }
        }

        return current;
    }
}
