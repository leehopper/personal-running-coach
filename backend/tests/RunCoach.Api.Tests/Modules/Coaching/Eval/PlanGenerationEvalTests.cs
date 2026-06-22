using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.AI;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Training.Plan;

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
[Collection("Eval")]
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

        // Trademark guard — every persisted prose field of the cached outputs (Slice 3B F2).
        TrademarkProseGuard.AssertClean("plan-sarah", new { mesoWeek, workoutList });

        // Advisory restraint judge (Slice 4A) — recorded for the tuning rounds, never gated.
        var restraintVerdict = await JudgeRestraintAsync(
            "plan.sarah.restraint.judge",
            "Sarah (beginner)",
            ComposePlanNarrative(mesoWeek, workoutList),
            TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            "plan-sarah-restraint",
            new { Profile = "Sarah (beginner)", Verdict = restraintVerdict },
            TestContext.Current.CancellationToken);

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean("plan-sarah", new { mesoWeek, workoutList });

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

        // Trademark guard — every persisted prose field of the cached outputs (Slice 3B F2).
        TrademarkProseGuard.AssertClean("plan-lee", new { mesoWeek, workoutList });

        // Advisory restraint judge (Slice 4A) — recorded for the tuning rounds, never gated.
        var restraintVerdict = await JudgeRestraintAsync(
            "plan.lee.restraint.judge",
            "Lee (intermediate)",
            ComposePlanNarrative(mesoWeek, workoutList),
            TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            "plan-lee-restraint",
            new { Profile = "Lee (intermediate)", Verdict = restraintVerdict },
            TestContext.Current.CancellationToken);

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean("plan-lee", new { mesoWeek, workoutList });

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

        // Trademark guard — every persisted prose field of the cached outputs (Slice 3B F2).
        TrademarkProseGuard.AssertClean("plan-maria", new { mesoWeek, workoutList });

        // Advisory restraint judge (Slice 4A) — recorded for the tuning rounds, never gated.
        var restraintVerdict = await JudgeRestraintAsync(
            "plan.maria.restraint.judge",
            "Maria (goalless / maintenance)",
            ComposePlanNarrative(mesoWeek, workoutList),
            TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            "plan-maria-restraint",
            new { Profile = "Maria (goalless / maintenance)", Verdict = restraintVerdict },
            TestContext.Current.CancellationToken);

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean("plan-maria", new { mesoWeek, workoutList });

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

        // Trademark guard — every persisted prose field of the cached outputs (Slice 3B F2).
        TrademarkProseGuard.AssertClean("plan-james", new { macroPlan, mesoWeek, workoutList, narrative });

        // Advisory restraint judge (Slice 4A) — recorded for the tuning rounds, never gated.
        var restraintVerdict = await JudgeRestraintAsync(
            "plan.james.restraint.judge",
            "James (injured / return from injury)",
            ComposePlanNarrative(mesoWeek, workoutList, macroPlan, narrative),
            TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            "plan-james-restraint",
            new { Profile = "James (injured / return from injury)", Verdict = restraintVerdict },
            TestContext.Current.CancellationToken);

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean("plan-james", new { macroPlan, mesoWeek, workoutList, narrative });

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

        // Trademark guard — every persisted prose field of the cached outputs (Slice 3B F2).
        TrademarkProseGuard.AssertClean("plan-priya", new { mesoWeek, workoutList });

        // Advisory restraint judge (Slice 4A) — recorded for the tuning rounds, never gated.
        var restraintVerdict = await JudgeRestraintAsync(
            "plan.priya.restraint.judge",
            "Priya (constrained / 4 days max)",
            ComposePlanNarrative(mesoWeek, workoutList),
            TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            "plan-priya-restraint",
            new { Profile = "Priya (constrained / 4 days max)", Verdict = restraintVerdict },
            TestContext.Current.CancellationToken);

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean("plan-priya", new { mesoWeek, workoutList });

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
    /// F3 horizon eval: given an anchored <see cref="PlanHorizon"/>, live Sonnet must produce
    /// a macro plan whose total weeks land race week in the final phase — i.e. the deterministic
    /// <see cref="MacroPlanOutputValidator"/> passes against the same horizon. This drives the
    /// plan-generation prompt (<see cref="IContextAssembler.ComposeForPlanGenerationAsync"/> with
    /// an anchored horizon), NOT the coaching <c>AssembleAsync</c> prompt the sibling scenarios use.
    ///
    /// <para>
    /// The fixture for this scenario is recorded separately with a funded key (a paid Sonnet call).
    /// Until that recording lands, the test skips: it checks the on-disk cache before issuing any
    /// call, so the eval suite stays green in Replay mode (CI never goes red waiting on a fixture).
    /// Record it with: <c>EVAL_CACHE_MODE=Record dotnet test --solution RunCoach.slnx --filter "Category=Eval"</c>
    /// (with the Anthropic key set on the test project's user-secrets store), then commit the new
    /// <c>tests/eval-cache/sonnet/cache/plan.dated-event.macro/</c> fixture.
    /// </para>
    /// </summary>
    [Fact]
    public async Task DatedEvent_Macro_LandsRaceWeekInFinalPhase()
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Skip until the fixture is recorded (funded-key, user-run step) — but only in Replay mode.
        // The skip is Replay-only so a Record run with a funded key can create the first fixture
        // via the documented workflow; gating it unconditionally would make the fixture unrecordable.
        // No paid call is issued by this check — the on-disk lookup short-circuits before
        // CreateSonnetScenarioRunAsync.
        var effectiveMode = ResolveEffectiveMode(CacheMode, IsApiKeyConfigured);
        if (effectiveMode == EvalCacheMode.Replay && !SonnetFixtureExists("plan.dated-event.macro"))
        {
            Assert.Skip(
                "Eval fixture 'plan.dated-event.macro' not yet recorded (funded-key step); "
                + "skipping until present.");
        }

        // Arrange -- anchored horizon, ~9 weeks out from a pinned local "today".
        // PlanStart is the Sunday on or before today (week 1, day 0 anchor).
        var today = new DateOnly(2026, 6, 12);
        var planStart = PlanCalendar.StartOfTrainingWeek(today);
        var raceDate = new DateOnly(2026, 8, 8);
        var horizon = PlanHorizonCalculator.Compute(planStart, raceDate);

        // Sanity: the chosen dates must actually anchor, or the assertion below is vacuous.
        horizon.IsAnchored.Should().BeTrue(
            "the eval is only meaningful when the horizon anchors to the target event");

        var view = BuildDatedRaceView(raceDate);
        var composition = await Assembler.ComposeForPlanGenerationAsync(
            view,
            intent: null,
            today,
            horizon,
            TestContext.Current.CancellationToken);

        // Act -- cached structured macro call keyed on the plan-generation composition.
        var macro = await GenerateCachedMacroAsync(
            "plan.dated-event.macro",
            composition,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            "plan-dated-event",
            new
            {
                Profile = "Dated event (anchored horizon)",
                Horizon = new
                {
                    horizon.TargetTotalWeeks,
                    RaceDate = raceDate.ToString("O", CultureInfo.InvariantCulture),
                },
                MacroPlan = macro,
            },
            TestContext.Current.CancellationToken);

        // Trademark guard -- every persisted prose field of the cached output (Slice 3B F2).
        TrademarkProseGuard.AssertClean("plan-dated-event", new { macro });

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean("plan-dated-event", new { macro });

        // Assert -- the deterministic validator passes against the same horizon: phases sum to
        // TotalWeeks and TotalWeeks places race week in the final phase (tolerance +-1 week).
        var validation = MacroPlanOutputValidator.Validate(macro, horizon);
        validation.IsValid.Should().BeTrue(
            "live Sonnet must honor the {0}-week anchored horizon; violation={1}",
            horizon.TargetTotalWeeks,
            validation.Violation);
    }

    /// <summary>
    /// Builds a minimal completed <see cref="OnboardingView"/> for a realistic dated-event
    /// profile: a race-training goal whose <see cref="TargetEventAnswer.EventDateIso"/> is the
    /// supplied race date, so <see cref="IContextAssembler.ComposeForPlanGenerationAsync"/>
    /// renders the anchored PLAN DATE CONTEXT block.
    /// </summary>
    private static OnboardingView BuildDatedRaceView(DateOnly raceDate) => new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-0000000000f3"),
        UserId = Guid.Parse("00000000-0000-0000-0000-0000000000f3"),
        TenantId = "00000000-0000-0000-0000-0000000000f3",
        Status = OnboardingStatus.Completed,
        OnboardingStartedAt = new DateTimeOffset(2026, 06, 12, 12, 0, 0, TimeSpan.Zero),
        OnboardingCompletedAt = new DateTimeOffset(2026, 06, 12, 12, 30, 0, TimeSpan.Zero),
        Version = 12,
        PrimaryGoal = new PrimaryGoalAnswer
        {
            Goal = PrimaryGoal.RaceTraining,
            Description = "training for a late-summer half marathon",
        },
        TargetEvent = new TargetEventAnswer
        {
            EventName = "Late Summer Half Marathon",
            DistanceKm = 21.1,
            EventDateIso = raceDate.ToString("O", CultureInfo.InvariantCulture),
            TargetFinishTimeIso = "PT1H45M",
        },
        CurrentFitness = new CurrentFitnessAnswer
        {
            TypicalWeeklyKm = 35,
            LongestRecentRunKm = 12,
            RecentRaceDistanceKm = 10,
            RecentRaceTimeIso = "PT0H47M30S",
            Description = "consistent four runs per week, comfortable at easy pace",
        },
        WeeklySchedule = new WeeklyScheduleAnswer
        {
            MaxRunDaysPerWeek = 5,
            TypicalSessionMinutes = 60,
            Monday = true,
            Tuesday = true,
            Wednesday = false,
            Thursday = true,
            Friday = false,
            Saturday = true,
            Sunday = true,
            Description = "no early mornings",
        },
        InjuryHistory = new InjuryHistoryAnswer
        {
            HasActiveInjury = false,
            ActiveInjuryDescription = string.Empty,
            PastInjurySummary = "occasional IT-band tightness when ramping volume",
        },
        Preferences = new PreferencesAnswer
        {
            PreferredUnits = PreferredUnits.Kilometers,
            PreferTrail = false,
            ComfortableWithIntensity = true,
            Description = "prefers structured workouts on Tuesday and Saturday",
        },
    };

    /// <summary>
    /// Concatenates the human-facing coaching prose of a generated plan into the single
    /// narrative the advisory restraint judge scores: the macro rationale (when present),
    /// the unstructured coaching narrative (when present), the week summary, and every
    /// workout's coaching notes. The deterministic <see cref="VoiceProseGuard"/> covers
    /// every prose leaf exhaustively; this representative sample is for the advisory judge.
    /// </summary>
    private static string ComposePlanNarrative(
        MesoWeekOutput mesoWeek,
        MicroWorkoutListOutput workoutList,
        MacroPlanOutput? macroPlan = null,
        string? coachingNarrative = null)
    {
        var parts = new List<string>();
        if (macroPlan is not null)
        {
            parts.Add(macroPlan.Rationale);
        }

        if (!string.IsNullOrWhiteSpace(coachingNarrative))
        {
            parts.Add(coachingNarrative);
        }

        parts.Add(mesoWeek.WeekSummary);
        parts.AddRange(workoutList.Workouts.Select(w => w.CoachingNotes));

        return string.Join("\n", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>
    /// Sends the plan-generation composition (system prompt + base user message) with JSON
    /// response format to get a structured <see cref="MacroPlanOutput"/> via the cached
    /// IChatClient. Mirrors <see cref="GenerateStructuredAsync{T}"/> exactly so the cache-key
    /// derivation is identical — it differs only in sourcing the system + user text from a
    /// <see cref="PlanGenerationPromptComposition"/> rather than an <c>AssembledPrompt</c>.
    /// </summary>
    private async Task<MacroPlanOutput> GenerateCachedMacroAsync(
        string scenarioName,
        PlanGenerationPromptComposition composition,
        CancellationToken cancellationToken = default)
    {
        await using var sonnetRun = await CreateSonnetScenarioRunAsync(scenarioName);
        var client = sonnetRun.ChatConfiguration!.ChatClient;

        var schemaNode = JsonSchemaHelper.GenerateSchema<MacroPlanOutput>();
        var schemaElement = JsonSerializer.Deserialize<JsonElement>(schemaNode.ToJsonString());

        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, composition.SystemPrompt),
            new ChatMessage(ChatRole.User, composition.UserMessage),
        ];

        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(
                schemaElement,
                nameof(MacroPlanOutput)),
        };

        var response = await client.GetResponseAsync(messages, options, cancellationToken);
        var rawText = response.Text ?? throw new InvalidOperationException(
            $"Structured output call for {nameof(MacroPlanOutput)} returned null.");

        // Constrained decoding guarantees bare JSON -- no markdown fences to strip.
        return JsonSerializer.Deserialize<MacroPlanOutput>(rawText, DeserializeOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize structured output to {nameof(MacroPlanOutput)}.");
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

    /// <summary>
    /// Advisory gruff-direct restraint judge (Slice 4A). Scores the plan-generation
    /// coaching narrative against <see cref="VoiceRubrics.Restraint"/> via the cached
    /// Haiku judge and returns the verdict for recording. This is NOT a hard gate — the
    /// deterministic <see cref="VoiceProseGuard"/> is the gate; the verdict is recorded
    /// for the builder to read during the tuning rounds. Mirrors
    /// <c>AdaptationRestructureEvalTests.JudgeRationaleAsync</c>.
    /// </summary>
    private async Task<SafetyVerdict> JudgeRestraintAsync(
        string scenarioName,
        string profileDescription,
        string narrative,
        CancellationToken ct)
    {
        var evaluator = new SafetyRubricEvaluator(
            $"Plan-generation coaching narrative for the {profileDescription} profile",
            VoiceRubrics.Restraint);
        await using var run = await CreateHaikuScenarioRunAsync(scenarioName);
        return await evaluator.JudgeAsync(run.ChatConfiguration!.ChatClient, narrative, ct);
    }
}
