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
using NSubstitute.ExceptionExtensions;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Tests.Infrastructure;
using Wolverine;

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
    /// Authenticated but no antiforgery token → 400 ProblemDetails. Mirrors
    /// the Auth pattern (Register_MissingAntiforgeryToken_Returns_400_ProblemDetails)
    /// to make sure the [RequireAntiforgeryToken] attribute on /turns can't be
    /// silently dropped without test failure.
    /// </summary>
    [Fact]
    public async Task SubmitTurn_AuthenticatedNoAntiforgery_Returns_400_ProblemDetails()
    {
        // Arrange — register/login but skip PrimeAntiforgeryAsync so neither
        // the cookie nor the header is attached on the POST.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/onboarding/turns")
        {
            Content = JsonContent.Create(new OnboardingTurnRequestDto(Guid.NewGuid(), "hello")),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    /// <summary>
    /// Authenticated with cookie + valid request → 200 with OnboardingTurnResponseDto.
    /// IMessageBus is stubbed to short-circuit the Wolverine handler chain so the
    /// integration test stays focused on the controller's mapping behaviour and
    /// never calls the real Anthropic API. Handler-side coverage is owned by
    /// OnboardingTurnHandlerUnitTests + InvokeAsyncTransactionScopeTests.
    /// </summary>
    [Fact]
    public async Task SubmitTurn_Authenticated_Returns_200_With_ResponseDto()
    {
        // Arrange — stub the bus to return a deterministic Ask response.
        var expected = BuildAskResponseDto();
        var busStub = Substitute.For<IMessageBus>();
        busStub
            .InvokeForTenantAsync<OnboardingTurnResponseDto>(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>())
            .Returns(expected);

        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svc => svc.AddSingleton(busStub)));
        var (client, container) = CreateCookieClient(customFactory);

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
    /// Already-complete stream — bus throws OnboardingAlreadyCompleteException;
    /// controller maps it to 409 ProblemDetails with the documented type URI.
    /// </summary>
    [Fact]
    public async Task SubmitTurn_AlreadyCompleteStream_Returns_409_ProblemDetails()
    {
        // Arrange — stub the bus to surface the contract exception.
        var busStub = Substitute.For<IMessageBus>();
        busStub
            .InvokeForTenantAsync<OnboardingTurnResponseDto>(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>())
            .Throws(new OnboardingAlreadyCompleteException(Guid.NewGuid()));

        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svc => svc.AddSingleton(busStub)));
        var (client, container) = CreateCookieClient(customFactory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

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
    /// Multi-tenant isolation: authenticated as user B, calling GET /state
    /// must NOT expose user A's seeded stream — Marten conjoined-tenancy must
    /// hold even if tests share the same Postgres container. Catches refactors
    /// that drop the per-request <c>store.LightweightSession(userId.ToString())</c>
    /// tenant scoping.
    /// </summary>
    [Fact]
    public async Task GetState_AuthenticatedAsUserB_DoesNotSeeUserAStream()
    {
        // Arrange — seed user A's stream directly via a tenanted session.
        var (clientA, containerA) = CreateCookieClient(Factory);
        var emailA = GenerateEmail();
        var userAId = await RegisterAsync(clientA, containerA, emailA, StrongPassword);
        await LoginAsync(clientA, containerA, emailA, StrongPassword);

        var now = DateTimeOffset.UtcNow;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using (var seedSession = store.LightweightSession(userAId.ToString()))
        {
            seedSession.Events.StartStream<OnboardingView>(
                userAId,
                new OnboardingStarted(userAId, now));
            seedSession.Events.Append(userAId, new TopicAsked(OnboardingTopic.PrimaryGoal, now));
            await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // User B logs in fresh — different identity, different tenant.
        var (clientB, containerB) = CreateCookieClient(Factory);
        var emailB = GenerateEmail();
        await RegisterAsync(clientB, containerB, emailB, StrongPassword);
        await LoginAsync(clientB, containerB, emailB, StrongPassword);

        // Act
        var response = await clientB.GetAsync(
            "/api/v1/onboarding/state",
            TestContext.Current.CancellationToken);

        // Assert — user B sees no stream of their own and CANNOT see A's.
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
    /// Authenticated but no antiforgery token → 400 ProblemDetails.
    /// Mirrors the SubmitTurn antiforgery negative test for the second
    /// state-changing POST endpoint.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_AuthenticatedNoAntiforgery_Returns_400_ProblemDetails()
    {
        // Arrange — register/login but skip PrimeAntiforgeryAsync.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
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

    /// <summary>
    /// Multi-tenant isolation: authenticated as user B, calling POST /answers/revise
    /// must NOT mutate user A's seeded stream — Marten conjoined-tenancy
    /// scoping must prevent the cross-tenant write. Asserts user B sees a 404
    /// (their own stream does not exist) and user A's view remains untouched.
    /// </summary>
    [Fact]
    public async Task ReviseAnswer_AuthenticatedAsUserB_CannotMutateUserAStream()
    {
        // Arrange — seed user A's stream via a tenanted session.
        var (clientA, containerA) = CreateCookieClient(Factory);
        var emailA = GenerateEmail();
        var userAId = await RegisterAsync(clientA, containerA, emailA, StrongPassword);
        await LoginAsync(clientA, containerA, emailA, StrongPassword);

        var now = DateTimeOffset.UtcNow;
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using (var seedSession = store.LightweightSession(userAId.ToString()))
        {
            seedSession.Events.StartStream<OnboardingView>(
                userAId,
                new OnboardingStarted(userAId, now));
            seedSession.Events.Append(userAId, new TopicAsked(OnboardingTopic.PrimaryGoal, now));
            await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // User B logs in fresh — different identity, different tenant.
        var (clientB, containerB) = CreateCookieClient(Factory);
        var emailB = GenerateEmail();
        await RegisterAsync(clientB, containerB, emailB, StrongPassword);
        await LoginAsync(clientB, containerB, emailB, StrongPassword);
        var token = await PrimeAntiforgeryAsync(clientB, containerB);

        // Act — user B attempts to revise; the controller's tenanted session
        // scopes to userB.ToString() so user A's stream is NOT visible.
        using var request = BuildRequest(
            HttpMethod.Post, "/api/v1/onboarding/answers/revise", token);
        request.Content = JsonContent.Create(
            new ReviseAnswerRequestDto(
                OnboardingTopic.PrimaryGoal,
                JsonSerializer.SerializeToElement(
                    new PrimaryGoalAnswer { Goal = PrimaryGoal.BuildSpeed, Description = "user-B intrusion" })));
        var response = await clientB.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — user B's tenant has no stream so they get 404.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify user A's stream is unchanged (no AnswerCaptured event was appended).
        await using var verifySession = store.LightweightSession(userAId.ToString());
        var aEvents = await verifySession.Events.FetchStreamAsync(
            userAId,
            token: TestContext.Current.CancellationToken);
        aEvents.Should().NotContain(e => e.Data is AnswerCaptured);
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

    private static OnboardingTurnResponseDto BuildAskResponseDto() =>
        new(
            Kind: OnboardingTurnKind.Ask,
            AssistantBlocks: JsonSerializer.SerializeToElement(
                new[]
                {
                    new AnthropicContentBlock
                    {
                        Type = AnthropicContentBlockType.Text,
                        Text = "What is your primary training goal?",
                    },
                }),
            Topic: OnboardingTopic.PrimaryGoal,
            SuggestedInputType: SuggestedInputType.SingleSelect,
            Progress: new OnboardingProgressDto(0, 5),
            PlanId: null);
}
