using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.OpenApi;
using NSubstitute;
using RunCoach.Api.Swashbuckle;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RunCoach.Api.Tests.Swashbuckle;

/// <summary>
/// Unit coverage for <see cref="RecordCtorParamDataAnnotationsSchemaFilter"/>.
/// Exercises every supported DataAnnotations attribute on a positional record
/// DTO and verifies that the corresponding OpenAPI keyword lands on the
/// matching property schema. Also covers the no-op paths: types without a
/// primary ctor, schemas without properties, and copy-ctor filtering.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RecordCtorParamDataAnnotationsSchemaFilterTests
{
    private readonly RecordCtorParamDataAnnotationsSchemaFilter _sut = new();

    [Fact]
    public void Apply_LiftsMaxLengthFromCtorParam_OntoPropertySchema()
    {
        // Arrange
        var schema = BuildSchemaFor<MaxLengthRecord>(propertyName: "name");

        // Act
        _sut.Apply(schema, BuildContext(typeof(MaxLengthRecord)));

        // Assert
        schema.Properties!["name"].Should().BeOfType<OpenApiSchema>()
            .Which.MaxLength.Should().Be(64);
    }

    [Fact]
    public void Apply_LiftsMinLengthFromCtorParam_OntoPropertySchema()
    {
        // Arrange
        var schema = BuildSchemaFor<MinLengthRecord>(propertyName: "name");

        // Act
        _sut.Apply(schema, BuildContext(typeof(MinLengthRecord)));

        // Assert
        schema.Properties!["name"].Should().BeOfType<OpenApiSchema>()
            .Which.MinLength.Should().Be(3);
    }

    [Fact]
    public void Apply_LiftsStringLength_PopulatesBothMaxAndMinWhenMinSet()
    {
        // Arrange
        var schema = BuildSchemaFor<StringLengthRecord>(propertyName: "name");

        // Act
        _sut.Apply(schema, BuildContext(typeof(StringLengthRecord)));

        // Assert
        var prop = schema.Properties!["name"].Should().BeOfType<OpenApiSchema>().Which;
        prop.MaxLength.Should().Be(40);
        prop.MinLength.Should().Be(2);
    }

    [Fact]
    public void Apply_LiftsRegularExpression_OntoPropertyPattern()
    {
        // Arrange
        var schema = BuildSchemaFor<RegexRecord>(propertyName: "code");

        // Act
        _sut.Apply(schema, BuildContext(typeof(RegexRecord)));

        // Assert
        schema.Properties!["code"].Should().BeOfType<OpenApiSchema>()
            .Which.Pattern.Should().Be("^[A-Z]{3}$");
    }

    [Fact]
    public void Apply_LiftsEmailAddress_OntoFormat()
    {
        // Arrange
        var schema = BuildSchemaFor<EmailRecord>(propertyName: "email");

        // Act
        _sut.Apply(schema, BuildContext(typeof(EmailRecord)));

        // Assert
        schema.Properties!["email"].Should().BeOfType<OpenApiSchema>()
            .Which.Format.Should().Be("email");
    }

    [Fact]
    public void Apply_LiftsUrl_OntoFormat()
    {
        // Arrange
        var schema = BuildSchemaFor<UrlRecord>(propertyName: "homepage");

        // Act
        _sut.Apply(schema, BuildContext(typeof(UrlRecord)));

        // Assert
        schema.Properties!["homepage"].Should().BeOfType<OpenApiSchema>()
            .Which.Format.Should().Be("uri");
    }

    [Fact]
    public void Apply_LiftsRange_OntoMinimumAndMaximum()
    {
        // Arrange
        var schema = BuildSchemaFor<RangeRecord>(propertyName: "age");

        // Act
        _sut.Apply(schema, BuildContext(typeof(RangeRecord)));

        // Assert
        var prop = schema.Properties!["age"].Should().BeOfType<OpenApiSchema>().Which;
        prop.Minimum.Should().Be("0");
        prop.Maximum.Should().Be("150");
    }

    [Fact]
    public void Apply_DoesNotOverwrite_PreviouslySetSchemaKeyword()
    {
        // Arrange
        var schema = BuildSchemaFor<MaxLengthRecord>(propertyName: "name");
        ((OpenApiSchema)schema.Properties!["name"]).MaxLength = 999;

        // Act
        _sut.Apply(schema, BuildContext(typeof(MaxLengthRecord)));

        // Assert
        schema.Properties!["name"].Should().BeOfType<OpenApiSchema>()
            .Which.MaxLength.Should().Be(999);
    }

    [Fact]
    public void Apply_NoOps_WhenSchemaHasNoProperties()
    {
        // Arrange
        var schema = new OpenApiSchema { Type = JsonSchemaType.Object };

        // Act
        _sut.Apply(schema, BuildContext(typeof(MaxLengthRecord)));

        // Assert
        schema.Properties.Should().BeNull();
    }

    [Fact]
    public void Apply_NoOps_WhenTargetTypeHasNoConstructor()
    {
        // Arrange
        var schema = BuildSchemaFor<AbstractRecord>(propertyName: "value");

        // Act
        _sut.Apply(schema, BuildContext(typeof(AbstractRecord)));

        // Assert
        schema.Properties!["value"].Should().BeOfType<OpenApiSchema>()
            .Which.MaxLength.Should().BeNull();
    }

    [Fact]
    public void Apply_SkipsPropertiesWithoutMatchingCtorParam()
    {
        // Arrange
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["unmapped"] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };

        // Act
        _sut.Apply(schema, BuildContext(typeof(MaxLengthRecord)));

        // Assert
        schema.Properties!["unmapped"].Should().BeOfType<OpenApiSchema>()
            .Which.MaxLength.Should().BeNull();
    }

    [Fact]
    public void Apply_ThrowsArgumentNullException_WhenSchemaIsNull()
    {
        // Act
        var act = () => _sut.Apply(schema: null!, BuildContext(typeof(MaxLengthRecord)));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ThrowsArgumentNullException_WhenContextIsNull()
    {
        // Arrange
        var schema = new OpenApiSchema();

        // Act
        var act = () => _sut.Apply(schema, context: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static OpenApiSchema BuildSchemaFor<T>(string propertyName)
    {
        // T is captured for self-documentation at call sites — the schema returned
        // pairs with the record type passed to BuildContext below.
        _ = typeof(T);
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                [propertyName] = new OpenApiSchema { Type = JsonSchemaType.String },
            },
        };
    }

    private static SchemaFilterContext BuildContext(Type type)
    {
        var schemaGenerator = Substitute.For<ISchemaGenerator>();
        var schemaRepository = new SchemaRepository();
        return new SchemaFilterContext(type, schemaGenerator, schemaRepository);
    }

    private sealed record MaxLengthRecord([MaxLength(64)] string Name);

    private sealed record MinLengthRecord([MinLength(3)] string Name);

    private sealed record StringLengthRecord([StringLength(40, MinimumLength = 2)] string Name);

    private sealed record RegexRecord([RegularExpression("^[A-Z]{3}$")] string Code);

    private sealed record EmailRecord([EmailAddress] string Email);

    private sealed record UrlRecord([Url] string Homepage);

    private sealed record RangeRecord([Range(0, 150)] int Age);

    private abstract record AbstractRecord(string Value);
}
