using System.Collections.Immutable;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Coaching.Sanitization;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Integration matrix for the three Slice 1a6 onboarding endpoints
/// (<c>/turns</c>, <c>/state</c>, <c>/answers/revise</c>) against the
/// shared Testcontainers Postgres.
/// </summary>
[Trait("Category", "Integration")]
public class OnboardingControllerIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AlreadyCompleteType = "https://runcoach.app/problems/onboarding-already-complete";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;

    private static readonly Uri BaseUri = new("https://localhost");

    // ------------------------------------------------------------------ //
    // POST /api/v1/onboarding/turns
    // ------------------------------------------------------------------ //

    /// <summary>Anonymous → 401 on the SubmitTurn endpoint.</summary>
    [Fact]
    public async Task SubmitTurn_Anonymous_Returns_401()
    {
        // Arrange
        var (client, _) = CreateCookieClient(Factory);

        // Act — no session cookie; the policy denies before antiforgery is checked
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/onboarding/turns")
        {
            Content = JsonContent.Create(new OnboardingTurnRequestDto(Guid.NewGuid(), "hello")),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Authenticated with cookie + valid request → 200 with OnboardingTurnResponseDto.
    /// ICoachingLlm is replaced with a stub that returns a predictable Ask response
    /// so the test is deterministic and never calls the real Anthropic API.
    /// </summary>
    [Fact]
    public async Task SubmitTurn_Authenticated_Returns_200_With_ResponseDto()
    {
        // Arrange — stub the LLM so we never call the real Anthropic API.
        var llmStub = Substitute.For<ICoachingLlm>();
        llmStub
            .GenerateStructuredAsync<OnboardingTurnOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildAskOutput());

        var assemblerStub = Substitute.For<IContextAssembler>();
        assemblerStub
            .ComposeForOnboardingAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<OnboardingTopic>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new OnboardingPromptComposition(
                SystemPrompt: "sys",
                UserMessage: "user",
                Findings: ImmutableArray<SanitizationFinding>.Empty,
                Neutralized: false));

        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svc =>
            {
                svc.AddSingleton(llmStub);
                svc.AddSingleton(assemblerStub);
            }));
        var (client, container) = CreateCookieClient(customFactory);

        // Register + login (userId not needed for this test)
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);

        // Act
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/onboarding/turns", token);
        request.Content = JsonContent.Create(new OnboardingTurnRequestDto(Guid.NewGuid(), "I want to run a 5k"));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OnboardingTurnResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.Kind.Should().Be(OnboardingTurnKind.Ask);
        dto.Progress.Should().NotBeNull();
    }

    /// <summary>
    /// Already-complete stream → 409 with the onboarding-already-complete
    /// ProblemDetails type.
    /// </summary>
    [Fact]
    public async Task SubmitTurn_AlreadyCompleteStream_Returns_409_ProblemDetails()
    {
        // Arrange — stub LLM to prevent real API calls.
        var llmStub = Substitute.For<ICoachingLlm>();
        llmStub
            .GenerateStructuredAsync<OnboardingTurnOutput>(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, JsonElement>>(),
                Arg.Any<CacheControl?>(),
                Arg.Any<CancellationToken>())
            .Returns(BuildAskOutput());

        var assemblerStub = Substitute.For<IContextAssembler>();
        assemblerStub
            .ComposeForOnboardingAsync(
                Arg.Any<OnboardingView>(),
                Arg.Any<OnboardingTopic>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new OnboardingPromptComposition(
                SystemPrompt: "sys",
                UserMessage: "user",
                Findings: ImmutableArray<SanitizationFinding>.Empty,
                Neutralized: false));

        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svc =>
            {
                svc.AddSingleton(llmStub);
                svc.AddSingleton(assemblerStub);
            }));
        var (client, container) = CreateCookieClient(customFactory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        // Seed an OnboardingCompleted event directly onto the user's stream so
        // the handler sees a terminal stream and throws
        // OnboardingAlreadyCompleteException.
        var now = DateTimeOffset.UtcNow;
        var store = customFactory.Services.GetRequiredService<IDocumentStore>();
        await using (var seedSession = store.LightweightSession(userId.ToString()))
        {
            seedSession.Events.StartStream<OnboardingView>(
                userId,
                new OnboardingStarted(userId, now));
            seedSession.Events.Append(userId, new OnboardingCompleted(Guid.NewGuid(), now));
            await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var token = await PrimeAntiforgeryAsync(client, container);

        // Act
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/onboarding/turns", token);
        request.Content = JsonContent.Create(new OnboardingTurnRequestDto(Guid.NewGuid(), "another turn"));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Type.Should().Be(AlreadyCompleteType);
    }

    // ------------------------------------------------------------------ //
    // GET /api/v1/onboarding/state
    // ------------------------------------------------------------------ //

    /// <summary>Anonymous → 401 on the GetState endpoint.</summary>
    [Fact]
    public async Task GetState_Anonymous_Returns_401()
    {
        // Arrange
        var (client, _) = CreateCookieClient(Factory);

        // Act
        var response = await client.GetAsync(
            "/api/v1/onboarding/state",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Authenticated, no stream yet → 404 because the inline projection
    /// has no document for this user.
    /// </summary>
    [Fact]
    public async Task GetState_Authenticated_NoStream_Returns_404()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        // Act
        var response = await client.GetAsync(
            "/api/v1/onboarding/state",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Authenticated with a seeded stream → 200 with a populated
    /// <see cref="OnboardingStateDto"/>.
    /// </summary>
    [Fact]
    public async Task GetState_Authenticated_SeededStream_Returns_200_With_StateDto()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var now = DateTimeOffset.UtcNow;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using (var seedSession = store.LightweightSession(userId.ToString()))
        {
            seedSession.Events.StartStream<OnboardingView>(
                userId,
                new OnboardingStarted(userId, now));
            seedSession.Events.Append(userId, new TopicAsked(OnboardingTopic.PrimaryGoal, now));
            await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync(
            "/api/v1/onboarding/state",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(userId);
        dto.Status.Should().Be(OnboardingStatus.InProgress);
        dto.CompletedTopics.Should().Be(0);
        dto.TotalTopics.Should().BeGreaterThan(0);
        dto.IsComplete.Should().BeFalse();
        dto.OutstandingClarifications.Should().NotBeNull();
    }

    // ------------------------------------------------------------------ //
    // POST /api/v1/onboarding/answers/revise
    // ------------------------------------------------------------------ //

    /// <summary>Anonymous → 401 on the ReviseAnswer endpoint.</summary>
    [Fact]
    public async Task ReviseAnswer_Anonymous_Returns_401()
    {
        // Arrange
        var (client, _) = CreateCookieClient(Factory);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/onboarding/answers/revise")
        {
            Content = JsonContent.Create(
                new ReviseAnswerRequestDto(
                    OnboardingTopic.PrimaryGoal,
                    JsonSerializer.SerializeToElement(
                        new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "test" }))),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Authenticated but no stream exists → 404 because the onboarding
    /// projection document is absent.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_Authenticated_NoStream_Returns_404()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);

        // Act
        using var request = BuildRequest(
            HttpMethod.Post, "/api/v1/onboarding/answers/revise", token);
        request.Content = JsonContent.Create(
            new ReviseAnswerRequestDto(
                OnboardingTopic.PrimaryGoal,
                JsonSerializer.SerializeToElement(
                    new PrimaryGoalAnswer { Goal = PrimaryGoal.GeneralFitness, Description = "revised" })));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Authenticated with a seeded stream → 200 with updated
    /// <see cref="OnboardingStateDto"/>. Checks that the returned DTO
    /// carries the revised PrimaryGoal answer.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_Authenticated_SeededStream_Returns_200_With_UpdatedDto()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var now = DateTimeOffset.UtcNow;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using (var seedSession = store.LightweightSession(userId.ToString()))
        {
            seedSession.Events.StartStream<OnboardingView>(
                userId,
                new OnboardingStarted(userId, now));
            seedSession.Events.Append(userId, new TopicAsked(OnboardingTopic.PrimaryGoal, now));
            await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var token = await PrimeAntiforgeryAsync(client, container);
        var revisedAnswer = new PrimaryGoalAnswer { Goal = PrimaryGoal.BuildSpeed, Description = "speed" };

        // Act
        using var request = BuildRequest(
            HttpMethod.Post, "/api/v1/onboarding/answers/revise", token);
        request.Content = JsonContent.Create(
            new ReviseAnswerRequestDto(
                OnboardingTopic.PrimaryGoal,
                JsonSerializer.SerializeToElement(revisedAnswer)));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<OnboardingStateDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.UserId.Should().Be(userId);
        dto.PrimaryGoal.Should().NotBeNull("the revised PrimaryGoal answer must be reflected in the returned DTO");
        dto.PrimaryGoal!.Goal.Should().Be(PrimaryGoal.BuildSpeed);
    }

    // ------------------------------------------------------------------ //
    // Helpers
    // ------------------------------------------------------------------ //
    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var container = new System.Net.CookieContainer();
        var client = factory.CreateDefaultClient(
            new Infrastructure.CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
    }

    private static HttpRequestMessage BuildRequest(
        HttpMethod method, string path, string antiforgeryToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        return request;
    }

    private static async Task<string> PrimeAntiforgeryAsync(
        HttpClient client, System.Net.CookieContainer container)
    {
        using var response = await client.GetAsync(
            "/api/v1/auth/xsrf",
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requestCookie = GetCookie(container, AntiforgeryRequestCookieName);
        requestCookie.Should().NotBeNull("/xsrf must issue the SPA-readable request token cookie");
        GetCookie(container, AntiforgeryCookieName).Should().NotBeNull(
            "the framework antiforgery cookie must also be set");
        return requestCookie!.Value;
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client,
        System.Net.CookieContainer container,
        string email,
        string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should()
            .Be(
                HttpStatusCode.Created,
                because: $"register helper must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!.UserId;
    }

    private static async Task LoginAsync(
        HttpClient client,
        System.Net.CookieContainer container,
        string email,
        string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/login", token);
        request.Content = JsonContent.Create(new LoginRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should()
            .Be(
                HttpStatusCode.OK,
                because: $"login helper must succeed — got {(int)response.StatusCode}");
    }

    private static System.Net.Cookie? GetCookie(
        System.Net.CookieContainer container, string name)
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

    private static string GenerateEmail() =>
        $"user-{Guid.NewGuid():N}@example.test";

    private static OnboardingTurnOutput BuildAskOutput() => new()
    {
        Reply =
        [
            new AnthropicContentBlock
            {
                Type = AnthropicContentBlockType.Text,
                Text = "What is your primary training goal?",
            },
        ],
        Extracted = null,
        NeedsClarification = false,
        ClarificationReason = null,
        ReadyForPlan = false,
    };
}
