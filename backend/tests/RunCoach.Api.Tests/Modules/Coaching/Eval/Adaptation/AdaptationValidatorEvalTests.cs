using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Deterministic validator-reject eval (Slice 3 Unit 6): the post-deserialization
/// invariants <c>PlanAdaptationOutputValidator</c> enforces (DEC-058 / DEC-079),
/// plus the eval-side week-over-week mileage-jump guardrail
/// (<see cref="AdaptationConstraintEvaluator"/>) the prompt states but constrained
/// decoding cannot. No LLM — these construct outputs directly.
/// </summary>
[Trait("Category", "Eval")]
public sealed class AdaptationValidatorEvalTests
{
    [Fact]
    public void Validate_WellFormedGreenRestructure_IsValid()
    {
        // Arrange
        var output = Restructure(SafetyTier.Green, netLoadDelta: -6);

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.None);
    }

    [Theory]
    [InlineData(SafetyTier.Amber)]
    [InlineData(SafetyTier.Red)]
    public void Validate_NonGreenTierWithLoadIncrease_RejectsGateBeforeIncrease(SafetyTier tier)
    {
        // Arrange — GATE-BEFORE-INCREASE: a flagged tier forbids a positive net load delta.
        var output = Restructure(tier, netLoadDelta: 8) with { ReferralCategory = ReferralCategory.Injury };

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.LoadIncreaseUnderNonGreenTier);
    }

    [Fact]
    public void Validate_BothSlotsFilled_RejectsMultipleSlots()
    {
        // Arrange
        var output = Restructure(SafetyTier.Green, netLoadDelta: -4) with
        {
            NudgePatch = new NudgePatch { WeekNumber = 1, RevisedWorkouts = [] },
        };

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.MultipleSlots);
    }

    [Fact]
    public void Validate_RestructureKindWithNoSlot_RejectsSlotKindMismatch()
    {
        // Arrange — the discriminator says Restructure but neither slot is filled.
        var output = Restructure(SafetyTier.Green, netLoadDelta: -4) with { RestructurePlan = null };

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(PlanAdaptationOutputValidationViolation.SlotKindMismatch);
    }

    [Fact]
    public void Validate_LoadReducingRestructureWithoutForwardPath_RejectsMissingForwardPath()
    {
        // Arrange — a cut must show the path back.
        var output = Restructure(SafetyTier.Green, netLoadDelta: -6, forwardPath: "   ");

        // Act
        var result = PlanAdaptationOutputValidator.Validate(output);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Violation.Should().Be(
            PlanAdaptationOutputValidationViolation.LoadReducingRestructureMissingForwardPath);
    }

    [Fact]
    public void EvalConstraint_RestructureWithMileageJumpOver10Percent_IsFlagged()
    {
        // Arrange — week 2 jumps 40 -> 50km (+25%) over a 40km baseline.
        var plan = new RestructurePlan
        {
            RevisedWeeklyTargets =
            [
                new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 50 },
            ],
            RevisedCurrentWeekWorkouts = [],
            ForwardPath = "Ramp back gradually.",
        };

        // Act
        var violations = AdaptationConstraintEvaluator.Evaluate(plan, baselineWeeklyKm: 40);

        // Assert — the eval rejects the jump the prompt guardrail forbids.
        violations.Should().ContainSingle()
            .Which.Should().Contain("Week 2").And.Contain("ceiling");
    }

    [Fact]
    public void EvalConstraint_RecoveryRampBackToRecentlyHeldBaseline_IsClean()
    {
        // Arrange — the restructure cut to 32km from a recently-held 40km; the ramp
        // back (32 -> 36 -> 40, +12.5% and +11.1%) exceeds +10% week-over-week but
        // never exceeds the recently-held baseline — re-acclimatization, not novel load.
        var plan = new RestructurePlan
        {
            RevisedWeeklyTargets =
            [
                new WeeklyTargetEdit { WeekNumber = 1, WeeklyTargetKm = 32 },
                new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 36 },
                new WeeklyTargetEdit { WeekNumber = 3, WeeklyTargetKm = 40 },
            ],
            RevisedCurrentWeekWorkouts = [],
            ForwardPath = "Rebuild to the held volume over two weeks.",
        };

        // Act
        var violations = AdaptationConstraintEvaluator.Evaluate(plan, baselineWeeklyKm: 40);

        // Assert — the recovery-ramp exemption admits the rebuild.
        violations.Should().BeEmpty();
    }

    [Fact]
    public void EvalConstraint_RampPastTheRecentlyHeldBaseline_IsFlagged()
    {
        // Arrange — week 2 grows past the 40km recently-held baseline (36 -> 48km):
        // beyond the baseline the exemption ends and the +10% rate limit applies.
        var plan = new RestructurePlan
        {
            RevisedWeeklyTargets =
            [
                new WeeklyTargetEdit { WeekNumber = 1, WeeklyTargetKm = 36 },
                new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 48 },
            ],
            RevisedCurrentWeekWorkouts = [],
            ForwardPath = "Ramp back gradually.",
        };

        // Act
        var violations = AdaptationConstraintEvaluator.Evaluate(plan, baselineWeeklyKm: 40);

        // Assert
        violations.Should().ContainSingle()
            .Which.Should().Contain("Week 2").And.Contain("ceiling");
    }

    [Fact]
    public void EvalConstraint_RestructureWithinTenPercentRamp_IsClean()
    {
        // Arrange — a compliant ramp: 40 -> 44 (+10%) -> 48 (+~9%).
        var plan = new RestructurePlan
        {
            RevisedWeeklyTargets =
            [
                new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 44 },
                new WeeklyTargetEdit { WeekNumber = 3, WeeklyTargetKm = 48 },
            ],
            RevisedCurrentWeekWorkouts = [],
            ForwardPath = "Ramp back gradually.",
        };

        // Act
        var violations = AdaptationConstraintEvaluator.Evaluate(plan, baselineWeeklyKm: 40);

        // Assert
        violations.Should().BeEmpty();
    }

    private static PlanAdaptationOutput Restructure(
        SafetyTier tier,
        int netLoadDelta,
        string forwardPath = "Hold the reduced volume this week, then ramp back ~10% per week.") =>
        new()
        {
            AdaptationKind = AdaptationKind.Restructure,
            SafetyTier = tier,
            NudgePatch = null,
            RestructurePlan = new RestructurePlan
            {
                RevisedWeeklyTargets = [new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = 24 }],
                RevisedCurrentWeekWorkouts = [Workout(2, WorkoutType.Easy)],
                ForwardPath = forwardPath,
            },
            NetLoadDelta = netLoadDelta,
            Rationale = "Trimmed next week and eased Tuesday so you recover and rebuild.",
            ReferralCategory = null,
        };

    private static WorkoutOutput Workout(int dayOfWeek, WorkoutType type) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = type,
            Title = type.ToString(),
            TargetDistanceKm = 8,
            TargetDurationMinutes = 45,
            TargetPaceEasySecPerKm = 330,
            TargetPaceFastSecPerKm = 280,
            Segments = [],
            WarmupNotes = string.Empty,
            CooldownNotes = string.Empty,
            CoachingNotes = string.Empty,
            PerceivedEffort = 4,
        };
}
