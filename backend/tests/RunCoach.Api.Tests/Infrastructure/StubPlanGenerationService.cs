using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Deterministic <see cref="IPlanGenerationService"/> stub used by every
/// integration test that drives <c>POST /api/v1/plan/regenerate</c> or
/// <c>POST /api/v1/onboarding/turn</c> through the live HTTP + Wolverine bus
/// pipeline. Replaces the production <c>PlanGenerationService</c> registration
/// in <see cref="RunCoachAppFactory"/> so Wolverine's handler-chain codegen
/// resolves this stub when it instantiates the regenerate / onboarding
/// terminal-branch handlers — no LLM calls, fully deterministic event sequence,
/// and the full Marten + EF projection stack is exercised end-to-end.
/// </summary>
/// <remarks>
/// <para>
/// Returns the canonical Slice 1 event sequence:
/// <c>[PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated]</c> with the
/// <see cref="PlanGenerated.PreviousPlanId"/> slot threaded through from the
/// <c>previousPlanId</c> parameter exactly the way
/// <see cref="PlanGenerationService"/> does in production. The only thing this
/// stub elides is the six structured-output LLM calls.
/// </para>
/// <para>
/// Live LLM coverage of the structured-output chain remains the responsibility
/// of <c>PlanGenerationServiceTests</c> (eval-cached unit coverage) and the
/// committed manual smoke proof captured at T05.1 (commit <c>13464e0</c>) — the
/// integration tier covers the Marten + Wolverine + EF projection wiring, which
/// is what the regenerate / onboarding flows actually own.
/// </para>
/// </remarks>
public sealed class StubPlanGenerationService : IPlanGenerationService
{
    public Task<IReadOnlyList<object>> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct)
    {
        _ = profileSnapshot;
        _ = intent;

        var generated = new PlanGenerated(
            planId,
            userId,
            BuildMacro(goal: previousPlanId is null ? "Stub plan" : "Regenerated plan"),
            new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero),
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: previousPlanId);

        var events = new object[]
        {
            generated,
            new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)),
            new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)),
            new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)),
            new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)),
            new FirstMicroCycleCreated(BuildMicro()),
        };

        return Task.FromResult<IReadOnlyList<object>>(events);
    }

    internal static MacroPlanOutput BuildMacro(string goal)
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 16,
            GoalDescription = goal,
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

    internal static MesoWeekOutput BuildMeso(int weekNumber, PhaseType phase, bool isDeload)
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

    internal static MicroWorkoutListOutput BuildMicro()
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
