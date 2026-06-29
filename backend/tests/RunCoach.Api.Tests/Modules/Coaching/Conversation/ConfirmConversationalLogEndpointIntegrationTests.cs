using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;
using RestructurePlanOutput = RunCoach.Api.Modules.Coaching.Adaptation.RestructurePlan;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Integration coverage for the confirm-then-commit endpoint
/// <c>POST /api/v1/conversation/logs/confirm</c> (Slice 4B PR5, DEC-085 D4). Drives the real
/// <see cref="RunCoachAppFactory"/> SUT end-to-end: the CookieOrBearer gate, antiforgery, the
/// unchanged Slice 2b create path (EF-native idempotency on the DERIVED key), the identical
/// post-create adaptation seam (the real <c>EvaluateAdaptationHandler</c> with the
/// <see cref="StubCoachingLlm"/>), and the coach acknowledgment turn persisted on the user-scoped
/// <see cref="ConversationView"/> afterward. Asserts each acceptance scenario from
/// <c>conversational-logging.feature</c>. The deterministic absorb/nudge/restructure/error and
/// safety mechanics themselves are owned by <c>AdaptationOrchestrationIntegrationTests</c>; the
/// confirm path reuses the identical dispatcher, so these tests cover the confirm-specific wiring.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ConfirmConversationalLogEndpointIntegrationTests : DbBackedIntegrationTestBase
{
    private const string ConfirmPath = "/api/v1/conversation/logs/confirm";
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const int RevisedWeek2TargetKm = 40;

    // 2026-06-07 is a Sunday — the PlanCalendar week-1/day-0 anchor for the seeded plan (mirrors
    // AdaptationOrchestrationIntegrationTests). The Tuesday tempo is the under-perform / L2 target.
    private static readonly DateOnly Week1Tuesday = new(2026, 6, 9);
    private static readonly DateOnly OffPlanDay = new(2026, 7, 15);
    private static readonly DateTimeOffset GeneratedAt = new(2026, 6, 7, 8, 0, 0, TimeSpan.Zero);
    private static readonly Uri BaseUri = new("https://localhost");

    public ConfirmConversationalLogEndpointIntegrationTests(RunCoachAppFactory factory)
        : base(factory)
    {
        StubCoachingLlm.Reset();
    }

    [Fact]
    public async Task Confirm_OffPlanLog_CommitsOneLog_AbsorbsAdaptation_AndPersistsTheLlmAck()
    {
        // Arrange — an off-plan run absorbs (no plan change, no structured LLM call); the only LLM
        // call is the free-text ack.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId, Guid.NewGuid(), ct);
        StubCoachingLlm.UseGenerateBehavior(() => "Logged your run. On track — nothing to change.");
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostConfirmAsync(client, token, EasyDraft(OffPlanDay), clientMessageId, ct);
        var body = await response.Content.ReadFromJsonAsync<ConfirmConversationalLogResponseDto>(cancellationToken: ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Adapted);
        body.Adaptation.AdaptationKind.Should().Be(AdaptationKind.Absorb, because: "an off-plan run is absorbed");
        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "absorb runs no structured LLM call");
        StubCoachingLlm.GenerateCallCount.Should().Be(1, because: "the ack is the only LLM call on the absorb path");

        (await CountWorkoutLogsAsync(userId)).Should().Be(1, because: "Confirm commits exactly one log");
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Be("Logged your run. On track — nothing to change.");
    }

    [Fact]
    public async Task Confirm_DoubleConfirm_SameClientMessageId_CommitsExactlyOneLog_AndOneAck()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        await SeedActivePlanAsync(userId, Guid.NewGuid(), ct);
        StubCoachingLlm.UseGenerateBehavior(() => "Logged.");
        var clientMessageId = Guid.NewGuid();
        var draft = EasyDraft(OffPlanDay);

        // Act — confirm the identical card twice (a client retry).
        using (var first = await PostConfirmAsync(client, token, draft, clientMessageId, ct))
        {
            first.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using (var second = await PostConfirmAsync(client, token, draft, clientMessageId, ct))
        {
            second.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Assert — EF-native idempotency on the DERIVED key commits one log; the coach turn is
        // idempotent on the derived coach turn id, so the timeline holds exactly one ack.
        (await CountWorkoutLogsAsync(userId)).Should().Be(
            1, because: "a double-confirm replays the same derived EF key (DEC-077)");
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Count(t => t.Participant == ConversationParticipant.Coach).Should().Be(
            1, because: "the ack coach turn is idempotent on the server-derived turn id");
    }

    [Fact]
    public async Task Confirm_RestructureOutcome_CommitsProactiveTurn_ThenPersistsTheLlmAck()
    {
        // Arrange — an under-performing key tempo with the signal primed one step shy of L2: the
        // single confirm crosses the threshold and restructures. The structured LLM call returns
        // the canned restructure; the free-text call returns the ack.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(CannedRestructureOutput);
        StubCoachingLlm.UseGenerateBehavior(() => "Logged your tempo. Reworked the week — check your plan.");
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostConfirmAsync(
            client, token, UnderPerformingTempoDraft(), clientMessageId, ct);
        var body = await response.Content.ReadFromJsonAsync<ConfirmConversationalLogResponseDto>(cancellationToken: ct);

        // Assert — the restructure committed a proactive turn to the plan stream, and exactly one
        // ack persisted on the user stream AFTER it.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Adapted);
        body.Adaptation.AdaptationKind.Should().Be(AdaptationKind.Restructure);
        StubCoachingLlm.StructuredCallCount.Should().Be(1, because: "the L2 restructure makes exactly one structured call");

        (await FetchPlanEventsAsync(userId, planId, ct)).OfType<PlanAdaptedFromLog>().Should().ContainSingle(
            because: "the restructure committed a proactive adaptation turn to the plan stream");
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Be("Logged your tempo. Reworked the week — check your plan.");
    }

    [Fact]
    public async Task Confirm_NudgeOutcome_CommitsInlineProactiveTurn_ThenPersistsTheLlmAck()
    {
        // Arrange — a skipped key tempo classifies a deterministic L1 nudge (no signal priming, no
        // structured LLM call); the only LLM call is the free-text ack.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        StubCoachingLlm.UseGenerateBehavior(() => "Logged the miss. Adjusted the next day or two — check your plan.");
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostConfirmAsync(client, token, SkippedTempoDraft(), clientMessageId, ct);
        var body = await response.Content.ReadFromJsonAsync<ConfirmConversationalLogResponseDto>(cancellationToken: ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Adapted);
        body.Adaptation.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        StubCoachingLlm.StructuredCallCount.Should().Be(0, because: "a deterministic L1 nudge runs no structured LLM call");
        StubCoachingLlm.GenerateCallCount.Should().Be(1, because: "the ack is the only LLM call on the nudge path");

        (await FetchPlanEventsAsync(userId, planId, ct)).OfType<PlanAdaptedFromLog>().Should().ContainSingle(
            because: "the nudge committed an inline proactive turn to the plan stream");
        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Be("Logged the miss. Adjusted the next day or two — check your plan.");
    }

    [Fact]
    public async Task Confirm_InjuryNoteOnDraft_DrivesTheSafetyGate_RaisesAmberReferral_BeforeTheAck()
    {
        // Arrange — the confirmed draft's note carries an injury signal. The note must flow through
        // the confirm wiring into the adaptation handler's SafetyGate exactly as on the form-logged
        // path (Gherkin Scenario 8), raising an Amber injury referral on the plan stream; the L2
        // restructure echoes the gate's Amber tier (echo integrity). The ack persists afterward.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(() => CannedRestructureOutput() with { SafetyTier = SafetyTier.Amber });
        StubCoachingLlm.UseGenerateBehavior(() => "Logged your tempo. Eased the week — and get that knee looked at.");
        var clientMessageId = Guid.NewGuid();

        // Act
        var draft = UnderPerformingTempoDraft(notes: "Sharp pain in my right knee from about halfway.");
        using var response = await PostConfirmAsync(client, token, draft, clientMessageId, ct);

        // Assert — the SafetyGate fired on the forwarded note (proving notes flow through confirm),
        // raising the Amber injury referral, and the ack persisted on the user stream afterward.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var signal = (await FetchPlanEventsAsync(userId, planId, ct)).OfType<SafetySignalRaised>().Should().ContainSingle(
            because: "an injury note on the confirmed draft drives the existing SafetyGate path unchanged").Subject;
        signal.SafetyTier.Should().Be(SafetyTier.Amber);
        signal.ReferralCategory.Should().Be(ReferralCategory.Injury);

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Be("Logged your tempo. Eased the week — and get that knee looked at.");
    }

    [Fact]
    public async Task Confirm_AdaptationFailsTerminally_StillSavesTheLog_ReturnsRetryableError_AndScriptsTheAck()
    {
        // Arrange — the same L2-crossing log, but the restructure LLM call fails terminally. The
        // save must survive; the response surfaces a retryable coach-review failure; the ack is the
        // scripted "saved; retrying" copy with NO second LLM call.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, ct);
        await SeedSignalStateAsync(userId, planId, rollingScore: 2.0, ct);
        StubCoachingLlm.UseStructuredBehavior(() => throw new TransientCoachingLlmException(
            "The coaching service is busy right now.", retryAfterSeconds: 30, innerException: null));
        var clientMessageId = Guid.NewGuid();

        // Act
        using var response = await PostConfirmAsync(
            client, token, UnderPerformingTempoDraft(), clientMessageId, ct);
        var body = await response.Content.ReadFromJsonAsync<ConfirmConversationalLogResponseDto>(cancellationToken: ct);

        // Assert — the log saved; the envelope reports a retryable error; the ack is scripted.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Adaptation.Kind.Should().Be(AdaptationResponseKind.Error);
        body.Adaptation.Retryable.Should().BeTrue();
        (await CountWorkoutLogsAsync(userId)).Should().Be(1, because: "an adaptation failure never fails the save");
        StubCoachingLlm.GenerateCallCount.Should().Be(0, because: "the review failed — the ack must not call the LLM again");

        var view = await LoadConversationViewAsync(userId);
        view!.Turns.Should().ContainSingle(t => t.Participant == ConversationParticipant.Coach)
            .Which.Content.Should().Be(ConversationAckScripts.SavedReviewRetrying);
    }

    [Fact]
    public async Task Confirm_MissingAntiforgeryToken_IsRejected()
    {
        // Arrange — a logged-in cookie client, but the request omits the X-XSRF-TOKEN header.
        var (client, _, _) = await RegisterLoginAndPrimeAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ConfirmPath)
        {
            Content = JsonContent.Create(new ConfirmConversationalLogRequestDto(EasyDraft(OffPlanDay), Guid.NewGuid())),
        };

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest, because: "a mutation without the antiforgery token is rejected");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Confirm_EmptyClientMessageId_IsRejectedWith400()
    {
        // Arrange — an empty client id would collide every confirm onto one derived (EF key, coach
        // id) pair.
        var ct = TestContext.Current.CancellationToken;
        var (client, _, token) = await RegisterLoginAndPrimeAsync();

        // Act
        using var response = await PostConfirmAsync(client, token, EasyDraft(OffPlanDay), Guid.Empty, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        StubCoachingLlm.GenerateCallCount.Should().Be(0, because: "the boundary rejects before any commit or ack");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        StubCoachingLlm.Reset();
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static StructuredLogDraft EasyDraft(DateOnly occurredOn) => new()
    {
        OccurredOn = occurredOn,
        DistanceValue = 8,
        DistanceUnit = RunnerDistanceUnit.Kilometers,
        DurationHours = 0,
        DurationMinutes = 48,
        DurationSeconds = 0,
        CompletionStatus = CompletionStatus.Complete,
        Notes = null,
    };

    // 5 km in 25 min against the 10 km Tuesday tempo: pace in band but distance 50% short — the
    // under-performing log that steps the seeded 2.0 rolling score across the L2 enter threshold.
    private static StructuredLogDraft UnderPerformingTempoDraft(string? notes = null) => new()
    {
        OccurredOn = Week1Tuesday,
        DistanceValue = 5,
        DistanceUnit = RunnerDistanceUnit.Kilometers,
        DurationHours = 0,
        DurationMinutes = 25,
        DurationSeconds = 0,
        CompletionStatus = CompletionStatus.Complete,
        Notes = notes,
    };

    // A skipped Tuesday tempo — a missed key workout with a valid forward swap, classifying a
    // deterministic L1 nudge (no signal priming, no structured LLM call).
    private static StructuredLogDraft SkippedTempoDraft() => new()
    {
        OccurredOn = Week1Tuesday,
        DistanceValue = 0,
        DistanceUnit = RunnerDistanceUnit.Kilometers,
        DurationHours = 0,
        DurationMinutes = 0,
        DurationSeconds = 0,
        CompletionStatus = CompletionStatus.Skipped,
        Notes = null,
    };

    private static PlanAdaptationOutput CannedRestructureOutput() =>
        new()
        {
            AdaptationKind = AdaptationKind.Restructure,
            SafetyTier = SafetyTier.Green,
            NudgePatch = null,
            RestructurePlan = new RestructurePlanOutput
            {
                RevisedWeeklyTargets =
                [
                    new WeeklyTargetEdit { WeekNumber = 2, WeeklyTargetKm = RevisedWeek2TargetKm },
                ],
                RevisedCurrentWeekWorkouts = [],
                ForwardPath = "Hold the reduced volume for a week, then ramp back about ten percent per week.",
            },
            NetLoadDelta = -5,
            Rationale = "Recent sessions ran well short of plan, so I trimmed next week's volume to consolidate.",
            ReferralCategory = null,
        };

    private static MicroWorkoutListOutput BuildAdaptationMicro() =>
        new()
        {
            Workouts =
            [
                BuildWorkout(0, WorkoutType.Easy, "Sunday Easy Run", km: 8, minutes: 48, fast: 330, easy: 390, effort: 3),
                BuildWorkout(2, WorkoutType.Tempo, "Tuesday Threshold Tempo", km: 10, minutes: 50, fast: 280, easy: 330, effort: 7),
                BuildWorkout(4, WorkoutType.Easy, "Thursday Easy Run", km: 6, minutes: 39, fast: 330, easy: 420, effort: 3),
                BuildWorkout(5, WorkoutType.Easy, "Friday Easy Run", km: 5, minutes: 33, fast: 330, easy: 420, effort: 3),
            ],
        };

    private static WorkoutOutput BuildWorkout(
        int dayOfWeek, WorkoutType type, string title, int km, int minutes, int fast, int easy, int effort) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            WorkoutType = type,
            Title = title,
            TargetDistanceKm = km,
            TargetDurationMinutes = minutes,
            TargetPaceEasySecPerKm = easy,
            TargetPaceFastSecPerKm = fast,
            Segments = [],
            WarmupNotes = "10 min easy.",
            CooldownNotes = "10 min easy.",
            CoachingNotes = "Run to feel.",
            PerceivedEffort = effort,
        };

    private static async Task<HttpResponseMessage> PostConfirmAsync(
        HttpClient client, string antiforgeryToken, StructuredLogDraft draft, Guid clientMessageId, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ConfirmPath);
        request.Headers.Add(AuthCookieNames.AntiforgeryHeader, antiforgeryToken);
        request.Content = JsonContent.Create(new ConfirmConversationalLogRequestDto(draft, clientMessageId));
        return await client.SendAsync(request, ct);
    }

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
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
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
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
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

    private async Task SeedActivePlanAsync(Guid userId, Guid planId, CancellationToken ct)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.RunnerOnboardingProfiles.Add(new RunnerOnboardingProfile
            {
                UserId = userId,
                TenantId = userId.ToString(),
                OnboardingCompletedAt = now,
                CurrentPlanId = planId,
                CreatedOn = now,
                ModifiedOn = now,
            });
            await db.SaveChangesAsync(ct);
        }

        var sequence = StubPlanGenerationService.BuildCanonicalSequence(
                planId, userId, goal: "Conversational logging plan", GeneratedAt, previousPlanId: null)
            with
        { Micro = new FirstMicroCycleCreated(BuildAdaptationMicro()) };

        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.StartStream<PlanProjectionDto>(planId, [.. sequence.ToEvents()]);
        await session.SaveChangesAsync(ct);
    }

    private async Task SeedSignalStateAsync(Guid userId, Guid planId, double rollingScore, CancellationToken ct)
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Store(new AdaptationSignalStateDocument(
            planId,
            PlanState.MinorDeviation,
            rollingScore,
            ConsecutiveMissedDays: 0,
            LastAdaptationOn: null));
        await session.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<object>> FetchPlanEventsAsync(Guid userId, Guid planId, CancellationToken ct)
    {
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var events = await session.Events.FetchStreamAsync(planId, token: ct);
        return [.. events.Select(e => e.Data)];
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

    private async Task<(HttpClient Client, Guid UserId, string Token)> RegisterLoginAndPrimeAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = $"confirm-{Guid.NewGuid():N}@example.test";
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);
        return (client, userId, token);
    }
}
