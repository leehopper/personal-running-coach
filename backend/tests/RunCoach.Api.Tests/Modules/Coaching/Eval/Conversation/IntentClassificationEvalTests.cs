using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Prompts;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Conversation;

/// <summary>
/// Conversation intent-classifier accuracy eval (Slice 4B Unit 7). Drives the real
/// <c>conversation-classifier.v1</c> prompt + <see cref="ClassifierSchema.Frozen"/>
/// over a labelled ground-truth set through the cached Haiku binding (the classifier's
/// production model, <c>claude-haiku-4-5</c>), reproduces production's
/// validate-then-coerce-to-Ambiguous policy, and scores a 3x3 confusion matrix.
/// </summary>
/// <remarks>
/// <para>
/// <b>Zero-regression gate.</b> The committed <see cref="IntentScenarioLibrary"/>
/// labels are the baseline: every scenario must reproduce its label in Replay or the
/// PR fails (mirrors <c>AdaptationClassificationEvalTests</c>). The
/// <see cref="IntentConfusionMatrix"/> additionally flags DEC-085 bias-to-ask
/// "dangerous" misclassifications (a confident guess where the classifier should have
/// asked) as a hard gate, the analog of the adaptation suite's under-reaction hard fail.
/// </para>
/// <para>
/// <b>Why it builds the prompt by hand.</b> Production's
/// <c>ContextAssembler.ComposeForClassificationAsync</c> mints a fresh CSPRNG spotlight
/// nonce per call (busting the response-cache key) and throws on the
/// <see cref="EvalTestBase"/> assembler, which is built without the sanitizer. This eval
/// loads the same versioned prompt through a real <see cref="YamlPromptStore"/> and
/// renders it with production's <see cref="PromptRenderer"/>, wrapping the message in the
/// same <c>CURRENT_USER_INPUT</c> spotlight delimiter the sanitizer emits but with a
/// pinned nonce, so the message — and therefore the cache key — is byte-stable. Eval
/// inputs are clean ASCII, so the sanitizer's escaping is a no-op here; this eval tests
/// the classifier prompt + schema, not the sanitizer (which has its own tests).
/// </para>
/// </remarks>
[Collection("Eval")]
[Trait("Category", "Eval")]
public sealed class IntentClassificationEvalTests : EvalTestBase
{
    private const string ClassifierPromptId = "conversation-classifier";
    private const string ClassifierVersion = "v1";
    private const string FixedNonce = "evalfixednonce0000000a";

    /// <summary>A pinned "today" so any relative-date resolution in the prompt is byte-stable.</summary>
    private static readonly DateOnly FixedToday = new(2026, 6, 30);

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Classifier_ReproducesLabels_AndHoldsBiasToAsk()
    {
        if (!CanRunEvals)
        {
            return;
        }

        var effectiveMode = ResolveEffectiveMode(CacheMode, IsApiKeyConfigured);
        var matrix = new IntentConfusionMatrix();
        var predictions = new List<object>();
        var regressions = new List<string>();
        var skipped = new List<string>();

        foreach (var scenario in IntentScenarioLibrary.Scenarios)
        {
            var fixtureName = FixtureName(scenario);

            // Replay-only, PER-SCENARIO skip: an un-recorded scenario is skipped on its own so it
            // never disables the zero-regression gate on the already-recorded scenarios (and never
            // issues a live call on a cache miss). A Record run still records it.
            if (effectiveMode == EvalCacheMode.Replay && !HaikuFixtureExists(fixtureName))
            {
                skipped.Add(scenario.Id);
                continue;
            }

            var predicted = await ClassifyAsync(fixtureName, scenario.Message, TestContext.Current.CancellationToken);
            matrix.Record(scenario.Expected, predicted);
            predictions.Add(new { scenario.Id, scenario.Message, Expected = scenario.Expected.ToString(), Predicted = predicted.ToString() });

            if (predicted != scenario.Expected)
            {
                regressions.Add($"{scenario.Id}: expected {scenario.Expected} but classifier resolved {predicted}");
            }
        }

        await WriteEvalResultAsync(
            "conversation-intent-confusion-matrix",
            new { Matrix = matrix.ToSnapshot(), Predictions = predictions, Skipped = skipped },
            TestContext.Current.CancellationToken);

        // Nothing recorded yet (fresh checkout before the funded recording) — skip rather than
        // assert over an empty matrix.
        if (matrix.Total == 0)
        {
            Assert.Skip(
                "No conversation intent-classifier fixtures recorded yet (funded-key step); skipping until present.");
        }

        // Bias-to-ask is the dangerous direction (DEC-085): a confident class on a truly-Ambiguous
        // message, or a reported run silently answered as a question. Gate it explicitly.
        matrix.AnyDangerous.Should().BeFalse(
            because: "DEC-085 biases the classifier to ask rather than guess; a confident misclassification "
                + $"on an Ambiguous message (or ignoring a reported run) is a hard fail "
                + $"(matrix: {JsonSerializer.Serialize(matrix.ToSnapshot())})");

        // Zero-regression gate: every recorded label must reproduce.
        regressions.Should().BeEmpty(
            because: "the committed ground-truth labels are the classifier-accuracy baseline; "
                + $"a label flipping off is a regression ({string.Join("; ", regressions)})");
    }

    [Fact]
    public void Catalog_CoversEachIntentWithMinimumPerClass()
    {
        // Pure checks (no LLM, always runs). The accuracy eval drives the cached Haiku JUDGE
        // binding as a stand-in for the classifier's Haiku model; both default to the same alias.
        // Guard that they stay aligned so the eval never silently measures a different model than
        // production's classifier.
        Settings.ClassifierModelId.Should().Be(
            Settings.JudgeModelId,
            because: "the classifier eval drives the Haiku judge binding; a divergent classifier id would measure the wrong model");

        // The confusion matrix is only meaningful with enough labelled examples per class.
        foreach (var intent in Enum.GetValues<MessageIntent>())
        {
            var count = IntentScenarioLibrary.Scenarios.Count(s => s.Expected == intent);
            count.Should().BeGreaterThanOrEqualTo(
                IntentScenarioLibrary.MinimumPerClass,
                because: $"intent '{intent}' needs at least {IntentScenarioLibrary.MinimumPerClass} labelled scenarios");
        }
    }

    private static string FixtureName(IntentScenario scenario) => $"intent.{scenario.Id}";

    /// <summary>
    /// Builds the classifier system prompt + grounded user message exactly as production's
    /// <c>ComposeForClassificationAsync</c> does — the same versioned YAML loaded through a
    /// real <see cref="YamlPromptStore"/> and rendered with <see cref="PromptRenderer"/> —
    /// but with a pinned spotlight nonce and date so the cache key is byte-stable.
    /// </summary>
    private static async Task<(string System, string User)> BuildClassifierPromptAsync(string message, CancellationToken ct)
    {
        var store = new YamlPromptStore(
            new PromptStoreSettings
            {
                ActiveVersions = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ClassifierPromptId] = ClassifierVersion,
                },
            },
            GetPromptsDirectory(),
            NullLogger<YamlPromptStore>.Instance);

        var template = await store.GetPromptAsync(ClassifierPromptId, ClassifierVersion, ct).ConfigureAwait(false);
        var system = template.StaticSystemPrompt.TrimEnd();

        var wrappedMessage = $"<CURRENT_USER_INPUT id=\"{FixedNonce}\">{message}</CURRENT_USER_INPUT>";
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["today"] = FixedToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["current_message"] = wrappedMessage,
        };
        var user = PromptRenderer.Render(template.ContextTemplate, tokens).TrimEnd();

        return (system, user);
    }

    /// <summary>
    /// Classifies one message through the cached Haiku client and reproduces production's
    /// validate-then-coerce-to-Ambiguous policy, returning the post-coercion intent the
    /// runner would actually be routed on.
    /// </summary>
    private async Task<MessageIntent> ClassifyAsync(string fixtureName, string message, CancellationToken ct)
    {
        await using var run = await CreateHaikuScenarioRunAsync(fixtureName, ct);
        var client = run.ChatConfiguration!.ChatClient;

        var (system, user) = await BuildClassifierPromptAsync(message, ct);
        var schemaElement = JsonSerializer.SerializeToElement(ClassifierSchema.Frozen);
        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, system),
            new ChatMessage(ChatRole.User, user),
        ];
        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(schemaElement, nameof(MessageIntentOutput)),
        };

        var response = await client.GetResponseAsync(messages, options, ct);
        var text = response.Text ?? throw new InvalidOperationException(
            $"Classifier returned null text for '{fixtureName}'.");
        var output = JsonSerializer.Deserialize<MessageIntentOutput>(text, DeserializeOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize MessageIntentOutput for '{fixtureName}'.");

        // Mirror MessageIntentClassifier: a structurally-invalid union or out-of-range
        // draft is coerced to Ambiguous (DEC-085 bias-to-ask) before routing.
        var validation = MessageIntentOutputValidator.Validate(output);
        return validation.IsValid ? output.Intent : MessageIntent.Ambiguous;
    }
}
