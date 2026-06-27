using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation.Streaming;

/// <summary>
/// Integration coverage for the streaming Q&amp;A endpoint
/// <c>POST /api/v1/conversation/messages</c> (Slice 4B PR4, the integration gate). Drives
/// the real <see cref="RunCoachAppFactory"/> SUT end-to-end: the CookieOrBearer gate,
/// antiforgery, the real deterministic <c>SafetyGate</c>, the stubbed classifier + answer
/// stream (<see cref="StubCoachingLlm"/>), the two-write user-scoped persistence, and the
/// hand-rolled SSE framing. Asserts each acceptance scenario from
/// <c>sse-conversation-endpoint.feature</c>.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConversationMessagesEndpointIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string MessagesPath = "/api/v1/conversation/messages";
    private const string StrongPassword = "Str0ngTestPassw0rd!";

    private static readonly Uri BaseUri = new("https://localhost");
    private static readonly DateTimeOffset PlanGeneratedAt = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GreenQuestion_StreamsTokens_ThenDoneCarryingThePersistedTurnId()
    {
        // Arrange
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => YieldTokensAsync("Run it easy.", " Keep the effort relaxed."));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(
            client, token, "How should I pace my long run this weekend?", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        var tokens = frames.Where(f => f.Event == "token").ToArray();
        tokens.Should().NotBeEmpty(because: "a green answer streams as token frames");
        string.Concat(tokens.Select(f => Payload<TokenPayload>(f).Delta))
            .Should().Be("Run it easy. Keep the effort relaxed.");

        var done = frames.Should().ContainSingle(f => f.Event == "done").Subject;
        Payload<DonePayload>(done).TurnId.Should().Be(
            ConversationTurnId.DeriveCoachTurnId(clientMessageId),
            because: "the done frame carries the server-derived coach turn id the client reconciles on");
        frames.Should().NotContain(f => f.Event == "error");

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().HaveCount(2);
        view.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.User)
            .Which.TurnId.Should().Be(clientMessageId);
        view.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Be("Run it easy. Keep the effort relaxed.");
    }

    [Fact]
    public async Task GreenQuestion_WithNoActivePlan_StillStreamsAnAnswer()
    {
        // Arrange — a registered runner with no onboarding profile / plan at all: the
        // answer context composes a "No active plan." grounding rather than NRE-ing.
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => YieldTokensAsync("Let's get you started."));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(client, token, "What should I do first?", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        frames.Should().Contain(f => f.Event == "token");
        frames.Should().ContainSingle(f => f.Event == "done");
        frames.Should().NotContain(f => f.Event == "error");
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().Contain(t => t.Participant == ConversationParticipant.Coach
            && t.Content == "Let's get you started.");
    }

    [Fact]
    public async Task RedCrisis_EmitsScriptedResources_AndNeverCallsTheLlm()
    {
        // Arrange — no classifier/stream scripted: a Red short-circuit must never reach them.
        StubCoachingLlm.Reset();
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(
            client, token, "Honestly lately I just want to end it all.", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var safety = frames.Should().ContainSingle(f => f.Event == "safety").Subject;
        var payload = Payload<SafetyPayload>(safety);
        payload.Tier.Should().Be(SafetyTier.Red);
        payload.Content.Should().Contain("988 Suicide & Crisis Lifeline");
        payload.Content.Should().Contain("Crisis Text Line: text 741741");
        frames.Should().Contain(f => f.Event == "done");
        frames.Should().NotContain(f => f.Event == "token", because: "the crisis short-circuit streams no LLM answer");

        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "Red never reaches the classifier");
        StubCoachingLlm.StreamCallCount.Should().Be(0, because: "Red never reaches the answer stream");

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Contain("988");
    }

    [Fact]
    public async Task AmberInjury_SurfacesReferralTurn_AlongsideTheStreamedAnswer()
    {
        // Arrange
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => YieldTokensAsync("Let's ease off."));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(
            client, token, "I had sharp pain in my knee on today's run.", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var safety = frames.Should().ContainSingle(f => f.Event == "safety").Subject;
        var payload = Payload<SafetyPayload>(safety);
        payload.Tier.Should().Be(SafetyTier.Amber);
        payload.Category.Should().Be(ReferralCategory.Injury);
        payload.Content.Should().Be(AmberReferralContent.InjuryReferral);
        frames.Should().Contain(f => f.Event == "token", because: "the coach still answers an Amber Q&A");

        // The Amber path persists TWO coach turns off the same client id (the referral on
        // DeriveSafetyTurnId, the answer on DeriveCoachTurnId); the done frame must carry the
        // ANSWER id so the client reconciles the live bubble onto the answer, not the referral.
        var amberDone = frames.Should().ContainSingle(f => f.Event == "done").Subject;
        Payload<DonePayload>(amberDone).TurnId.Should().Be(
            ConversationTurnId.DeriveCoachTurnId(clientMessageId),
            because: "the done frame carries the answer coach turn id, not the safety referral turn id");

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().HaveCount(3, because: "user turn + scripted referral coach turn + answer coach turn");
        view.Turns.Should().Contain(t => t.Content == AmberReferralContent.InjuryReferral);
        view.Turns.Should().Contain(t => t.Content == "Let's ease off.");
    }

    [Fact]
    public async Task WorkoutLog_ReturnsConfirmationCard_AndCommitsNothing()
    {
        // Arrange
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => WorkoutLog(LocalToday()));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(
            client, token, "Did my easy 5k this morning, felt good.", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var card = frames.Should().ContainSingle(f => f.Event == "card").Subject;
        var payload = Payload<CardPayload>(card);
        payload.Draft.DistanceValue.Should().Be(5);
        payload.Draft.DistanceUnit.Should().Be(RunnerDistanceUnit.Kilometers);
        frames.Should().NotContain(f => f.Event == "token", because: "a workout-log card streams no answer");

        StubCoachingLlm.StreamCallCount.Should().Be(0, because: "the card path never streams");
        (await CountWorkoutLogsAsync(userId)).Should().Be(0, because: "the card commits nothing until Confirm (PR5)");

        // The card branch commits NOTHING beyond the durable user turn: no coach turn, no
        // log row — a regression that appended a coach turn for the card would be caught here.
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(because: "only the durable-first user turn persists for a card")
            .Which.Participant.Should().Be(ConversationParticipant.User);
    }

    [Fact]
    public async Task Ambiguous_StreamsAClarification_AndDoesNotRouteToALog()
    {
        // Arrange
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Ambiguous());
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(client, token, "knee thing, dunno", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var tokens = frames.Where(f => f.Event == "token").ToArray();
        tokens.Should().NotBeEmpty();
        string.Concat(tokens.Select(f => Payload<TokenPayload>(f).Delta))
            .Should().Contain("question or a workout", because: "the coach asks rather than guessing");
        frames.Should().Contain(f => f.Event == "done");
        frames.Should().NotContain(f => f.Event == "card", because: "an ambiguous message is never silently logged");
        StubCoachingLlm.StreamCallCount.Should().Be(0, because: "the clarification is scripted, not LLM-streamed");
        (await CountWorkoutLogsAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task MidStreamFailure_EmitsErrorFrame_AndPersistsAnErroredMarker_NotAPartial()
    {
        // Arrange — the stream yields one token then throws a transient failure.
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => FailingStreamAsync("partial answer"));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(client, token, "What's my week look like?", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var error = frames.Should().ContainSingle(f => f.Event == "error").Subject;
        var errorPayload = Payload<ErrorPayload>(error);
        errorPayload.Retryable.Should().BeTrue(because: "a transient failure is retryable");
        errorPayload.RetryAfterSeconds.Should().Be(
            5,
            because: "a mid-stream transient carries no Retry-After hint, so the configured DefaultMidStreamRetryAfterSeconds is surfaced");
        frames.Should().NotContain(f => f.Event == "done", because: "a failed turn has no terminal done");

        var view = await LoadConversationViewAsync(userId);
        var coach = view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach).Subject;
        coach.IsErrored.Should().BeTrue(because: "the truncated reply is marked errored, never stored as complete");
        coach.Content.Should().BeEmpty(because: "an errored marker carries no partial text");
        view.Turns.Should().Contain(t => t.Participant == ConversationParticipant.User && t.TurnId == clientMessageId);
    }

    [Fact]
    public async Task ClassifierFailure_EmitsErrorFrame_AndNeverGuessesIntent()
    {
        // Arrange — the classifier call throws; no stream is scripted (it must not be reached).
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(
            () => throw new PermanentCoachingLlmException("classifier exploded", null));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(client, token, "How's my training going?", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        frames.Should().ContainSingle(f => f.Event == "error");
        frames.Should().NotContain(f => f.Event == "token", because: "intent is never guessed into an answer");
        frames.Should().NotContain(f => f.Event == "card", because: "intent is never guessed into a log");
        StubCoachingLlm.StreamCallCount.Should().Be(0);

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle()
            .Which.Participant.Should().Be(
                ConversationParticipant.User,
                because: "nothing beyond the durable user turn is persisted on a classifier failure");
    }

    [Fact]
    public async Task ClientAbort_PersistsNoCoachTurn_AndDoesNotServerError()
    {
        // Arrange — the stream yields one token then blocks until the request is aborted.
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior((ct) => AbortableStreamAsync("first", ct));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();
        using var abort = new CancellationTokenSource();

        // Act — read the first token, then abort the request mid-stream.
        using var request = BuildPost(token, "Tell me about my plan.", clientMessageId);
        var aborted = false;
        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, abort.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(abort.Token);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(abort.Token)) is not null)
            {
                if (line.StartsWith("data:", StringComparison.Ordinal) && line.Contains("first", StringComparison.Ordinal))
                {
                    await abort.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            aborted = true;
        }

        // Assert — the user turn is durable (written before the stream), but no coach turn is
        // ever persisted on abort (not even an errored marker). The server logs no 5xx.
        aborted.Should().BeTrue(because: "cancelling the in-flight read surfaces a client cancellation");
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().Contain(t => t.Participant == ConversationParticipant.User && t.TurnId == clientMessageId);
        view.Turns.Should().NotContain(
            t => t.Participant == ConversationParticipant.Coach,
            because: "an aborted stream persists nothing for the coach reply");
    }

    [Fact]
    public async Task MissingAntiforgeryToken_IsRejected()
    {
        // Arrange — a logged-in cookie client, but the request omits the X-XSRF-TOKEN header.
        StubCoachingLlm.Reset();
        var (client, _, _) = await RegisterLoginAndPrimeAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, MessagesPath)
        {
            Content = JsonContent.Create(new ConversationMessageRequestDto("hi", Guid.NewGuid())),
        };

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest, because: "a mutation without the antiforgery token is rejected before streaming");
        response.Content.Headers.ContentType?.MediaType.Should().Be(
            "application/problem+json", because: "the antiforgery rejection returns structured ProblemDetails, not a bare 400");
    }

    [Fact]
    public async Task BlankMessage_IsRejectedWith400_BeforeAnyLlmCall()
    {
        // Arrange — a valid antiforgery token so the request reaches the boundary validation.
        StubCoachingLlm.Reset();
        var (client, _, token) = await RegisterLoginAndPrimeAsync();

        // Act
        using var response = await PostMessageAsync(client, token, "   ", Guid.NewGuid());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "a blank message never reaches the classifier");
    }

    [Fact]
    public async Task EmptyClientMessageId_IsRejectedWith400()
    {
        // Arrange — an empty client id would collide every empty-id post on the derived keys.
        StubCoachingLlm.Reset();
        var (client, _, token) = await RegisterLoginAndPrimeAsync();

        // Act
        using var response = await PostMessageAsync(client, token, "How's my plan?", Guid.Empty);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        StubCoachingLlm.StructuredCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RedEmergency_EmitsStopAndReferContent_AndNeverCallsTheLlm()
    {
        // Arrange — a cardiac signal classifies Red/EmergencyReferral, a distinct branch from
        // the crisis branch: it must route to the stop-and-call-911 copy, never the 988 lines.
        StubCoachingLlm.Reset();
        var (client, _, token) = await RegisterLoginAndPrimeAsync();
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(
            client, token, "I had chest pain partway through the run and felt dizzy.", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var safety = frames.Should().ContainSingle(f => f.Event == "safety").Subject;
        var payload = Payload<SafetyPayload>(safety);
        payload.Tier.Should().Be(SafetyTier.Red);
        payload.Category.Should().Be(ReferralCategory.EmergencyReferral);
        payload.Content.Should().Contain("911", because: "an urgent medical signal routes to emergency care, not the crisis lines");
        payload.Content.Should().NotContain("988", because: "the mental-health crisis copy must not be sent for a cardiac symptom");
        StubCoachingLlm.StructuredCallCount.Should().Be(0);
        StubCoachingLlm.StreamCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AmberRedS_SurfacesEnergyBalanceReferral_AlongsideTheStreamedAnswer()
    {
        // Arrange — a RED-S signal classifies Amber/RedS, the other Amber branch: it must route
        // to the sports-dietitian / energy-balance copy, not the injury physiotherapy referral.
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => YieldTokensAsync("Fuel comes first."));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(
            client, token, "I'm not eating enough but I keep training hard.", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var safety = frames.Should().ContainSingle(f => f.Event == "safety").Subject;
        var payload = Payload<SafetyPayload>(safety);
        payload.Tier.Should().Be(SafetyTier.Amber);
        payload.Category.Should().Be(ReferralCategory.RedS);
        payload.Content.Should().Be(AmberReferralContent.RedSReferral);
        payload.Content.Should().Contain("dietitian");
        frames.Should().Contain(f => f.Event == "token", because: "the coach still answers on an Amber RED-S message");
    }

    [Fact]
    public async Task GreenQuestion_RePostedWithSameClientMessageId_IsIdempotent()
    {
        // Arrange
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => YieldTokensAsync("Same answer."));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act — submit the identical message twice (a client retry / double-post).
        using (var first = await PostMessageAsync(client, token, "How's it going?", clientMessageId))
        {
            await ReadFramesAsync(first);
        }

        using (var second = await PostMessageAsync(client, token, "How's it going?", clientMessageId))
        {
            await ReadFramesAsync(second);
        }

        // Assert — the durable-first user turn and the server-derived coach turn are both
        // idempotent, so the append-only stream holds exactly one of each, not duplicates.
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().HaveCount(2, because: "a re-POST with the same client id double-appends nothing");
    }

    [Fact]
    public async Task AmberReferral_RePostedWithSameClientMessageId_DoesNotDoubleAppend()
    {
        // Arrange — exercises PostScriptedSafetyTurnHandler's idempotency on the derived
        // safety turn id (the scripted referral must never double-append on a retry).
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => YieldTokensAsync("Ease back."));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();
        const string message = "I had sharp pain in my knee on today's run.";

        // Act
        using (var first = await PostMessageAsync(client, token, message, clientMessageId))
        {
            await ReadFramesAsync(first);
        }

        using (var second = await PostMessageAsync(client, token, message, clientMessageId))
        {
            await ReadFramesAsync(second);
        }

        // Assert
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().HaveCount(
            3, because: "the user turn, the scripted referral turn, and the answer turn are each idempotent on a retry");
        view.Turns.Count(t => t.Content == AmberReferralContent.InjuryReferral).Should().Be(
            1, because: "the scripted Amber referral is appended exactly once across both posts");
    }

    [Theory]
    [InlineData(IncompleteReason.MaxTokens, true)]
    [InlineData(IncompleteReason.ContextWindowExceeded, false)]
    public async Task MidStreamIncompleteFinish_EmitsErrorFrame_WithRetryabilityFromTheReason(
        IncompleteReason reason, bool expectedRetryable)
    {
        // Arrange — a free-text-incomplete finish (max_tokens / context overflow): retryability
        // is instance-specific (a shorter retry can fit a max_tokens truncation, but not an
        // oversized context), distinct from the hardcoded Transient/Permanent retryability.
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => IncompleteStreamAsync("partial", reason));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(client, token, "Give me my week.", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var error = frames.Should().ContainSingle(f => f.Event == "error").Subject;
        Payload<ErrorPayload>(error).Retryable.Should().Be(
            expectedRetryable, because: "an incomplete finish derives retryability from its reason, not a fixed value");
        frames.Should().NotContain(f => f.Event == "done");

        var view = await LoadConversationViewAsync(userId);
        var coach = view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach).Subject;
        coach.IsErrored.Should().BeTrue(because: "a truncated reply is an errored marker, never a complete turn");
        coach.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task MidStreamPermanentFailure_EmitsNonRetryableError_AndErroredMarker()
    {
        // Arrange
        StubCoachingLlm.Reset();
        StubCoachingLlm.UseStructuredBehavior(() => Question());
        StubCoachingLlm.UseStreamBehavior(_ => PermanentFailingStreamAsync("partial"));
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId);
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostMessageAsync(client, token, "Why is my pace off?", clientMessageId);
        var frames = await ReadFramesAsync(response);

        // Assert
        var error = frames.Should().ContainSingle(f => f.Event == "error").Subject;
        var payload = Payload<ErrorPayload>(error);
        payload.Retryable.Should().BeFalse(because: "a permanent mid-stream failure is not worth retrying");
        payload.RetryAfterSeconds.Should().BeNull();
        frames.Should().NotContain(f => f.Event == "done");

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.IsErrored.Should().BeTrue();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        StubCoachingLlm.Reset();
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static MessageIntentOutput Question() =>
        new() { Intent = MessageIntent.Question, WorkoutLog = null };

    private static MessageIntentOutput Ambiguous() =>
        new() { Intent = MessageIntent.Ambiguous, WorkoutLog = null };

    private static MessageIntentOutput WorkoutLog(DateOnly today) =>
        new()
        {
            Intent = MessageIntent.WorkoutLog,
            WorkoutLog = new StructuredLogDraft
            {
                OccurredOn = today,
                DistanceValue = 5,
                DistanceUnit = RunnerDistanceUnit.Kilometers,
                DurationHours = 0,
                DurationMinutes = 25,
                DurationSeconds = 0,
                CompletionStatus = CompletionStatus.Complete,
                Notes = "felt good",
            },
        };

    private static async IAsyncEnumerable<string> YieldTokensAsync(params string[] tokens)
    {
        foreach (var t in tokens)
        {
            await Task.Yield();
            yield return t;
        }
    }

    private static async IAsyncEnumerable<string> FailingStreamAsync(string firstToken)
    {
        yield return firstToken;
        await Task.Yield();
        throw new TransientCoachingLlmException("mid-stream boom", retryAfterSeconds: null, innerException: null);
    }

    private static async IAsyncEnumerable<string> PermanentFailingStreamAsync(string firstToken)
    {
        yield return firstToken;
        await Task.Yield();
        throw new PermanentCoachingLlmException("mid-stream permanent", innerException: null);
    }

    private static async IAsyncEnumerable<string> IncompleteStreamAsync(string firstToken, IncompleteReason reason)
    {
        // A free-text-incomplete finish: the adapter yields its partial deltas, then throws
        // IncompleteCoachingLlmException at the END of a clean enumeration (max_tokens etc.).
        yield return firstToken;
        await Task.Yield();
        throw new IncompleteCoachingLlmException("truncated", reason);
    }

    private static async IAsyncEnumerable<string> AbortableStreamAsync(
        string firstToken, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return firstToken;
        await Task.Delay(Timeout.Infinite, ct);
        yield return "unreachable";
    }

    private static T Payload<T>(SseFrame frame) =>
        JsonSerializer.Deserialize<T>(frame.Data, WebOptions)
        ?? throw new InvalidOperationException($"frame '{frame.Event}' had no JSON payload");

    private static async Task<List<SseFrame>> ReadFramesAsync(HttpResponseMessage response)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var frames = new List<SseFrame>();
        string? currentEvent = null;
        var data = new StringBuilder();

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (line.Length == 0)
            {
                if (currentEvent is not null)
                {
                    frames.Add(new SseFrame(currentEvent, data.ToString()));
                }

                currentEvent = null;
                data.Clear();
                continue;
            }

            // An SSE comment line (heartbeat) starts with ':' and is not a frame.
            if (line.StartsWith(':'))
            {
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                currentEvent = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                data.Append(line["data:".Length..].Trim());
            }
        }

        return frames;
    }

    private static HttpRequestMessage BuildPost(string antiforgeryToken, string message, Guid clientMessageId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MessagesPath);
        request.Headers.Add(AuthCookieNames.AntiforgeryHeader, antiforgeryToken);
        request.Content = JsonContent.Create(new ConversationMessageRequestDto(message, clientMessageId));
        return request;
    }

    private static async Task<HttpResponseMessage> PostMessageAsync(
        HttpClient client, string antiforgeryToken, string message, Guid clientMessageId)
    {
        var request = BuildPost(antiforgeryToken, message, clientMessageId);
        return await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
    }

    private static DateOnly LocalToday() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static (HttpClient Client, System.Net.CookieContainer Container) CreateCookieClient(RunCoachAppFactory factory)
    {
        var container = new System.Net.CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
    }

    private static async Task<string> PrimeAntiforgeryAsync(HttpClient client, System.Net.CookieContainer container)
    {
        using var response = await client.GetAsync("/api/v1/auth/xsrf", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requestCookie = GetCookie(container, AuthCookieNames.AntiforgeryRequest);
        requestCookie.Should().NotBeNull("/xsrf must issue the SPA-readable request token cookie");
        return requestCookie!.Value;
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client, System.Net.CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        request.Headers.Add(AuthCookieNames.AntiforgeryHeader, token);
        request.Content = JsonContent.Create(new RegisterRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: "register helper must succeed");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        return body!.UserId;
    }

    private static async Task LoginAsync(
        HttpClient client, System.Net.CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        request.Headers.Add(AuthCookieNames.AntiforgeryHeader, token);
        request.Content = JsonContent.Create(new LoginRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: "login helper must succeed");
    }

    private static System.Net.Cookie? GetCookie(System.Net.CookieContainer container, string name)
    {
        foreach (System.Net.Cookie c in container.GetCookies(BaseUri))
        {
            if (string.Equals(c.Name, name, StringComparison.Ordinal))
            {
                return c;
            }
        }

        return null;
    }

    private async Task<(HttpClient Client, Guid UserId, string Token)> RegisterLoginAndPrimeAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = $"sse-{Guid.NewGuid():N}@example.test";
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);
        return (client, userId, token);
    }

    private async Task<ConversationView?> LoadConversationViewAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        return await session.LoadAsync<ConversationView>(userId, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountWorkoutLogsAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        return await EntityFrameworkQueryableExtensions.CountAsync(
            db.WorkoutLogs.Where(w => w.UserId == userId), TestContext.Current.CancellationToken);
    }

    private async Task SeedActivePlanAsync(Guid userId)
    {
        var planId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            await using var session = store.LightweightSession(userId.ToString());
            var sequence = StubPlanGenerationService.BuildCanonicalSequence(
                planId, userId, goal: "Half Marathon", PlanGeneratedAt, previousPlanId: null);
            session.Events.StartStream<PlanProjectionDto>(planId, [.. sequence.ToEvents()]);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
            var profile = await EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
                db.RunnerOnboardingProfiles.Where(p => p.UserId == userId), TestContext.Current.CancellationToken);
            if (profile is null)
            {
                var now = DateTimeOffset.UtcNow;
                db.RunnerOnboardingProfiles.Add(new RunnerOnboardingProfile
                {
                    UserId = userId,
                    TenantId = userId.ToString(),
                    CurrentPlanId = planId,
                    CreatedOn = now,
                    ModifiedOn = now,
                });
            }
            else
            {
                profile.CurrentPlanId = planId;
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed record SseFrame(string Event, string Data);

    private sealed record TokenPayload(string Delta);

    private sealed record SafetyPayload(string Content, SafetyTier Tier, ReferralCategory Category);

    private sealed record CardPayload(StructuredLogDraft Draft, CandidatePrescriptionDto? Prescription);

    private sealed record ErrorPayload(string Message, bool Retryable, int? RetryAfterSeconds);

    private sealed record DonePayload(Guid TurnId);
}
