using FluentAssertions;
using RunCoach.Api.Modules.Training.Constants;

namespace RunCoach.Api.Tests.Modules.Training.Constants;

public class WorkoutMetricKeysTests
{
    // The canonical metric keys documented in DEC-072 / slice-2b spec § Unit 2.
    // Pinned here so adding or removing a key forces a conscious test update.
    private static readonly string[] DocumentedKeys =
    [
        "rpe", "hrAvg", "hrMax", "calories", "hrv", "sleepScore", "recoveryScore",
        "cadence", "elevationGain", "power", "weather", "terrain", "splits",
    ];

    // Reserved-but-unpopulated running-dynamics keys (DEC-072): named now so
    // future HealthKit/Garmin ingestion is a zero-migration switch-on.
    private static readonly string[] ReservedKeys =
    [
        "verticalOscillation", "groundContactTime", "strideLength",
    ];

    [Fact]
    public void All_ContainsEveryDocumentedAndReservedKey()
    {
        // Assert
        WorkoutMetricKeys.All.Should().Contain(DocumentedKeys);
        WorkoutMetricKeys.All.Should().Contain(ReservedKeys);
        WorkoutMetricKeys.All.Should().HaveCount(DocumentedKeys.Length + ReservedKeys.Length);
    }

    [Fact]
    public void ConstSet_AndEnum_AreDriftFree()
    {
        // Arrange — derive the wire key for every enum member.
        var enumKeys = Enum.GetValues<WorkoutMetricKey>()
            .Select(WorkoutMetricKeys.ToWireKey)
            .ToHashSet(StringComparer.Ordinal);

        // Assert — symmetric: no key appears in the const set but not the enum,
        // and none appears in the enum but not the const set.
        WorkoutMetricKeys.All.Should().BeEquivalentTo(enumKeys);
    }

    [Theory]
    [InlineData(WorkoutMetricKey.Rpe, "rpe")]
    [InlineData(WorkoutMetricKey.HrAvg, "hrAvg")]
    [InlineData(WorkoutMetricKey.HrMax, "hrMax")]
    [InlineData(WorkoutMetricKey.SleepScore, "sleepScore")]
    [InlineData(WorkoutMetricKey.ElevationGain, "elevationGain")]
    [InlineData(WorkoutMetricKey.VerticalOscillation, "verticalOscillation")]
    public void ToWireKey_LowercasesFirstCharacterOfEnumName(WorkoutMetricKey key, string expected)
    {
        // Act
        var actual = WorkoutMetricKeys.ToWireKey(key);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void Constants_MatchTheirEnumWireKeys()
    {
        // Assert — the ergonomic const strings stay locked to the enum-derived wire keys.
        WorkoutMetricKeys.Rpe.Should().Be(WorkoutMetricKeys.ToWireKey(WorkoutMetricKey.Rpe));
        WorkoutMetricKeys.HrAvg.Should().Be(WorkoutMetricKeys.ToWireKey(WorkoutMetricKey.HrAvg));
        WorkoutMetricKeys.Splits.Should().Be(WorkoutMetricKeys.ToWireKey(WorkoutMetricKey.Splits));
        WorkoutMetricKeys.StrideLength.Should().Be(WorkoutMetricKeys.ToWireKey(WorkoutMetricKey.StrideLength));
    }
}
