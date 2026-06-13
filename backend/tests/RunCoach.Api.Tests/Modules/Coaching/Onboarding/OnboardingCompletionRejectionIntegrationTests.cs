using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// F3 integration coverage for the onboarding completion turn's terminal plan-generation
/// rejection. When <see cref="IPlanGenerationService.GeneratePlanAsync"/> throws a
/// <see cref="PlanGenerationRejectedException"/> on the completion turn, the controller must
/// map it to an HTTP-200 <see cref="OnboardingTurnKind.Error"/> envelope (not a 500), the
/// Wolverine transaction must abort with nothing staged (no <c>OnboardingCompleted</c>, no
/// plan), and the same final turn must be re-submittable to succeed.
/// </summary>
/// <remarks>
/// The flow drives the LIVE HTTP + Wolverine pipeline. The onboarding view is seeded directly
/// with the five required <see cref="AnswerCaptured"/> answers so the deterministic completion
/// gate is satisfied; a single submitted turn then reaches the terminal plan-generation branch.
/// <see cref="StubCoachingLlm"/> (swapped in at host boot by <see cref="RunCoachAppFactory"/>)
/// is scripted to return a ready-for-plan turn output, and a per-test
/// <see cref="RejectOnceThenSucceedPlanGenerationService"/> singleton rejects the first
/// generation call then delegates to the deterministic <see cref="StubPlanGenerationService"/>
/// on the retry — proving both the error mapping and the re-submittability in one faithful test.
/// </remarks>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class OnboardingCompletionRejectionIntegrationTests : DbBackedIntegrationTestBase
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string TurnsPath = "/api/v1/onboarding/turns";
    private const string StatePath = "/api/v1/onboarding/state";

    private static readonly Uri BaseUri = new("https://localhost");

    public OnboardingCompletionRejectionIntegrationTests(RunCoachAppFactory factory)
        : base(factory)
    {
        // xUnit constructs one instance per test, so this clears any scripted
        // behavior the assembly-swapped StubCoachingLlm carries between tests.
        StubCoachingLlm.Reset();
    }

    [Fact]
    public async Task CompletionTurn_PlanGenerationRejected_Returns200ErrorEnvelope_NothingStaged_ThenResubmitSucceeds()
    {
        // Arrange — a per-test factory whose IPlanGenerationService rejects the first
        // generation call (PlanGenerationRejectedException) then delegates to the
        // deterministic stub. ConfigureTestServices runs after the fixture's
        // ConfigureWebHost, so this registration wins for the freshly-built host and
        // Wolverine's codegen resolves it into the onboarding terminal-branch handler.
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
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        // Seed the onboarding stream with all five required answers so the
        // deterministic completion gate is satisfied (PrimaryGoal=GeneralFitness, so
        // TargetEvent is not required) — a single submitted turn then reaches the
        // terminal plan-generation branch.
        await SeedGateSatisfiedStreamAsync(rejectingFactory, userId, ct);

        // The completion turn's single LLM call returns a ready-for-plan output with no
        // extraction (matches OnboardingTurnHandlerUnitTests' terminal-branch fixture).
        StubCoachingLlm.UseStructuredBehavior(BuildReadyForPlanOutput);

        // Act 1 — submit the completion turn; the first plan-gen call throws, so
        // Wolverine aborts the Marten transaction and nothing is staged.
        var token1 = await PrimeAntiforgeryAsync(client, container);
        var firstResponse = await PostTurnAsync(client, token1, new OnboardingTurnRequestDto(Guid.NewGuid(), "ready"), ct);

        // Assert 1 — HTTP 200 with an Error-kind envelope carrying a user-facing message.
        firstResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: "a terminal plan-generation rejection maps to an HTTP-200 error envelope, not a 500");
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<OnboardingTurnResponseDto>(cancellationToken: ct);
        firstBody.Should().NotBeNull();
        firstBody!.Kind.Should().Be(OnboardingTurnKind.Error);
        firstBody.PlanId.Should().BeNull();
        firstBody.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        firstBody.ErrorMessage.Should().NotContainEquivalentOf(
            "vdot", because: "user-facing error copy must use approved vocabulary only");

        // Assert 1 (nothing staged) — the onboarding is NOT complete and no plan is linked.
        var stateAfterReject = await GetStateAsync(client, ct);
        stateAfterReject.IsComplete.Should().BeFalse(because: "the aborted transaction staged no OnboardingCompleted");
        stateAfterReject.CurrentPlanId.Should().BeNull(because: "the aborted transaction linked no plan");

        // Act 2 — re-submit the final turn with a NEW idempotency key; the second
        // plan-gen call succeeds.
        StubCoachingLlm.UseStructuredBehavior(BuildReadyForPlanOutput);
        var token2 = await PrimeAntiforgeryAsync(client, container);
        var secondResponse = await PostTurnAsync(client, token2, new OnboardingTurnRequestDto(Guid.NewGuid(), "ready"), ct);

        // Assert 2 — HTTP 200 with a Complete-kind envelope carrying the generated plan id.
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<OnboardingTurnResponseDto>(cancellationToken: ct);
        secondBody.Should().NotBeNull();
        secondBody!.Kind.Should().Be(
            OnboardingTurnKind.Complete,
            because: "the re-submitted turn re-runs plan generation, which now succeeds");
        secondBody.PlanId.Should().NotBeNull().And.NotBe(Guid.Empty);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        StubCoachingLlm.Reset();

        // Onboarding + plan streams live in Marten's runcoach_events schema, which
        // Respawn skips — reset them explicitly. Base type calls GC.SuppressFinalize.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
    }

    private static OnboardingTurnOutput BuildReadyForPlanOutput() => new()
    {
        Reply = [new AnthropicContentBlock { Type = AnthropicContentBlockType.Text, Text = "all set" }],
        Extracted = null,
        NeedsClarification = false,
        ClarificationReason = null,
        ReadyForPlan = true,
    };

    /// <summary>
    /// Seeds the runner's onboarding stream with <see cref="OnboardingStarted"/> plus the five
    /// required <see cref="AnswerCaptured"/> answers (GeneralFitness primary goal, so the
    /// optional TargetEvent slot is not required) on a tenant-scoped session, so the inline
    /// projection materializes a view the deterministic completion gate accepts.
    /// </summary>
    private static async Task SeedGateSatisfiedStreamAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        Guid userId,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var store = factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.StartStream<OnboardingView>(userId, new OnboardingStarted(userId, now));
        AppendAnswer(
            session,
            userId,
            OnboardingTopic.PrimaryGoal,
            now,
            new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "fitness" });
        AppendAnswer(
            session,
            userId,
            OnboardingTopic.CurrentFitness,
            now,
            new CurrentFitnessAnswer
            {
                TypicalWeeklyKm = 30,
                LongestRecentRunKm = 12,
                RecentRaceDistanceKm = null,
                RecentRaceTimeIso = null,
                Description = "moderate",
            });
        AppendAnswer(
            session,
            userId,
            OnboardingTopic.WeeklySchedule,
            now,
            new WeeklyScheduleAnswer
            {
                MaxRunDaysPerWeek = 4,
                TypicalSessionMinutes = 45,
                Monday = true,
                Tuesday = false,
                Wednesday = true,
                Thursday = false,
                Friday = true,
                Saturday = false,
                Sunday = true,
                Description = "evenings",
            });
        AppendAnswer(
            session,
            userId,
            OnboardingTopic.InjuryHistory,
            now,
            new InjuryHistoryAnswer
            {
                HasActiveInjury = false,
                ActiveInjuryDescription = string.Empty,
                PastInjurySummary = "none",
            });
        AppendAnswer(
            session,
            userId,
            OnboardingTopic.Preferences,
            now,
            new PreferencesAnswer
            {
                PreferredUnits = PreferredUnits.Kilometers,
                PreferTrail = false,
                ComfortableWithIntensity = true,
                Description = "ok",
            });
        await session.SaveChangesAsync(ct);
    }

    private static void AppendAnswer<TAnswer>(
        IDocumentSession session, Guid userId, OnboardingTopic topic, DateTimeOffset now, TAnswer answer)
    {
        // The captured-answer payload stays default-cased (PascalCase) because the inline
        // OnboardingProjection reads it back via JsonDocument.Deserialize<T>() — the same
        // casing the production handler's ExtractAnswer path uses.
        var payload = JsonSerializer.SerializeToDocument(answer);
        session.Events.Append(userId, new AnswerCaptured(topic, payload, Confidence: 1.0, CapturedAt: now));
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

    private static async Task<HttpResponseMessage> PostTurnAsync(
        HttpClient client, string antiforgeryToken, OnboardingTurnRequestDto dto, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Post, TurnsPath, antiforgeryToken);
        request.Content = JsonContent.Create(dto);
        return await client.SendAsync(request, ct);
    }

    private static async Task<OnboardingStateDto> GetStateAsync(HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(StatePath, ct);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, because: "the seeded stream means /state returns the in-progress view");
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
        GetCookie(container, AntiforgeryCookieName).Should().NotBeNull(
            "the framework antiforgery cookie must also be set");
        return requestCookie!.Value;
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client, CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.Created, because: $"register helper must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!.UserId;
    }

    private static async Task LoginAsync(
        HttpClient client, CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/login", token);
        request.Content = JsonContent.Create(new LoginRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, because: $"login helper must succeed — got {(int)response.StatusCode}");
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

    private static string GenerateEmail() => $"onboarding-reject-{Guid.NewGuid():N}@example.test";
}
