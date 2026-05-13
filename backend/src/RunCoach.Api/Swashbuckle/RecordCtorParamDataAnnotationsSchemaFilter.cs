using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RunCoach.Api.Swashbuckle;

/// <summary>
/// Reads DataAnnotations attributes from the primary constructor parameters of
/// a C# record (positional ctor form) and applies the corresponding OpenAPI
/// keywords to the matching property schemas. Required because ASP.NET Core's
/// model-binding validation pipeline insists DataAnnotations on positional
/// record DTOs live on the ctor parameter (and refuses to start with
/// validation metadata on the synthesized property), while Swashbuckle's
/// built-in <c>DataAnnotationsSchemaFilter</c> only inspects properties — so
/// without this bridge the swagger output is missing every <c>maxLength</c>,
/// <c>minLength</c>, <c>format</c>, and <c>pattern</c> constraint on every
/// positional-record DTO in the API surface.
/// </summary>
/// <remarks>
/// <para>
/// The frontend codegen pipeline (DEC-066 / R-071) consumes
/// <c>backend/openapi/swagger.json</c> as the source of truth for Zod
/// validators. Without ctor-param attribute lifting, the generated Zod
/// schemas degrade to bare <c>z.string()</c> everywhere, masking exactly the
/// rename / drop / drift class of bug the slice exists to surface.
/// </para>
/// <para>
/// Matching strategy: the filter walks <c>schema.Properties</c> (which
/// Swashbuckle keys by camelCased property name) and looks up the
/// PascalCased CLR property on <see cref="SchemaFilterContext.Type"/>. The
/// CLR property's name is then matched against the primary ctor parameter
/// names; a hit copies every supported DataAnnotations attribute onto the
/// child schema. Types without a primary ctor (or whose ctor params don't
/// carry DataAnnotations) are no-ops.
/// </para>
/// <para>
/// Supported attributes — the union of Swashbuckle's built-in
/// <c>DataAnnotationsSchemaFilter</c> coverage and what the codegen surface
/// needs: <see cref="MaxLengthAttribute"/>, <see cref="MinLengthAttribute"/>,
/// <see cref="StringLengthAttribute"/>, <see cref="EmailAddressAttribute"/>,
/// <see cref="UrlAttribute"/>, <see cref="RegularExpressionAttribute"/>,
/// <see cref="RangeAttribute"/>. <c>[Required]</c> is intentionally NOT
/// processed here — the sibling
/// <see cref="RequireNonNullablePropertiesSchemaFilter"/> owns the OpenAPI
/// <c>required</c> array via the non-nullable-reference-type signal, which
/// is strictly broader than <c>[Required]</c>.
/// </para>
/// <para>
/// The filter only mutates fields that Swashbuckle left unset, so it does
/// not stomp explicitly authored property-level attributes on hybrid record
/// types that carry both forms.
/// </para>
/// </remarks>
public sealed class RecordCtorParamDataAnnotationsSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (schema is not OpenApiSchema concrete || concrete.Properties is null || concrete.Properties.Count == 0)
        {
            return;
        }

        // The primary constructor of a positional record is the
        // longest-arity public ctor whose parameters line up 1:1 with the
        // record's synthesized init-only properties by name. Records also
        // emit a copy ctor (single parameter of the record type itself) —
        // skip that one.
        var ctor = FindPrimaryConstructor(context.Type);
        if (ctor is null)
        {
            return;
        }

        var paramsByName = ctor
            .GetParameters()
            .ToDictionary(p => p.Name ?? string.Empty, StringComparer.Ordinal);

        foreach (var (jsonPropertyName, propertyValue) in concrete.Properties)
        {
            if (propertyValue is not OpenApiSchema propertySchema)
            {
                continue;
            }

            // Swashbuckle keys property names by JSON casing (camelCase by
            // default). Map back to the CLR property name (PascalCase) so we
            // can match against the ctor parameter set.
            var clrName = ToPascalCase(jsonPropertyName);
            if (!paramsByName.TryGetValue(clrName, out var parameter))
            {
                continue;
            }

            ApplyAttributesToSchema(parameter, propertySchema);
        }
    }

    private static ConstructorInfo? FindPrimaryConstructor(Type type)
    {
        if (!type.IsClass && !type.IsValueType)
        {
            return null;
        }

        // Records and record structs expose a public primary constructor.
        // The compiler-emitted copy constructor for records takes a single
        // parameter of the record type itself; filter it out.
        ConstructorInfo? best = null;
        foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == type)
            {
                continue;
            }

            if (best is null || parameters.Length > best.GetParameters().Length)
            {
                best = ctor;
            }
        }

        return best;
    }

    private static string ToPascalCase(string camelCase)
    {
        if (string.IsNullOrEmpty(camelCase))
        {
            return camelCase;
        }

        return char.ToUpperInvariant(camelCase[0]) + camelCase[1..];
    }

    private static void ApplyAttributesToSchema(ParameterInfo parameter, OpenApiSchema schema)
    {
        foreach (var attr in parameter.GetCustomAttributes(inherit: false))
        {
            switch (attr)
            {
                case EmailAddressAttribute:
                    schema.Format ??= "email";
                    break;

                case UrlAttribute:
                    schema.Format ??= "uri";
                    break;

                case RegularExpressionAttribute regex:
                    schema.Pattern ??= regex.Pattern;
                    break;

                case StringLengthAttribute stringLength:
                    schema.MaxLength ??= stringLength.MaximumLength;
                    if (stringLength.MinimumLength > 0)
                    {
                        schema.MinLength ??= stringLength.MinimumLength;
                    }

                    break;

                case MaxLengthAttribute maxLength when maxLength.Length >= 0:
                    schema.MaxLength ??= maxLength.Length;
                    break;

                case MinLengthAttribute minLength when minLength.Length >= 0:
                    schema.MinLength ??= minLength.Length;
                    break;

                case RangeAttribute range:
                    ApplyRange(range, schema);
                    break;

                // [Required] is owned by RequireNonNullablePropertiesSchemaFilter.
                // Other attributes (e.g. [Phone], [CreditCard], [Compare]) are
                // intentionally not surfaced — none of the wire DTOs use them
                // today, and silent partial coverage would mislead consumers.
                default:
                    break;
            }
        }
    }

    private static void ApplyRange(RangeAttribute range, OpenApiSchema schema)
    {
        // Range carries Minimum/Maximum as plain object boxes typed as
        // double or int depending on the constructor used. Convert to
        // decimal for the OpenAPI wire shape — Microsoft.OpenApi 2.x exposes
        // Minimum/Maximum on OpenApiSchema as nullable strings (number
        // strings) for OpenAPI 3.1 compatibility, but accepts decimal
        // assignments via implicit conversion.
        if (range.Minimum is IConvertible minConvertible)
        {
            schema.Minimum ??= Convert.ToDecimal(minConvertible, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (range.Maximum is IConvertible maxConvertible)
        {
            schema.Maximum ??= Convert.ToDecimal(maxConvertible, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
