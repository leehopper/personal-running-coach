using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RunCoach.Api.Swashbuckle;

/// <summary>
/// Promotes every non-nullable C# reference-type property on a schema into the
/// OpenAPI <c>required</c> array. Paired with
/// <c>SwaggerGenOptions.SupportNonNullableReferenceTypes()</c>, this collapses
/// the default Swashbuckle behaviour of leaving non-nullable C# properties
/// out of <c>required</c>, which the frontend Zod codegen would then mark
/// <c>.nullish()</c> everywhere — masking the rename/drop drift this slice
/// exists to surface (DEC-066 / R-071 §3, Sebastian Chwastek's pattern).
/// </summary>
/// <remarks>
/// <para>
/// Microsoft.OpenApi 2.x (the dep of Swashbuckle 10.1.x) re-models OpenAPI
/// schemas around OpenAPI 3.1: there is no <c>Nullable: bool</c> property
/// anymore; nullability is encoded as the <see cref="JsonSchemaType.Null"/>
/// flag on <see cref="IOpenApiSchema.Type"/>. The filter checks each
/// property's <c>Type</c> for the <c>Null</c> flag and, when absent, adds
/// the property name to the parent schema's <see cref="OpenApiSchema.Required"/>
/// set.
/// </para>
/// <para>
/// Swashbuckle hands the filter <see cref="IOpenApiSchema"/>, whose surface
/// is read-only; the runtime instance is the concrete
/// <see cref="OpenApiSchema"/> with writable setters. The filter casts to
/// the concrete type before mutating <c>Required</c>; non-<c>OpenApiSchema</c>
/// implementations (e.g. <c>OpenApiSchemaReference</c>) are skipped because
/// their backing object's mutations should land on the referenced target,
/// not the reference.
/// </para>
/// <para>
/// The filter is idempotent — duplicate insertions into the required set
/// are guarded by <see cref="ISet{T}.Add"/> semantics.
/// </para>
/// </remarks>
public sealed class RequireNonNullablePropertiesSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return;
        }

        if (schema is not OpenApiSchema concrete || concrete.Properties is null)
        {
            return;
        }

        concrete.Required ??= new HashSet<string>(StringComparer.Ordinal);

        foreach (var (propertyName, propertySchema) in concrete.Properties)
        {
            if (!IsNullable(propertySchema))
            {
                concrete.Required.Add(propertyName);
            }
        }
    }

    private static bool IsNullable(IOpenApiSchema schema)
    {
        // OpenAPI 3.1 encodes nullability as the `Null` flag on the Type
        // union. Swashbuckle's `SupportNonNullableReferenceTypes()` walks
        // C# nullable annotations and toggles this flag accordingly. When
        // the property is a `$ref` (composed via `OneOf`/`AnyOf` to keep
        // 3.0 emission valid), Swashbuckle places `Null` on the wrapper
        // schema's `Type`, so a single check on the immediate child
        // suffices.
        var type = schema.Type;
        return type.HasValue && (type.Value & JsonSchemaType.Null) != 0;
    }
}
