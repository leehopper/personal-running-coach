using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Integration tests for the GET <c>/state</c> onboarding endpoint against the
/// shared Testcontainers Postgres. The form-first <c>POST /answers</c> endpoint is
/// covered by <c>OnboardingAnswersEndpointIntegrationTests</c>.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OnboardingControllerIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;

    private static readonly Uri BaseUri = new("https://localhost");

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
}
