using System.Globalization;
using System.Xml;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Deterministic validator/mapper from the loosened wire request
/// (<see cref="SubmitStructuredAnswersRequestDto"/>) to the validated
/// <see cref="SubmitStructuredAnswers"/> command (DU-1 FR-1.8). This is the deterministic
/// replacement for the retired LLM <c>OnboardingTurnOutputValidator</c>.
/// </summary>
/// <remarks>
/// The loosened input DTOs bind without throwing, so numeric-range violations do not surface as an
/// uncatchable HTTP 500 during model binding. Validation is layered: explicit guards for the rules
/// the answer records cannot express (enum definedness, ISO date/duration parseability, the
/// TargetEvent ⇒ RaceTraining cross-field rule), then construction of the canonical answer records
/// whose <c>init</c> accessors enforce the numeric ranges — an <see cref="ArgumentException"/> from
/// any of those is caught and reported as a validation failure. The controller maps a
/// <see langword="false"/> result to a 400 <c>ProblemDetails</c>.
/// </remarks>
public static class SubmitStructuredAnswersRequestMapper
{
    // Distances above this are physically implausible. Combined with double.IsFinite this closes the
    // hole where a JSON literal overflowing double range (e.g. 1e400) deserializes to
    // double.PositiveInfinity, slips past the answer records' one-sided lower-bound guards, and then
    // crashes JsonSerializer.SerializeToDocument in the handler as an uncatchable 500 (the exact
    // failure mode FR-1.8 exists to prevent).
    private const double MaxDistanceKm = 100_000;

    // A finish/race time longer than this is garbage; the longest multi-day ultras are well under it.
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(60);

    /// <summary>
    /// Validates <paramref name="request"/> and, on success, builds the
    /// <see cref="SubmitStructuredAnswers"/> command carrying the canonical answer records.
    /// </summary>
    /// <param name="request">The client request payload.</param>
    /// <param name="userId">The authenticated runner's id.</param>
    /// <param name="command">The built command when validation succeeds; otherwise <see langword="null"/>.</param>
    /// <param name="error">A human-readable validation error when validation fails; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the request is valid and <paramref name="command"/> is populated.</returns>
    public static bool TryMap(
        SubmitStructuredAnswersRequestDto request,
        Guid userId,
        out SubmitStructuredAnswers? command,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(request);
        command = null;
        error = null;

        if (request.IdempotencyKey == Guid.Empty)
        {
            error = "IdempotencyKey must be a non-empty GUID.";
            return false;
        }

        var anyTopic = request.PrimaryGoal is not null
            || request.TargetEvent is not null
            || request.CurrentFitness is not null
            || request.WeeklySchedule is not null
            || request.InjuryHistory is not null
            || request.Preferences is not null;
        if (!anyTopic)
        {
            error = "At least one topic answer must be provided.";
            return false;
        }

        try
        {
            if (!TryMapPrimaryGoal(request.PrimaryGoal, out var primaryGoal, out error))
            {
                return false;
            }

            // A target event is only meaningful for a race-training primary goal submitted alongside
            // it. Checked AFTER the goal enum is validated so an undefined goal reports the accurate
            // "not a recognized goal" error rather than this cross-field message. Enforcing
            // co-submission keeps this a pure, stateless check and prevents stale race metadata
            // surviving on the projection (the single-page form always co-submits both).
            if (request.TargetEvent is not null
                && (primaryGoal is null || primaryGoal.Goal != PrimaryGoal.RaceTraining))
            {
                error = "TargetEvent is only valid when PrimaryGoal is RaceTraining in the same submission.";
                return false;
            }

            if (!TryMapTargetEvent(request.TargetEvent, out var targetEvent, out error)
                || !TryMapCurrentFitness(request.CurrentFitness, out var currentFitness, out error)
                || !TryMapPreferences(request.Preferences, out var preferences, out error))
            {
                return false;
            }

            var weeklySchedule = MapWeeklySchedule(request.WeeklySchedule);
            var injuryHistory = MapInjuryHistory(request.InjuryHistory);

            command = new SubmitStructuredAnswers(
                userId,
                request.IdempotencyKey,
                primaryGoal,
                targetEvent,
                currentFitness,
                weeklySchedule,
                injuryHistory,
                preferences);
            return true;
        }
        catch (ArgumentException ex)
        {
            // A canonical answer record's init accessor rejected an out-of-range numeric value
            // (e.g. DistanceKm <= 0, MaxRunDaysPerWeek outside 1-7). Report as a validation failure
            // rather than letting it propagate as a 500.
            command = null;
            error = ex.Message;
            return false;
        }
    }

    private static bool TryMapPrimaryGoal(PrimaryGoalInputDto? input, out PrimaryGoalAnswer? answer, out string? error)
    {
        answer = null;
        error = null;
        if (input is null)
        {
            return true;
        }

        if (!Enum.IsDefined(input.Goal))
        {
            error = $"PrimaryGoal.Goal value '{(int)input.Goal}' is not a recognized goal.";
            return false;
        }

        answer = new PrimaryGoalAnswer
        {
            Goal = input.Goal,
            Description = input.Description ?? string.Empty,
        };
        return true;
    }

    private static bool TryMapTargetEvent(TargetEventInputDto? input, out TargetEventAnswer? answer, out string? error)
    {
        answer = null;
        error = null;
        if (input is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(input.EventName))
        {
            error = "TargetEvent.EventName is required.";
            return false;
        }

        if (!IsSaneDistanceKm(input.DistanceKm))
        {
            error = "TargetEvent.DistanceKm must be a finite distance in kilometers within a sane range.";
            return false;
        }

        if (!DateOnly.TryParseExact(input.EventDateIso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            error = "TargetEvent.EventDateIso must be an ISO-8601 calendar date (yyyy-MM-dd).";
            return false;
        }

        if (!TryNormalizeOptionalDuration(input.TargetFinishTimeIso, out var targetFinish))
        {
            error = "TargetEvent.TargetFinishTimeIso must be an ISO-8601 duration (e.g. PT1H45M30S).";
            return false;
        }

        answer = new TargetEventAnswer
        {
            EventName = input.EventName,
            DistanceKm = input.DistanceKm,
            EventDateIso = input.EventDateIso,
            TargetFinishTimeIso = targetFinish,
        };
        return true;
    }

    private static bool TryMapCurrentFitness(CurrentFitnessInputDto? input, out CurrentFitnessAnswer? answer, out string? error)
    {
        answer = null;
        error = null;
        if (input is null)
        {
            return true;
        }

        if (!IsSaneDistanceKm(input.TypicalWeeklyKm)
            || !IsSaneDistanceKm(input.LongestRecentRunKm)
            || (input.RecentRaceDistanceKm is { } recentRaceDistance && !IsSaneDistanceKm(recentRaceDistance)))
        {
            error = "CurrentFitness distances must be finite values in kilometers within a sane range.";
            return false;
        }

        if (!TryNormalizeOptionalDuration(input.RecentRaceTimeIso, out var recentRaceTime))
        {
            error = "CurrentFitness.RecentRaceTimeIso must be an ISO-8601 duration (e.g. PT0H45M30S).";
            return false;
        }

        answer = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = input.TypicalWeeklyKm,
            LongestRecentRunKm = input.LongestRecentRunKm,
            RecentRaceDistanceKm = input.RecentRaceDistanceKm,
            RecentRaceTimeIso = recentRaceTime,
            Description = input.Description ?? string.Empty,
        };
        return true;
    }

    private static bool TryMapPreferences(PreferencesInputDto? input, out PreferencesAnswer? answer, out string? error)
    {
        answer = null;
        error = null;
        if (input is null)
        {
            return true;
        }

        if (!Enum.IsDefined(input.PreferredUnits))
        {
            error = $"Preferences.PreferredUnits value '{(int)input.PreferredUnits}' is not a recognized unit.";
            return false;
        }

        answer = new PreferencesAnswer
        {
            PreferredUnits = input.PreferredUnits,
            PreferTrail = input.PreferTrail,
            ComfortableWithIntensity = input.ComfortableWithIntensity,
            Description = input.Description ?? string.Empty,
        };
        return true;
    }

    private static WeeklyScheduleAnswer? MapWeeklySchedule(WeeklyScheduleInputDto? input)
    {
        // Numeric ranges (MaxRunDaysPerWeek 1-7, TypicalSessionMinutes > 0) are enforced by the
        // record's init accessors; a violation throws ArgumentException, caught by TryMap.
        if (input is null)
        {
            return null;
        }

        return new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = input.MaxRunDaysPerWeek,
            TypicalSessionMinutes = input.TypicalSessionMinutes,
            Monday = input.Monday,
            Tuesday = input.Tuesday,
            Wednesday = input.Wednesday,
            Thursday = input.Thursday,
            Friday = input.Friday,
            Saturday = input.Saturday,
            Sunday = input.Sunday,
            Description = input.Description ?? string.Empty,
        };
    }

    private static InjuryHistoryAnswer? MapInjuryHistory(InjuryHistoryInputDto? input)
    {
        if (input is null)
        {
            return null;
        }

        return new InjuryHistoryAnswer
        {
            HasActiveInjury = input.HasActiveInjury,
            ActiveInjuryDescription = input.ActiveInjuryDescription ?? string.Empty,
            PastInjurySummary = input.PastInjurySummary ?? string.Empty,
        };
    }

    private static bool TryNormalizeOptionalDuration(string? value, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = null;
            return true;
        }

        try
        {
            // XmlConvert.ToTimeSpan (xsd:duration) permissively accepts a leading '-' and arbitrarily
            // large components, so a negative or absurd duration parses without throwing. Reject those:
            // a race/finish time must be non-negative and within a sane bound.
            var duration = XmlConvert.ToTimeSpan(value);
            if (duration < TimeSpan.Zero || duration > MaxDuration)
            {
                normalized = null;
                return false;
            }

            normalized = value;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // OverflowException: XmlConvert.ToTimeSpan does checked arithmetic and throws for a
            // syntactically valid but numerically huge xsd:duration (e.g. "P1000000000D") — the
            // duration twin of the 1e400→Infinity distance hole. Reject cleanly instead of bubbling a 500.
            normalized = null;
            return false;
        }
    }

    private static bool IsSaneDistanceKm(double value) => double.IsFinite(value) && value <= MaxDistanceKm;
}
