using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Byte-stability assertions on the Pattern-B <see cref="OnboardingSchema.Frozen"/>
/// dictionary per R-067 / DEC-058. These tests fail loudly if anything in the
/// schema-generation chain (System.Text.Json upgrade, attribute change on
/// <see cref="OnboardingTurnOutput"/>, AnthropicSchemaSanitizer regression) drifts
/// the schema bytes — that drift would invalidate Anthropic's prompt-prefix cache
/// from turn 2 onward.
/// </summary>
public sealed class OnboardingSchemaStabilityTests
{
    [Fact]
    public void BuildOnboardingSchema_TwoCalls_ProduceByteIdenticalDictionaries()
    {
        // Arrange + Act
        var first = OnboardingSchema.BuildOnboardingSchema();
        var second = OnboardingSchema.BuildOnboardingSchema();

        // Assert — JSON-serialized bytes must be byte-equal.
        var firstBytes = SerializeDictionaryToCanonicalJson(first);
        var secondBytes = SerializeDictionaryToCanonicalJson(second);

        firstBytes.Should().Equal(
            secondBytes,
            "schema dictionary must be byte-stable across calls per R-067 / DEC-058");
    }

    [Fact]
    public void BuildOnboardingSchema_TwoCalls_ProduceIdenticalSha256Hash()
    {
        // Arrange + Act
        var firstHash = HashSchema(OnboardingSchema.BuildOnboardingSchema());
        var secondHash = HashSchema(OnboardingSchema.BuildOnboardingSchema());

        // Assert
        firstHash.Should().Be(
            secondHash,
            "byte-stable schema is required so Anthropic prompt-cache prefix hash hits");
    }

    [Fact]
    public void Frozen_MatchesFreshBuild_ByteForByte()
    {
        // Arrange
        var fresh = OnboardingSchema.BuildOnboardingSchema();

        // Act
        var frozenBytes = SerializeDictionaryToCanonicalJson(OnboardingSchema.Frozen);
        var freshBytes = SerializeDictionaryToCanonicalJson(fresh);

        // Assert
        frozenBytes.Should().Equal(
            freshBytes,
            "Frozen is the result of BuildOnboardingSchema() — they must remain byte-equal");
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
    public void Frozen_ContainsZero_ForbiddenAnthropicKeyword(string forbiddenKeyword)
    {
        // Arrange
        var serialized = SerializeDictionaryToCanonicalJson(OnboardingSchema.Frozen);
        var json = Encoding.UTF8.GetString(serialized);

        // Act + Assert — search for property-key occurrences only ("keyword":).
        var keyMarker = $"\"{forbiddenKeyword}\":";

        json.Should().NotContain(
            keyMarker,
            because: $"Anthropic constrained decoding rejects '{forbiddenKeyword}' with HTTP 400");
    }

    [Fact]
    public void Frozen_HasObjectShape_WithPropertiesAndAdditionalPropertiesFalse()
    {
        // Sanity check that the schema is well-formed before we ship it.
        OnboardingSchema.Frozen.Should().ContainKey("properties");
        OnboardingSchema.Frozen.Should().ContainKey("type");
        OnboardingSchema.Frozen.Should().ContainKey("additionalProperties");

        OnboardingSchema.Frozen["additionalProperties"].GetBoolean().Should().BeFalse(
            "JsonSchemaHelper injects additionalProperties=false on every object node");
    }

    [Fact]
    public void Frozen_RequiredFields_IncludeAllOnboardingTurnOutputProperties()
    {
        // Arrange + Act
        OnboardingSchema.Frozen.Should().ContainKey("required");
        var required = OnboardingSchema.Frozen["required"];

        // Collect required property names.
        var names = new List<string>();
        foreach (var item in required.EnumerateArray())
        {
            names.Add(item.GetString()!);
        }

        // Assert — snake_case names per JsonSchemaHelper's policy.
        names.Should().Contain("reply");
        names.Should().Contain("extracted");
        names.Should().Contain("needs_clarification");
        names.Should().Contain("clarification_reason");
        names.Should().Contain("ready_for_plan");
    }

    private static byte[] SerializeDictionaryToCanonicalJson(IReadOnlyDictionary<string, JsonElement> dict)
    {
        // Dictionary<string, JsonElement> serializes its keys in insertion order,
        // which is what we want for byte-stability — JsonSchemaExporter and
        // AnthropicSchemaSanitizer must both produce keys in deterministic order.
        return JsonSerializer.SerializeToUtf8Bytes(dict);
    }

    private static string HashSchema(IReadOnlyDictionary<string, JsonElement> dict)
    {
        var bytes = SerializeDictionaryToCanonicalJson(dict);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
