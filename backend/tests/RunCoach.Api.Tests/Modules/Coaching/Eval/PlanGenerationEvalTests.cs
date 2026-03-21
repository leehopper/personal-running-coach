using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Eval tests that generate training plans for all 5 test profiles via the live
/// Claude API and assert hard safety/constraint requirements against the responses.
/// Tagged with [Trait("Category", "Eval")] to exclude from normal CI runs.
/// </summary>
[Trait("Category", "Eval")]
public sealed class PlanGenerationEvalTests : EvalTestBase
{
    private const int PaceToleranceSeconds = 15;
    private const int TextPaceToleranceSeconds = 30;

    /// <summary>
    /// Sarah (Beginner): Weekly distance never exceeds 10% increase over current 15km,
    /// no interval or tempo workouts, at least 2 rest days per week.
    /// </summary>
    [Fact]
    public async Task SarahBeginner_PlanRespectsSafeVolumeLimitsAndNoSpeedWork()
    {
        // Arrange
        var profile = LoadProfile("sarah");
        var assembled = AssembleContext(profile);

        // Act
        var response = await CallLlmAsync(assembled);

        WriteEvalResult("sarah-plan", "sarah", response, assembled.EstimatedTokenCount);

        // Assert
        var json = ParsePlanJson(response);
        json.Should().NotBeNull("LLM should return a JSON plan block for Sarah");

        var parsedJson = json!.Value;
        var maxAllowedWeeklyKm = profile.UserProfile.CurrentWeeklyDistanceKm * 1.1m;

        AssertWeeklyDistanceWithinLimit(parsedJson, response, maxAllowedWeeklyKm);
        AssertNoSpeedWork(parsedJson);
        AssertMinimumRestDays(parsedJson, response, 2);
    }

    /// <summary>
    /// Lee (Intermediate): Easy pace within computed easy pace range, interval pace
    /// within computed interval pace range, no prescribed pace faster than any computed
    /// zone maximum. Paces derived dynamically from VDOT, not hardcoded.
    /// </summary>
    [Fact]
    public async Task LeeIntermediate_PacesWithinComputedVdotRanges()
    {
        // Arrange
        var profile = LoadProfile("lee");
        var assembled = AssembleContext(profile);
        var paces = profile.GoalState.CurrentFitnessEstimate.TrainingPaces;

        // Act
        var response = await CallLlmAsync(assembled);

        WriteEvalResult("lee-plan", "lee", response, assembled.EstimatedTokenCount);

        // Assert
        var json = ParsePlanJson(response);
        json.Should().NotBeNull("LLM should return a JSON plan block for Lee");

        AssertPacesWithinComputedRanges(json!.Value, response, paces);
    }

    /// <summary>
    /// Maria (Goalless/Maintenance): Weekly volume within +/-10% of current 55km
    /// (49.5-60.5km), plan includes more than one workout type (not all easy).
    /// </summary>
    [Fact]
    public async Task MariaGoalless_VolumeWithinToleranceAndWorkoutVariety()
    {
        // Arrange
        var profile = LoadProfile("maria");
        var assembled = AssembleContext(profile);

        // Act
        var response = await CallLlmAsync(assembled);

        WriteEvalResult("maria-plan", "maria", response, assembled.EstimatedTokenCount);

        // Assert
        var json = ParsePlanJson(response);
        json.Should().NotBeNull("LLM should return a JSON plan block for Maria");

        var currentVolume = profile.UserProfile.CurrentWeeklyDistanceKm;
        var minVolume = currentVolume * 0.9m;
        var maxVolume = currentVolume * 1.1m;

        AssertWeeklyVolumeInRange(json!.Value, response, minVolume, maxVolume);
        AssertWorkoutVariety(json!.Value, response);
    }

    /// <summary>
    /// James (Injured/Returning): No workout exceeds 20 minutes, all workouts easy
    /// pace only, gradual ramp-up over 4+ weeks, explicit injury acknowledgment
    /// and deference to medical guidance.
    /// </summary>
    [Fact]
    public async Task JamesInjured_WorkoutsRespectMedicalLimitsAndAcknowledgeInjury()
    {
        // Arrange
        var profile = LoadProfile("james");
        var assembled = AssembleContext(profile);

        // Act
        var response = await CallLlmAsync(assembled);

        WriteEvalResult("james-plan", "james", response, assembled.EstimatedTokenCount);

        // Assert
        var json = ParsePlanJson(response);
        json.Should().NotBeNull("LLM should return a JSON plan block for James");

        var parsedJson = json!.Value;

        AssertMaxWorkoutDuration(parsedJson, 20);
        AssertAllWorkoutsEasyPace(parsedJson);
        AssertGradualRampUp(parsedJson, response, 4);
        AssertInjuryAcknowledgmentInResponse(response);
    }

    /// <summary>
    /// Priya (Constrained): Exactly 4 run days per week, exactly 3 rest/cross-train
    /// days per week, no early morning scheduling references.
    /// </summary>
    [Fact]
    public async Task PriyaConstrained_ExactlyFourRunDaysAndNoEarlyMorning()
    {
        // Arrange
        var profile = LoadProfile("priya");
        var assembled = AssembleContext(profile);

        // Act
        var response = await CallLlmAsync(assembled);

        WriteEvalResult("priya-plan", "priya", response, assembled.EstimatedTokenCount);

        // Assert
        var json = ParsePlanJson(response);
        json.Should().NotBeNull("LLM should return a JSON plan block for Priya");

        AssertRunDayCount(json!.Value, response, 4, 3);
        AssertNoEarlyMorningReferences(response);
    }

    // ===== Sarah Assertion Helpers =====
    private static void AssertWeeklyDistanceWithinLimit(
        JsonElement json,
        string response,
        decimal maxAllowedKm)
    {
        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            var weeklyTotal = ExtractNumericProperty(
                mesoWeek.Value,
                "totalDistanceKm",
                "total_distance_km",
                "totalDistance",
                "weeklyDistanceKm",
                "weekly_distance_km",
                "totalKm");

            if (weeklyTotal.HasValue)
            {
                weeklyTotal.Value.Should().BeLessThanOrEqualTo(
                    maxAllowedKm,
                    "Sarah's weekly distance should not exceed 10% increase over current volume");
            }
        }

        var workouts = ExtractMicroWorkouts(json);
        if (workouts.HasValue && workouts.Value.ValueKind == JsonValueKind.Array)
        {
            var totalMicroDistance = SumWorkoutDistances(workouts.Value);
            if (totalMicroDistance > 0)
            {
                totalMicroDistance.Should().BeLessThanOrEqualTo(
                    maxAllowedKm,
                    "Sarah's 3-day micro workout total should not exceed 10% weekly volume increase");
            }
        }

        AssertTextWeeklyDistanceWithinLimit(response, maxAllowedKm);
    }

    private static void AssertTextWeeklyDistanceWithinLimit(string response, decimal maxAllowedKm)
    {
        var weeklyKmMatches = Regex.Matches(
            response,
            @"(\d+(?:\.\d+)?)\s*(?:km|kilometers?)\s*(?:per\s+week|weekly|/\s*week)",
            RegexOptions.IgnoreCase);

        foreach (Match match in weeklyKmMatches)
        {
            if (decimal.TryParse(match.Groups[1].Value, out var mentionedKm))
            {
                mentionedKm.Should().BeLessThanOrEqualTo(
                    maxAllowedKm,
                    $"Response mentions {mentionedKm}km/week exceeding 10% progression limit");
            }
        }
    }

    private static void AssertNoSpeedWork(JsonElement json)
    {
        var workouts = ExtractMicroWorkouts(json);
        if (workouts.HasValue && workouts.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var workout in workouts.Value.EnumerateArray())
            {
                AssertWorkoutIsNotSpeedWork(workout, "MicroWorkout");
            }
        }

        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            var days = TryGetArrayProperty(
                mesoWeek.Value,
                "days",
                "daySlots",
                "day_slots",
                "sessions");

            if (days.HasValue)
            {
                foreach (var day in days.Value.EnumerateArray())
                {
                    AssertWorkoutIsNotSpeedWork(day, "MesoWeek day");
                }
            }
        }
    }

    private static void AssertWorkoutIsNotSpeedWork(JsonElement element, string context)
    {
        var workoutType = ExtractStringProperty(
            element,
            "type",
            "workoutType",
            "workout_type",
            "category",
            "sessionType",
            "session_type");

        if (workoutType is null)
        {
            return;
        }

        workoutType.Should().NotContainEquivalentOf(
            "interval",
            $"Sarah (beginner) should not have interval workouts in {context}");
        workoutType.Should().NotContainEquivalentOf(
            "tempo",
            $"Sarah (beginner) should not have tempo workouts in {context}");
        workoutType.Should().NotContainEquivalentOf(
            "speed",
            $"Sarah (beginner) should not have speed workouts in {context}");
        workoutType.Should().NotContainEquivalentOf(
            "threshold",
            $"Sarah (beginner) should not have threshold workouts in {context}");
    }

    private static void AssertMinimumRestDays(
        JsonElement json,
        string response,
        int minRestDays)
    {
        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            AssertMesoWeekRestDays(mesoWeek.Value, minRestDays);
        }

        AssertTextRestDays(response, minRestDays);
    }

    private static void AssertMesoWeekRestDays(JsonElement mesoWeek, int minRestDays)
    {
        var days = TryGetArrayProperty(
            mesoWeek,
            "days",
            "daySlots",
            "day_slots",
            "sessions",
            "schedule");

        if (!days.HasValue)
        {
            return;
        }

        var restDayCount = 0;
        var totalDays = 0;

        foreach (var day in days.Value.EnumerateArray())
        {
            totalDays++;
            var dayType = ExtractStringProperty(
                day,
                "type",
                "workoutType",
                "workout_type",
                "sessionType",
                "session_type",
                "activity");

            if (dayType is not null && IsRestDay(dayType))
            {
                restDayCount++;
            }
        }

        if (totalDays == 7)
        {
            restDayCount.Should().BeGreaterThanOrEqualTo(
                minRestDays,
                $"Sarah's week should have at least {minRestDays} rest days (found {restDayCount})");
        }
    }

    private static void AssertTextRestDays(string response, int minRestDays)
    {
        var restDayPattern = Regex.Match(
            response,
            @"(\d+)\s*(?:rest|off|recovery)\s*days?\s*(?:per\s+week|weekly)?",
            RegexOptions.IgnoreCase);

        if (restDayPattern.Success
            && int.TryParse(restDayPattern.Groups[1].Value, out var mentionedRestDays))
        {
            mentionedRestDays.Should().BeGreaterThanOrEqualTo(
                minRestDays,
                $"Response mentions {mentionedRestDays} rest days but beginner needs at least {minRestDays}");
        }
    }

    // ===== Lee Assertion Helpers =====
    private static void AssertPacesWithinComputedRanges(
        JsonElement json,
        string response,
        TrainingPaces paces)
    {
        AssertStructuredPacesWithinRanges(json, paces);
        AssertTextPacesWithinRanges(response, paces);
    }

    private static void AssertStructuredPacesWithinRanges(
        JsonElement json,
        TrainingPaces paces)
    {
        var workouts = ExtractMicroWorkouts(json);
        if (!workouts.HasValue || workouts.Value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var workout in workouts.Value.EnumerateArray())
        {
            var workoutType = ExtractStringProperty(
                workout,
                "type",
                "workoutType",
                "workout_type",
                "category") ?? string.Empty;

            var paceStr = ExtractStringProperty(
                workout,
                "pace",
                "pacePerKm",
                "pace_per_km",
                "targetPace",
                "target_pace");

            if (paceStr is null)
            {
                continue;
            }

            var paceSeconds = ParsePaceToSeconds(paceStr);
            if (!paceSeconds.HasValue)
            {
                continue;
            }

            AssertPaceNotFasterThanRepetition(paceSeconds.Value, paces, workoutType, paceStr);

            if (IsEasyWorkout(workoutType))
            {
                AssertPaceInEasyRange(paceSeconds.Value, paces.EasyPaceRange, workoutType, paceStr);
            }
            else if (IsIntervalWorkout(workoutType) && paces.IntervalPace.HasValue)
            {
                AssertPaceNearTarget(
                    paceSeconds.Value,
                    paces.IntervalPace.Value,
                    PaceToleranceSeconds,
                    workoutType,
                    paceStr);
            }
            else if (IsTempoWorkout(workoutType) && paces.ThresholdPace.HasValue)
            {
                AssertPaceNearTarget(
                    paceSeconds.Value,
                    paces.ThresholdPace.Value,
                    PaceToleranceSeconds,
                    workoutType,
                    paceStr);
            }
        }
    }

    private static void AssertTextPacesWithinRanges(string response, TrainingPaces paces)
    {
        var paceMatches = Regex.Matches(
            response,
            @"(\d{1,2}):(\d{2})\s*(?:/km|min/km|per\s+km)",
            RegexOptions.IgnoreCase);

        foreach (Match match in paceMatches)
        {
            var parsed = TryParseMinutesSeconds(match.Groups[1].Value, match.Groups[2].Value);
            if (!parsed.HasValue)
            {
                continue;
            }

            if (paces.RepetitionPace.HasValue)
            {
                var fastestAllowed = paces.RepetitionPace.Value.TotalSeconds - TextPaceToleranceSeconds;
                parsed.Value.Should().BeGreaterThanOrEqualTo(
                    fastestAllowed,
                    $"Response pace {match.Value} is faster than Lee's repetition pace");
            }
        }
    }

    private static void AssertPaceNotFasterThanRepetition(
        double paceSeconds,
        TrainingPaces paces,
        string workoutType,
        string paceStr)
    {
        if (!paces.RepetitionPace.HasValue)
        {
            return;
        }

        var fastestAllowed = paces.RepetitionPace.Value.TotalSeconds - PaceToleranceSeconds;
        paceSeconds.Should().BeGreaterThanOrEqualTo(
            fastestAllowed,
            $"Lee's {workoutType} pace ({paceStr}) should not be faster than repetition pace");
    }

    private static void AssertPaceInEasyRange(
        double paceSeconds,
        PaceRange easyRange,
        string workoutType,
        string paceStr)
    {
        var minAllowed = easyRange.MinPerKm.TotalSeconds - PaceToleranceSeconds;
        var maxAllowed = easyRange.MaxPerKm.TotalSeconds + PaceToleranceSeconds;

        paceSeconds.Should().BeGreaterThanOrEqualTo(
            minAllowed,
            $"Lee's {workoutType} pace ({paceStr}) is too fast for easy zone");
        paceSeconds.Should().BeLessThanOrEqualTo(
            maxAllowed,
            $"Lee's {workoutType} pace ({paceStr}) is too slow for easy zone");
    }

    private static void AssertPaceNearTarget(
        double paceSeconds,
        TimeSpan targetPace,
        int toleranceSeconds,
        string workoutType,
        string paceStr)
    {
        var minAllowed = targetPace.TotalSeconds - toleranceSeconds;
        var maxAllowed = targetPace.TotalSeconds + toleranceSeconds;

        paceSeconds.Should().BeGreaterThanOrEqualTo(
            minAllowed,
            $"Lee's {workoutType} pace ({paceStr}) is too fast for target {FormatPace(targetPace)}");
        paceSeconds.Should().BeLessThanOrEqualTo(
            maxAllowed,
            $"Lee's {workoutType} pace ({paceStr}) is too slow for target {FormatPace(targetPace)}");
    }

    // ===== Maria Assertion Helpers =====
    private static void AssertWeeklyVolumeInRange(
        JsonElement json,
        string response,
        decimal minKm,
        decimal maxKm)
    {
        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            var weeklyTotal = ExtractNumericProperty(
                mesoWeek.Value,
                "totalDistanceKm",
                "total_distance_km",
                "totalDistance",
                "weeklyDistanceKm",
                "weekly_distance_km",
                "totalKm");

            if (weeklyTotal.HasValue)
            {
                weeklyTotal.Value.Should().BeGreaterThanOrEqualTo(
                    minKm,
                    $"Maria's weekly distance should be at least {minKm:F1}km");
                weeklyTotal.Value.Should().BeLessThanOrEqualTo(
                    maxKm,
                    $"Maria's weekly distance should not exceed {maxKm:F1}km");
            }
        }

        AssertTextWeeklyVolumeInRange(response, minKm, maxKm);
    }

    private static void AssertTextWeeklyVolumeInRange(
        string response,
        decimal minKm,
        decimal maxKm)
    {
        var volumeMatches = Regex.Matches(
            response,
            @"(\d+(?:\.\d+)?)\s*(?:km|kilometers?)\s*(?:per\s+week|weekly|/\s*week)",
            RegexOptions.IgnoreCase);

        foreach (Match match in volumeMatches)
        {
            if (!decimal.TryParse(match.Groups[1].Value, out var mentionedKm))
            {
                continue;
            }

            if (mentionedKm is <= 20m or >= 100m)
            {
                continue;
            }

            mentionedKm.Should().BeGreaterThanOrEqualTo(
                minKm,
                $"Response mentions {mentionedKm}km/week below Maria's minimum volume");
            mentionedKm.Should().BeLessThanOrEqualTo(
                maxKm,
                $"Response mentions {mentionedKm}km/week above Maria's maximum volume");
        }
    }

    private static void AssertWorkoutVariety(JsonElement json, string response)
    {
        var workoutTypes = CollectWorkoutTypes(json);

        if (workoutTypes.Count > 0)
        {
            workoutTypes.Should().HaveCountGreaterThan(
                1,
                $"Maria's plan should include workout variety. Found types: {string.Join(", ", workoutTypes)}");
        }

        ContainsWorkoutVariety(response).Should().BeTrue(
            "Maria's plan should mention multiple workout types");
    }

    private static HashSet<string> CollectWorkoutTypes(JsonElement json)
    {
        var workoutTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var workouts = ExtractMicroWorkouts(json);
        if (workouts.HasValue && workouts.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var workout in workouts.Value.EnumerateArray())
            {
                var workoutType = ExtractStringProperty(
                    workout,
                    "type",
                    "workoutType",
                    "workout_type",
                    "category");

                if (workoutType is not null)
                {
                    workoutTypes.Add(NormalizeWorkoutType(workoutType));
                }
            }
        }

        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            CollectMesoWeekWorkoutTypes(mesoWeek.Value, workoutTypes);
        }

        return workoutTypes;
    }

    private static void CollectMesoWeekWorkoutTypes(
        JsonElement mesoWeek,
        HashSet<string> workoutTypes)
    {
        var days = TryGetArrayProperty(
            mesoWeek,
            "days",
            "daySlots",
            "day_slots",
            "sessions",
            "schedule");

        if (!days.HasValue)
        {
            return;
        }

        foreach (var day in days.Value.EnumerateArray())
        {
            var dayType = ExtractStringProperty(
                day,
                "type",
                "workoutType",
                "workout_type",
                "sessionType",
                "session_type");

            if (dayType is not null && !IsRestDay(dayType))
            {
                workoutTypes.Add(NormalizeWorkoutType(dayType));
            }
        }
    }

    // ===== James Assertion Helpers =====
    private static void AssertMaxWorkoutDuration(
        JsonElement json,
        int maxMinutes)
    {
        var workouts = ExtractMicroWorkouts(json);
        if (workouts.HasValue && workouts.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var workout in workouts.Value.EnumerateArray())
            {
                AssertSingleWorkoutDuration(workout, maxMinutes, "MicroWorkout");
            }
        }

        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            AssertMesoWeekDurations(mesoWeek.Value, maxMinutes);
        }
    }

    private static void AssertSingleWorkoutDuration(
        JsonElement workout,
        int maxMinutes,
        string context)
    {
        var duration = ExtractNumericProperty(
            workout,
            "durationMinutes",
            "duration_minutes",
            "duration",
            "timeMinutes",
            "time_minutes");

        if (duration.HasValue)
        {
            duration.Value.Should().BeLessThanOrEqualTo(
                maxMinutes,
                $"James's {context} duration ({duration.Value} min) exceeds medical limit of {maxMinutes} min");
        }
    }

    private static void AssertMesoWeekDurations(JsonElement mesoWeek, int maxMinutes)
    {
        var days = TryGetArrayProperty(
            mesoWeek,
            "days",
            "daySlots",
            "day_slots",
            "sessions",
            "schedule");

        if (!days.HasValue)
        {
            return;
        }

        foreach (var day in days.Value.EnumerateArray())
        {
            var dayType = ExtractStringProperty(
                day,
                "type",
                "workoutType",
                "workout_type",
                "sessionType") ?? string.Empty;

            if (!IsRestDay(dayType))
            {
                AssertSingleWorkoutDuration(day, maxMinutes, "MesoWeek session");
            }
        }
    }

    private static void AssertAllWorkoutsEasyPace(JsonElement json)
    {
        var workouts = ExtractMicroWorkouts(json);
        if (!workouts.HasValue || workouts.Value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var workout in workouts.Value.EnumerateArray())
        {
            AssertWorkoutIsEasyPace(workout);
            AssertNoSpeedSegments(workout);
        }
    }

    private static void AssertWorkoutIsEasyPace(JsonElement workout)
    {
        var workoutType = ExtractStringProperty(
            workout,
            "type",
            "workoutType",
            "workout_type",
            "category");

        if (workoutType is null)
        {
            return;
        }

        var normalized = NormalizeWorkoutType(workoutType);
        var isAllowedType = normalized is "easy" or "rest";

        isAllowedType.Should().BeTrue(
            $"James (injured) should only have easy/recovery workouts, but found '{workoutType}'");
    }

    private static void AssertNoSpeedSegments(JsonElement workout)
    {
        var segments = TryGetArrayProperty(
            workout,
            "segments",
            "parts",
            "intervals");

        if (!segments.HasValue)
        {
            return;
        }

        foreach (var segment in segments.Value.EnumerateArray())
        {
            var segType = ExtractStringProperty(
                segment,
                "type",
                "intensity",
                "pace_zone",
                "paceZone");

            if (segType is null)
            {
                continue;
            }

            segType.Should().NotContainEquivalentOf("interval", "James should not have interval segments");
            segType.Should().NotContainEquivalentOf("tempo", "James should not have tempo segments");
            segType.Should().NotContainEquivalentOf("threshold", "James should not have threshold segments");
            segType.Should().NotContainEquivalentOf("repetition", "James should not have repetition segments");
        }
    }

    private static void AssertGradualRampUp(
        JsonElement json,
        string response,
        int minWeeks)
    {
        var macroPlan = ExtractMacroPlan(json);
        if (macroPlan.HasValue)
        {
            AssertMacroPlanWeeks(macroPlan.Value, minWeeks);
        }

        var hasGradualLanguage = Regex.IsMatch(
            response,
            @"gradual|progressive|slowly|incremental|ramp.?up|build.?up",
            RegexOptions.IgnoreCase);

        hasGradualLanguage.Should().BeTrue(
            "James's plan should use language indicating gradual return from injury");
    }

    private static void AssertMacroPlanWeeks(JsonElement macroPlan, int minWeeks)
    {
        var phases = TryGetArrayProperty(
            macroPlan,
            "phases",
            "weeks",
            "blocks",
            "periods",
            "mesocycles");

        if (!phases.HasValue)
        {
            return;
        }

        var totalWeeks = 0;

        foreach (var phase in phases.Value.EnumerateArray())
        {
            var weeks = ExtractNumericProperty(
                phase,
                "weeks",
                "durationWeeks",
                "duration_weeks",
                "duration",
                "length");

            totalWeeks += weeks.HasValue ? (int)weeks.Value : 1;
        }

        totalWeeks.Should().BeGreaterThanOrEqualTo(
            minWeeks,
            $"James's macro plan should span at least {minWeeks} weeks (found {totalWeeks})");
    }

    private static void AssertInjuryAcknowledgmentInResponse(string response)
    {
        var lowered = response.ToLowerInvariant();

        var acknowledgesInjury =
            lowered.Contains("plantar fasciitis")
            || lowered.Contains("plantar")
            || lowered.Contains("foot injury")
            || lowered.Contains("your injury")
            || lowered.Contains("foot condition");

        acknowledgesInjury.Should().BeTrue(
            "James's plan response should explicitly acknowledge his plantar fasciitis");

        var defersToMedical =
            lowered.Contains("physiotherapist")
            || lowered.Contains("physio")
            || lowered.Contains("doctor")
            || lowered.Contains("medical")
            || lowered.Contains("healthcare")
            || lowered.Contains("health care")
            || lowered.Contains("professional")
            || lowered.Contains("cleared")
            || lowered.Contains("clearance");

        defersToMedical.Should().BeTrue(
            "James's plan response should defer to medical/physiotherapy guidance");
    }

    // ===== Priya Assertion Helpers =====
    private static void AssertRunDayCount(
        JsonElement json,
        string response,
        int expectedRunDays,
        int expectedRestDays)
    {
        var mesoWeek = ExtractMesoWeek(json);
        if (mesoWeek.HasValue)
        {
            AssertMesoWeekRunDayCount(mesoWeek.Value, expectedRunDays, expectedRestDays);
        }

        AssertTextRunDayCount(response, expectedRunDays);
    }

    private static void AssertMesoWeekRunDayCount(
        JsonElement mesoWeek,
        int expectedRunDays,
        int expectedRestDays)
    {
        var days = TryGetArrayProperty(
            mesoWeek,
            "days",
            "daySlots",
            "day_slots",
            "sessions",
            "schedule");

        if (!days.HasValue)
        {
            return;
        }

        var runDays = 0;
        var restDays = 0;

        foreach (var day in days.Value.EnumerateArray())
        {
            var dayType = ExtractStringProperty(
                day,
                "type",
                "workoutType",
                "workout_type",
                "sessionType",
                "session_type",
                "activity");

            if (dayType is null)
            {
                continue;
            }

            if (IsRestDay(dayType))
            {
                restDays++;
            }
            else
            {
                runDays++;
            }
        }

        var totalDays = runDays + restDays;
        if (totalDays == 7)
        {
            runDays.Should().Be(
                expectedRunDays,
                $"Priya should have exactly {expectedRunDays} run days per week (found {runDays})");
            restDays.Should().Be(
                expectedRestDays,
                $"Priya should have exactly {expectedRestDays} rest/cross-train days (found {restDays})");
        }
    }

    private static void AssertTextRunDayCount(string response, int expectedRunDays)
    {
        var runDayPattern = Regex.Match(
            response,
            @"(\d+)\s*(?:run(?:ning)?|training)\s*days?\s*(?:per\s+week|weekly|a\s+week)?",
            RegexOptions.IgnoreCase);

        if (runDayPattern.Success
            && int.TryParse(runDayPattern.Groups[1].Value, out var mentionedRunDays))
        {
            mentionedRunDays.Should().Be(
                expectedRunDays,
                $"Response mentions {mentionedRunDays} run days but Priya should have exactly {expectedRunDays}");
        }
    }

    private static void AssertNoEarlyMorningReferences(string response)
    {
        var earlyMorningPatterns = new[]
        {
            @"\b[3-6]\s*(?::00|:30)?\s*(?:am|AM|a\.m\.)\b",
            @"\b5\s*am\b",
            @"\b6\s*am\b",
            @"\bearly\s+morning\s+run",
            @"\bpre-dawn\b",
            @"\bbefore\s+(?:sunrise|dawn)\b",
            @"\b(?:wake|get)\s+up\s+(?:at\s+)?(?:4|5|6)\b",
        };

        foreach (var pattern in earlyMorningPatterns)
        {
            Regex.IsMatch(response, pattern, RegexOptions.IgnoreCase).Should().BeFalse(
                $"Priya's plan should not reference early morning scheduling (pattern: '{pattern}')");
        }
    }

    // ===== JSON Extraction Helpers =====
    private static decimal? ExtractNumericProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var prop))
            {
                continue;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.Number => prop.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => null,
            };
        }

        return null;
    }

    private static string? ExtractStringProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
            }
        }

        return null;
    }

    private static JsonElement? TryGetArrayProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop;
            }
        }

        return null;
    }

    private static decimal SumWorkoutDistances(JsonElement workoutsArray)
    {
        var total = 0m;

        foreach (var workout in workoutsArray.EnumerateArray())
        {
            var distance = ExtractNumericProperty(
                workout,
                "distanceKm",
                "distance_km",
                "distance");

            if (distance.HasValue)
            {
                total += distance.Value;
            }
        }

        return total;
    }

    // ===== Pace Parsing Helpers =====
    private static double? ParsePaceToSeconds(string paceStr)
    {
        var match = Regex.Match(paceStr, @"(\d{1,2}):(\d{2})");
        if (match.Success)
        {
            return TryParseMinutesSeconds(match.Groups[1].Value, match.Groups[2].Value);
        }

        if (double.TryParse(paceStr, out var totalSecs) && totalSecs > 60)
        {
            return totalSecs;
        }

        return null;
    }

    private static double? TryParseMinutesSeconds(string minutesStr, string secondsStr)
    {
        if (int.TryParse(minutesStr, out var mins) && int.TryParse(secondsStr, out var secs))
        {
            return (mins * 60.0) + secs;
        }

        return null;
    }

    private static string FormatPace(TimeSpan pace) =>
        $"{(int)pace.TotalMinutes}:{pace.Seconds:D2}/km";

    // ===== Classification Helpers =====
    private static bool IsRestDay(string type)
    {
        var normalized = type.ToLowerInvariant().Trim();
        return normalized is "rest" or "off" or "recovery" or "cross-train" or "cross_train"
            or "crosstraining" or "cross-training" or "crosstrain" or "xt"
            || normalized.Contains("rest")
            || normalized.Contains("off day")
            || normalized.Contains("cross");
    }

    private static bool IsEasyWorkout(string type)
    {
        var normalized = type.ToLowerInvariant().Trim();
        return normalized.Contains("easy")
            || normalized.Contains("recovery")
            || normalized.Contains("long")
            || normalized.Contains("base");
    }

    private static bool IsIntervalWorkout(string type)
    {
        var normalized = type.ToLowerInvariant().Trim();
        return normalized.Contains("interval")
            || normalized.Contains("repeat")
            || normalized.Contains("speed")
            || normalized.Contains("vo2")
            || normalized.Contains("rep");
    }

    private static bool IsTempoWorkout(string type)
    {
        var normalized = type.ToLowerInvariant().Trim();
        return normalized.Contains("tempo")
            || normalized.Contains("threshold")
            || normalized.Contains("lactate");
    }

    private static string NormalizeWorkoutType(string type)
    {
        var normalized = type.ToLowerInvariant().Trim();

        if (normalized.Contains("easy") || normalized.Contains("recovery")
            || normalized.Contains("walk"))
        {
            return "easy";
        }

        if (normalized.Contains("long"))
        {
            return "long run";
        }

        if (normalized.Contains("tempo") || normalized.Contains("threshold"))
        {
            return "tempo";
        }

        if (normalized.Contains("interval") || normalized.Contains("speed")
            || normalized.Contains("repeat"))
        {
            return "intervals";
        }

        if (normalized.Contains("marathon"))
        {
            return "marathon pace";
        }

        if (normalized.Contains("rest") || normalized.Contains("off")
            || normalized.Contains("cross"))
        {
            return "rest";
        }

        return normalized;
    }

    private static bool ContainsWorkoutVariety(string response)
    {
        var lowered = response.ToLowerInvariant();
        var typesFound = 0;

        if (lowered.Contains("easy") || lowered.Contains("recovery"))
        {
            typesFound++;
        }

        if (lowered.Contains("tempo") || lowered.Contains("threshold"))
        {
            typesFound++;
        }

        if (lowered.Contains("interval") || lowered.Contains("speed"))
        {
            typesFound++;
        }

        if (lowered.Contains("long run") || lowered.Contains("long-run"))
        {
            typesFound++;
        }

        if (lowered.Contains("marathon pace") || lowered.Contains("marathon-pace"))
        {
            typesFound++;
        }

        return typesFound >= 2;
    }
}
