using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Adaptation;

/// <summary>
/// Post-deserialization validation of the Pattern-B <see cref="PlanAdaptationOutput"/>
/// per DEC-058 / DEC-079 (Slice 3 Unit 4). Anthropic constrained decoding cannot express
/// "exactly the slot matching the discriminator is non-null", the GATE-BEFORE-INCREASE
/// safety invariant, or "a load-reducing restructure must include a forward path", so the
/// backend enforces all three after deserialization.
/// </summary>
public sealed class PlanAdaptationOutputValidatorTests
{
    [Fact]
    public void Validate_ReturnsValid_ForWellFormedGreenNudge()
    {
        // Arrange — nudge slot filled, restructure null, Green tier, non-positive load.
        var output = BuildNudge(SafetyTier.Green, netLoadDelta: 0);

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.None);
    }

    [Fact]
    public void Validate_ReturnsValid_ForAbsorbWithNoSlots()
    {
        // Arrange — absorb fills neither slot.
        var output = BuildAbsorb();

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.None);
    }

    [Fact]
    public void Validate_ReturnsValid_ForLoadReducingRestructureWithForwardPath()
    {
        // Arrange — Amber, load-reducing, with a forward path.
        var output = BuildRestructure(SafetyTier.Amber, netLoadDelta: -12, forwardPath: "Hold this volume for one week, then add 10% back.");

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.None);
    }

    [Fact]
    public void Validate_ReturnsMultipleSlots_WhenBothSlotsNonNull()
    {
        // Arrange
        var output = BuildNudge(SafetyTier.Green, netLoadDelta: 0) with
        {
            RestructurePlan = SampleRestructure("ramp back"),
        };

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.MultipleSlots);
    }

    [Fact]
    public void Validate_ReturnsSlotKindMismatch_WhenRestructureKindFillsNudgeSlot()
    {
        // Arrange — discriminator says restructure but only the nudge slot is populated.
        var output = BuildNudge(SafetyTier.Green, netLoadDelta: 0) with
        {
            AdaptationKind = AdaptationKind.Restructure,
        };

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.SlotKindMismatch);
    }

    [Fact]
    public void Validate_ReturnsSlotKindMismatch_WhenAbsorbKindFillsASlot()
    {
        // Arrange — absorb must fill neither slot.
        var output = BuildNudge(SafetyTier.Green, netLoadDelta: 0) with
        {
            AdaptationKind = AdaptationKind.Absorb,
        };

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.SlotKindMismatch);
    }

    [Theory]
    [InlineData(SafetyTier.Amber)]
    [InlineData(SafetyTier.Red)]
    public void Validate_ReturnsLoadIncreaseUnderNonGreenTier_WhenNonGreenAndPositiveDelta(SafetyTier tier)
    {
        // Arrange — GATE-BEFORE-INCREASE: any non-Green tier forbids a load increase.
        var output = BuildNudge(tier, netLoadDelta: 5);

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.LoadIncreaseUnderNonGreenTier);
    }

    [Fact]
    public void Validate_ReturnsValid_WhenGreenTierIncreasesLoad()
    {
        // Arrange — Green permits a load increase (the gate only clamps non-Green).
        var output = BuildNudge(SafetyTier.Green, netLoadDelta: 5);

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReturnsLoadReducingRestructureMissingForwardPath_WhenForwardPathBlank(string forwardPath)
    {
        // Arrange — restructure that cuts load must show the path back.
        var output = BuildRestructure(SafetyTier.Green, netLoadDelta: -10, forwardPath: forwardPath);

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.LoadReducingRestructureMissingForwardPath);
    }

    [Fact]
    public void Validate_ReturnsValid_ForNonReducingRestructureWithBlankForwardPath()
    {
        // Arrange — a non-load-reducing restructure (delta >= 0) does not require a forward path.
        var output = BuildRestructure(SafetyTier.Green, netLoadDelta: 0, forwardPath: string.Empty);

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ThrowsArgumentNullException_WhenOutputIsNull()
    {
        // Arrange + Act
        var act = () => PlanAdaptationOutputValidator.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static PlanAdaptationOutput BuildNudge(SafetyTier tier, int netLoadDelta) => new()
    {
        AdaptationKind = AdaptationKind.Nudge,
        SafetyTier = tier,
        NudgePatch = new NudgePatch
        {
            WeekNumber = 1,
            RevisedWorkouts = [SampleWorkout()],
        },
        RestructurePlan = null,
        NetLoadDelta = netLoadDelta,
        Rationale = "I moved your Thursday workout to Friday so it lands on a fresher day.",
        ReferralCategory = tier == SafetyTier.Green ? null : ReferralCategory.Injury,
    };

    private static PlanAdaptationOutput BuildRestructure(SafetyTier tier, int netLoadDelta, string forwardPath) => new()
    {
        AdaptationKind = AdaptationKind.Restructure,
        SafetyTier = tier,
        NudgePatch = null,
        RestructurePlan = SampleRestructure(forwardPath),
        NetLoadDelta = netLoadDelta,
        Rationale = "Your last two weeks show a sustained dip, so I'm easing this week and ramping back next.",
        ReferralCategory = tier == SafetyTier.Green ? null : ReferralCategory.Injury,
    };

    private static PlanAdaptationOutput BuildAbsorb() => new()
    {
        AdaptationKind = AdaptationKind.Absorb,
        SafetyTier = SafetyTier.Green,
        NudgePatch = null,
        RestructurePlan = null,
        NetLoadDelta = 0,
        Rationale = "That run was right on target — nothing to change.",
        ReferralCategory = null,
    };

    private static RestructurePlan SampleRestructure(string forwardPath) => new()
    {
        RevisedWeeklyTargets = [new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 35 }],
        RevisedCurrentWeekWorkouts = [SampleWorkout()],
        ForwardPath = forwardPath,
    };

    private static WorkoutOutput SampleWorkout(int dayOfWeek = 1) => new()
    {
        DayOfWeek = dayOfWeek,
        WorkoutType = WorkoutType.Easy,
        Title = "Easy Aerobic Run",
        TargetDistanceKm = 8,
        TargetDurationMinutes = 45,
        TargetPaceEasySecPerKm = 360,
        TargetPaceFastSecPerKm = 330,
        Segments = [],
        WarmupNotes = "five minutes of easy jogging",
        CooldownNotes = "five minutes of easy jogging",
        CoachingNotes = "keep it conversational the whole way",
        PerceivedEffort = 3,
    };
}
