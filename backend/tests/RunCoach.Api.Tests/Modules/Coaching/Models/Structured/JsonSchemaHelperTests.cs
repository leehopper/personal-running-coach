using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Models.Structured;

public class JsonSchemaHelperTests
{
    [Fact]
    public void GenerateSchema_MacroPlanOutput_ReturnsValidJsonSchema()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MacroPlanOutput>();

        // Assert
        schema.Should().NotBeNull();
        var schemaObj = schema.AsObject();
        schemaObj.ContainsKey("type").Should().BeTrue();
        schemaObj["type"]!.GetValue<string>().Should().Be("object");
        schemaObj.ContainsKey("properties").Should().BeTrue();
    }

    [Fact]
    public void GenerateSchema_MesoWeekOutput_ReturnsValidJsonSchema()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MesoWeekOutput>();

        // Assert
        schema.Should().NotBeNull();
        var schemaObj = schema.AsObject();
        schemaObj.ContainsKey("type").Should().BeTrue();
        schemaObj["type"]!.GetValue<string>().Should().Be("object");
        schemaObj.ContainsKey("properties").Should().BeTrue();
    }

    [Fact]
    public void GenerateSchema_MicroWorkoutListOutput_ReturnsValidJsonSchema()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MicroWorkoutListOutput>();

        // Assert
        schema.Should().NotBeNull();
        var schemaObj = schema.AsObject();
        schemaObj.ContainsKey("type").Should().BeTrue();
        schemaObj["type"]!.GetValue<string>().Should().Be("object");
        schemaObj.ContainsKey("properties").Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(MacroPlanOutput))]
    [InlineData(typeof(MesoWeekOutput))]
    [InlineData(typeof(MicroWorkoutListOutput))]
    [InlineData(typeof(SafetyVerdict))]
    public void GenerateSchema_AllObjectNodes_HaveAdditionalPropertiesFalse(Type outputType)
    {
        // Arrange
        var method = typeof(JsonSchemaHelper)
            .GetMethod(nameof(JsonSchemaHelper.GenerateSchema))!
            .MakeGenericMethod(outputType);

        // Act
        var schema = (JsonNode)method.Invoke(null, null)!;

        // Assert
        var objectNodes = CollectObjectNodes(schema);
        objectNodes.Should().NotBeEmpty("there should be at least one object node");

        foreach (var objectNode in objectNodes)
        {
            objectNode.ContainsKey("additionalProperties").Should().BeTrue(
                "object node should have additionalProperties");
            objectNode["additionalProperties"]!.GetValue<bool>().Should().BeFalse(
                "additionalProperties should be false on all object nodes");
        }
    }

    [Fact]
    public void GenerateSchema_MacroPlanOutput_IncludesDescriptionsFromAttributes()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MacroPlanOutput>();

        // Assert
        var schemaStr = schema.ToJsonString();
        schemaStr.Should().Contain("Total number of weeks");
        schemaStr.Should().Contain("goal race or training objective");
        schemaStr.Should().Contain("periodized training phases");
    }

    [Fact]
    public void GenerateSchema_MesoWeekOutput_IncludesDescriptionsFromAttributes()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MesoWeekOutput>();

        // Assert
        var schemaStr = schema.ToJsonString();
        schemaStr.Should().Contain("week number within the current training phase");
        schemaStr.Should().Contain("deload week");
        schemaStr.Should().Contain("Activity plan for Sunday");
    }

    [Fact]
    public void GenerateSchema_MesoWeekOutput_RequiredArrayContainsAllSevenDaysAndScalars()
    {
        // Arrange — the structural-enforcement guarantee for constrained
        // decoding depends on every required property appearing in the
        // top-level `required` array. This test pins that contract so a
        // schema-generator regression dropping entries is caught in CI.
        var expectedRequiredNames = new[]
        {
            // Seven named day slots — the whole point of the Days[] → named
            // properties refactor (eliminates the 8-day flake).
            "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday",

            // Scalar required fields.
            "week_number", "phase_type", "weekly_target_km", "is_deload_week", "week_summary",
        };

        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MesoWeekOutput>();
        var requiredArray = schema.AsObject()["required"]!.AsArray();
        var actualRequiredNames = requiredArray
            .Select(n => n!.GetValue<string>())
            .ToArray();

        // Assert
        actualRequiredNames.Should().Contain(
            expectedRequiredNames,
            because: "MesoWeekOutput relies on constrained decoding enforcing these required fields");
    }

    [Fact]
    public void GenerateSchema_MicroWorkoutListOutput_IncludesDescriptionsFromAttributes()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MicroWorkoutListOutput>();

        // Assert
        var schemaStr = schema.ToJsonString();
        schemaStr.Should().Contain("detailed workout prescriptions");
    }

    [Theory]
    [InlineData(typeof(MacroPlanOutput))]
    [InlineData(typeof(MesoWeekOutput))]
    [InlineData(typeof(MicroWorkoutListOutput))]
    public void GenerateSchema_NoObjectNode_ExceedsPropertyCountLimit(Type outputType)
    {
        // Arrange
        const int maxPropertiesPerObject = 30;
        var method = typeof(JsonSchemaHelper)
            .GetMethod(nameof(JsonSchemaHelper.GenerateSchema))!
            .MakeGenericMethod(outputType);

        // Act
        var schema = (JsonNode)method.Invoke(null, null)!;

        // Assert
        var objectNodes = CollectObjectNodes(schema);
        foreach (var objectNode in objectNodes)
        {
            var properties = objectNode["properties"]!.AsObject();
            properties.Count.Should().BeLessThanOrEqualTo(
                maxPropertiesPerObject,
                $"object in {outputType.Name} exceeds property limit");
        }
    }

    [Theory]
    [InlineData(typeof(MacroPlanOutput), 2)]
    [InlineData(typeof(MesoWeekOutput), 2)]
    [InlineData(typeof(MicroWorkoutListOutput), 3)]
    [InlineData(typeof(SafetyVerdict), 2)]
    public void GenerateSchema_NestingDepth_DoesNotExceedLimit(
        Type outputType,
        int expectedMaxDepth)
    {
        // Arrange
        const int absoluteMaxDepth = 3;
        var method = typeof(JsonSchemaHelper)
            .GetMethod(nameof(JsonSchemaHelper.GenerateSchema))!
            .MakeGenericMethod(outputType);

        // Act
        var schema = (JsonNode)method.Invoke(null, null)!;

        // Assert
        var actualDepth = MeasureNestingDepth(schema);
        actualDepth.Should().BeLessThanOrEqualTo(
            absoluteMaxDepth,
            $"{outputType.Name} schema nesting depth exceeds limit");
        actualDepth.Should().Be(
            expectedMaxDepth,
            $"{outputType.Name} schema nesting depth mismatch");
    }

    [Fact]
    public void GenerateSchemaString_ProducesValidJson()
    {
        // Act
        var schemaString = JsonSchemaHelper.GenerateSchemaString<MacroPlanOutput>();

        // Assert
        schemaString.Should().NotBeNullOrWhiteSpace();
        var parsed = JsonNode.Parse(schemaString);
        parsed.Should().NotBeNull();
    }

    [Fact]
    public void GenerateSchema_MacroPlanOutput_EnumsAppearAsStringValues()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MacroPlanOutput>();
        var schemaStr = schema.ToJsonString();

        // Assert
        schemaStr.Should().Contain("\"Base\"");
        schemaStr.Should().Contain("\"Build\"");
        schemaStr.Should().Contain("\"Peak\"");
        schemaStr.Should().Contain("\"Taper\"");
        schemaStr.Should().Contain("\"Recovery\"");
    }

    [Fact]
    public void GenerateSchema_MicroWorkoutListOutput_SegmentEnumsAppearAsStringValues()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MicroWorkoutListOutput>();
        var schemaStr = schema.ToJsonString();

        // Assert
        schemaStr.Should().Contain("\"Warmup\"");
        schemaStr.Should().Contain("\"Work\"");
        schemaStr.Should().Contain("\"Cooldown\"");
        schemaStr.Should().Contain("\"Easy\"");
        schemaStr.Should().Contain("\"Threshold\"");
        schemaStr.Should().Contain("\"VO2Max\"");
    }

    [Fact]
    public void GenerateSchema_UsesSnakeCasePropertyNames()
    {
        // Act
        var schema = JsonSchemaHelper.GenerateSchema<MacroPlanOutput>();
        var properties = schema.AsObject()["properties"]!.AsObject();

        // Assert
        properties.ContainsKey("total_weeks").Should().BeTrue();
        properties.ContainsKey("goal_description").Should().BeTrue();
        properties.ContainsKey("TotalWeeks").Should().BeFalse();
        properties.ContainsKey("GoalDescription").Should().BeFalse();
    }

    private static List<JsonObject> CollectObjectNodes(JsonNode node)
    {
        var result = new List<JsonObject>();
        CollectObjectNodesRecursive(node, result);
        return result;
    }

    private static void CollectObjectNodesRecursive(JsonNode? node, List<JsonObject> result)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        if (obj.ContainsKey("properties"))
        {
            result.Add(obj);

            foreach (var prop in obj["properties"]!.AsObject())
            {
                CollectObjectNodesRecursive(prop.Value, result);
            }
        }

        if (obj.ContainsKey("items"))
        {
            CollectObjectNodesRecursive(obj["items"], result);
        }

        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (obj.ContainsKey(keyword) && obj[keyword] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    CollectObjectNodesRecursive(item, result);
                }
            }
        }
    }

    private static int MeasureNestingDepth(JsonNode node)
    {
        return MeasureNestingDepthRecursive(node, 0);
    }

    private static int MeasureNestingDepthRecursive(JsonNode? node, int currentDepth)
    {
        if (node is not JsonObject obj)
        {
            return currentDepth;
        }

        var isObjectType = obj.ContainsKey("properties");
        var depth = isObjectType ? currentDepth + 1 : currentDepth;
        var maxDepth = depth;

        if (isObjectType && obj["properties"] is JsonObject properties)
        {
            foreach (var prop in properties)
            {
                var childDepth = MeasureNestingDepthRecursive(prop.Value, depth);
                maxDepth = Math.Max(maxDepth, childDepth);
            }
        }

        if (obj.ContainsKey("items"))
        {
            var childDepth = MeasureNestingDepthRecursive(obj["items"], depth);
            maxDepth = Math.Max(maxDepth, childDepth);
        }

        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (obj.ContainsKey(keyword) && obj[keyword] is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    var childDepth = MeasureNestingDepthRecursive(item, depth);
                    maxDepth = Math.Max(maxDepth, childDepth);
                }
            }
        }

        return maxDepth;
    }
}
