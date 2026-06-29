using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RunCoach.Api.Swashbuckle;

/// <summary>
/// Adds an <see cref="OpenApiSecurityRequirement"/> to every operation whose
/// controller or action carries <see cref="IAuthorizeData"/>, referencing the
/// <c>cookieAuth</c> and <c>bearerAuth</c> security schemes registered in
/// <c>Program.cs</c>. Operations decorated with <see cref="IAllowAnonymous"/>
/// are skipped unconditionally so anonymous endpoints (e.g. login, register)
/// emit <c>security: []</c> rather than a populated requirement. Operations
/// with neither attribute are also left unmodified.
/// </summary>
internal sealed class AuthorizeOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        // `CustomAttributes()` is the Swashbuckle-documented extension method
        // on `ApiDescription` that yields the union of controller-level and
        // action-level attributes. It reads from
        // `ApiDescription.ActionDescriptor.EndpointMetadata` under the hood,
        // so it covers both controller-level and action-level decorations
        // without requiring two separate attribute walks.
        var attributes = context.ApiDescription.CustomAttributes();

        if (attributes.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        if (!attributes.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        // In Microsoft.OpenApi 2.x, `OpenApiSecurityRequirement` uses
        // `OpenApiSecuritySchemeReference` as the key type. The reference must be
        // bound to the host `OpenApiDocument` (the second constructor argument) so
        // the serializer can resolve the scheme name from the document's
        // `components/securitySchemes` section and emit the expected
        // `{"cookieAuth": []}` JSON shape. Without the host document, the reference
        // cannot be resolved and serializes as `{}`.
        //
        // The endpoints satisfy the `CookieOrBearer` policy — EITHER scheme is
        // sufficient. In OpenAPI, schemes listed inside ONE requirement object are
        // AND-ed, while SEPARATE requirement objects are OR-ed. Emit two distinct
        // requirements so generated clients model cookie-or-bearer correctly rather
        // than treating both as mandatory.
        var document = context.Document;
        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("cookieAuth", document)] = [],
        });
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearerAuth", document)] = [],
        });
    }
}
