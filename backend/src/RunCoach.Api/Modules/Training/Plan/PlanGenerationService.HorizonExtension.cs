using System.Diagnostics;
using System.Globalization;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Rolling-horizon extension seam (DEC-090). Adds <see cref="GenerateWeekAsync"/> — generating the
/// meso template and/or the detailed micro workouts for one target plan week — plus its sibling
/// prompt builders, correction builder, and loggers. This PR ships the seam only: no handler and no
/// sweeper call it yet (PR2/PR3); it is exercised only by unit tests in this PR. Split into its own
/// partial file (rather than growing <c>PlanGenerationService.cs</c>) so the bootstrap chain's
/// byte-frozen prompt builders stay visibly untouched.
/// </summary>
public sealed partial class PlanGenerationService
{
    /// <summary>
    /// Activity name for the parent span wrapping one <see cref="GenerateWeekAsync"/> call. Sibling
    /// to <see cref="PlanGenerationActivityName"/> (the bootstrap chain's parent span).
    /// </summary>
    internal const string HorizonWeekActivityName = "runcoach.plan.horizon.week";

    /// <summary>
    /// Marker label that begins the per-retry meso correction suffix appended to the horizon
    /// extension meso user message when a bounded validation-rejection retry fires (DEC-090). Same
    /// wire bytes as <see cref="MacroCorrectionLabel"/> / <see cref="MicroCorrectionLabel"/>, held
    /// under its own name so the horizon-extension meso retry tests can locate it symmetrically.
    /// </summary>
    internal const string MesoCorrectionLabel = "[CORRECTION]";

    /// <summary>
    /// Upper bound on the configured <see cref="_settings"/> meso-validation retry count for the
    /// horizon extension seam, so a misconfigured large value can't burn dozens of extra LLM calls
    /// per extension call. Mirrors <see cref="MaxAllowedMacroValidationRetries"/> /
    /// <see cref="MaxAllowedMicroValidationRetries"/>.
    /// </summary>
    private const int MaxAllowedMesoValidationRetries = 5;

    /// <inheritdoc />
    public async Task<WeekGenerationResult> GenerateWeekAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        MacroPlanOutput macro,
        DateOnly planStartDate,
        DateOnly? targetEventDate,
        int targetWeekIndex,
        MesoWeekOutput? existingMesoWeek,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profileSnapshot);
        ArgumentNullException.ThrowIfNull(macro);
        if (targetWeekIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(targetWeekIndex), targetWeekIndex, "Week index is 1-based.");
        }

        ct.ThrowIfCancellationRequested();

        // Own parent span, sibling to the bootstrap chain's PlanGenerationActivityName — the
        // caller's own activity (the eventual PR2 handler / PR3 sweeper, or a unit test) is the
        // ambient parent.
        using var weekActivity = ActivitySource.StartActivity(HorizonWeekActivityName, ActivityKind.Internal);
        weekActivity?.SetTag(PlanGenerationTagNames.PlanId, planId.ToString());
        weekActivity?.SetTag(PlanGenerationTagNames.UserId, userId.ToString());
        weekActivity?.SetTag(PlanGenerationTagNames.WeekIndex, targetWeekIndex);

        try
        {
            var today = _localDate.Today();
            var horizon = PlanHorizonCalculator.Compute(planStartDate, targetEventDate);

            // Same cacheable-prefix composition the bootstrap chain uses — no regeneration intent on
            // the extension path.
            var composition = await _assembler
                .ComposeForPlanGenerationAsync(profileSnapshot, intent: null, today, horizon, ct)
                .ConfigureAwait(false);

            var systemPrompt = composition.SystemPrompt;
            var basePrompt = composition.UserMessage;

            // Meso tier — conditional. When the caller already has the target week's meso (the
            // micro-only backfill case), skip generation entirely and use it as the micro tier's
            // source of truth. Otherwise generate it with the same bounded corrective-hint retry
            // shape as the bootstrap macro tier (DEC-087), guarded by MesoWeekOutputValidator
            // (DEC-090 D8) instead of the bootstrap loop's total silence on validation.
            MesoWeekOutput targetMeso;
            MesoCycleCreated? mesoEvent;
            if (existingMesoWeek is not null)
            {
                targetMeso = existingMesoWeek;
                mesoEvent = null;
            }
            else
            {
                var maxMesoRetries = Math.Clamp(_settings.MesoValidationMaxRetries, 0, MaxAllowedMesoValidationRetries);
                var weekContext = WeekContext.FromMacro(macro, targetWeekIndex);
                var mesoAttempts = 0;
                string? mesoCorrection = null;
                while (true)
                {
                    mesoAttempts++;
                    var (candidate, _, _) = await InvokeTierAsync<MesoWeekOutput>(
                        tier: TierMeso,
                        planId: planId,
                        systemPrompt: systemPrompt,
                        userMessage: BuildHorizonExtensionMesoUserMessage(basePrompt, macro, weekContext, mesoCorrection),
                        extraTags: a =>
                        {
                            a?.SetTag(PlanGenerationTagNames.WeekIndex, targetWeekIndex);
                            a?.SetTag(PlanGenerationTagNames.IsDeloadCandidate, weekContext.IsDeloadCandidate);
                        },
                        ct).ConfigureAwait(false);

                    var validation = MesoWeekOutputValidator.Validate(candidate, targetWeekIndex);
                    if (validation.IsValid)
                    {
                        targetMeso = candidate;
                        break;
                    }

                    if (mesoAttempts > maxMesoRetries)
                    {
                        LogHorizonMesoRejected(_logger, planId, targetWeekIndex, validation.Violation, mesoAttempts);
                        throw new MesoWeekRejectedException(validation.Violation);
                    }

                    LogHorizonMesoRetry(_logger, planId, targetWeekIndex, validation.Violation, mesoAttempts);
                    mesoCorrection = BuildHorizonMesoCorrection(validation.Violation, candidate, targetWeekIndex);
                }

                mesoEvent = new MesoCycleCreated(targetWeekIndex, targetMeso);
            }

            // Micro tier — always generated, with the same bounded meso/micro consistency retry
            // shape as the bootstrap micro tier (DEC-088), reusing BuildMicroCorrection unchanged —
            // it is week-agnostic.
            var maxMicroRetries = Math.Clamp(_settings.MicroValidationMaxRetries, 0, MaxAllowedMicroValidationRetries);
            MicroWorkoutListOutput micro;
            var microAttempts = 0;
            string? microCorrection = null;
            while (true)
            {
                microAttempts++;
                var (candidate, _, _) = await InvokeTierAsync<MicroWorkoutListOutput>(
                    tier: TierMicro,
                    planId: planId,
                    systemPrompt: systemPrompt,
                    userMessage: BuildHorizonExtensionMicroUserMessage(basePrompt, macro, targetMeso, targetWeekIndex, microCorrection),
                    extraTags: a => a?.SetTag(PlanGenerationTagNames.WeekIndex, targetWeekIndex),
                    ct).ConfigureAwait(false);

                var consistency = MesoMicroConsistencyValidator.Validate(targetMeso, candidate);
                if (consistency.IsValid)
                {
                    micro = candidate;
                    break;
                }

                if (microAttempts > maxMicroRetries)
                {
                    LogHorizonMicroRejected(_logger, planId, targetWeekIndex, consistency.Violation, microAttempts);
                    throw new MesoMicroConsistencyRejectedException(consistency.Violation);
                }

                LogHorizonMicroRetry(_logger, planId, targetWeekIndex, consistency.Violation, microAttempts);
                microCorrection = BuildMicroCorrection(targetMeso, candidate);
            }

            var microEvent = new MicroCycleCreated(targetWeekIndex, micro);
            return new WeekGenerationResult(mesoEvent, microEvent);
        }
        catch (Exception ex)
        {
            weekActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            weekActivity?.AddException(ex);
            throw;
        }
    }

    /// <summary>
    /// Builds the deterministic per-retry correction suffix for a horizon-extension meso week
    /// rejected by <see cref="MesoWeekOutputValidator"/> (DEC-090). Names the exact defect so the
    /// re-roll becomes a narrow reconciliation task: for
    /// <see cref="MesoWeekOutputValidationViolation.WeekNumberMismatch"/> the wrong week number the
    /// model emitted vs. the one it must emit; for
    /// <see cref="MesoWeekOutputValidationViolation.NoRunDay"/> the missing-run-day defect. Internal
    /// (not private) so a unit test can assert its bytes, mirroring <see cref="BuildMicroCorrection"/>.
    /// </summary>
    internal static string BuildHorizonMesoCorrection(
        MesoWeekOutputValidationViolation violation,
        MesoWeekOutput candidate,
        int expectedWeekIndex)
    {
        switch (violation)
        {
            case MesoWeekOutputValidationViolation.WeekNumberMismatch:
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{MesoCorrectionLabel} Your previous week template set week_number to {candidate.WeekNumber} but this must be week {expectedWeekIndex}. Set week_number to {expectedWeekIndex} and keep the same day structure.");

            case MesoWeekOutputValidationViolation.NoRunDay:
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{MesoCorrectionLabel} Your previous week template scheduled no run days. Schedule at least one Run day slot for week {expectedWeekIndex} consistent with the phase's volume.");

            default:
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{MesoCorrectionLabel} Your previous week template failed validation ({violation}). Emit a valid week {expectedWeekIndex} template.");
        }
    }

    /// <summary>
    /// Appends the meso-tier suffix for the horizon extension seam on the cacheable base prompt.
    /// Deliberately a near-copy of <see cref="BuildMesoUserMessage"/> rather than a call into it, so
    /// a later PR (DEC-090 D6) can inject a <c>TrainingHistorySummary</c> block here without editing
    /// the byte-frozen bootstrap builder that the 5 committed eval fixtures pin. When
    /// <paramref name="correction"/> is non-null (a bounded validation-rejection retry) it is
    /// appended after a blank-line separator, the one capability <see cref="BuildMesoUserMessage"/>
    /// lacks.
    /// </summary>
    private static string BuildHorizonExtensionMesoUserMessage(
        string basePrompt,
        MacroPlanOutput macro,
        WeekContext weekContext,
        string? correction = null)
    {
        var sb = BeginTierMessage(basePrompt, MesoTierLabel, extraCapacity: correction is null ? 256 : 512);
        AppendMacroRecap(sb, macro);
        sb.AppendLine(CultureInfo.InvariantCulture, $"WeekIndex: {weekContext.WeekIndex}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"PhaseType: {weekContext.PhaseType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"IsDeloadCandidate: {(weekContext.IsDeloadCandidate ? "true" : "false")}");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"Generate the week template for week {weekContext.WeekIndex}.");
        if (correction is not null)
        {
            sb.AppendLine();
            sb.Append(correction);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends the micro-tier suffix for the horizon extension seam on the cacheable base prompt.
    /// Deliberately a near-copy of <see cref="BuildMicroUserMessage"/> rather than a call into it —
    /// same DEC-090 D6 rationale as <see cref="BuildHorizonExtensionMesoUserMessage"/> — generalized
    /// to an arbitrary <paramref name="weekIndex"/> and reading phase/target/deload off the supplied
    /// <paramref name="meso"/> instead of always week 1. When <paramref name="correction"/> is
    /// non-null (a bounded meso/micro consistency-rejection retry) it is appended after a blank-line
    /// separator.
    /// </summary>
    private static string BuildHorizonExtensionMicroUserMessage(
        string basePrompt,
        MacroPlanOutput macro,
        MesoWeekOutput meso,
        int weekIndex,
        string? correction = null)
    {
        var sb = BeginTierMessage(basePrompt, MicroTierLabel, extraCapacity: correction is null ? 512 : 768);
        AppendMacroRecap(sb, macro);
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week {weekIndex} phase: {meso.PhaseType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week {weekIndex} weekly target km: {meso.WeeklyTargetKm}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week {weekIndex} is deload: {(meso.IsDeloadWeek ? "true" : "false")}");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"Generate the detailed workouts for week {weekIndex}, one per scheduled run day.");
        if (correction is not null)
        {
            sb.AppendLine();
            sb.Append(correction);
        }

        return sb.ToString();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Horizon-extension meso week rejected, retrying with correction: PlanId={PlanId} WeekIndex={WeekIndex} Violation={Violation} Attempt={Attempt}")]
    private static partial void LogHorizonMesoRetry(
        ILogger logger,
        Guid planId,
        int weekIndex,
        MesoWeekOutputValidationViolation violation,
        int attempt);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Horizon-extension meso week rejected by validation (retries exhausted): PlanId={PlanId} WeekIndex={WeekIndex} Violation={Violation} Attempts={Attempts}")]
    private static partial void LogHorizonMesoRejected(
        ILogger logger,
        Guid planId,
        int weekIndex,
        MesoWeekOutputValidationViolation violation,
        int attempts);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Horizon-extension micro workouts inconsistent with meso, retrying with correction: PlanId={PlanId} WeekIndex={WeekIndex} Violation={Violation} Attempt={Attempt}")]
    private static partial void LogHorizonMicroRetry(
        ILogger logger,
        Guid planId,
        int weekIndex,
        MesoMicroConsistencyViolation violation,
        int attempt);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Horizon-extension micro workouts rejected by meso/micro consistency (retries exhausted): PlanId={PlanId} WeekIndex={WeekIndex} Violation={Violation} Attempts={Attempts}")]
    private static partial void LogHorizonMicroRejected(
        ILogger logger,
        Guid planId,
        int weekIndex,
        MesoMicroConsistencyViolation violation,
        int attempts);
}
