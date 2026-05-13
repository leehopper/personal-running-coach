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
        // Arrange
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        // Act
        _sut.Apply(schema, BuildContext());

        // Assert
        var actualRequired = schema.Required;
        actualRequired.Should().NotBeNull();
        actualRequired.Should().Contain("name");
    }

    [Fact]
    public void Apply_DoesNotPromoteNullableProperty_IntoRequired()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["maybe"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            },
        };

        // Act
        _sut.Apply(schema, BuildContext());

        // Assert
        var actualRequired = schema.Required;
        actualRequired.Should().NotBeNull();
        actualRequired.Should().NotContain("maybe");
    }

    [Fact]
    public void Apply_PreservesExistingRequiredEntries()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Required = new HashSet<string>(StringComparer.Ordinal) { "pre" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["pre"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["post"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        // Act
        _sut.Apply(schema, BuildContext());

        // Assert
        var actualRequired = schema.Required;
        actualRequired.Should().BeEquivalentTo(ExpectedPreAndPost);
    }

    [Theory]
    [InlineData(false)] // null Properties dict
    [InlineData(true)] // empty Properties dict
    public void Apply_NoOps_WhenSchemaHasNoMaterializedProperties(bool useEmptyDict)
    {
        // Arrange
        var schema = useEmptyDict
            ? new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>() }
            : new OpenApiSchema { Type = JsonSchemaType.String };

        // Act
        _sut.Apply(schema, BuildContext());

        // Assert
        schema.Required.Should().BeNull();
    }

    [Fact]
    public void Apply_ThrowsArgumentNullException_WhenSchemaIsNull()
    {
        // Arrange
        var act = () => _sut.Apply(schema: null!, BuildContext());

        // Act + Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_IsIdempotent_WhenInvokedTwice()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        // Act
        _sut.Apply(schema, BuildContext());
        _sut.Apply(schema, BuildContext());

        // Assert
        schema.Required.Should().ContainSingle().Which.Should().Be("name");
    }

    private static SchemaFilterContext BuildContext(Type? type = null)
    {
        var schemaGenerator = Substitute.For<ISchemaGenerator>();
        var schemaRepository = new SchemaRepository();
        return new SchemaFilterContext(type ?? typeof(object), schemaGenerator, schemaRepository);
    }
}
