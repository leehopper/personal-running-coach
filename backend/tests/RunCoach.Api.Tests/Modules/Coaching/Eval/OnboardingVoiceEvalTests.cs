using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.AI;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// The onboarding-voice eval (Slice 4A PR6): the only eval that exercises the
/// onboarding prompt's LLM output. It closes the gap PR3 surfaced — no eval
/// guarded the gruff-direct <c>onboarding-v1.yaml</c> rewrite, yet the
/// 2026-06-13 live pass saw the onboarding turns gush ("Love it!", "Great
/// foundation!"). Each scenario drives the real onboarding system prompt + a
/// representative turn through a cached Sonnet structured-output call, asserts
/// the decoded <see cref="OnboardingTurnOutput"/> passes
/// <see cref="OnboardingTurnOutputValidator"/>, hard-gates every prose field
/// with <see cref="VoiceProseGuard"/> (and keeps <see cref="TrademarkProseGuard"/>),
/// and records an advisory <see cref="VoiceRubrics.Restraint"/> Haiku verdict for
/// the builder to read during the tuning rounds. Replay-only in CI; the committed
/// cache is recorded once against a funded key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this builds the prompt by hand rather than calling production.</b>
/// Onboarding does not flow through <see cref="ContextAssembler"/>'s prompt store:
/// <c>onboarding-v1.yaml</c> uses the dash-named convention the dot-versioned
/// <c>YamlPromptStore</c> cannot resolve, so production reads only the file's
/// <c>static_system_prompt</c> key off disk (<c>ContextAssembler.LoadOnboardingSystemPromptAsync</c>)
/// and builds the user message programmatically in <c>BuildOnboardingUserMessage</c>
/// (ONBOARDING STATE slot summary, CURRENT_TOPIC, then the sanitized + delimiter-wrapped
/// runner input). The YAML's <c>context_template</c> block is never rendered for the
/// onboarding flow. This eval mirrors both: it parses <c>static_system_prompt</c> the
/// same way and reproduces the production user-message layout.
/// </para>
/// <para>
/// It does <b>not</b> call <c>ComposeForOnboardingAsync</c> — that path generates a fresh
/// CSPRNG nonce per call (busting the response-cache key) and throws on the
/// <see cref="EvalTestBase"/> assembler, which is built without the onboarding
/// sanitizer dependencies. Instead the runner input is wrapped in the same
/// <c>CURRENT_USER_INPUT</c> delimiter the sanitizer emits, with a fixed nonce so the
/// message — and therefore the cache key — is byte-stable. The eval tests the prompt's
/// voice, not the sanitizer (which has its own tests).
/// </para>
/// </remarks>
[Collection("Eval")]
[Trait("Category", "Eval")]
public sealed class OnboardingVoiceEvalTests : EvalTestBase
{
    private const string FixedNonce = "evalfixednonce0000000a";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    // Mirrors `ContextAssembler.OnboardingSlotSerializerOptions` so a populated
    // captured-so-far slot renders byte-identically to production's user message.
    private static readonly JsonSerializerOptions SlotSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public static TheoryData<string> VoiceScenarios => ["primary-goal", "current-fitness"];

    [Theory]
    [MemberData(nameof(VoiceScenarios))]
    public async Task OnboardingReply_MatchesGruffDirectRegister(string scenarioName)
    {
        if (!CanRunEvals)
        {
            return;
        }

        // Arrange — the real onboarding system prompt + a representative turn.
        var prompt = await BuildOnboardingPromptAsync(scenarioName, TestContext.Current.CancellationToken);

        // Act — exactly one structured onboarding turn (cached).
        var output = await GenerateOnboardingAsync(
            $"onboarding.voice.{scenarioName}", prompt, TestContext.Current.CancellationToken);

        // Advisory gruff-direct restraint judge (Slice 4A), recorded for the tuning
        // rounds and never gated. The deterministic `VoiceProseGuard` below is the hard
        // gate, and this verdict is only for the builder to read. Read it per criterion
        // rather than by its overall score. An onboarding turn is a question, so the shared
        // rubric's coaching-recommendation criteria do not apply and score low by design,
        // while its register criteria are what flag a cheery regression here.
        var replyText = ConcatReplyText(output);
        var restraintEvaluator = new SafetyRubricEvaluator(
            $"Onboarding reply voice restraint for the {scenarioName} turn",
            VoiceRubrics.Restraint);
        var restraintVerdict = await JudgeReplyAsync(
            $"onboarding.voice.{scenarioName}.judge",
            restraintEvaluator,
            replyText,
            TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            $"onboarding-voice-{scenarioName}",
            new { Scenario = scenarioName, Output = output, RestraintVerdict = restraintVerdict },
            TestContext.Current.CancellationToken);

        // Assert — structurally valid Pattern-B onboarding turn.
        var currentTopic = TopicFor(scenarioName);
        var validation = OnboardingTurnOutputValidator.Validate(output, currentTopic);
        validation.IsValid.Should().BeTrue(
            because: $"the onboarding turn must satisfy the Pattern-B invariants (violation: {validation.Violation})");

        // Trademark guard on the LLM-authored copy — every prose field (consistency
        // with the other recorded-output evals).
        TrademarkProseGuard.AssertClean($"onboarding-voice-{scenarioName}", output);

        // Voice guard (Slice 4A) — hard gate: every prose field matches the gruff-direct register.
        VoiceProseGuard.AssertClean($"onboarding-voice-{scenarioName}", output);
    }

    /// <summary>
    /// Concatenates the runner-visible text of the reply: the <c>Text</c> payloads
    /// of the Text-typed content blocks (Thinking blocks carry empty text). This is
    /// the onboarding analog of the adaptation eval's single rationale string handed
    /// to the Haiku judge.
    /// </summary>
    private static string ConcatReplyText(OnboardingTurnOutput output) =>
        string.Join(
            "\n",
            output.Reply
                .Where(block => block.Type == AnthropicContentBlockType.Text)
                .Select(block => block.Text));

    private static OnboardingTopic TopicFor(string scenarioName) => ScenarioInputs(scenarioName).Topic;

    /// <summary>
    /// The deterministic scenario inputs. Two turns the 2026-06-13 live pass saw gush:
    /// the opening PrimaryGoal turn and a CurrentFitness turn. The view state is
    /// consistent with each turn's current topic so the captured-so-far summary mirrors
    /// production (the CurrentFitness turn carries an already-captured PrimaryGoal).
    /// </summary>
    private static (OnboardingView View, OnboardingTopic Topic, string UserInput) ScenarioInputs(string scenarioName) =>
        scenarioName switch
        {
            "primary-goal" => (
                new OnboardingView(),
                OnboardingTopic.PrimaryGoal,
                "I want to run a sub-2 hour half marathon in October."),
            "current-fitness" => (
                new OnboardingView
                {
                    PrimaryGoal = new PrimaryGoalAnswer
                    {
                        Goal = PrimaryGoal.GeneralFitness,
                        Description = "general fitness and staying consistent",
                    },
                },
                OnboardingTopic.CurrentFitness,
                "I run about 25 km a week across four runs. Longest recent run was 14 km. No recent race times."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenarioName), scenarioName, "No onboarding voice scenario for this name."),
        };

    private static async Task<(string System, string User)> BuildOnboardingPromptAsync(
        string scenarioName, CancellationToken ct)
    {
        var system = await LoadOnboardingSystemPromptAsync(ct);
        var (view, topic, userInput) = ScenarioInputs(scenarioName);
        var user = BuildOnboardingUserMessage(view, topic, WrapUserInput(userInput));
        return (system, user);
    }

    /// <summary>
    /// Loads the onboarding system prompt the same way production does
    /// (<c>ContextAssembler.LoadOnboardingSystemPromptAsync</c>): parse the
    /// <c>static_system_prompt</c> key out of the dash-named YAML with an
    /// underscored-naming, unmatched-property-ignoring deserializer, then trim.
    /// </summary>
    private static async Task<string> LoadOnboardingSystemPromptAsync(CancellationToken ct)
    {
        var path = Path.Combine(GetPromptsDirectory(), ContextAssembler.OnboardingPromptFileName);
        var yaml = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var doc = deserializer.Deserialize<OnboardingYamlDocument>(yaml);
        if (string.IsNullOrWhiteSpace(doc?.StaticSystemPrompt))
        {
            throw new InvalidOperationException(
                $"Onboarding prompt YAML at '{path}' is missing the 'static_system_prompt' key.");
        }

        return doc.StaticSystemPrompt.TrimEnd();
    }

    /// <summary>
    /// Reproduces <c>ContextAssembler.BuildOnboardingUserMessage</c>: the captured-so-far
    /// slot summary, the current-topic line, then the delimiter-wrapped runner input last.
    /// Explicit <c>\n</c> line endings keep the bytes platform-independent across the
    /// record machine and CI (production uses <c>AppendLine</c>, identical on both).
    /// </summary>
    private static string BuildOnboardingUserMessage(
        OnboardingView view, OnboardingTopic currentTopic, string wrappedUserInput)
    {
        var sb = new StringBuilder();
        sb.Append("ONBOARDING STATE (captured so far):\n");
        AppendSlot(sb, "PrimaryGoal", view.PrimaryGoal);
        AppendSlot(sb, "TargetEvent", view.TargetEvent);
        AppendSlot(sb, "CurrentFitness", view.CurrentFitness);
        AppendSlot(sb, "WeeklySchedule", view.WeeklySchedule);
        AppendSlot(sb, "InjuryHistory", view.InjuryHistory);
        AppendSlot(sb, "Preferences", view.Preferences);
        sb.Append('\n');
        sb.Append("CURRENT_TOPIC: ").Append(currentTopic.ToString()).Append('\n');
        sb.Append('\n');
        sb.Append(wrappedUserInput);
        return sb.ToString();
    }

    private static void AppendSlot<T>(StringBuilder sb, string label, T? value)
        where T : class
    {
        var rendered = value is null
            ? "<not yet captured>"
            : JsonSerializer.Serialize(value, value.GetType(), SlotSerializerOptions);
        sb.Append("  ").Append(label).Append(": ").Append(rendered).Append('\n');
    }

    /// <summary>
    /// Wraps the runner input in the same <c>CURRENT_USER_INPUT</c> delimiter the
    /// sanitizer emits for the current-user-message section, with a pinned nonce so the
    /// cache key is byte-stable. Eval inputs are clean ASCII, so the sanitizer's
    /// angle-bracket escaping is a no-op here.
    /// </summary>
    private static string WrapUserInput(string userInput) =>
        $"<CURRENT_USER_INPUT id=\"{FixedNonce}\">{userInput}</CURRENT_USER_INPUT>";

    private async Task<OnboardingTurnOutput> GenerateOnboardingAsync(
        string scenarioName, (string System, string User) prompt, CancellationToken ct)
    {
        await using var run = await CreateSonnetScenarioRunAsync(scenarioName, ct);
        var client = run.ChatConfiguration!.ChatClient;

        var schemaElement = JsonSerializer.SerializeToElement(OnboardingSchema.Frozen);
        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, prompt.System),
            new ChatMessage(ChatRole.User, prompt.User),
        ];
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement, nameof(OnboardingTurnOutput)),
        };

        var response = await client.GetResponseAsync(messages, options, ct);
        var text = response.Text ?? throw new InvalidOperationException(
            "Onboarding structured-output call returned null text.");

        return JsonSerializer.Deserialize<OnboardingTurnOutput>(text, DeserializeOptions)
            ?? throw new InvalidOperationException("Failed to deserialize the OnboardingTurnOutput.");
    }

    private async Task<SafetyVerdict> JudgeReplyAsync(
        string scenarioName, SafetyRubricEvaluator evaluator, string reply, CancellationToken ct)
    {
        await using var run = await CreateHaikuScenarioRunAsync(scenarioName, ct);
        return await evaluator.JudgeAsync(run.ChatConfiguration!.ChatClient, reply, ct);
    }

    /// <summary>
    /// Minimal YAML deserialization shape for <c>onboarding-v1.yaml</c> — only the
    /// <c>static_system_prompt</c> key is read, mirroring production's loader.
    /// </summary>
    private sealed class OnboardingYamlDocument
    {
#pragma warning disable S3459, S1144, CA1822 // YamlDotNet sets the property via reflection.
        public string? StaticSystemPrompt { get; set; }
#pragma warning restore S3459, S1144, CA1822
    }
}
