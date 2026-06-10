using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Runs an <see cref="EscalationScenario"/> through the real deterministic chain
/// — <see cref="DeviationEngine"/> then <see cref="EscalationClassifier"/> — with
/// each step's prescription grounded in the scenario profile's Daniels-Gilbert
/// zones. The signal state is threaded across steps so multi-step scenarios
/// exercise the rolling-score, consecutive-miss, and hysteresis logic exactly as
/// the production handler would. No LLM, no Marten — this is the calibration
/// harness for the threshold constants (Unit 6).
/// </summary>
internal static class EscalationScenarioRunner
{
    /// <summary>Half-width (sec/km) applied around a single-point pace zone to form a band.</summary>
    private const double SinglePointBandHalfWidthSecPerKm = 15.0;

    /// <summary>Nominal prescribed distance (km). Absolute value is irrelevant to the classifier — only the deviations matter.</summary>
    private const double NominalPrescribedKm = 6.0;

    /// <summary>Margin (sec/km) past a band bound so an intended slow/fast pace decisively clears the bound for any band width.</summary>
    private const double BandClearanceSecPerKm = 20.0;

    private static readonly DateOnly BaseDate = new(2026, 6, 1);
    private static readonly Guid PlanId = Guid.Parse("9e7c0c00-0000-4000-8000-00000000ada9");

    /// <summary>
    /// Runs the scenario and returns the resolved final level and the per-step levels.
    /// </summary>
    /// <param name="profile">The runner profile whose pace zones ground the prescriptions.</param>
    /// <param name="scenario">The scenario to run.</param>
    /// <returns>The chain's result across all steps.</returns>
    internal static EscalationScenarioRun Run(TestProfile profile, EscalationScenario scenario)
    {
        var deviationEngine = new DeviationEngine();
        var classifier = new EscalationClassifier(NullLogger<EscalationClassifier>.Instance);

        var state = AdaptationSignalState.Initial;
        var levels = new List<EscalationLevel>(scenario.Steps.Count);

        foreach (var step in scenario.Steps)
        {
            var (fast, slow) = ResolveBand(profile, step.WorkoutType);
            var bandMidSecPerKm = (fast.SecondsPerKm + slow.SecondsPerKm) / 2.0;
            var prescribedMinutes = MinutesFor(NominalPrescribedKm, bandMidSecPerKm);

            var snapshot = WorkoutPrescriptionSnapshot.Create(
                sourcePlanId: PlanId,
                weekNumber: 1,
                dayOfWeek: step.DayOffset % 7,
                workoutType: step.WorkoutType,
                prescribedDistance: Distance.FromKilometers(NominalPrescribedKm),
                prescribedDuration: Duration.FromMinutes(prescribedMinutes),
                prescribedPaceFast: fast,
                prescribedPaceSlow: slow);

            var (actualKm, actualMinutes, status) = RealizeIntent(step.Intent, fast, slow, bandMidSecPerKm);
            var log = BuildLog(actualKm, actualMinutes, status, snapshot, step.DayOffset);

            // Always on-plan here (a snapshot is present), so Evaluate is non-null.
            var deviation = deviationEngine.Evaluate(log)!;
            var decision = classifier.Classify(deviation, scenario.SafetyTier, state);

            state = decision.NextState;
            levels.Add(decision.EscalationLevel);
        }

        return new EscalationScenarioRun(levels[^1], levels);
    }

    private static WorkoutLog BuildLog(
        double actualKm,
        double actualMinutes,
        CompletionStatus status,
        WorkoutPrescriptionSnapshot snapshot,
        int dayOffset) =>
        new()
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid().ToString(),
            IdempotencyKey = Guid.NewGuid(),
            OccurredOn = BaseDate.AddDays(dayOffset),
            Distance = Distance.FromKilometers(actualKm),
            Duration = Duration.FromMinutes(actualMinutes),
            CompletionStatus = status,
            Prescription = snapshot,
            CreatedOn = default,
            ModifiedOn = default,
        };

    /// <summary>Minutes to cover <paramref name="km"/> at <paramref name="secPerKm"/>.</summary>
    private static double MinutesFor(double km, double secPerKm) => km * secPerKm / 60.0;

    /// <summary>
    /// Translates a <see cref="DeviationIntent"/> into concrete actuals against the
    /// resolved band. Slow/fast intents are computed past the band bound by a fixed
    /// clearance so the intended band membership holds for any band width.
    /// </summary>
    private static (double ActualKm, double ActualMinutes, CompletionStatus Status) RealizeIntent(
        DeviationIntent intent,
        Pace fast,
        Pace slow,
        double bandMidSecPerKm)
    {
        var slowerThanSlow = slow.SecondsPerKm + BandClearanceSecPerKm;
        var fasterThanFast = fast.SecondsPerKm - BandClearanceSecPerKm;

        return intent switch
        {
            // In-band, on distance.
            DeviationIntent.OnTarget =>
                (NominalPrescribedKm, MinutesFor(NominalPrescribedKm, bandMidSecPerKm), CompletionStatus.Complete),

            // ~2% short — inside the ±5% distance tolerance, pace at band mid.
            DeviationIntent.WithinTolerance =>
                (NominalPrescribedKm * 0.98, MinutesFor(NominalPrescribedKm * 0.98, bandMidSecPerKm), CompletionStatus.Complete),

            // Full distance, decisively slower than the slow bound.
            DeviationIntent.MinorSlow =>
                (NominalPrescribedKm, MinutesFor(NominalPrescribedKm, slowerThanSlow), CompletionStatus.Complete),

            // 15% short, pace held in-band (isolating the distance-shortfall clause).
            DeviationIntent.ShortDistance =>
                (NominalPrescribedKm * 0.85, MinutesFor(NominalPrescribedKm * 0.85, bandMidSecPerKm), CompletionStatus.Complete),

            // Cut short and slow — under-performs via the not-Complete clause, never a missed day.
            DeviationIntent.Partial =>
                (NominalPrescribedKm * 0.6, MinutesFor(NominalPrescribedKm * 0.6, slowerThanSlow), CompletionStatus.Partial),

            // Slightly longer and decisively faster than the fast bound.
            DeviationIntent.OverPerform =>
                (NominalPrescribedKm * 1.05, MinutesFor(NominalPrescribedKm * 1.05, fasterThanFast), CompletionStatus.Complete),

            // ~3% long at an in-band pace near the fast bound — beats the prescription
            // within the easy-day over-performance cap.
            DeviationIntent.OverPerformInCap =>
                (NominalPrescribedKm * 1.03, MinutesFor(NominalPrescribedKm * 1.03, fast.SecondsPerKm + 5.0), CompletionStatus.Complete),

            // Skipped entirely.
            DeviationIntent.Missed =>
                (0.0, 0.0, CompletionStatus.Skipped),

            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unsupported deviation intent."),
        };
    }

    private static (Pace Fast, Pace Slow) ResolveBand(TestProfile profile, WorkoutType type)
    {
        var paces = profile.GoalState.CurrentFitnessEstimate.TrainingPaces;

        return type switch
        {
            WorkoutType.Easy or WorkoutType.LongRun or WorkoutType.Recovery or WorkoutType.CrossTrain =>
                EasyBand(profile, paces),
            WorkoutType.Tempo => Widen(Require(profile, type, paces.ThresholdPace)),
            WorkoutType.Interval => Widen(Require(profile, type, paces.IntervalPace)),
            WorkoutType.Repetition => Widen(Require(profile, type, paces.RepetitionPace)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported workout type."),
        };
    }

    private static (Pace Fast, Pace Slow) EasyBand(TestProfile profile, TrainingPaces paces)
    {
        var range = paces.EasyPaceRange
            ?? throw new InvalidOperationException(
                $"Profile '{profile.UserProfile.Name}' has no easy pace range.");
        return (range.Fast, range.Slow);
    }

    private static (Pace Fast, Pace Slow) Widen(Pace center) =>
        (Pace.FromSecondsPerKm(center.SecondsPerKm - SinglePointBandHalfWidthSecPerKm),
         Pace.FromSecondsPerKm(center.SecondsPerKm + SinglePointBandHalfWidthSecPerKm));

    private static Pace Require(TestProfile profile, WorkoutType type, Pace? pace) =>
        pace ?? throw new InvalidOperationException(
            $"Profile '{profile.UserProfile.Name}' has no pace zone for {type}; scenario mis-authored.");
}
