using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Observability;
using RunCoach.Api.Modules.Observability.Controllers;
using RunCoach.Api.Modules.Observability.Models;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Observability;

/// <summary>
/// Integration matrix for <c>POST /api/v1/client-errors</c> (DEC-068 backend
/// half / R-073). Asserts the happy-path event append, the auth and shape
/// gates, the 64 KB payload cap, and the server-side 16 KB stack truncation.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ClientErrorsControllerIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;

    private static readonly Uri BaseUri = new("https://localhost");

    [Fact]
    public async Task Report_Anonymous_Returns_401()
    {
        // Arrange — no session cookie.
        var (client, _) = CreateCookieClient(Factory);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = JsonContent.Create(BuildValidRequest()),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — the cookie-or-bearer policy denies before any model
        // binding runs. No antiforgery dance — the endpoint does NOT
        // require XSRF per DEC-068.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Report_Authenticated_HappyPath_Returns_204_And_Persists_Event()
    {
        // Arrange — login, then POST a well-formed report. No antiforgery
        // token attached — DEC-068 says the endpoint does NOT require XSRF
        // because the boundary may not be able to read the SPA-readable
        // cookie reliably from a partially-rendered state.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var dto = BuildValidRequest();
        var store = Factory.Services.GetRequiredService<IDocumentStore>();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = JsonContent.Create(dto),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — 204 NoContent on success.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the stream landed under the runner's tenant and the
        // stream id is the boundary-generated correlation id.
        await using var session = store.LightweightSession(userId.ToString());
        var events = await session.Events.FetchStreamAsync(
            dto.CorrelationId,
            token: TestContext.Current.CancellationToken);
        events.Should().HaveCount(1, because: "the controller appends exactly one event per report");
        var stored = events[0].Data.Should().BeOfType<ClientErrorReported>().Subject;
        stored.CorrelationId.Should().Be(dto.CorrelationId);
        stored.Kind.Should().Be(ClientErrorKind.Render);
        stored.ErrorName.Should().Be(dto.ErrorName);
        stored.Message.Should().Be(dto.Message);
        stored.Stack.Should().Be(dto.Stack, because: "short stacks land unchanged");
        stored.ComponentStack.Should().Be(dto.ComponentStack);
        stored.Url.Should().Be(dto.Url);
        stored.UserAgent.Should().Be(dto.UserAgent);
        stored.AppVersion.Should().Be(dto.AppVersion);

        // Tenant column check — the row must be scoped to the runner's
        // tenant id so cross-tenant reads cannot surface the report.
        events[0].TenantId.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task Report_Authenticated_MalformedShape_Returns_400()
    {
        // Arrange — POST a body missing the required CorrelationId field.
        // JsonRequired surfaces the missing slot as a JsonException which
        // the framework translates to 400 ProblemDetails.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var malformedJson = """
            {
              "occurredAt": "2026-05-12T00:00:00Z",
              "kind": "render",
              "errorName": "TypeError",
              "message": "boom",
              "stack": "at boom()",
              "url": "https://app/",
              "userAgent": "Mozilla/5.0",
              "appVersion": "1.0.0"
            }
            """;

        using var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = content,
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Report_Authenticated_OutOfRangeNumericKind_Returns_400_With_InvalidKindProblem()
    {
        // Arrange — authenticate, then POST a well-formed body whose `kind`
        // is a numeric integer outside ClientErrorKind's declared range.
        // System.Text.Json's JsonStringEnumConverter rejects unknown wire
        // *strings* before model binding completes, but a numeric value
        // bypasses that converter and arrives as (ClientErrorKind)999.
        // The controller's Enum.IsDefined defense-in-depth check catches
        // the residual case and returns 400 with the InvalidKindType URI.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var rawJson = $$"""
            {
              "correlationId": "{{Guid.NewGuid()}}",
              "occurredAt": "2026-05-12T00:00:00Z",
              "kind": 999,
              "errorName": "TypeError",
              "message": "boom",
              "stack": "at boom()",
              "url": "https://app/",
              "userAgent": "Mozilla/5.0",
              "appVersion": "1.0.0"
            }
            """;

        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = content,
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var actualProblem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        actualProblem.Should().NotBeNull();
        actualProblem!.Type.Should().Be(
            "https://runcoach.app/problems/invalid-client-error-kind",
            because: "the controller's Enum.IsDefined gate must surface the canonical problem type URI");
    }

    [Fact]
    public async Task Report_BearerToken_With_Non_Guid_Sub_Returns_401_With_MissingUserClaimProblem()
    {
        // Arrange — present a validly-signed bearer token whose `sub` claim
        // is a non-Guid string. The framework accepts the token (signature,
        // issuer, audience all valid) so the request reaches the controller.
        // TryGetUserId then fails to parse the sub as a Guid and the
        // MissingUserClaim() branch returns 401 with the canonical URI.
        var expectedToken = MintBearerTokenWithRawSub("not-a-guid");
        using var client = CreateBearerClient(Factory, expectedToken);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = JsonContent.Create(BuildValidRequest()),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var actualProblem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        actualProblem.Should().NotBeNull();
        actualProblem!.Type.Should().Be(
            "https://runcoach.app/problems/missing-user-claim",
            because: "the controller's TryGetUserId branch must surface the canonical missing-user-claim URI");
    }

    [Fact]
    public async Task Report_Authenticated_PayloadOver64Kb_Returns_413()
    {
        // Arrange — craft a body whose declared Content-Length exceeds
        // the 64 KB cap. Padding the message field is the cleanest way to
        // grow the body deterministically past the limit. StringContent
        // (vs JsonContent.Create) sets an explicit Content-Length header
        // from the serialized byte array so the controller's pre-binding
        // Content-Length gate sees a concrete value rather than a chunked
        // stream's null length.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var oversizedDto = BuildValidRequest() with
        {
            Message = new string('a', ClientErrorsController.MaxRequestBodyBytes + 1024),
        };
        var serialized = JsonSerializer.Serialize(oversizedDto);
        using var content = new StringContent(serialized, Encoding.UTF8, "application/json");

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = content,
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — the controller's Content-Length gate rejects the body
        // before model binding runs.
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Report_Authenticated_StackOver16Kb_TruncatesServerSide()
    {
        // Arrange — Stack is bounded server-side at 16 KB measured in UTF-8
        // bytes. Use a multibyte character ('漢', 3 UTF-8 bytes each) so
        // 8 000 repetitions yield ~24 KB UTF-8, well above the cap. This
        // exercises the rune-walk truncation path for non-ASCII input that
        // would silently exceed the cap if byte counting were skipped.
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        var longStack = new string('漢', 8000);
        var dto = BuildValidRequest() with { Stack = longStack };
        var store = Factory.Services.GetRequiredService<IDocumentStore>();

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = JsonContent.Create(dto),
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — 204, but the stored stack is truncated.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var session = store.LightweightSession(userId.ToString());
        var events = await session.Events.FetchStreamAsync(
            dto.CorrelationId,
            token: TestContext.Current.CancellationToken);
        var stored = events[0].Data.Should().BeOfType<ClientErrorReported>().Subject;
        stored.Stack.Should().EndWith(
            ClientErrorsController.StackTruncationSuffix,
            because: "the truncation marker is visible to humans inspecting the row");
        Encoding.UTF8.GetByteCount(stored.Stack).Should().BeLessThanOrEqualTo(
            ClientErrorsController.MaxStackBytes,
            because: "the UTF-8 byte length of the stored stack must not exceed the documented cap");
    }

    [Fact]
    public async Task Report_Authenticated_NonSession_Tenants_Cannot_Read_Other_Tenant_Streams()
    {
        // Arrange — runner A reports an error; runner B attempts to
        // fetch the stream under runner B's tenant. With conjoined
        // tenancy, runner B's session must see an empty stream because
        // the row's tenant column is runner A's user id.
        var (clientA, containerA) = CreateCookieClient(Factory);
        var emailA = GenerateEmail();
        var userAId = await RegisterAsync(clientA, containerA, emailA, StrongPassword);
        await LoginAsync(clientA, containerA, emailA, StrongPassword);

        var dto = BuildValidRequest();
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client-errors")
        {
            Content = JsonContent.Create(dto),
        })
        {
            var response = await clientA.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var (clientB, containerB) = CreateCookieClient(Factory);
        var emailB = GenerateEmail();
        var userBId = await RegisterAsync(clientB, containerB, emailB, StrongPassword);
        await LoginAsync(clientB, containerB, emailB, StrongPassword);

        var store = Factory.Services.GetRequiredService<IDocumentStore>();

        // Act + Assert — runner B's tenant has no events for the
        // correlation id, and runner A's tenant has exactly one.
        await using (var bSession = store.LightweightSession(userBId.ToString()))
        {
            var bEvents = await bSession.Events.FetchStreamAsync(
                dto.CorrelationId,
                token: TestContext.Current.CancellationToken);
            bEvents.Should().BeEmpty(
                because: "Marten's conjoined tenancy column scopes the row to runner A");
        }

        await using (var aSession = store.LightweightSession(userAId.ToString()))
        {
            var aEvents = await aSession.Events.FetchStreamAsync(
                dto.CorrelationId,
                token: TestContext.Current.CancellationToken);
            aEvents.Should().ContainSingle();
        }
    }

    private static ClientErrorRequestDto BuildValidRequest() => new(
        CorrelationId: Guid.NewGuid(),
        OccurredAt: DateTimeOffset.UtcNow,
        Kind: ClientErrorKind.Render,
        ErrorName: "TypeError",
        Message: "Cannot read properties of undefined",
        Stack: "TypeError: Cannot read properties of undefined\n    at App (App.tsx:42)",
        ComponentStack: "\n    in App\n    in ErrorBoundary",
        Url: "https://app.example/",
        UserAgent: "Mozilla/5.0 (Macintosh; Intel Mac OS X)",
        AppVersion: "1.0.0");

    // ------------------------------------------------------------------ //
    // Helpers — same shape as OnboardingControllerIntegrationTests /
    // AuthControllerIntegrationTests. Duplicated locally per the codebase
    // convention that integration test classes carry their own auth setup
    // (each suite reaches for a different subset of cookie + antiforgery
    // operations and consolidating them into a shared base hides the per-
    // suite requirements).
    // ------------------------------------------------------------------ //
    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
    }

    private static HttpRequestMessage BuildAntiforgeryRequest(
        HttpMethod method, string path, string antiforgeryToken)
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
        using var request = BuildAntiforgeryRequest(HttpMethod.Post, "/api/v1/auth/register", token);
        request.Content = JsonContent.Create(new RegisterRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.Created,
            because: $"register helper must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!.UserId;
    }

    private static async Task LoginAsync(
        HttpClient client, CookieContainer container, string email, string password)
    {
        var token = await PrimeAntiforgeryAsync(client, container);
        using var request = BuildAntiforgeryRequest(HttpMethod.Post, "/api/v1/auth/login", token);
        request.Content = JsonContent.Create(new LoginRequestDto(email, password));
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: $"login helper must succeed — got {(int)response.StatusCode}");
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

    private static string GenerateEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static HttpClient CreateBearerClient(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string token)
    {
        var client = factory.CreateClient();
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string MintBearerTokenWithRawSub(string sub)
    {
        // Mints a validly-signed JWT with a raw (non-Guid) `sub` claim so the
        // framework's bearer middleware accepts the token while the controller's
        // TryGetUserId returns false. Mirrors the helper in
        // PlanRenderingControllerIntegrationTests.
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(RunCoachAppFactory.TestJwtSigningKey))
        {
            KeyId = RunCoachAppFactory.TestJwtKeyId,
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: RunCoachAppFactory.TestJwtIssuer,
            audience: RunCoachAppFactory.TestJwtAudience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, sub)],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
