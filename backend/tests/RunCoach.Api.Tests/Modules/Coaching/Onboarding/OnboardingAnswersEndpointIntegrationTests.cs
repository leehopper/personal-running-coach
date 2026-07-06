using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Integration coverage for the deterministic form-answer origination endpoint
/// POST /api/v1/onboarding/answers (DU-1). Drives the LIVE HTTP + Wolverine pipeline through the
/// real <see cref="SubmitStructuredAnswersHandler"/>: the endpoint appends one whole-record
/// <c>AnswerCaptured</c> per submitted topic, evaluates the deterministic completion gate, and runs
/// the existing inline plan-generation terminal branch — with no onboarding-time LLM call. The
/// assembly-swapped <see cref="StubPlanGenerationService"/> supplies the deterministic plan events,
/// so both the Marten <c>OnboardingView</c> and the EF <see cref="RunnerOnboardingProfile"/>
/// projections materialize end-to-end (DEC-047 / DEC-060).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class OnboardingAnswersEndpointIntegrationTests : DbBackedIntegrationTestBase
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string AnswersPath = "/api/v1/onboarding/answers";
    private const string StatePath = "/api/v1/onboarding/state";

    private static readonly Uri BaseUri = new("https://localhost");

    public OnboardingAnswersEndpointIntegrationTests(RunCoachAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task SubmitAnswers_AllSixTopics_RaceTraining_GeneratesPlan_MaterializesBothProjections()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var request = RaceTrainingRequest(Guid.NewGuid());

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        var response = await PostAnswersAsync(client, token, request, ct);

        // Assert — HTTP contract: completed onboarding with a linked plan.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        state.Should().NotBeNull();
        state!.IsComplete.Should().BeTrue(because: "all required topics were submitted, satisfying the deterministic gate");
        state.CurrentPlanId.Should().NotBeNull().And.NotBe(Guid.Empty);

        // Assert — OnboardingView (Marten projection) slots equal the submitted records.
        state.PrimaryGoal.Should().Be(new PrimaryGoalAnswer { Goal = PrimaryGoal.RaceTraining, Description = "Sub-4 marathon" });
        state.TargetEvent!.EventName.Should().Be("Berlin Marathon");
        state.TargetEvent.DistanceKm.Should().Be(42.2);
        state.TargetEvent.EventDateIso.Should().Be("2026-09-27");
        state.WeeklySchedule!.MaxRunDaysPerWeek.Should().Be(5);
        state.WeeklySchedule.Saturday.Should().BeTrue();
        state.Preferences!.PreferredUnits.Should().Be(PreferredUnits.Miles);
        state.InjuryHistory!.PastInjurySummary.Should().Be("Rolled ankle 2024");

        // Assert — RunnerOnboardingProfile (EF projection) materialized identically (DEC-060).
        var profile = await LoadProfileAsync(Factory, userId, ct);
        profile.Should().NotBeNull();
        profile!.PrimaryGoal.Should().Be(PrimaryGoal.RaceTraining);
        profile.TargetEvent!.EventName.Should().Be("Berlin Marathon");
        profile.WeeklySchedule!.MaxRunDaysPerWeek.Should().Be(5);
        profile.CurrentPlanId.Should().Be(state.CurrentPlanId);
        profile.OnboardingCompletedAt.Should().NotBeNull();

        // Assert — a plan stream was actually created at the linked id.
        (await PlanStreamExistsAsync(Factory, userId, state.CurrentPlanId!.Value, ct))
            .Should().BeTrue(because: "the terminal branch starts a plan stream at the linked plan id");
    }

    [Fact]
    public async Task SubmitAnswers_FitnessProfile_NoTargetEvent_GeneratesPlan()
    {
        // Arrange — general fitness needs no TargetEvent to satisfy the gate.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var request = FitnessRequest(Guid.NewGuid());

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        var response = await PostAnswersAsync(client, token, request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        state!.IsComplete.Should().BeTrue();
        state.CurrentPlanId.Should().NotBeNull();
        state.TargetEvent.Should().BeNull(because: "no target event is required or submitted for general fitness");
    }

    [Fact]
    public async Task SubmitAnswers_IncompleteSet_GateNotSatisfied_NoPlan_StateReflectsPartial()
    {
        // Arrange — only two of the five required topics.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var request = new SubmitStructuredAnswersRequestDto(
            Guid.NewGuid(),
            new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, "Stay healthy"),
            TargetEvent: null,
            new CurrentFitnessInputDto(30, 12, null, null, "Moderate"),
            WeeklySchedule: null,
            InjuryHistory: null,
            Preferences: null);

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        var response = await PostAnswersAsync(client, token, request, ct);

        // Assert — 200 with partial progress; no plan generated.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        state!.IsComplete.Should().BeFalse();
        state.CurrentPlanId.Should().BeNull();
        state.PrimaryGoal.Should().NotBeNull();
        state.CurrentFitness.Should().NotBeNull();
        state.WeeklySchedule.Should().BeNull();

        // Assert — GET /state reflects the same partial progress (the resume contract).
        var resumed = await GetStateAsync(client, ct);
        resumed.IsComplete.Should().BeFalse();
        resumed.WeeklySchedule.Should().BeNull();
    }

    [Fact]
    public async Task SubmitAnswers_DuplicateIdempotencyKey_SinglePlan_NoDoubleAppend()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var key = Guid.NewGuid();
        var request = RaceTrainingRequest(key);

        // Act — submit twice with the SAME idempotency key.
        var token1 = await PrimeAntiforgeryAsync(client, container);
        var first = await PostAnswersAsync(client, token1, request, ct);
        var firstState = await first.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        var eventCountAfterFirst = await OnboardingStreamEventCountAsync(Factory, userId, ct);

        var token2 = await PrimeAntiforgeryAsync(client, container);
        var second = await PostAnswersAsync(client, token2, request, ct);
        var secondState = await second.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);

        // Assert — same plan, no new events appended on replay.
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondState!.CurrentPlanId.Should().Be(firstState!.CurrentPlanId, because: "a duplicate key must not generate a second plan");
        var eventCountAfterSecond = await OnboardingStreamEventCountAsync(Factory, userId, ct);
        eventCountAfterSecond.Should().Be(eventCountAfterFirst, because: "the idempotent replay appends nothing");
    }

    [Fact]
    public async Task SubmitAnswers_AlreadyComplete_Returns409()
    {
        // Arrange — complete onboarding first.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var token1 = await PrimeAntiforgeryAsync(client, container);
        var first = await PostAnswersAsync(client, token1, RaceTrainingRequest(Guid.NewGuid()), ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — submit again (new key) against a completed stream.
        var token2 = await PrimeAntiforgeryAsync(client, container);
        var second = await PostAnswersAsync(client, token2, RaceTrainingRequest(Guid.NewGuid()), ct);

        // Assert — 409, no second plan.
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SubmitAnswers_OutOfRangeNumeric_Returns400_WithoutStagingEvents()
    {
        // Arrange — a hostile weekly-schedule value that would trip the answer record's init validator.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var request = new SubmitStructuredAnswersRequestDto(
            Guid.NewGuid(),
            new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, "Stay healthy"),
            TargetEvent: null,
            CurrentFitness: null,
            new WeeklyScheduleInputDto(99, 45, true, false, true, false, true, false, true, "Evenings"),
            InjuryHistory: null,
            Preferences: null);

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        var response = await PostAnswersAsync(client, token, request, ct);

        // Assert — clean 400 ProblemDetails, not a 500, and nothing was appended.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await OnboardingStreamEventCountAsync(Factory, userId, ct)).Should().Be(0, because: "an invalid request is rejected before any event is appended");
    }

    [Fact]
    public async Task SubmitAnswers_TargetEventWithoutRaceTraining_Returns400()
    {
        // Arrange — a target event paired with a non-racing goal is a cross-field violation.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var request = new SubmitStructuredAnswersRequestDto(
            Guid.NewGuid(),
            new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, "Stay healthy"),
            new TargetEventInputDto("Berlin Marathon", 42.2, "2026-09-27", null),
            CurrentFitness: null,
            WeeklySchedule: null,
            InjuryHistory: null,
            Preferences: null);

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        var response = await PostAnswersAsync(client, token, request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitAnswers_PresentTopicMissingRequiredField_Returns400()
    {
        // Arrange — a raw body with a weeklySchedule object missing its required fields. The loosened
        // input DTO's [JsonRequired] presence enforcement must surface this as a clean 400 at model
        // binding, never a 500.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var rawBody = $"{{\"idempotencyKey\":\"{Guid.NewGuid()}\",\"weeklySchedule\":{{\"maxRunDaysPerWeek\":5}}}}";

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, AnswersPath, token);
        request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitAnswers_PlanGenerationRejected_Returns422_NothingStaged_ThenResubmitSucceeds()
    {
        // Arrange — a per-test factory whose plan generation rejects the first call then succeeds.
        var ct = TestContext.Current.CancellationToken;
        using var rejectingFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPlanGenerationService>();
                services.AddSingleton<IPlanGenerationService>(
                    new RejectOnceThenSucceedPlanGenerationService(new StubPlanGenerationService()));
            }));
        var (client, container) = CreateCookieClient(rejectingFactory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);

        // Act 1 — submit all six; the gate is satisfied and plan generation rejects the first call.
        var token1 = await PrimeAntiforgeryAsync(client, container);
        var first = await PostAnswersAsync(client, token1, RaceTrainingRequest(Guid.NewGuid()), ct);

        // Assert 1 — a handled 422 (not a 500), and the whole single-submit transaction rolled back:
        // nothing staged means no onboarding stream exists at all (DEC-080 posture).
        first.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await OnboardingStreamEventCountAsync(Factory, userId, ct))
            .Should().Be(0, because: "a terminal plan-generation rejection aborts the transaction with nothing staged");

        // Act 2 — re-submit with a NEW key; plan generation now succeeds.
        var token2 = await PrimeAntiforgeryAsync(client, container);
        var second = await PostAnswersAsync(client, token2, RaceTrainingRequest(Guid.NewGuid()), ct);

        // Assert 2 — the form is re-submittable; onboarding completes with a plan.
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondState = await second.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        secondState!.IsComplete.Should().BeTrue();
        secondState.CurrentPlanId.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAnswers_WhenConversationStreamAlreadyExists_AppendsWithoutCollision()
    {
        // Arrange — a runner who chatted before onboarding already has the shared per-user Marten stream
        // (tagged ConversationView). The handler must APPEND OnboardingStarted to it, not StartStream
        // (which would throw ExistingStreamIdCollisionException) — the PR-A #259 bootstrap fix.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        await PreseedConversationStreamAsync(Factory, userId, ct);

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        var response = await PostAnswersAsync(client, token, RaceTrainingRequest(Guid.NewGuid()), ct);

        // Assert — no collision; onboarding completes over the pre-existing shared stream.
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: "the handler appends OnboardingStarted to the existing shared stream instead of colliding on StartStream");
        var state = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        state!.IsComplete.Should().BeTrue();
        state.CurrentPlanId.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAnswers_PartialThenCompleting_MergesPriorTopics_AndGeneratesPlan()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);

        // Act 1 — submit a subset (goal + fitness + weekly schedule); the gate is not yet satisfied.
        var token1 = await PrimeAntiforgeryAsync(client, container);
        var partial = new SubmitStructuredAnswersRequestDto(
            Guid.NewGuid(),
            new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, "Stay healthy"),
            TargetEvent: null,
            new CurrentFitnessInputDto(30, 12, null, null, "Moderate"),
            new WeeklyScheduleInputDto(4, 45, true, false, true, false, true, false, true, "Evenings"),
            InjuryHistory: null,
            Preferences: null);
        var first = await PostAnswersAsync(client, token1, partial, ct);
        var firstState = await first.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        firstState!.IsComplete.Should().BeFalse();
        firstState.CurrentPlanId.Should().BeNull();

        // Act 2 — submit the REMAINING topics with a NEW key; the merge completes onboarding.
        var token2 = await PrimeAntiforgeryAsync(client, container);
        var rest = new SubmitStructuredAnswersRequestDto(
            Guid.NewGuid(),
            PrimaryGoal: null,
            TargetEvent: null,
            CurrentFitness: null,
            WeeklySchedule: null,
            new InjuryHistoryInputDto(false, string.Empty, "none"),
            new PreferencesInputDto(PreferredUnits.Kilometers, false, true, string.Empty));
        var second = await PostAnswersAsync(client, token2, rest, ct);

        // Assert — the first submission's topics survived onto the merged view; onboarding completes.
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondState = await second.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        secondState!.IsComplete.Should().BeTrue();
        secondState.CurrentPlanId.Should().NotBeNull();
        secondState.PrimaryGoal.Should().NotBeNull(because: "the earlier submission's topics survived the merge");
        secondState.CurrentFitness.Should().NotBeNull();
        secondState.WeeklySchedule.Should().NotBeNull();
        secondState.InjuryHistory.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAnswers_OverflowingDistanceLiteral_Returns400_NotServerError()
    {
        // Arrange — a JSON numeric literal that overflows double range deserializes to +Infinity, which
        // slips past the answer record's one-sided guard. The mapper must reject it as a clean 400, never
        // crash serialization as a 500 (the FR-1.8 failure mode).
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email);
        await LoginAsync(client, container, email);
        var rawBody =
            $"{{\"idempotencyKey\":\"{Guid.NewGuid()}\",\"currentFitness\":{{\"typicalWeeklyKm\":1e400,\"longestRecentRunKm\":12,\"recentRaceDistanceKm\":null,\"recentRaceTimeIso\":null,\"description\":\"x\"}}}}";

        // Act
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, AnswersPath, token);
        request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        // Onboarding + plan streams live in Marten's runcoach_events schema, which Respawn skips.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
    }

    private static SubmitStructuredAnswersRequestDto RaceTrainingRequest(Guid key) => new(
        key,
        new PrimaryGoalInputDto(PrimaryGoal.RaceTraining, "Sub-4 marathon"),
        new TargetEventInputDto("Berlin Marathon", 42.2, "2026-09-27", "PT3H55M0S"),
        new CurrentFitnessInputDto(45, 20, 21.1, "PT1H45M0S", "Feeling strong"),
        new WeeklyScheduleInputDto(5, 60, true, false, true, false, true, true, false, "Evenings only"),
        new InjuryHistoryInputDto(false, string.Empty, "Rolled ankle 2024"),
        new PreferencesInputDto(PreferredUnits.Miles, true, true, "Prefer mornings"));

    private static SubmitStructuredAnswersRequestDto FitnessRequest(Guid key) => new(
        key,
        new PrimaryGoalInputDto(PrimaryGoal.GeneralFitness, "Stay healthy"),
        TargetEvent: null,
        new CurrentFitnessInputDto(30, 12, null, null, "Moderate"),
        new WeeklyScheduleInputDto(4, 45, true, false, true, false, true, false, true, "Evenings"),
        new InjuryHistoryInputDto(false, string.Empty, string.Empty),
        new PreferencesInputDto(PreferredUnits.Kilometers, false, true, string.Empty));

    private static async Task<RunnerOnboardingProfile?> LoadProfileAsync(RunCoachAppFactory factory, Guid userId, CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        var query = db.RunnerOnboardingProfiles.AsNoTracking().Where(p => p.UserId == userId);
        return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, ct);
    }

    private static async Task PreseedConversationStreamAsync(RunCoachAppFactory factory, Guid userId, CancellationToken ct)
    {
        // Create the runner's per-user event stream via a conversation turn (as if they chatted before
        // onboarding), tagging the physical stream ConversationView with no onboarding events on it yet.
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.StartStream<ConversationView>(userId, new UserMessagePosted(userId, Guid.NewGuid(), "hey coach"));
        await session.SaveChangesAsync(ct);
    }

    private static async Task<bool> PlanStreamExistsAsync(RunCoachAppFactory factory, Guid userId, Guid planId, CancellationToken ct)
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var state = await session.Events.FetchStreamStateAsync(planId, ct);
        return state is not null;
    }

    private static async Task<int> OnboardingStreamEventCountAsync(RunCoachAppFactory factory, Guid userId, CancellationToken ct)
    {
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var events = await session.Events.FetchStreamAsync(userId, token: ct);
        return events.Count;
    }

    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
    }

    private static async Task<HttpResponseMessage> PostAnswersAsync(
        HttpClient client, string antiforgeryToken, SubmitStructuredAnswersRequestDto dto, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Post, AnswersPath, antiforgeryToken);
        request.Content = JsonContent.Create(dto);
        return await client.SendAsync(request, ct);
    }

    private static async Task<OnboardingStateDto> GetStateAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(StatePath, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(cancellationToken: ct);
        dto.Should().NotBeNull();
        return dto!;
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, string antiforgeryToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        return request;
    }

    private static async Task<string> PrimeAntiforgeryAsync(HttpClient client, CookieContainer container)
    {
        using var response = await client.GetAsync("/api/v1/auth/xsrf", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requestCookie = GetCookie(container, AntiforgeryRequestCookieName);
        requestCookie.Should().NotBeNull("/xsrf must issue the SPA-readable request token cookie");
        GetCookie(container, AntiforgeryCookieName).Should().NotBeNull("the framework antiforgery cookie must also be set");
        return requestCookie!.Value;
    }

    private static async Task<Guid> RegisterAsync(HttpClient client, CookieContainer container, string email)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequestDto(email, StrongPassword));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, because: $"register helper must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!.UserId;
    }

    private static async Task LoginAsync(HttpClient client, CookieContainer container, string email)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/login", token);
        request.Content = JsonContent.Create(new LoginRequestDto(email, StrongPassword));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"login helper must succeed — got {(int)response.StatusCode}");
    }

    private static Cookie? GetCookie(CookieContainer container, string name)
    {
        foreach (Cookie c in container.GetCookies(BaseUri))
        {
            if (string.Equals(c.Name, name, StringComparison.Ordinal))
            {
                return c;
            }
        }

        return null;
    }

    private static string GenerateEmail() => $"onboarding-answers-{Guid.NewGuid():N}@example.test";
}
