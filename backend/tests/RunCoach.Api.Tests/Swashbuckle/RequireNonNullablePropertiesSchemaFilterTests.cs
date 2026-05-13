using FluentAssertions;
using Microsoft.OpenApi;
using NSubstitute;
using RunCoach.Api.Swashbuckle;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RunCoach.Api.Tests.Swashbuckle;

/// <summary>
/// Unit coverage for <see cref="RequireNonNullablePropertiesSchemaFilter"/>.
/// Verifies that the filter promotes non-nullable properties into the OpenAPI
/// <c>required</c> array, that it leaves nullable properties out, and that
/// it no-ops on schemas without properties or on non-mutable
/// <see cref="IOpenApiSchema"/> shapes.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RequireNonNullablePropertiesSchemaFilterTests
{
    private static readonly string[] ExpectedPreAndPost = ["pre", "post"];

    private readonly RequireNonNullablePropertiesSchemaFilter _sut = new();

    [Fact]
    public void Apply_PromotesNonNullableProperty_IntoRequired()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        _sut.Apply(schema, BuildContext());

        schema.Required.Should().NotBeNull();
        schema.Required.Should().Contain("name");
    }

    [Fact]
    public void Apply_DoesNotPromoteNullableProperty_IntoRequired()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["maybe"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            },
        };

        _sut.Apply(schema, BuildContext());

        schema.Required.Should().NotBeNull();
        schema.Required.Should().NotContain("maybe");
    }

    [Fact]
    public void Apply_PreservesExistingRequiredEntries()
    {
        var schema = new OpenApiSchema
        {
            Required = new HashSet<string>(StringComparer.Ordinal) { "pre" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["pre"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["post"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        _sut.Apply(schema, BuildContext());

        schema.Required.Should().BeEquivalentTo(ExpectedPreAndPost);
    }

    [Fact]
    public void Apply_NoOps_WhenSchemaHasNoProperties()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };

        _sut.Apply(schema, BuildContext());

        schema.Required.Should().BeNull();
    }

    [Fact]
    public void Apply_NoOps_WhenSchemaHasEmptyPropertiesDictionary()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>(),
        };

        _sut.Apply(schema, BuildContext());

        schema.Required.Should().BeNull();
    }

    [Fact]
    public void Apply_ThrowsArgumentNullException_WhenSchemaIsNull()
    {
        var act = () => _sut.Apply(schema: null!, BuildContext());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_IsIdempotent_WhenInvokedTwice()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        _sut.Apply(schema, BuildContext());
        _sut.Apply(schema, BuildContext());

        schema.Required.Should().ContainSingle().Which.Should().Be("name");
    }

    private static SchemaFilterContext BuildContext(Type? type = null)
    {
        var schemaGenerator = Substitute.For<ISchemaGenerator>();
        var schemaRepository = new SchemaRepository();
        return new SchemaFilterContext(type ?? typeof(object), schemaGenerator, schemaRepository);
    }
}
