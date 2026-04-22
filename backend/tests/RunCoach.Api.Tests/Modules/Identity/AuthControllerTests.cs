using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Identity;

/// <summary>
/// Integration matrix for the five Slice 0 auth endpoints
/// (<c>/xsrf</c>, <c>/register</c>, <c>/login</c>, <c>/me</c>, <c>/logout</c>)
/// against the shared Testcontainers Postgres. Every case drives the real
/// <see cref="RunCoachAppFactory"/> SUT — no mocks — and uses
/// <see cref="CookieContainerHandler"/> so the antiforgery + session cookie
/// lifecycle is observable end-to-end.
/// </summary>
[Trait("Category", "Integration")]
public class AuthControllerTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string SessionCookieName = "__Host-RunCoach";
    private const string AntiforgeryCookieName = "__Host-Xsrf";
    private const string AntiforgeryRequestCookieName = "__Host-Xsrf-Request";
    private const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    private const string InvalidCredentialsType = "https://runcoach.app/problems/invalid-credentials";
    private const string RegistrationConflictType = "https://runcoach.app/problems/registration-conflict";

    private static readonly Uri BaseUri = new("https://localhost");

    /// <summary>
    /// Case 1 — GET <c>/xsrf</c> returns 204 with the SPA-readable request-token
    /// cookie. Spec wording used <c>XSRF-TOKEN</c>; DEC-054 renamed the cookie
    /// to <c>__Host-Xsrf-Request</c>, which is what AuthController emits.
    /// </summary>
    [Fact]
    public async Task Xsrf_Returns_204_WithRequestTokenCookie()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);

        // Act
        var response = await client.GetAsync("/api/v1/auth/xsrf", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requestCookie = GetCookie(container, AntiforgeryRequestCookieName);
        requestCookie.Should().NotBeNull("the SPA-readable antiforgery request token must be issued by /xsrf");
        requestCookie!.Value.Should().NotBeNullOrEmpty();
    }

    /// <summary>Case 2 — POST <c>/register</c> happy path returns 201 with AuthResponse.</summary>
    [Fact]
    public async Task Register_HappyPath_Returns_201_WithAuthResponse()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);
        var token = await PrimeAntiforgeryAsync(client, container);
        var email = GenerateEmail();

        // Act
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequest(email, StrongPassword));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Email.Should().Be(email);
        body.UserId.Should().NotBeEmpty();
    }

    /// <summary>Case 3 — POST <c>/register</c> duplicate email returns 409 ProblemDetails.</summary>
    [Fact]
    public async Task Register_DuplicateEmail_Returns_409_ProblemDetails()
    {
        // Arrange
        var email = GenerateEmail();
        var (firstClient, firstContainer) = CreateCookieClient(Factory);
        await RegisterAsync(firstClient, firstContainer, email, StrongPassword);

        var (client, container) = CreateCookieClient(Factory);
        var token = await PrimeAntiforgeryAsync(client, container);

        // Act
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequest(email, StrongPassword));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Type.Should().Be(RegistrationConflictType);
    }

    /// <summary>
    /// Case 4 — POST <c>/register</c> with a weak password returns 400
    /// ValidationProblemDetails with the <c>password</c> error bucket populated.
    /// <c>RegisterRequest</c> uses <c>[MinLength(12)]</c> so a 5-char password
    /// is rejected by <c>[ApiController]</c> auto-400 before reaching Identity.
    /// </summary>
    [Fact]
    public async Task Register_WeakPassword_Returns_400_WithPasswordBucket()
    {
        // Arrange
        var (client, container) = CreateCookieClient(Factory);
        var token = await PrimeAntiforgeryAsync(client, container);

        // Act
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequest(GenerateEmail(), "short"));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using var doc = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        doc.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        var errorKeys = errors.EnumerateObject().Select(p => p.Name).ToArray();
        var hasPasswordBucket = errorKeys.Any(k => string.Equals(k, "password", StringComparison.OrdinalIgnoreCase));
        hasPasswordBucket.Should()
            .BeTrue($"weak-password errors must surface under the DTO-property bucket — got keys: [{string.Join(", ", errorKeys)}]");
    }

    /// <summary>
    /// Case 5 — POST <c>/register</c> with a missing antiforgery token returns
    /// 400. ASP.NET Core's <c>ValidateAntiForgeryToken</c> authorization filter
    /// surfaces as <see cref="HttpStatusCode.BadRequest"/> when the token is
    /// missing or malformed.
    /// </summary>
    [Fact]
    public async Task Register_MissingAntiforgeryToken_Returns_400()
    {
        // Arrange
        var (client, _) = CreateCookieClient(Factory);

        // Act — deliberately skip PrimeAntiforgeryAsync so neither the
        // cookie nor the header is attached.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest(GenerateEmail(), StrongPassword)),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Case 6 — POST <c>/login</c> happy path returns 200 and issues
    /// <c>__Host-RunCoach</c> as HttpOnly, Secure, SameSite=Lax with a 14-day
    /// expiry.
    /// </summary>
    [Fact]
    public async Task Login_HappyPath_Returns_200_AndIssuesSessionCookie()
    {
        // Arrange
        var email = GenerateEmail();
        var (registerClient, registerContainer) = CreateCookieClient(Factory);
        await RegisterAsync(registerClient, registerContainer, email, StrongPassword);

        var (client, _) = CreateCookieClient(Factory);

        // Act — capture the raw Set-Cookie header so attribute asserts don't
        // depend on CookieContainer's attribute-stripping behavior.
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, StrongPassword),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessionHeader = GetRawSetCookie(response, SessionCookieName);
        sessionHeader.Should().NotBeNull($"login must issue {SessionCookieName}");
        var lowered = sessionHeader!.ToLowerInvariant();
        lowered.Should().Contain("httponly");
        lowered.Should().Contain("secure");
        lowered.Should().Contain("samesite=lax");

        // 14-day expiry: parse the `expires=` attribute and assert it is
        // between 13.5 and 14.5 days from now. Identity emits an RFC 1123
        // timestamp when isPersistent=true + ExpireTimeSpan=14d are set.
        var expires = ParseExpires(sessionHeader);
        expires.Should().NotBeNull("login uses isPersistent=true so Expires must be present");
        var days = (expires!.Value - DateTimeOffset.UtcNow).TotalDays;
        days.Should().BeInRange(13.5, 14.5);
    }

    /// <summary>Case 7 — POST <c>/login</c> wrong password returns 401 ProblemDetails.</summary>
    [Fact]
    public async Task Login_WrongPassword_Returns_401_GenericProblemDetails()
    {
        // Arrange
        var email = GenerateEmail();
        var (registerClient, registerContainer) = CreateCookieClient(Factory);
        await RegisterAsync(registerClient, registerContainer, email, StrongPassword);

        var (client, _) = CreateCookieClient(Factory);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "Wr0ngTestPassw0rd!"),
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem!.Type.Should().Be(InvalidCredentialsType);
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Case 8 — POST <c>/login</c> for a non-existent user returns the same
    /// status and byte-identical body as case 7 so an enumerator cannot learn
    /// whether the account exists.
    /// </summary>
    [Fact]
    public async Task Login_UnknownUser_Returns_401_IndistinguishableFromWrongPassword()
    {
        // Arrange
        var knownEmail = GenerateEmail();
        var (seedClient, seedContainer) = CreateCookieClient(Factory);
        await RegisterAsync(seedClient, seedContainer, knownEmail, StrongPassword);

        var (client, _) = CreateCookieClient(Factory);

        // Act
        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(knownEmail, "Wr0ngTestPassw0rd!"),
            TestContext.Current.CancellationToken);

        var unknownUserResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(GenerateEmail(), StrongPassword),
            TestContext.Current.CancellationToken);

        // Assert — identical status codes …
        unknownUserResponse.StatusCode.Should().Be(wrongPasswordResponse.StatusCode)
            .And.Be(HttpStatusCode.Unauthorized);

        // … and identical ProblemDetails contract (type / title / status / detail).
        // The serialized body also contains a per-request `traceId` under
        // ProblemDetails.Extensions, which is expected to differ. Compare the
        // typed contract instead of raw bytes so the traceId noise is excluded.
        var wrongPasswordProblem = await wrongPasswordResponse.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        var unknownUserProblem = await unknownUserResponse.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        wrongPasswordProblem.Should().NotBeNull();
        unknownUserProblem.Should().NotBeNull();
        unknownUserProblem!.Type.Should().Be(wrongPasswordProblem!.Type);
        unknownUserProblem.Title.Should().Be(wrongPasswordProblem.Title);
        unknownUserProblem.Status.Should().Be(wrongPasswordProblem.Status);
        unknownUserProblem.Detail.Should().Be(wrongPasswordProblem.Detail);
        unknownUserProblem.Type.Should()
            .Be(InvalidCredentialsType, because: "the single InvalidCredentials helper must drive both branches");
    }

    /// <summary>
    /// Case 9 — GET <c>/me</c> returns 200 with the DB-backed row. Mutating
    /// the row in place and asserting the new email proves <c>Me()</c> reads
    /// from UserManager / DbContext rather than from cookie-baked claims.
    /// </summary>
    [Fact]
    public async Task Me_Authenticated_Returns_200_WithDbBackedEmail()
    {
        // Arrange
        var originalEmail = GenerateEmail();
        var (client, container) = CreateCookieClient(Factory);
        var registeredUserId = await RegisterAsync(client, container, originalEmail, StrongPassword);
        await LoginAsync(client, originalEmail, StrongPassword);

        // Mutate the row directly via the SUT's DbContext so the claim
        // (unchanged) and the DB (new email) disagree. A claim-reading Me()
        // would return the original email; a DB-reading Me() returns the new.
        var mutatedEmail = GenerateEmail();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
            var user = await db.Users.SingleAsync(
                u => u.Id == registeredUserId,
                TestContext.Current.CancellationToken);
            user.Email = mutatedEmail;
            user.NormalizedEmail = mutatedEmail.ToUpperInvariant();
            user.UserName = mutatedEmail;
            user.NormalizedUserName = mutatedEmail.ToUpperInvariant();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var response = await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.UserId.Should().Be(registeredUserId);
        body.Email.Should()
            .Be(mutatedEmail, because: "Me() must read the live DB row, not claims baked into the cookie");
    }

    /// <summary>
    /// Case 10 — GET <c>/me</c> without a session returns 401.
    /// <c>Accept: application/json</c> keeps Identity in the API 401 branch.
    /// </summary>
    [Fact]
    public async Task Me_Anonymous_Returns_401()
    {
        // Arrange
        var (client, _) = CreateCookieClient(Factory);

        // Act
        var response = await client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Case 11 — POST <c>/logout</c> authenticated with an antiforgery token
    /// returns 204 and clears <c>__Host-RunCoach</c> via an epoch <c>expires</c>
    /// or <c>max-age=0</c>.
    /// </summary>
    [Fact]
    public async Task Logout_Authenticated_Returns_204_AndClearsSessionCookie()
    {
        // Arrange
        var email = GenerateEmail();
        var (client, container) = CreateCookieClient(Factory);
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);

        // Act
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/logout", token);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var sessionClear = GetRawSetCookie(response, SessionCookieName);
        sessionClear.Should().NotBeNull($"logout must re-issue {SessionCookieName} to clear it");
        var isCleared =
            sessionClear!.Contains("expires=thu, 01 jan 1970", StringComparison.OrdinalIgnoreCase) ||
            sessionClear.Contains("max-age=0", StringComparison.OrdinalIgnoreCase);
        isCleared.Should().BeTrue(
            $"cookie-clear must use an epoch expires attribute or max-age=0, got: {sessionClear}");
    }

    /// <summary>Case 12 — POST <c>/logout</c> without a session returns 401.</summary>
    [Fact]
    public async Task Logout_Anonymous_Returns_401()
    {
        // Arrange
        var (client, _) = CreateCookieClient(Factory);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Case 13 — POST <c>/logout</c> authenticated but without an antiforgery
    /// token returns 400 (the ValidateAntiForgery filter's surfacing).
    /// </summary>
    [Fact]
    public async Task Logout_Authenticated_NoAntiforgery_Returns_400()
    {
        // Arrange
        var email = GenerateEmail();
        var (client, container) = CreateCookieClient(Factory);
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, email, StrongPassword);

        // Act — deliberately omit the X-XSRF-TOKEN header.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Helpers.
    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(RunCoachAppFactory factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;

        // Accept: application/json steers ASP.NET Core Identity's default
        // OnRedirectToLogin events into the 401 branch instead of redirecting.
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
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

    private static async Task<Guid> RegisterAsync(HttpClient client, CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequest(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should()
            .Be(HttpStatusCode.Created, because: $"register helper must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!.UserId;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should()
            .Be(HttpStatusCode.OK, because: $"login helper must succeed — got {(int)response.StatusCode}");
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

    private static string? GetRawSetCookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        foreach (var raw in values)
        {
            if (raw.StartsWith(name + "=", StringComparison.Ordinal))
            {
                return raw;
            }
        }

        return null;
    }

    private static DateTimeOffset? ParseExpires(string setCookie)
    {
        foreach (var segment in setCookie.Split(';', StringSplitOptions.TrimEntries))
        {
            if (segment.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))
            {
                var value = segment["expires=".Length..];
                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static string GenerateEmail() => $"user-{Guid.NewGuid():N}@example.test";
}
