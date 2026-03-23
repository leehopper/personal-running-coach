using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Generates JSON schemas from structured output record types using
/// .NET's built-in <see cref="JsonSchemaExporter"/>. Injects
/// <c>additionalProperties: false</c> on every object node via
/// <see cref="JsonSchemaExporterOptions.TransformSchemaNode"/> so
/// that Anthropic constrained decoding rejects unexpected properties.
/// Also injects <c>description</c> from <see cref="DescriptionAttribute"/> attributes.
/// </summary>
public static class JsonSchemaHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// Generates a JSON schema for the specified type <typeparamref name="T"/>,
    /// injecting <c>additionalProperties: false</c> on all object nodes
    /// and <c>description</c> from <see cref="DescriptionAttribute"/> attributes.
    /// </summary>
    /// <typeparam name="T">The structured output record type to generate a schema for.</typeparam>
    /// <returns>A <see cref="JsonNode"/> representing the JSON schema.</returns>
    public static JsonNode GenerateSchema<T>()
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) =>
            {
                if (node is not JsonObject obj)
                {
                    return node;
                }

                // Inject additionalProperties: false on all object nodes.
                if (obj.ContainsKey("properties"))
                {
                    obj["additionalProperties"] = false;
                }

                // Inject description from DescriptionAttribute.
                var descAttr = GetDescriptionAttribute(context);
                if (descAttr is not null)
                {
                    obj["description"] = descAttr.Description;
                }

                return node;
            },
        };

        return SerializerOptions.GetJsonSchemaAsNode(typeof(T), exporterOptions);
    }

    /// <summary>
    /// Generates a JSON schema string for the specified type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The structured output record type to generate a schema for.</typeparam>
    /// <returns>A JSON string representing the schema, formatted with indentation.</returns>
    public static string GenerateSchemaString<T>()
    {
        var schemaNode = GenerateSchema<T>();
        return schemaNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static DescriptionAttribute? GetDescriptionAttribute(
        JsonSchemaExporterContext context)
    {
        // Check property-level attributes first (most common case).
        if (context.PropertyInfo?.AttributeProvider is { } propertyProvider)
        {
            var attrs = propertyProvider.GetCustomAttributes(
                typeof(DescriptionAttribute),
                inherit: true);
            if (attrs.Length > 0)
            {
                return (DescriptionAttribute)attrs[0];
            }
        }

        // Fall back to type-level attributes.
        if (context.TypeInfo.Type.GetCustomAttributes(
                typeof(DescriptionAttribute),
                inherit: true)
            is { Length: > 0 } typeAttrs)
        {
            return (DescriptionAttribute)typeAttrs[0];
        }

        return null;
    }
}
