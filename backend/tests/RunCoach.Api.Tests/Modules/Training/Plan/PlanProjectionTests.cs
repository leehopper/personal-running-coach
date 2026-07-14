using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Pure-function tests over the <see cref="PlanProjection"/>'s Apply methods.
/// Exercises the static <c>Create</c> and <c>Apply</c> overloads against
/// hand-built event sequences without spinning up Marten or Postgres - the
/// integration path (Marten transaction participant + inline projection
/// upsert) is covered separately by the integration tests landed alongside
/// the plan-rendering controller in T02.4.
/// </summary>
public sealed class PlanProjectionTests
{
    private const string PromptVersion = "coaching-v1";

    private const string ModelId = "claude-sonnet-4-5";

    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    // 2026-04-19 is the Sunday opening the week that contains Now (2026-04-25, a Saturday).
    private static readonly DateOnly PlanStartDate = new(2026, 4, 19);

    private static readonly Guid PlanId = new("22222222-2222-2222-2222-222222222222");

    private static readonly Guid UserId = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid PreviousPlanId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Create_PlanGenerated_PopulatesProvenanceAndMacro()
    {
        // Arrange
        var expectedMacro = BuildMacro();
        var expectedPlanId = PlanId;
        var expectedUserId = UserId;
        var expectedGeneratedAt = Now;
        var expectedPlanStartDate = PlanStartDate;
        var expectedPromptVersion = PromptVersion;
        var expectedModelId = ModelId;
        var generated = new PlanGenerated(
            expectedPlanId,
            expectedUserId,
            expectedMacro,
            expectedGeneratedAt,
            expectedPlanStartDate,
            expectedPromptVersion,
            expectedModelId,
            PreviousPlanId: null,
            TargetEventName: null,
            TargetEventDistanceKm: null,
            TargetEventDate: null);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.PlanId.Should().Be(expectedPlanId);
        actual.UserId.Should().Be(expectedUserId);
        actual.GeneratedAt.Should().Be(expectedGeneratedAt);
        actual.PlanStartDate.Should().Be(expectedPlanStartDate);
        actual.PromptVersion.Should().Be(expectedPromptVersion);
        actual.ModelId.Should().Be(expectedModelId);
        actual.PreviousPlanId.Should().BeNull();
        actual.Macro.Should().BeSameAs(expectedMacro);
        actual.MesoWeeks.Should().BeEmpty();
        actual.MicroWorkoutsByWeek.Should().BeEmpty();
    }

    [Fact]
    public void Create_PlanGenerated_ThreadsPreviousPlanId_ForRegeneration()
    {
        // Arrange — Unit 5's regenerate handler threads the prior plan id onto
        // the new stream's creation event so the projection retains the audit link.
        var expectedPreviousPlanId = PreviousPlanId;
        var generated = new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PlanStartDate,
            PromptVersion,
            ModelId,
            PreviousPlanId: expectedPreviousPlanId,
            TargetEventName: null,
            TargetEventDistanceKm: null,
            TargetEventDate: null);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.PreviousPlanId.Should().Be(expectedPreviousPlanId);
    }

    [Fact]
    public void Create_PlanGenerated_PopulatesTargetEventFields_ForRaceTraining()
    {
        // Arrange
        var expectedTargetEventName = "Berlin Marathon";
        var expectedTargetEventDistanceKm = 42.195;
        var expectedTargetEventDate = new DateOnly(2026, 9, 27);
        var generated = new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PlanStartDate,
            PromptVersion,
            ModelId,
            PreviousPlanId: null,
            TargetEventName: expectedTargetEventName,
            TargetEventDistanceKm: expectedTargetEventDistanceKm,
            TargetEventDate: expectedTargetEventDate);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.TargetEventName.Should().Be(expectedTargetEventName);
        actual.TargetEventDistanceKm.Should().Be(expectedTargetEventDistanceKm);
        actual.TargetEventDate.Should().Be(expectedTargetEventDate);
    }

    [Fact]
    public void Create_PlanGenerated_TargetEventFieldsNull_ForGeneralFitness()
    {
        // Arrange
        var generated = new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PlanStartDate,
            PromptVersion,
            ModelId,
            PreviousPlanId: null,
            TargetEventName: null,
            TargetEventDistanceKm: null,
            TargetEventDate: null);

        // Act
        var actual = PlanProjection.Create(generated);

        // Assert
        actual.TargetEventName.Should().BeNull();
        actual.TargetEventDistanceKm.Should().BeNull();
        actual.TargetEventDate.Should().BeNull();
    }

    [Fact]
    public void Apply_MesoCycleCreated_AppendsWeekInOrder()
    {
        // Arrange
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var expectedCount = 2;
        int[] expectedWeekNumbers = [1, 2];

        // Act
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), actualDto);

        // Assert
        actualDto.MesoWeeks.Should().HaveCount(expectedCount);
        actualDto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(expectedWeekNumbers);
    }

    [Fact]
    public void Apply_MesoCycleCreated_FourWeeks_PopulatesAllSlotsInWeekOrder()
    {
        // Arrange — the canonical Slice 1 four-meso sequence.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var expectedCount = 4;
        int[] expectedWeekNumbers = [1, 2, 3, 4];
        var expectedFinalIsDeload = true;
        var expectedFinalPhase = PhaseType.Build;

        // Act — append in ascending order as IPlanGenerationService emits them.
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), actualDto);

        // Assert
        actualDto.MesoWeeks.Should().HaveCount(expectedCount);
        actualDto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(expectedWeekNumbers);
        actualDto.MesoWeeks[3].IsDeloadWeek.Should().Be(expectedFinalIsDeload);
        actualDto.MesoWeeks[3].PhaseType.Should().Be(expectedFinalPhase);
    }

    [Fact]
    public void Apply_MesoCycleCreated_OutOfOrderArrival_StillSortsByWeekNumber()
    {
        // Arrange — defensive: even if Marten codegen reorders applies during
        // a re-projection run, the frontend still sees ascending week order.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        int[] expectedWeekNumbers = [1, 2, 3, 4];

        // Act — deliberately apply weeks in non-ascending order.
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), actualDto);

        // Assert
        actualDto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(expectedWeekNumbers);
    }

    [Fact]
    public void Apply_MesoCycleCreated_DuplicateWeekIndex_ReplacesEntry()
    {
        // Arrange — a re-projection that re-applies the same week index keeps
        // the slot count stable and uses the latest payload.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var expectedCount = 1;
        var expectedReplacementWeeklyTargetKm = 60;
        var originalMeso = BuildMeso(1, PhaseType.Base, isDeload: false);
        var replacementMeso = originalMeso with { WeeklyTargetKm = expectedReplacementWeeklyTargetKm };

        // Act
        PlanProjection.Apply(new MesoCycleCreated(1, originalMeso), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(1, replacementMeso), actualDto);

        // Assert
        actualDto.MesoWeeks.Should().HaveCount(expectedCount);
        actualDto.MesoWeeks[0].WeeklyTargetKm.Should().Be(expectedReplacementWeeklyTargetKm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Apply_MesoCycleCreated_NonPositiveWeekIndex_Throws(int invalidWeekIndex)
    {
        // Arrange — guard against malformed events; week indices are 1-based by spec.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var meso = BuildMeso(1, PhaseType.Base, isDeload: false);

        // Act
        var act = () => PlanProjection.Apply(new MesoCycleCreated(invalidWeekIndex, meso), actualDto);

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*event.WeekIndex must be 1-based*");
    }

    [Fact]
    public void Apply_MesoCycleCreated_WeekNumberMismatch_Throws()
    {
        // Arrange — the find-or-replace logic keys by WeekIndex but writes
        // Meso payloads carrying their own WeekNumber; divergence would
        // produce a slot indexed under one number and rendered under another.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var divergentMeso = BuildMeso(2, PhaseType.Base, isDeload: false);

        // Act — event WeekIndex=1 but payload WeekNumber=2.
        var act = () => PlanProjection.Apply(new MesoCycleCreated(1, divergentMeso), actualDto);

        // Assert
        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*WeekIndex=1*WeekNumber=2*");
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_PopulatesWeek1Slot()
    {
        // Arrange
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var expectedMicro = BuildMicro();
        var expectedWeekKey = 1;

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(expectedMicro), actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek.Should().ContainKey(expectedWeekKey);
        actualDto.MicroWorkoutsByWeek[expectedWeekKey].Should().BeSameAs(expectedMicro);
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_ReApply_ReplacesExistingWeek1Entry()
    {
        // Arrange — a Marten Daemon re-projection replays historical events
        // against a partially built DTO. The week-1 slot may already be
        // populated when the event is reapplied; the projection contract is to
        // overwrite, never throw or skip. Locks in the idempotent-replay
        // semantics of <see cref="PlanProjection.Apply(FirstMicroCycleCreated, PlanProjectionDto)"/>.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var expectedReplacementMicro = BuildMicro();
        var expectedWeekKey = 1;
        var expectedSlotCount = 1;
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), actualDto);

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(expectedReplacementMicro), actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek.Should().HaveCount(expectedSlotCount);
        actualDto.MicroWorkoutsByWeek[expectedWeekKey].Should().BeSameAs(expectedReplacementMicro);
    }

    [Fact]
    public void Apply_FirstMicroCycleCreated_DoesNotMutateOtherKeys()
    {
        // Arrange — a future slice may have already populated week 2, week 3, etc.
        // Re-applying the first-week event must not clobber those entries.
        var actualDto = PlanProjection.Create(BuildPlanGenerated());
        var expectedWeekTwoMicro = BuildMicro();
        var expectedRetainedWeekKey = 2;
        var expectedNewlyPopulatedWeekKey = 1;
        actualDto.MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
        {
            [expectedRetainedWeekKey] = expectedWeekTwoMicro,
        };

        // Act
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek.Should().ContainKeys(expectedNewlyPopulatedWeekKey, expectedRetainedWeekKey);
        actualDto.MicroWorkoutsByWeek[expectedRetainedWeekKey].Should().BeSameAs(expectedWeekTwoMicro);
    }

    [Fact]
    public void FullSequence_PlanGeneratedFourMesoFirstMicro_LandsCanonicalShape()
    {
        // Arrange — the canonical Slice 1 plan event order:
        // [PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated].
        var expectedPlanId = PlanId;
        var expectedUserId = UserId;
        var expectedMesoCount = 4;
        var expectedSingleMicroWeekKey = 1;
        var generated = BuildPlanGenerated();

        // Act
        var actualDto = PlanProjection.Create(generated);
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), actualDto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), actualDto);
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), actualDto);

        // Assert
        actualDto.PlanId.Should().Be(expectedPlanId);
        actualDto.UserId.Should().Be(expectedUserId);
        actualDto.Macro.Should().NotBeNull();
        actualDto.MesoWeeks.Should().HaveCount(expectedMesoCount);
        actualDto.MicroWorkoutsByWeek.Should().ContainSingle().Which.Key.Should().Be(expectedSingleMicroWeekKey);
        actualDto.MicroWorkoutsByWeek[expectedSingleMicroWeekKey].Workouts.Should().NotBeEmpty();
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_Restructure_SwapsCurrentWeekWorkout_AndChangesUpcomingMesoTarget()
    {
        // Arrange — canonical plan: macro + four meso weeks + week-1 micro detail.
        var actualDto = BuildCanonicalDto();
        var originalWorkout = actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0);
        var revisedWorkout = originalWorkout with { Title = "Reduced Easy Run", TargetDistanceKm = 5 };
        var originalWeek2Target = actualDto.MesoWeeks.Single(w => w.WeekNumber == 2).WeeklyTargetKm;
        var expectedWeek2Target = originalWeek2Target - 10;
        var diff = new PlanAdaptationDiff(
            [new WorkoutChange(WeekNumber: 1, DayOfWeek: 0, originalWorkout, revisedWorkout)],
            [new WeeklyTargetChange(WeekNumber: 2, originalWeek2Target, expectedWeek2Target)]);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Green,
            "Backed off week-2 volume after a rough patch; we build back from there.",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0)
            .Should().BeEquivalentTo(revisedWorkout, because: "the restructure swaps the current micro week's workout");
        actualDto.MesoWeeks.Single(w => w.WeekNumber == 2).WeeklyTargetKm
            .Should().Be(expectedWeek2Target, because: "the restructure revises the upcoming meso weekly target");
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_RevisesUpcomingMesoTarget_WithoutSynthesizingFutureMicroDetail()
    {
        // Arrange — only week 1 carries micro detail today; a restructure that
        // revises an upcoming meso target must not invent micro weeks (spec non-goal).
        var actualDto = BuildCanonicalDto();
        var originalWeek3Target = actualDto.MesoWeeks.Single(w => w.WeekNumber == 3).WeeklyTargetKm;
        var expectedWeek3Target = originalWeek3Target - 5;
        var diff = new PlanAdaptationDiff(
            WorkoutChanges: [],
            [new WeeklyTargetChange(WeekNumber: 3, originalWeek3Target, expectedWeek3Target)]);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Green,
            "Trimmed week 3 to keep the build sustainable.",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert
        actualDto.MesoWeeks.Single(w => w.WeekNumber == 3).WeeklyTargetKm
            .Should().Be(expectedWeek3Target);
        actualDto.MicroWorkoutsByWeek.Should().ContainSingle()
            .Which.Key.Should().Be(1, because: "adaptation never synthesizes micro detail for future weeks");
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_Nudge_AppliesCurrentWeekWorkoutChange()
    {
        // Arrange
        var actualDto = BuildCanonicalDto();
        var original = actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0);
        var moved = original with { Title = "Moved Easy Run", TargetDistanceKm = 6 };
        var diff = new PlanAdaptationDiff([new WorkoutChange(1, 0, original, moved)], []);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Shuffled your easy run so the week still works.",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0)
            .Should().BeEquivalentTo(moved);
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_WorkoutChangeForWeekWithoutMicroDetail_IsSkipped()
    {
        // Arrange — a workout change targeting week 2 (no micro detail today) must
        // be skipped, not synthesize a new micro week.
        var actualDto = BuildCanonicalDto();
        var phantom = BuildMicro().Workouts[0] with { TargetDistanceKm = 99 };
        var diff = new PlanAdaptationDiff([new WorkoutChange(WeekNumber: 2, DayOfWeek: 0, Before: null, After: phantom)], []);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "n/a",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek.Should().ContainSingle()
            .Which.Key.Should().Be(1, because: "a change targeting a week with no micro detail must not synthesize one");
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_WorkoutChangeWithNullAfter_LeavesWorkoutUnchanged()
    {
        // Arrange — a removal (null After) is not modeled this slice; the existing
        // workout must remain rather than being dropped.
        var actualDto = BuildCanonicalDto();
        var existing = actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0);
        var diff = new PlanAdaptationDiff([new WorkoutChange(1, 0, existing, After: null)], []);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "n/a",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert
        actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0)
            .Should().BeEquivalentTo(existing, because: "a null After (removal) is skipped, not applied, this slice");
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_EmptyDiff_LeavesProjectionUnchanged()
    {
        // Arrange
        var actualDto = BuildCanonicalDto();
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Nothing to change.",
            PlanAdaptationDiff.Empty);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert
        actualDto.MesoWeeks.Select(w => w.WeeklyTargetKm).Should().Equal(45, 45, 45, 30);
        actualDto.MicroWorkoutsByWeek[1].Workouts.Single(w => w.DayOfWeek == 0).TargetDistanceKm.Should().Be(8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_PlanAdaptedFromLog_NonPositiveWeekNumber_Throws(int invalidWeekNumber)
    {
        // Arrange — week numbers are 1-based by spec; a malformed diff must fail
        // the transaction rather than silently corrupt the read model.
        var actualDto = BuildCanonicalDto();
        var diff = new PlanAdaptationDiff([], [new WeeklyTargetChange(invalidWeekNumber, 45, 40)]);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Green,
            "n/a",
            diff);

        // Act
        var act = () => PlanProjection.Apply(adaptation, actualDto);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_PlanAdaptedFromLog_NonPositiveWeekNumber_InWorkoutChange_Throws(int invalidWeekNumber)
    {
        // Arrange — Apply validates 1-based week numbers in a SECOND loop over
        // WorkoutChanges. Carry the invalid week on a WorkoutChange with NO weekly
        // target changes so this exercises the workout-change loop specifically: a
        // regression deleting just that loop would otherwise pass undetected.
        var actualDto = BuildCanonicalDto();
        var phantom = BuildMicro().Workouts[0];
        var diff = new PlanAdaptationDiff(
            [new WorkoutChange(invalidWeekNumber, DayOfWeek: 0, Before: phantom, After: phantom)],
            []);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "n/a",
            diff);

        // Act
        var act = () => PlanProjection.Apply(adaptation, actualDto);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_WeeklyTargetChangeForAbsentWeek_LeavesMesoWeeksUnchanged()
    {
        // Arrange — a target change for a positive week NOT present in MesoWeeks
        // (week 5 against the 4-week canonical plan) is silently skipped: the meso
        // tier is authoritative and the projection never synthesizes a week.
        var actualDto = BuildCanonicalDto();
        int[] expectedTargets = [45, 45, 45, 30];
        var diff = new PlanAdaptationDiff(
            WorkoutChanges: [],
            [new WeeklyTargetChange(WeekNumber: 5, BeforeWeeklyTargetKm: 40, AfterWeeklyTargetKm: 35)]);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Restructure,
            EscalationLevel.Restructure,
            SafetyTier.Green,
            "n/a",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert — no week is synthesized and the absent-week target change is skipped.
        actualDto.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(1, 2, 3, 4);
        actualDto.MesoWeeks.Select(w => w.WeeklyTargetKm).Should().Equal(expectedTargets);
    }

    [Fact]
    public void Apply_PlanAdaptedFromLog_WorkoutChangeForAbsentDay_AddsWorkoutToMicroWeek()
    {
        // Arrange — the canonical week-1 micro carries a single workout on day 0; a
        // change whose DayOfWeek matches no existing workout takes the add branch
        // (the null-Before addition WorkoutChange documents — e.g. a workout on a
        // formerly-rest day) rather than replacing one.
        var actualDto = BuildCanonicalDto();
        const int absentDay = 3;
        var addedWorkout = BuildMicro().Workouts[0] with { DayOfWeek = absentDay, Title = "Added Tempo" };
        var diff = new PlanAdaptationDiff(
            [new WorkoutChange(WeekNumber: 1, DayOfWeek: absentDay, Before: null, After: addedWorkout)],
            []);
        var adaptation = new PlanAdaptedFromLog(
            Guid.NewGuid(),
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Added a tempo on your former rest day.",
            diff);

        // Act
        PlanProjection.Apply(adaptation, actualDto);

        // Assert — the original day-0 workout is retained and the new day-3 one is appended.
        var workouts = actualDto.MicroWorkoutsByWeek[1].Workouts;
        workouts.Should().HaveCount(2, because: "an absent-day change adds rather than replaces");
        workouts.Should().ContainSingle(w => w.DayOfWeek == 0);
        workouts.Single(w => w.DayOfWeek == absentDay).Should().BeEquivalentTo(addedWorkout);
    }

    private static PlanProjectionDto BuildCanonicalDto()
    {
        var dto = PlanProjection.Create(BuildPlanGenerated());
        PlanProjection.Apply(new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)), dto);
        PlanProjection.Apply(new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)), dto);
        PlanProjection.Apply(new FirstMicroCycleCreated(BuildMicro()), dto);
        return dto;
    }

    private static PlanGenerated BuildPlanGenerated(
        string? targetEventName = null,
        double? targetEventDistanceKm = null,
        DateOnly? targetEventDate = null)
    {
        return new PlanGenerated(
            PlanId,
            UserId,
            BuildMacro(),
            Now,
            PlanStartDate,
            PromptVersion,
            ModelId,
            PreviousPlanId: null,
            TargetEventName: targetEventName,
            TargetEventDistanceKm: targetEventDistanceKm,
            TargetEventDate: targetEventDate);
    }

    private static MacroPlanOutput BuildMacro()
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 16,
            GoalDescription = "Half Marathon",
            Phases = new[]
            {
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 8,
                    WeeklyDistanceStartKm = 30,
                    WeeklyDistanceEndKm = 50,
                    IntensityDistribution = "80/20 easy/hard",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Recovery },
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 300,
                    Notes = "Aerobic base build.",
                    IncludesDeload = true,
                },
            },
            Rationale = "Progressive base then build to race specificity.",
            Warnings = "Stop and reassess if any sharp pain emerges.",
        };
    }

    private static MesoWeekOutput BuildMeso(int weekNumber, PhaseType phase, bool isDeload)
    {
        var restSlot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = "Recovery.",
        };
        var easySlot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Run,
            WorkoutType = WorkoutType.Easy,
            Notes = "Easy aerobic.",
        };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = phase,
            WeeklyTargetKm = isDeload ? 30 : 45,
            IsDeloadWeek = isDeload,
            Sunday = easySlot,
            Monday = restSlot,
            Tuesday = easySlot,
            Wednesday = restSlot,
            Thursday = easySlot,
            Friday = restSlot,
            Saturday = easySlot,
            WeekSummary = $"Week {weekNumber} - {phase}.",
        };
    }

    private static MicroWorkoutListOutput BuildMicro()
    {
        return new MicroWorkoutListOutput
        {
            Workouts = new[]
            {
                new WorkoutOutput
                {
                    DayOfWeek = 0,
                    WorkoutType = WorkoutType.Easy,
                    Title = "Easy Aerobic Run",
                    TargetDistanceKm = 8,
                    TargetDurationMinutes = 50,
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 360,
                    Segments = new[]
                    {
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Warmup,
                            DurationMinutes = 10,
                            TargetPaceSecPerKm = 400,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Warm up gradually.",
                        },
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Work,
                            DurationMinutes = 30,
                            TargetPaceSecPerKm = 360,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Steady aerobic effort.",
                        },
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Cooldown,
                            DurationMinutes = 10,
                            TargetPaceSecPerKm = 420,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Cool down easy.",
                        },
                    },
                    WarmupNotes = "10 min walk-jog.",
                    CooldownNotes = "10 min walk-jog.",
                    CoachingNotes = "Conversational pace.",
                    PerceivedEffort = 3,
                },
            },
        };
    }
}
