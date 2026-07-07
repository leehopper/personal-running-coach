using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Focused unit tests for <see cref="AnthropicSchemaSanitizer"/>: confirms each
/// forbidden keyword is stripped at every depth, non-validation keys survive,
/// the operation is idempotent, and dictionary materialization round-trips.
/// </summary>
/// <remarks>
/// <para>
/// The sanitizer mutates <see cref="JsonNode"/> trees in place, so every test
/// constructs a fresh input — we never share node instances across tests.
/// </para>
/// </remarks>
public sealed class AnthropicSchemaSanitizerTests
{
    [Theory]
    [InlineData("pattern")]
    [InlineData("format")]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("exclusiveMinimum")]
    [InlineData("exclusiveMaximum")]
    [InlineData("minLength")]
    [InlineData("maxLength")]
    [InlineData("minItems")]
    [InlineData("maxItems")]
    [InlineData("uniqueItems")]
    [InlineData("minProperties")]
    [InlineData("maxProperties")]
    [InlineData("oneOf")]
    [InlineData("allOf")]
    [InlineData("if")]
    [InlineData("then")]
    [InlineData("else")]
    [InlineData("not")]
    [InlineData("prefixItems")]
    public void Sanitize_RemovesEachForbiddenKeyword_AtRoot(string forbiddenKeyword)
    {
        // Arrange — fresh node per test (sanitizer mutates in place).
        var node = new JsonObject
        {
            [forbiddenKeyword] = "any-value",
            ["sentinel"] = "keep-me",
        };

        // Act
        var result = AnthropicSchemaSanitizer.Sanitize(node);

        // Assert
        result.Should().BeSameAs(node, "Sanitize returns the same node reference for fluent chaining");
        var resultObj = result.Should().BeOfType<JsonObject>().Subject;
        resultObj.ContainsKey(forbiddenKeyword).Should().BeFalse(
            $"'{forbiddenKeyword}' is in the forbidden-keyword set");
        resultObj.ContainsKey("sentinel").Should().BeTrue(
            "non-forbidden sibling keys must survive sanitization");
        resultObj["sentinel"]!.GetValue<string>().Should().Be("keep-me");
    }

    [Fact]
    public void Sanitize_RemovesForbiddenKeyword_NestedInPropertyValue()
    {
        // Arrange — `format: "email"` lives under properties.foo.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["foo"] = new JsonObject
                {
                    ["type"] = "string",
                    ["format"] = "email",
                },
            },
        };

        // Act
        AnthropicSchemaSanitizer.Sanitize(node);

        // Assert
        var properties = node["properties"].Should().BeOfType<JsonObject>().Subject;
        properties.ContainsKey("foo").Should().BeTrue("non-forbidden property key must survive");

        var foo = properties["foo"].Should().BeOfType<JsonObject>().Subject;
        foo.ContainsKey("format").Should().BeFalse("'format' must be stripped at any depth");
        foo.ContainsKey("type").Should().BeTrue("'type' must remain on the nested property");
        foo["type"]!.GetValue<string>().Should().Be("string");
    }

    [Fact]
    public void Sanitize_RemovesForbiddenKeyword_NestedInArrayItem()
    {
        // Arrange — array containing an object with a forbidden keyword.
        var nestedObject = new JsonObject
        {
            ["type"] = "string",
            ["minLength"] = 1,
        };
        var node = new JsonArray
        {
            nestedObject,
        };

        // Act
        AnthropicSchemaSanitizer.Sanitize(node);

        // Assert
        node.Count.Should().Be(1, "the object itself is not removed — only the forbidden keyword inside it");
        var item = node[0].Should().BeOfType<JsonObject>().Subject;
        item.ContainsKey("minLength").Should().BeFalse("'minLength' must be stripped from objects nested in arrays");
        item.ContainsKey("type").Should().BeTrue("non-forbidden keys inside array items must survive");
    }

    [Fact]
    public void Sanitize_PreservesNonForbiddenKeys()
    {
        // Arrange
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["name"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray { "name" },
            ["additionalProperties"] = false,
        };

        // Act
        AnthropicSchemaSanitizer.Sanitize(node);

        // Assert — all four non-validation keys survive intact.
        node.ContainsKey("type").Should().BeTrue();
        node.ContainsKey("properties").Should().BeTrue();
        node.ContainsKey("required").Should().BeTrue();
        node.ContainsKey("additionalProperties").Should().BeTrue();

        node["type"]!.GetValue<string>().Should().Be("object");
        node["additionalProperties"]!.GetValue<bool>().Should().BeFalse();

        var properties = node["properties"].Should().BeOfType<JsonObject>().Subject;
        properties.ContainsKey("name").Should().BeTrue();

        var required = node["required"].Should().BeOfType<JsonArray>().Subject;
        required.Count.Should().Be(1);
        required[0]!.GetValue<string>().Should().Be("name");
    }

    [Fact]
    public void Sanitize_IsIdempotent()
    {
        // Arrange — fresh nodes for the two passes (sanitizer mutates in place).
        var nodeOnePass = BuildIdempotencyFixture();
        var nodeTwoPasses = BuildIdempotencyFixture();

        // Act
        AnthropicSchemaSanitizer.Sanitize(nodeOnePass);

        AnthropicSchemaSanitizer.Sanitize(nodeTwoPasses);
        AnthropicSchemaSanitizer.Sanitize(nodeTwoPasses);

        // Assert — canonical-JSON serialization of the two trees must be byte-equal.
        var onePassBytes = JsonSerializer.SerializeToUtf8Bytes(nodeOnePass);
        var twoPassBytes = JsonSerializer.SerializeToUtf8Bytes(nodeTwoPasses);

        twoPassBytes.Should().Equal(
            onePassBytes,
            "running Sanitize twice must yield the same result as running it once");
    }

    [Fact]
    public void Sanitize_ReturnsNullPassthrough_ForNullInput()
    {
        // Act
        var act = () => AnthropicSchemaSanitizer.Sanitize(null);

        // Assert — does not throw and returns null.
        act.Should().NotThrow();
        AnthropicSchemaSanitizer.Sanitize(null).Should().BeNull();
    }

    [Fact]
    public void ToDictionary_Throws_ForNullNode()
    {
        // Act
        var act = () => AnthropicSchemaSanitizer.ToDictionary(null);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*null schema node*");
    }

    [Fact]
    public void ToDictionary_RoundTripsObject()
    {
        // Arrange — small valid object with one forbidden key to confirm sanitization happens
        // before materialization, plus a couple of structural keys we expect to round-trip.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["name"] = new JsonObject
                {
                    ["type"] = "string",
                    ["maxLength"] = 32,
                },
            },
            ["required"] = new JsonArray { "name" },
            ["additionalProperties"] = false,
        };

        // Act
        var dict = AnthropicSchemaSanitizer.ToDictionary(node);

        // Assert — expected keys present at the root.
        dict.Should().ContainKey("type");
        dict.Should().ContainKey("properties");
        dict.Should().ContainKey("required");
        dict.Should().ContainKey("additionalProperties");

        dict["type"].GetString().Should().Be("object");
        dict["additionalProperties"].GetBoolean().Should().BeFalse();

        // And the forbidden 'maxLength' deep inside must have been stripped before materialization.
        var serialized = JsonSerializer.Serialize(dict);
        serialized.Should().NotContain("\"maxLength\":");
        serialized.Should().Contain("\"name\":");
    }

    [Fact]
    public void ResolveReferences_LeavesARefFreeSchemaUntouched()
    {
        // Arrange — no $ref anywhere.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["name"] = new JsonObject { ["type"] = "string" } },
        };

        // Act
        var result = AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert — the original reference is returned, so ref-free schemas stay byte-stable.
        result.Should().BeSameAs(node);
    }

    [Fact]
    public void ResolveReferences_InlinesALocalRefIntoACopyOfItsTarget()
    {
        // Arrange — `mirror` references `primary` the way JsonSchemaExporter refs a
        // type used twice (e.g. WorkoutOutput in two adaptation slots).
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["primary"] = new JsonObject { ["type"] = "object", ["title"] = "Primary" },
                ["mirror"] = new JsonObject { ["$ref"] = "#/properties/primary" },
            },
        };

        // Act
        var result = AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert — `mirror` is now a full inline copy, no $ref survives.
        var properties = result.Should().BeOfType<JsonObject>().Subject["properties"]!.AsObject();
        var mirror = properties["mirror"]!.AsObject();
        mirror.ContainsKey("$ref").Should().BeFalse("Anthropic rejects a $ref into #/properties");
        mirror["title"]!.GetValue<string>().Should().Be("Primary", "the referenced target was inlined");
    }

    [Fact]
    public void ResolveReferences_SiblingKeywordsWinOverTheInlinedTarget()
    {
        // Arrange — the $ref node carries its own description (the way an injected
        // [Description] sibling rides a JsonSchemaExporter $ref); the target has a
        // competing description that must lose.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["primary"] = new JsonObject
                {
                    ["type"] = "object",
                    ["title"] = "Primary",
                    ["description"] = "original",
                },
                ["mirror"] = new JsonObject
                {
                    ["$ref"] = "#/properties/primary",
                    ["description"] = "overridden",
                },
            },
        };

        // Act
        var result = AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert — siblings win; non-conflicting target keys are still carried.
        var mirror = result!.AsObject()["properties"]!.AsObject()["mirror"]!.AsObject();
        mirror.ContainsKey("$ref").Should().BeFalse();
        mirror["description"]!.GetValue<string>().Should().Be(
            "overridden", "a keyword on the $ref node overrides the same keyword on the inlined target");
        mirror["title"]!.GetValue<string>().Should().Be("Primary");
    }

    [Fact]
    public void ResolveReferences_ResolvesARefNestedInsideASiblingValue()
    {
        // Arrange — the sibling's value itself contains a $ref, which must be
        // resolved rather than copied verbatim into the inlined object.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["primary"] = new JsonObject { ["type"] = "object", ["title"] = "Primary" },
                ["other"] = new JsonObject { ["type"] = "string", ["title"] = "Other" },
                ["mirror"] = new JsonObject
                {
                    ["$ref"] = "#/properties/primary",
                    ["items"] = new JsonObject { ["$ref"] = "#/properties/other" },
                },
            },
        };

        // Act
        var result = AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert — the materialized schema is reference-free and the nested ref was inlined.
        JsonSerializer.Serialize(result).Should().NotContain("\"$ref\":");
        var items = result!.AsObject()["properties"]!.AsObject()["mirror"]!.AsObject()["items"]!.AsObject();
        items["title"]!.GetValue<string>().Should().Be("Other");
    }

    [Fact]
    public void ResolveReferences_ResolvesAnArrayIndexPointerSegment()
    {
        // Arrange — a pointer through an array index, the shape an anyOf-wrapped
        // nullable type produces.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["anyOf"] = new JsonArray
            {
                new JsonObject { ["type"] = "string", ["title"] = "First" },
            },
            ["properties"] = new JsonObject
            {
                ["choice"] = new JsonObject { ["$ref"] = "#/anyOf/0" },
            },
        };

        // Act
        var result = AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert
        var choice = result!.AsObject()["properties"]!.AsObject()["choice"]!.AsObject();
        choice.ContainsKey("$ref").Should().BeFalse();
        choice["title"]!.GetValue<string>().Should().Be("First", "the array-index segment must resolve");
    }

    [Theory]
    [InlineData("a/b", "#/properties/a~1b", "Slash")]
    [InlineData("a~b", "#/properties/a~0b", "Tilde")]
    public void ResolveReferences_UnescapesJsonPointerEscapeSequences(
        string propertyName, string refPointer, string expectedTitle)
    {
        // Arrange — RFC 6901 escaping: ~1 -> /, ~0 -> ~ (in that order).
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                [propertyName] = new JsonObject { ["type"] = "string", ["title"] = expectedTitle },
                ["mirror"] = new JsonObject { ["$ref"] = refPointer },
            },
        };

        // Act
        var result = AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert
        var mirror = result!.AsObject()["properties"]!.AsObject()["mirror"]!.AsObject();
        mirror["title"]!.GetValue<string>().Should().Be(expectedTitle);
    }

    [Theory]
    [InlineData("#/properties/missing")]
    [InlineData("https://example.com/schema.json#/Foo")]
    public void ResolveReferences_ThrowsOnAnUnresolvablePointer(string refPointer)
    {
        // Arrange — a dangling local pointer and an external pointer are both unresolvable.
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["broken"] = new JsonObject { ["$ref"] = refPointer },
            },
        };

        // Act
        var act = () => AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Unresolvable $ref*");
    }

    [Fact]
    public void ResolveReferences_ThrowsOnARecursiveRef()
    {
        // Arrange — a self-referential schema cannot be inlined (or expressed for
        // constrained decoding).
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["self"] = new JsonObject { ["$ref"] = "#" } },
        };

        // Act
        var act = () => AnthropicSchemaSanitizer.ResolveReferences(node);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*Recursive $ref*");
    }

    [Fact]
    public void ToDictionary_InlinesRefs_SoTheMaterializedSchemaIsReferenceFree()
    {
        // Arrange
        var node = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["primary"] = new JsonObject { ["type"] = "object", ["title"] = "Primary" },
                ["mirror"] = new JsonObject { ["$ref"] = "#/properties/primary" },
            },
        };

        // Act
        var dict = AnthropicSchemaSanitizer.ToDictionary(node);

        // Assert
        var serialized = JsonSerializer.Serialize(dict);
        serialized.Should().NotContain("\"$ref\":", "the shipped schema must be reference-free for Anthropic");
    }

    private static JsonObject BuildIdempotencyFixture()
    {
        // A reasonably representative schema fragment with forbidden keys at multiple depths.
        return new JsonObject
        {
            ["type"] = "object",
            ["minProperties"] = 1,
            ["properties"] = new JsonObject
            {
                ["email"] = new JsonObject
                {
                    ["type"] = "string",
                    ["format"] = "email",
                    ["maxLength"] = 254,
                },
                ["tags"] = new JsonObject
                {
                    ["type"] = "array",
                    ["uniqueItems"] = true,
                    ["items"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["pattern"] = "^[a-z]+$",
                    },
                },
            },
            ["required"] = new JsonArray { "email" },
            ["additionalProperties"] = false,
        };
    }
}
