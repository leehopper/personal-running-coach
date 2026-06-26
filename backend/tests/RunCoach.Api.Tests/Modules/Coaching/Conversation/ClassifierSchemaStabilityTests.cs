using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Conversation;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Byte-stability + forbidden-keyword assertions on the Pattern-B
/// <see cref="ClassifierSchema.Frozen"/> dictionary per DEC-058 (Slice 4B Unit 4).
/// Mirror <c>PlanAdaptationSchemaStabilityTests</c>: they fail loudly if the
/// schema-generation chain (System.Text.Json upgrade, attribute change on
/// <see cref="MessageIntentOutput"/>, AnthropicSchemaSanitizer regression) drifts
/// the schema bytes — drift would invalidate Anthropic's prompt-prefix cache and,
/// for the forbidden-keyword cases, get the request rejected with HTTP 400.
/// </summary>
public sealed class ClassifierSchemaStabilityTests
{
    private static readonly string[] ExpectedMessageIntents = ["Question", "WorkoutLog", "Ambiguous"];
    private static readonly string[] ExpectedCompletionStatuses = ["Complete", "Partial", "Skipped"];

    [Fact]
    public void BuildClassifierSchema_TwoCalls_ProduceByteIdenticalDictionaries()
    {
        // Arrange + Act
        var first = ClassifierSchema.BuildClassifierSchema();
        var second = ClassifierSchema.BuildClassifierSchema();

        // Assert
        SerializeDictionaryToCanonicalJson(first).Should().Equal(
            SerializeDictionaryToCanonicalJson(second),
            "schema dictionary must be byte-stable across calls per DEC-058");
    }

    [Fact]
    public void Frozen_MatchesFreshBuild_ByteForByte()
    {
        // Arrange
        var fresh = ClassifierSchema.BuildClassifierSchema();

        // Act + Assert
        SerializeDictionaryToCanonicalJson(ClassifierSchema.Frozen).Should().Equal(
            SerializeDictionaryToCanonicalJson(fresh),
            "Frozen is the result of BuildClassifierSchema() — they must remain byte-equal");
    }

    [Fact]
    public void Frozen_TwoBuilds_ProduceIdenticalSha256Hash()
    {
        HashSchema(ClassifierSchema.BuildClassifierSchema()).Should().Be(
            HashSchema(ClassifierSchema.BuildClassifierSchema()),
            "byte-stable schema is required so the Anthropic prompt-cache prefix hash hits");
    }

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
    [InlineData("oneOf")]
    [InlineData("allOf")]
    [InlineData("if")]
    [InlineData("then")]
    [InlineData("not")]
    [InlineData("prefixItems")]
    [InlineData("minProperties")]
    [InlineData("maxProperties")]
    [InlineData("else")]
    public void Frozen_ContainsZero_ForbiddenAnthropicKeyword(string forbiddenKeyword)
    {
        // Arrange
        var json = Encoding.UTF8.GetString(SerializeDictionaryToCanonicalJson(ClassifierSchema.Frozen));

        // Act + Assert — search for property-key occurrences only ("keyword":).
        json.Should().NotContain(
            $"\"{forbiddenKeyword}\":",
            because: $"Anthropic constrained decoding rejects '{forbiddenKeyword}' with HTTP 400");
    }

    [Fact]
    public void Frozen_ContainsNoReference_SoAnthropicAcceptsTheSchema()
    {
        // Arrange
        var json = Encoding.UTF8.GetString(SerializeDictionaryToCanonicalJson(ClassifierSchema.Frozen));

        // Act + Assert
        json.Should().NotContain(
            "\"$ref\":",
            because: "Anthropic rejects a $ref not under $defs/definitions with HTTP 400 (output_config.format.schema)");
    }

    [Fact]
    public void Frozen_HasObjectShape_WithPropertiesAndAdditionalPropertiesFalse()
    {
        ClassifierSchema.Frozen.Should().ContainKey("properties");
        ClassifierSchema.Frozen.Should().ContainKey("type");
        ClassifierSchema.Frozen.Should().ContainKey("additionalProperties");
        ClassifierSchema.Frozen["additionalProperties"].GetBoolean().Should().BeFalse(
            "JsonSchemaHelper injects additionalProperties=false on every object node");
    }

    [Fact]
    public void Frozen_RequiredFields_IncludeBothMessageIntentOutputProperties()
    {
        // Arrange + Act
        ClassifierSchema.Frozen.Should().ContainKey("required");
        var names = new List<string>();
        foreach (var item in ClassifierSchema.Frozen["required"].EnumerateArray())
        {
            names.Add(item.GetString()!);
        }

        // Assert — snake_case names; both slots present-required (Pattern-B), the
        // nullable one may be null, which the validator (not the schema) enforces.
        names.Should().Contain("intent");
        names.Should().Contain("workout_log");
    }

    [Fact]
    public void Frozen_DeclaresIntent_AsTopLevelStringEnum()
    {
        // The intent discriminator must serialize as a string enum, never an integer —
        // the LLM emits "WorkoutLog", not 1. (Top-level non-nullable enum: safe to navigate.)
        var properties = ClassifierSchema.Frozen["properties"];

        EnumValues(properties.GetProperty("intent")).Should().Contain(ExpectedMessageIntents);
    }

    [Fact]
    public void Frozen_SerializesCompletionStatus_AsStringEnumMembers()
    {
        // The nested draft's completion_status is a string enum too. Assert via a
        // string search rather than navigating the nullable workout_log slot, whose
        // null-union wrapping shape is exporter-version-dependent.
        var json = Encoding.UTF8.GetString(SerializeDictionaryToCanonicalJson(ClassifierSchema.Frozen));

        foreach (var status in ExpectedCompletionStatuses)
        {
            json.Should().Contain($"\"{status}\"", $"completion_status must enumerate the string member {status}");
        }
    }

    private static IEnumerable<string> EnumValues(JsonElement schemaNode)
    {
        if (!schemaNode.TryGetProperty("enum", out var enumArray))
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in enumArray.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                values.Add(item.GetString()!);
            }
        }

        return values;
    }

    private static byte[] SerializeDictionaryToCanonicalJson(IReadOnlyDictionary<string, JsonElement> dict) =>
        JsonSerializer.SerializeToUtf8Bytes(dict);

    private static string HashSchema(IReadOnlyDictionary<string, JsonElement> dict) =>
        Convert.ToHexString(SHA256.HashData(SerializeDictionaryToCanonicalJson(dict)));
}
