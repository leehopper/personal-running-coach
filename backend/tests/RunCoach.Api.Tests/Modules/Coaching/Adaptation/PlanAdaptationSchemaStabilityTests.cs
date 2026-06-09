using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Adaptation;

/// <summary>
/// Byte-stability + forbidden-keyword assertions on the Pattern-B
/// <see cref="AdaptationSchema.Frozen"/> dictionary per DEC-058 (Slice 3 Unit 4).
/// These mirror <c>OnboardingSchemaStabilityTests</c>: they fail loudly if the
/// schema-generation chain (System.Text.Json upgrade, attribute change on
/// <see cref="PlanAdaptationOutput"/>, AnthropicSchemaSanitizer regression) drifts
/// the schema bytes — drift would invalidate Anthropic's prompt-prefix cache and,
/// for the forbidden-keyword cases, get the request rejected with HTTP 400.
/// Together with <c>PlanAdaptationOutputValidatorTests</c> this discharges the
/// "schema-valid, validator-passing PlanAdaptationOutput, no live API call" proof
/// artifact deterministically (Replay-safe, zero fixtures).
/// </summary>
public sealed class PlanAdaptationSchemaStabilityTests
{
    private static readonly string[] ExpectedAdaptationKinds = ["Absorb", "Nudge", "Restructure"];
    private static readonly string[] ExpectedSafetyTiers = ["Green", "Amber", "Red"];
    private static readonly string[] ExpectedReferralCategories = ["None", "Crisis", "EmergencyReferral", "Injury", "RedS"];

    [Fact]
    public void BuildAdaptationSchema_TwoCalls_ProduceByteIdenticalDictionaries()
    {
        // Arrange + Act
        var first = AdaptationSchema.BuildAdaptationSchema();
        var second = AdaptationSchema.BuildAdaptationSchema();

        // Assert
        var firstBytes = SerializeDictionaryToCanonicalJson(first);
        var secondBytes = SerializeDictionaryToCanonicalJson(second);

        firstBytes.Should().Equal(
            secondBytes,
            "schema dictionary must be byte-stable across calls per DEC-058");
    }

    [Fact]
    public void BuildAdaptationSchema_TwoCalls_ProduceIdenticalSha256Hash()
    {
        // Arrange + Act
        var firstHash = HashSchema(AdaptationSchema.BuildAdaptationSchema());
        var secondHash = HashSchema(AdaptationSchema.BuildAdaptationSchema());

        // Assert
        firstHash.Should().Be(
            secondHash,
            "byte-stable schema is required so the Anthropic prompt-cache prefix hash hits");
    }

    [Fact]
    public void Frozen_MatchesFreshBuild_ByteForByte()
    {
        // Arrange
        var fresh = AdaptationSchema.BuildAdaptationSchema();

        // Act
        var frozenBytes = SerializeDictionaryToCanonicalJson(AdaptationSchema.Frozen);
        var freshBytes = SerializeDictionaryToCanonicalJson(fresh);

        // Assert
        frozenBytes.Should().Equal(
            freshBytes,
            "Frozen is the result of BuildAdaptationSchema() — they must remain byte-equal");
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
        var serialized = SerializeDictionaryToCanonicalJson(AdaptationSchema.Frozen);
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
        AdaptationSchema.Frozen.Should().ContainKey("properties");
        AdaptationSchema.Frozen.Should().ContainKey("type");
        AdaptationSchema.Frozen.Should().ContainKey("additionalProperties");

        AdaptationSchema.Frozen["additionalProperties"].GetBoolean().Should().BeFalse(
            "JsonSchemaHelper injects additionalProperties=false on every object node");
    }

    [Fact]
    public void Frozen_RequiredFields_IncludeAllPlanAdaptationOutputProperties()
    {
        // Arrange + Act
        AdaptationSchema.Frozen.Should().ContainKey("required");
        var required = AdaptationSchema.Frozen["required"];

        var names = new List<string>();
        foreach (var item in required.EnumerateArray())
        {
            names.Add(item.GetString()!);
        }

        // Assert — snake_case names per JsonSchemaHelper's policy. All slots are
        // present-required (Pattern-B); the nullable ones may be null, which the
        // validator — not the schema — enforces.
        names.Should().Contain("adaptation_kind");
        names.Should().Contain("safety_tier");
        names.Should().Contain("nudge_patch");
        names.Should().Contain("restructure_plan");
        names.Should().Contain("net_load_delta");
        names.Should().Contain("rationale");
        names.Should().Contain("referral_category");
    }

    [Fact]
    public void Frozen_DeclaresAdaptationKindAndSafetyTier_AsStringEnumDiscriminators()
    {
        // The discriminators must serialize as string enums (JsonStringEnumConverter),
        // never as integers — the LLM emits "Restructure"/"Amber", not 2/1. Assert on
        // the enum member-name strings rather than the exporter's exact `type`
        // representation (which differs for nullable vs non-nullable enums).
        var properties = AdaptationSchema.Frozen["properties"];

        EnumValues(properties.GetProperty("adaptation_kind"))
            .Should().Contain(ExpectedAdaptationKinds);
        EnumValues(properties.GetProperty("safety_tier"))
            .Should().Contain(ExpectedSafetyTiers);
        EnumValues(properties.GetProperty("referral_category"))
            .Should().Contain(ExpectedReferralCategories);
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

    private static byte[] SerializeDictionaryToCanonicalJson(IReadOnlyDictionary<string, JsonElement> dict)
    {
        return JsonSerializer.SerializeToUtf8Bytes(dict);
    }

    private static string HashSchema(IReadOnlyDictionary<string, JsonElement> dict)
    {
        var bytes = SerializeDictionaryToCanonicalJson(dict);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
