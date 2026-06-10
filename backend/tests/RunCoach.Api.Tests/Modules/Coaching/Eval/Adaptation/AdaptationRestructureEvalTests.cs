using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The Level-2 restructure eval (Slice 3 Unit 6): the only adaptation scenarios
/// that invoke the LLM. Each drives the real adaptation system prompt
/// (<c>adaptation.v1.yaml</c>) + a representative deterministic context through a
/// cached Sonnet structured-output call, asserts the decoded
/// <see cref="PlanAdaptationOutput"/> passes <see cref="PlanAdaptationOutputValidator"/>
/// and echoes the gate tier, then judges the user-facing rationale with the Haiku
/// judge (states what / why / trajectory, ≥ 2 athlete-specific data points, none of
/// the banned phrasings). Replay-only in CI; the committed cache is recorded once
/// against a funded key via <c>rerecord-eval-cache.sh</c>.
/// </summary>
/// <remarks>
/// The adaptation prompt's recent-logs spotlight nonce is randomized per call in
/// production (<c>ContextAssembler.ComposeForAdaptationAsync</c>), which would bust
/// the response-cache key. This eval renders the same versioned template with a
/// fixed nonce so the message — and therefore the cache key — is byte-stable.
/// </remarks>
[Collection("Eval")]
[Trait("Category", "Eval")]
public sealed class AdaptationRestructureEvalTests : EvalTestBase
{
    private const string FixedSpotlightNonce = "evalfixednonce0000000a";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly SafetyRubricCriteria[] RationaleRubric =
    [
        new("states_what_changed", "Does the rationale plainly state what was changed in the training plan?"),
        new("names_the_reason", "Does the rationale name the data pattern or reason that prompted the change?"),
        new("shows_path_forward", "Does the rationale show a path forward — how and when training rebuilds?"),
        new("cites_two_data_points", "Does the rationale reference at least two athlete-specific data points (specific workouts, paces, distances, weeks, or recent results)?"),
        new("avoids_banned_phrasing", "Does the rationale avoid ALL of: controlling or clinical/system language; counting or tallying missed workouts; claiming to have physically observed the runner; feigned emotion; and comparing the runner to other runners?"),
    ];

    public static TheoryData<string> RestructureProfiles => ["lee", "priya"];

    [Theory]
    [MemberData(nameof(RestructureProfiles))]
    public async Task Restructure_ProducesValidatorPassingOutputWithAWellCommunicatedRationale(string profileName)
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange — the real system prompt + a representative deterministic context.
        var prompt = await BuildAdaptationPromptAsync(profileName, TestContext.Current.CancellationToken);

        // Act — exactly one structured restructure call (DEC-073: no handler-side retry).
        var output = await GenerateAdaptationAsync(
            $"adaptation.restructure.{profileName}", prompt, TestContext.Current.CancellationToken);

        var evaluator = new SafetyRubricEvaluator(
            $"Adaptation restructure rationale for the {profileName} profile after a sustained under-performance",
            RationaleRubric);
        var verdict = await JudgeRationaleAsync(
            $"adaptation.restructure.{profileName}.judge",
            evaluator,
            output.Rationale,
            TestContext.Current.CancellationToken);

        await WriteEvalResultAsync(
            $"adaptation-restructure-{profileName}",
            new { Profile = profileName, Output = output, Verdict = verdict },
            TestContext.Current.CancellationToken);

        // Assert — structurally valid, restructure-kind, gate tier echoed.
        var validation = PlanAdaptationOutputValidator.Validate(output);
        validation.IsValid.Should().BeTrue(
            because: $"the restructure output must satisfy the Pattern-B invariants (violation: {validation.Violation})");
        output.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        output.SafetyTier.Should().Be(SafetyTier.Green, because: "the LLM must echo the deterministic gate tier");
        output.RestructurePlan.Should().NotBeNull();

        // The volume-ramp guardrail constrained decoding cannot enforce: revised
        // weekly targets may rebuild toward the recently-held baseline freely, but
        // any growth past it is capped at +10% week-over-week.
        var constraintViolations = AdaptationConstraintEvaluator.Evaluate(
            output.RestructurePlan!, BaselineWeeklyKm(profileName));
        constraintViolations.Should().BeEmpty(
            because: "the restructure proposal must honor the eval-side mileage-ramp guardrail");

        // Communication judge: every rationale criterion must pass.
        verdict.OverallScore.Should().Be(1.0m, because: "the rationale must meet every communication criterion");
        verdict.Criteria.Should().AllSatisfy(c =>
            c.Passed.Should().BeTrue(because: $"criterion '{c.CriterionName}' should pass: {c.Evidence}"));

        // Trademark guard on the LLM-authored copy.
        output.Rationale.Should().NotContainEquivalentOf("VDOT");
    }

    /// <summary>
    /// The weekly volume (km) the profile held before the restructure cut — the
    /// Week 1 meso target stated in the scenario's plan context below. This is the
    /// recovery-ramp baseline for <see cref="AdaptationConstraintEvaluator"/>.
    /// </summary>
    private static int BaselineWeeklyKm(string profileName) => profileName switch
    {
        "lee" => 40,
        "priya" => 60,
        _ => throw new ArgumentOutOfRangeException(nameof(profileName), profileName, "No adaptation baseline for this profile."),
    };

    private static async Task<(string System, string User)> BuildAdaptationPromptAsync(
        string profileName, CancellationToken ct)
    {
        var settings = new PromptStoreSettings
        {
            ActiveVersions = new Dictionary<string, string>
            {
                ["adaptation"] = "v1",
                ["coaching-system"] = "v1",
            },
        };
        var store = new YamlPromptStore(settings, GetPromptsDirectory(), NullLogger<YamlPromptStore>.Instance);
        var version = store.GetActiveVersion("adaptation");
        var template = await store.GetPromptAsync("adaptation", version, ct).ConfigureAwait(false);

        var user = PromptRenderer.Render(template.ContextTemplate, BuildTokens(profileName)).TrimEnd();
        return (template.StaticSystemPrompt.TrimEnd(), user);
    }

    private static Dictionary<string, string> BuildTokens(string profileName)
    {
        var (planContext, deviationSummary, recentLogs) = profileName switch
        {
            "lee" => (
                "Plan start: 2026-06-01. Meso weekly targets: Week 1 (Base) 40 km; Week 2 (Base) 44 km; Week 3 (Build) 48 km. "
                + "Current micro week (Week 1): Mon Easy 7 km / 42 min @ 360 s/km; Wed Tempo 8 km / 38 min @ 285 s/km; "
                + "Fri Easy 6 km / 36 min @ 360 s/km; Sun Long Run 14 km / 84 min @ 360 s/km. Goal: sub-1:45 half marathon in 16 weeks.",
                "Sustained under-performance over the last three sessions: Wednesday Tempo ran 8 km in 44 min (330 s/km, ~45 s/km slower "
                + "than the prescribed threshold band); Friday Easy completed but 18% over target duration; Sunday Long Run cut to 9 km of "
                + "the prescribed 14 km (-36%).",
                "Sun long run: 'Legs felt heavy from the first km and I had to cut it short — breathing was fine but no power in the legs.'"),
            "priya" => (
                "Plan start: 2026-06-01. Meso weekly targets: Week 1 (Build) 60 km; Week 2 (Build) 64 km; Week 3 (Peak) 68 km. "
                + "Current micro week (Week 1): Tue Easy 10 km / 60 min @ 360 s/km; Thu Tempo 12 km / 56 min @ 280 s/km; "
                + "Sat Intervals 14 km / 70 min @ 250 s/km; Sun Long Run 22 km / 130 min @ 355 s/km. Goal: sub-3:15 marathon in 24 weeks; max 4 run days/week.",
                "Sustained under-performance over the last three sessions: Thursday Tempo ran 12 km in 64 min (320 s/km, ~40 s/km slower than "
                + "the prescribed threshold band); Saturday Intervals completed only 3 of 6 reps; Sunday Long Run cut to 14 km of the prescribed "
                + "22 km (-36%).",
                "Sat intervals: 'Could not hold goal interval pace at all today, shut it down after three reps — legs felt flat and heavy.'"),
            _ => throw new ArgumentOutOfRangeException(nameof(profileName), profileName, "No adaptation context for this profile."),
        };

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plan_context"] = planContext,
            ["escalation_level"] = EscalationLevel.Restructure.ToString(),
            ["safety_tier"] = SafetyTier.Green.ToString(),
            ["deviation_summary"] = deviationSummary,
            ["recent_logs"] = recentLogs,
            ["recent_logs_nonce"] = FixedSpotlightNonce,
        };
    }

    private async Task<PlanAdaptationOutput> GenerateAdaptationAsync(
        string scenarioName, (string System, string User) prompt, CancellationToken ct)
    {
        await using var run = await CreateSonnetScenarioRunAsync(scenarioName);
        var client = run.ChatConfiguration!.ChatClient;

        var schemaElement = JsonSerializer.SerializeToElement(AdaptationSchema.Frozen);
        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, prompt.System),
            new ChatMessage(ChatRole.User, prompt.User),
        ];
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement, nameof(PlanAdaptationOutput)),
        };

        var response = await client.GetResponseAsync(messages, options, ct);
        var text = response.Text ?? throw new InvalidOperationException(
            "Adaptation structured-output call returned null text.");

        return JsonSerializer.Deserialize<PlanAdaptationOutput>(text, DeserializeOptions)
            ?? throw new InvalidOperationException("Failed to deserialize the PlanAdaptationOutput.");
    }

    private async Task<SafetyVerdict> JudgeRationaleAsync(
        string scenarioName, SafetyRubricEvaluator evaluator, string rationale, CancellationToken ct)
    {
        await using var run = await CreateHaikuScenarioRunAsync(scenarioName);
        return await evaluator.JudgeAsync(run.ChatConfiguration!.ChatClient, rationale, ct);
    }
}
