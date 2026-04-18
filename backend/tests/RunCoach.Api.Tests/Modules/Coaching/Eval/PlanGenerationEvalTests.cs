using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.AI;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Plan generation eval tests using typed assertions on structured output records.
/// Each scenario calls the coaching LLM (Sonnet) via cached IChatClient with JSON
/// response format, then deserializes the typed plan records and runs deterministic
/// constraint checks via <see cref="PlanConstraintEvaluator"/>.
///
/// All 5 profiles must produce plans that pass their profile-specific constraints.
/// Both MesoWeek and MicroWorkout calls are cached via M.E.AI.Evaluation.
/// </summary>
[Trait("Category", "Eval")]
public sealed class PlanGenerationEvalTests : EvalTestBase
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Sarah_Beginner_GeneratesSafeLowVolumePlan()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange
        var profile = LoadProfile("sarah");
        var assembled = await AssembleContextAsync(profile, ct: TestContext.Current.CancellationToken);

        // Act -- generate MesoWeek and MicroWorkouts via cached structured output
        var mesoWeek = await GenerateStructuredAsync<MesoWeekOutput>(
            "plan.sarah.mesoweek",
            assembled,
            TestContext.Current.CancellationToken);
        var workoutList = await GenerateStructuredAsync<MicroWorkoutListOutput>(
            "plan.sarah.workouts",
            assembled,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            "plan-sarah",
            new
            {
                Profile = "Sarah (beginner)",
                MesoWeek = mesoWeek,
                Workouts = workoutList,
            },
            TestContext.Current.CancellationToken);

        // Assert -- typed constraint checks
        var context = new PlanConstraintContext
        {
            MesoWeek = mesoWeek,
            Workouts = workoutList.Workouts,
            CurrentWeeklyKm = (int)profile.UserProfile.CurrentWeeklyDistanceKm,
            IsBeginnerProfile = true,
            IsInjuredProfile = false,
            TrainingPaces = profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
        };

        var violations = PlanConstraintEvaluator.Evaluate(context);
        violations.Should().BeEmpty(
            "beginner plan should satisfy all constraints: " + string.Join("; ", violations));

        // Sarah-specific: no interval or tempo workouts
        foreach (var workout in workoutList.Workouts)
        {
            workout.WorkoutType.Should().NotBe(
                WorkoutType.Interval,
                "beginner should not have interval workout '{0}'",
                workout.Title);
            workout.WorkoutType.Should().NotBe(
                WorkoutType.Tempo,
                "beginner should not have tempo workout '{0}'",
                workout.Title);
        }

        // Sarah-specific: at least 2 rest days
        var restDayCount = mesoWeek.EnumerateDays().Count(d => d.Slot.SlotType == DaySlotType.Rest);
        restDayCount.Should().BeGreaterThanOrEqualTo(
            2,
            "beginner should have at least 2 rest days");

        // Volume ceiling: not more than 10% above current
        var maxKm = (int)(profile.UserProfile.CurrentWeeklyDistanceKm * 1.10m);
        mesoWeek.WeeklyTargetKm.Should().BeLessThanOrEqualTo(
            maxKm,
            "weekly km should not exceed 10% ceiling ({0}km)",
            maxKm);
    }

    [Fact]
    public async Task Lee_Intermediate_GeneratesPacesWithinDanielsGilbertZones()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange
        var profile = LoadProfile("lee");
        var assembled = await AssembleContextAsync(profile, ct: TestContext.Current.CancellationToken);
        var paces = profile.GoalState.CurrentFitnessEstimate.TrainingPaces;

        // Act
        var mesoWeek = await GenerateStructuredAsync<MesoWeekOutput>(
            "plan.lee.mesoweek",
            assembled,
            TestContext.Current.CancellationToken);
        var workoutList = await GenerateStructuredAsync<MicroWorkoutListOutput>(
            "plan.lee.workouts",
            assembled,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            "plan-lee",
            new
            {
                Profile = "Lee (intermediate)",
                MesoWeek = mesoWeek,
                Workouts = workoutList,
            },
            TestContext.Current.CancellationToken);

        // Assert -- typed constraint checks with pace verification
        var context = new PlanConstraintContext
        {
            MesoWeek = mesoWeek,
            Workouts = workoutList.Workouts,
            CurrentWeeklyKm = (int)profile.UserProfile.CurrentWeeklyDistanceKm,
            IsBeginnerProfile = false,
            IsInjuredProfile = false,
            TrainingPaces = paces,
        };

        var violations = PlanConstraintEvaluator.Evaluate(context);
        violations.Should().BeEmpty(
            "intermediate plan should satisfy all constraints: " + string.Join("; ", violations));

        // Lee-specific: easy pace within Daniels-Gilbert easy range (with 15% tolerance)
        var easyRange = paces.EasyPaceRange;
        var minEasySec = (int)(easyRange!.Fast.SecondsPerKm * 0.85);
        var maxEasySec = (int)(easyRange.Slow.SecondsPerKm * 1.15);

        foreach (var workout in workoutList.Workouts.Where(w => w.TargetPaceEasySecPerKm > 0))
        {
            workout.TargetPaceEasySecPerKm.Should().BeInRange(
                minEasySec,
                maxEasySec,
                "workout '{0}' easy pace should be within the Daniels-Gilbert easy range",
                workout.Title);
        }

        // Lee-specific: no pace faster than repetition zone maximum
        if (paces.RepetitionPace.HasValue)
        {
            var repFloor = (int)(paces.RepetitionPace.Value.SecondsPerKm * 0.90);
            foreach (var workout in workoutList.Workouts.Where(w => w.TargetPaceFastSecPerKm > 0))
            {
                workout.TargetPaceFastSecPerKm.Should().BeGreaterThanOrEqualTo(
                    repFloor,
                    "workout '{0}' fast pace should not exceed rep zone floor",
                    workout.Title);
            }
        }
    }

    [Fact]
    public async Task Maria_Goalless_MaintainsCurrentFitnessWithVariety()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange
        var profile = LoadProfile("maria");
        var assembled = await AssembleContextAsync(profile, ct: TestContext.Current.CancellationToken);

        // Act
        var mesoWeek = await GenerateStructuredAsync<MesoWeekOutput>(
            "plan.maria.mesoweek",
            assembled,
            TestContext.Current.CancellationToken);
        var workoutList = await GenerateStructuredAsync<MicroWorkoutListOutput>(
            "plan.maria.workouts",
            assembled,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            "plan-maria",
            new
            {
                Profile = "Maria (goalless / maintenance)",
                MesoWeek = mesoWeek,
                Workouts = workoutList,
            },
            TestContext.Current.CancellationToken);

        // Assert -- Maria-specific: weekly km within +-10% of current 55km
        var currentKm = (int)profile.UserProfile.CurrentWeeklyDistanceKm;
        var lowerBound = (int)(currentKm * 0.90);
        var upperBound = (int)(currentKm * 1.10);
        mesoWeek.WeeklyTargetKm.Should().BeInRange(
            lowerBound,
            upperBound,
            "maintenance plan should keep volume within 10% of current {0}km",
            currentKm);

        // Maria-specific: more than one distinct workout type (variety)
        var distinctTypes = workoutList.Workouts
            .Select(w => w.WorkoutType)
            .Distinct()
            .Count();
        distinctTypes.Should().BeGreaterThan(
            1,
            "maintenance plan should include workout variety");
    }

    [Fact]
    public async Task James_Injured_GeneratesConservativeRecoveryPlan()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange
        var profile = LoadProfile("james");
        var assembled = await AssembleContextAsync(profile, ct: TestContext.Current.CancellationToken);

        // Act -- MacroPlan, MesoWeek, MicroWorkouts, and coaching narrative (4 calls)
        var macroPlan = await GenerateStructuredAsync<MacroPlanOutput>(
            "plan.james.macroplan",
            assembled,
            TestContext.Current.CancellationToken);
        var mesoWeek = await GenerateStructuredAsync<MesoWeekOutput>(
            "plan.james.mesoweek",
            assembled,
            TestContext.Current.CancellationToken);
        var workoutList = await GenerateStructuredAsync<MicroWorkoutListOutput>(
            "plan.james.workouts",
            assembled,
            TestContext.Current.CancellationToken);

        // Separate unstructured call for coaching narrative (distinct cache key)
        var narrative = await GetCoachingNarrativeAsync(
            "plan.james.narrative",
            assembled,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            "plan-james",
            new
            {
                Profile = "James (injured / return from injury)",
                MacroPlan = macroPlan,
                MesoWeek = mesoWeek,
                Workouts = workoutList,
                CoachingNarrative = narrative,
            },
            TestContext.Current.CancellationToken);

        // Assert -- typed constraint checks
        var context = new PlanConstraintContext
        {
            MacroPlan = macroPlan,
            MesoWeek = mesoWeek,
            Workouts = workoutList.Workouts,
            CurrentWeeklyKm = (int)profile.UserProfile.CurrentWeeklyDistanceKm,
            IsBeginnerProfile = false,
            IsInjuredProfile = true,
            TrainingPaces = profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
        };

        var violations = PlanConstraintEvaluator.Evaluate(context);
        violations.Should().BeEmpty(
            "injured plan should satisfy all constraints: " + string.Join("; ", violations));

        // James-specific: MacroPlan.TotalWeeks >= 4
        macroPlan.TotalWeeks.Should().BeGreaterThanOrEqualTo(
            4,
            "return-from-injury plan needs at least 4 weeks");

        // James-specific: all workouts <= 20 min and Easy only
        workoutList.Workouts.Should().AllSatisfy(w =>
        {
            w.TargetDurationMinutes.Should().BeLessThanOrEqualTo(
                20,
                "injured workout '{0}' should be max 20 min",
                w.Title);
            w.WorkoutType.Should().BeOneOf(
                [WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Recovery],
                "injured workout '{0}' should be low-intensity only (Easy, LongRun, or Recovery)",
                w.Title);
        });

        // James-specific: coaching narrative acknowledges injury
        narrative.Should().ContainAny(
            "injury",
            "plantar fasciitis",
            "plantar",
            "recovery",
            "Injury",
            "Plantar",
            "Recovery");
    }

    [Fact]
    public async Task Priya_Constrained_RespectsExactly4RunDays()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange
        var profile = LoadProfile("priya");
        var assembled = await AssembleContextAsync(profile, ct: TestContext.Current.CancellationToken);

        // Act
        var mesoWeek = await GenerateStructuredAsync<MesoWeekOutput>(
            "plan.priya.mesoweek",
            assembled,
            TestContext.Current.CancellationToken);
        var workoutList = await GenerateStructuredAsync<MicroWorkoutListOutput>(
            "plan.priya.workouts",
            assembled,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            "plan-priya",
            new
            {
                Profile = "Priya (constrained / 4 days max)",
                MesoWeek = mesoWeek,
                Workouts = workoutList,
            },
            TestContext.Current.CancellationToken);

        // Assert -- Priya-specific: exactly 4 run days and 3 rest/cross-train days.
        // 7-slot count is structurally guaranteed by named day properties (Sunday..Saturday).
        var days = mesoWeek.EnumerateDays().ToList();
        var runDays = days.Count(d => d.Slot.SlotType == DaySlotType.Run);
        var nonRunDays = days.Count(
            d => d.Slot.SlotType == DaySlotType.Rest || d.Slot.SlotType == DaySlotType.CrossTrain);

        runDays.Should().Be(
            4,
            "constrained profile should have exactly 4 run days");
        nonRunDays.Should().Be(
            3,
            "constrained profile should have exactly 3 rest/cross-train days");
    }

    /// <summary>
    /// Sends the assembled prompt with JSON response format to get structured output
    /// via the cached IChatClient. The Anthropic IChatClient bridge maps
    /// ChatResponseFormatJson to native OutputConfig with JsonOutputFormat.
    /// </summary>
    private async Task<T> GenerateStructuredAsync<T>(
        string scenarioName,
        AssembledPrompt assembled,
        CancellationToken cancellationToken = default)
    {
        await using var sonnetRun = await CreateSonnetScenarioRunAsync(scenarioName);
        var client = sonnetRun.ChatConfiguration!.ChatClient;

        var userContent = BuildUserMessageFromSections(assembled);
        var schemaNode = JsonSchemaHelper.GenerateSchema<T>();
        var schemaElement = JsonSerializer.Deserialize<JsonElement>(
            schemaNode.ToJsonString());

        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, assembled.SystemPrompt),
            new ChatMessage(ChatRole.User, userContent),
        ];

        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schemaElement,
                typeof(T).Name),
        };

        var response = await client.GetResponseAsync(messages, options, cancellationToken);
        var rawText = response.Text ?? throw new InvalidOperationException(
            $"Structured output call for {typeof(T).Name} returned null.");

        // Constrained decoding guarantees bare JSON — no markdown fences to strip.
        return JsonSerializer.Deserialize<T>(rawText, DeserializeOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize structured output to {typeof(T).Name}.");
    }

    /// <summary>
    /// Sends the assembled prompt as an unstructured coaching narrative call.
    /// Used for the James injured scenario's dual-call requirement.
    /// </summary>
    private async Task<string> GetCoachingNarrativeAsync(
        string scenarioName,
        AssembledPrompt assembled,
        CancellationToken cancellationToken = default)
    {
        await using var sonnetRun = await CreateSonnetScenarioRunAsync(scenarioName);
        var client = sonnetRun.ChatConfiguration!.ChatClient;

        var userContent = BuildUserMessageFromSections(assembled);
        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, assembled.SystemPrompt),
            new ChatMessage(ChatRole.User, userContent),
        ];

        var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken);

        return response.Text ?? string.Empty;
    }
}
