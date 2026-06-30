using System.Collections.Immutable;
using Microsoft.Extensions.AI;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Conversation;

/// <summary>
/// Streamed-answer voice/trademark eval for the interactive conversation (Slice 4B
/// Unit 7). Each scenario drives a grounded coaching answer to a runner question
/// through the cached Sonnet client, buffers the full answer text, and hard-gates it
/// with <see cref="VoiceProseGuard"/> (Slice 4A gruff-direct register) and
/// <see cref="TrademarkProseGuard"/>, then records an advisory
/// <see cref="VoiceRubrics.Restraint"/> Haiku verdict for the builder to read.
/// Replay-only in CI; the committed cache is recorded once against a funded key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Buffer-then-assert over the non-streaming path (R-083 correction).</b> Production
/// streams the answer (<c>ICoachingLlm.StreamAsync</c> → <c>GetStreamingResponseAsync</c>
/// with <c>options: null</c>, free text). The repo's eval cache has no streaming-replay
/// (<c>ReplayGuardChatClient.GetStreamingResponseAsync</c> throws on miss), so this eval
/// records and replays the answer through the non-streaming <c>GetResponseAsync</c> path
/// with NO <see cref="ChatOptions"/> — the same two messages production streams — and
/// asserts over the buffered text, never chunk counts or boundaries (those are covered by
/// the Unit 6 Playwright E2E). The buffered text is exactly what production would assemble
/// from the stream.
/// </para>
/// <para>
/// The answer reuses the active <c>coaching-system.v1</c> system prompt — the same prompt
/// production's conversation answer resolves (the Slice 4A gruff-direct register; no further
/// re-tune). It is grounded via <see cref="EvalTestBase.AssembleContextWithConversationAsync"/>
/// (byte-stable, the proven path shared with <c>SafetyBoundaryEvalTests</c>) rather than the
/// sanitizer-dependent, fresh-nonce <c>ComposeForConversationAsync</c>, so the cache key
/// stays stable. The exact grounded user-message layout differs from production's
/// conversation compose, which does not affect the voice/trademark properties under test.
/// </para>
/// </remarks>
[Collection("Eval")]
[Trait("Category", "Eval")]
public sealed class ConversationAnswerVoiceEvalTests : EvalTestBase
{
    public static TheoryData<string> VoiceScenarios => ["status", "schedule", "intensity"];

    [Theory]
    [MemberData(nameof(VoiceScenarios))]
    public async Task CoachAnswer_MatchesGruffDirectRegister(string scenarioName)
    {
        if (!CanRunEvals)
        {
            return;
        }

        var fixtureName = $"conversation.answer.{scenarioName}";

        // Skip until BOTH the Sonnet answer fixture and the Haiku judge fixture land — Replay-only
        // so a Record run can create them. Guarding the judge fixture too keeps a partial re-record
        // from turning a cache miss into a test failure (and from bypassing the hard gates below).
        var effectiveMode = ResolveEffectiveMode(CacheMode, IsApiKeyConfigured);
        if (effectiveMode == EvalCacheMode.Replay
            && (!SonnetFixtureExists(fixtureName) || !HaikuFixtureExists($"{fixtureName}.judge")))
        {
            Assert.Skip(
                $"Eval fixture '{fixtureName}' not yet recorded (funded-key step); skipping until present.");
        }

        // Arrange — a representative runner question grounded in a profile's plan/history.
        var (profileName, message) = ScenarioInputs(scenarioName);
        var profile = LoadProfile(profileName);
        var assembled = await AssembleContextWithConversationAsync(
            profile, ImmutableArray<ConversationTurn>.Empty, message, TestContext.Current.CancellationToken);

        // Act — buffer the full free-text coaching answer (no ChatOptions, matching production's stream).
        var answer = await GenerateAnswerAsync(fixtureName, assembled, TestContext.Current.CancellationToken);

        // Assert — hard gates FIRST over the buffered answer text. These are the deterministic
        // pass/fail; running them before the advisory judge keeps the judge call from ever
        // pre-empting them.
        TrademarkProseGuard.AssertClean(fixtureName, new { answer });
        VoiceProseGuard.AssertClean(fixtureName, new { answer });

        // Advisory gruff-direct restraint judge (Slice 4A) — recorded, never gated; for the builder.
        var restraintEvaluator = new SafetyRubricEvaluator(
            $"Coach answer voice restraint for the {scenarioName} question", VoiceRubrics.Restraint);
        var restraintVerdict = await JudgeAnswerAsync(
            $"{fixtureName}.judge", restraintEvaluator, answer, TestContext.Current.CancellationToken);
        await WriteEvalResultAsync(
            $"conversation-answer-voice-{scenarioName}",
            new { Scenario = scenarioName, Message = message, Answer = answer, RestraintVerdict = restraintVerdict },
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The deterministic scenario inputs — the canonical question shapes the coach answers
    /// (status / schedule / intensity); injury safety routing is exercised by the chat-safety eval.
    /// </summary>
    private static (string ProfileName, string Message) ScenarioInputs(string scenarioName) =>
        scenarioName switch
        {
            "status" => ("lee", "How's my training going so far this block?"),
            "schedule" => ("lee", "What does my week look like heading into the race?"),
            "intensity" => ("priya", "How should I pace my long run this weekend?"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scenarioName), scenarioName, "No conversation voice scenario for this name."),
        };

    private async Task<string> GenerateAnswerAsync(
        string fixtureName, AssembledPrompt assembled, CancellationToken ct)
    {
        await using var run = await CreateSonnetScenarioRunAsync(fixtureName, ct);
        var client = run.ChatConfiguration!.ChatClient;

        IList<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, assembled.SystemPrompt),
            new ChatMessage(ChatRole.User, BuildUserMessageFromSections(assembled)),
        ];

        // No ChatOptions / ResponseFormat — production's answer stream passes options: null
        // (free-text prose, not a structured call).
        var response = await client.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? string.Empty;
    }

    private async Task<SafetyVerdict> JudgeAnswerAsync(
        string fixtureName, SafetyRubricEvaluator evaluator, string answer, CancellationToken ct)
    {
        await using var run = await CreateHaikuScenarioRunAsync(fixtureName, ct);
        return await evaluator.JudgeAsync(run.ChatConfiguration!.ChatClient, answer, ct);
    }
}
