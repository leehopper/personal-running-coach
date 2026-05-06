using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Deterministic completion gate per Slice 1 § Unit 1 R01.6 — a pure function
/// over <see cref="OnboardingView"/> that returns whether the onboarding flow
/// has captured every required slot AND has no outstanding clarifications.
/// </summary>
/// <remarks>
/// <para>
/// The gate is intentionally separate from any LLM judgment. The
/// <c>ReadyForPlan</c> field on <see cref="OnboardingTurnOutput"/> is an
/// additive precondition only — the handler never appends
/// <see cref="OnboardingCompleted"/> unless this gate also returns true.
/// </para>
/// <para>
/// Required slots are <c>PrimaryGoal</c>, <c>WeeklySchedule</c>,
/// <c>CurrentFitness</c>, <c>InjuryHistory</c>, <c>Preferences</c>.
/// <c>TargetEvent</c> is required only when <c>PrimaryGoal == RaceTraining</c>.
/// </para>
/// </remarks>
public static class OnboardingCompletionGate
{
    /// <summary>
    /// Evaluates the gate against the supplied <paramref name="view"/>.
    /// </summary>
    /// <param name="view">The runner's in-flight onboarding projection.</param>
    /// <returns><see langword="true"/> if every required slot is captured AND
    /// there are no outstanding clarifications; <see langword="false"/> otherwise.</returns>
    public static bool IsSatisfied(OnboardingView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.PrimaryGoal is null
            || view.WeeklySchedule is null
            || view.CurrentFitness is null
            || view.InjuryHistory is null
            || view.Preferences is null)
        {
            return false;
        }

        if (view.PrimaryGoal.Goal == PrimaryGoal.RaceTraining && view.TargetEvent is null)
        {
            return false;
        }

        if (view.OutstandingClarifications.Count > 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the next topic the deterministic next-topic selector should ask
    /// about, or <see langword="null"/> when the gate is satisfied.
    /// </summary>
    /// <param name="view">The runner's in-flight onboarding projection.</param>
    /// <returns>The next topic to ask, or <see langword="null"/> when complete.</returns>
    public static OnboardingTopic? NextTopic(OnboardingView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.PrimaryGoal is null)
        {
            return OnboardingTopic.PrimaryGoal;
        }

        if (view.PrimaryGoal.Goal == PrimaryGoal.RaceTraining && view.TargetEvent is null)
        {
            return OnboardingTopic.TargetEvent;
        }

        if (view.CurrentFitness is null)
        {
            return OnboardingTopic.CurrentFitness;
        }

        if (view.WeeklySchedule is null)
        {
            return OnboardingTopic.WeeklySchedule;
        }

        if (view.InjuryHistory is null)
        {
            return OnboardingTopic.InjuryHistory;
        }

        if (view.Preferences is null)
        {
            return OnboardingTopic.Preferences;
        }

        return null;
    }

    /// <summary>
    /// Counts how many of the canonical six topics have a captured answer on
    /// the <paramref name="view"/>. <c>TargetEvent</c> only counts when the
    /// runner's <c>PrimaryGoal</c> is race training; otherwise it is treated
    /// as N/A and excluded from both the numerator AND the denominator —
    /// callers receive a progress indicator that reflects the runner's actual
    /// remaining work.
    /// </summary>
    /// <param name="view">The runner's in-flight onboarding projection.</param>
    /// <returns>The (completed, total) pair the chat UI's progress indicator renders.</returns>
    public static (int Completed, int Total) Progress(OnboardingView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var requiresTargetEvent = view.PrimaryGoal?.Goal == PrimaryGoal.RaceTraining;
        var total = requiresTargetEvent ? 6 : 5;
        var completed = 0;

        if (view.PrimaryGoal is not null)
        {
            completed++;
        }

        if (requiresTargetEvent && view.TargetEvent is not null)
        {
            completed++;
        }

        if (view.CurrentFitness is not null)
        {
            completed++;
        }

        if (view.WeeklySchedule is not null)
        {
            completed++;
        }

        if (view.InjuryHistory is not null)
        {
            completed++;
        }

        if (view.Preferences is not null)
        {
            completed++;
        }

        return (completed, total);
    }
}
