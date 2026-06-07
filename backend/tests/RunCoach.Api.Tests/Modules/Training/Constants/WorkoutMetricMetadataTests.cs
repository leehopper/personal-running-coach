using FluentAssertions;
using RunCoach.Api.Modules.Training.Constants;

namespace RunCoach.Api.Tests.Modules.Training.Constants;

public class WorkoutMetricMetadataTests
{
    // Splits is a canonical key but is persisted in its own typed column, not
    // as a scalar bag value, so it carries no inline-display metadata.
    private static readonly string[] NonScalarKeys = [WorkoutMetricKeys.Splits];

    [Fact]
    public void Metadata_CoversEveryScalarKey_AndExcludesSplits()
    {
        // Arrange — every canonical key except the non-scalar ones.
        var expectedKeys = WorkoutMetricKeys.All.Except(NonScalarKeys).ToHashSet(StringComparer.Ordinal);

        // Assert — metadata is exactly the scalar keys: no gaps, no extras.
        WorkoutMetricKeys.Metadata.Keys.Should().BeEquivalentTo(expectedKeys);
        WorkoutMetricKeys.Metadata.Keys.Should().NotContain(WorkoutMetricKeys.Splits);
    }

    [Fact]
    public void DisplayOrder_ContainsEveryMetadataKey_ExactlyOnce()
    {
        // Assert — the render order lists each metadata key once and adds none.
        WorkoutMetricKeys.DisplayOrder.Should().OnlyHaveUniqueItems();
        WorkoutMetricKeys.DisplayOrder.Should().BeEquivalentTo(WorkoutMetricKeys.Metadata.Keys);
    }

    [Fact]
    public void DisplayOrder_RendersEffortSignalsBeforeEverythingElse()
    {
        // Arrange — index of each key in the render order.
        var order = WorkoutMetricKeys.DisplayOrder;

        var lastEffortIndex = order
            .Select((key, index) => (key, index))
            .Where(t => WorkoutMetricKeys.Metadata[t.key].Category == MetricCategory.Effort)
            .Max(t => t.index);

        var firstNonEffortIndex = order
            .Select((key, index) => (key, index))
            .Where(t => WorkoutMetricKeys.Metadata[t.key].Category != MetricCategory.Effort)
            .Min(t => t.index);

        // Assert — every effort signal precedes every non-effort metric so the
        // one-liner reads "HR …, RPE …, <peripheral>, <contextual>".
        lastEffortIndex.Should().BeLessThan(firstNonEffortIndex);
    }

    [Fact]
    public void Metadata_EffortCategory_IsExactlyHeartRateAndRpe()
    {
        // Arrange
        var effortKeys = WorkoutMetricKeys.Metadata
            .Where(kvp => kvp.Value.Category == MetricCategory.Effort)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.Ordinal);
        var expectedEffortKeys = new[] { WorkoutMetricKeys.HrAvg, WorkoutMetricKeys.HrMax, WorkoutMetricKeys.Rpe };

        // Assert — only these three drive the "(no HR/RPE)" absence marker.
        effortKeys.Should().BeEquivalentTo(expectedEffortKeys);
    }

    [Fact]
    public void Metadata_EveryEntry_HasANonEmptyLabel()
    {
        // Assert — a blank label would render a value with no name.
        WorkoutMetricKeys.Metadata.Values.Should().OnlyContain(m => m.Label.Length > 0);
    }

    [Fact]
    public void Metadata_EffortSignals_HaveCompactLabelsAndNoUnit()
    {
        // Assert — HR/RPE render label + value only (no unit), matching the
        // compact coaching convention ("HR 148", "RPE 7"). string.Empty cannot
        // appear in [InlineData] (attributes need compile-time constants), so
        // these unit-less cases are pinned here rather than in the Theory.
        WorkoutMetricKeys.Metadata[WorkoutMetricKeys.HrAvg].Label.Should().Be("HR");
        WorkoutMetricKeys.Metadata[WorkoutMetricKeys.HrAvg].Unit.Should().Be(string.Empty);
        WorkoutMetricKeys.Metadata[WorkoutMetricKeys.HrMax].Label.Should().Be("HR max");
        WorkoutMetricKeys.Metadata[WorkoutMetricKeys.HrMax].Unit.Should().Be(string.Empty);
        WorkoutMetricKeys.Metadata[WorkoutMetricKeys.Rpe].Label.Should().Be("RPE");
        WorkoutMetricKeys.Metadata[WorkoutMetricKeys.Rpe].Unit.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(WorkoutMetricKeys.Cadence, "cadence", "spm", MetricCategory.Peripheral)]
    [InlineData(WorkoutMetricKeys.Power, "power", "W", MetricCategory.Peripheral)]
    [InlineData(WorkoutMetricKeys.ElevationGain, "elev gain", "m", MetricCategory.Peripheral)]
    [InlineData(WorkoutMetricKeys.Calories, "calories", "kcal", MetricCategory.Contextual)]
    public void Metadata_PinsRepresentativeLabelUnitAndCategory(
        string key,
        string expectedLabel,
        string expectedUnit,
        MetricCategory expectedCategory)
    {
        // Act
        var actual = WorkoutMetricKeys.Metadata[key];

        // Assert
        actual.Label.Should().Be(expectedLabel);
        actual.Unit.Should().Be(expectedUnit);
        actual.Category.Should().Be(expectedCategory);
    }
}
