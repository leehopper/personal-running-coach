using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Settings;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Settings;

/// <summary>
/// Integration tests for <c>GET</c>/<c>PUT /api/v1/settings/units</c> (Slice 4C-units /
/// DEC-086), one per scenario in <c>usersettings-store.feature</c>. Drives the live
/// HTTP + auth + antiforgery + persistence stack against the Testcontainers Postgres
/// and asserts the persisted <see cref="UserSettings"/> row. The preference is a
/// frontend-display-only choice — these endpoints add no server-side unit conversion.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class SettingsControllerIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string UnitsPath = "/api/v1/settings/units";

    private static readonly Uri BaseUri = new("https://localhost");

    [Fact]
    public async Task GetUnits_RowLessUser_ReturnsDefaultKilometers()
    {
        // Arrange — a freshly registered user who has never set a preference.
        var ct = TestContext.Current.CancellationToken;
        var (client, _, _) = await RegisterLoginAndPrimeAsync();

        // Act
        var response = await client.GetAsync(UnitsPath, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: "a row-less user reads a default, never a 404");
        var body = await response.Content.ReadFromJsonAsync<UnitPreferenceDto>(cancellationToken: ct);
        body.Should().NotBeNull();
        body!.PreferredUnits.Should().Be(PreferredUnits.Kilometers);
    }

    [Fact]
    public async Task PutThenGet_RoundTripsPreference_LastWriteWins()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, _, token) = await RegisterLoginAndPrimeAsync();

        // Act — set Miles, then read it back, then flip to Kilometers and read again.
        var putMiles = await PutUnitsAsync(client, token, PreferredUnits.Miles, ct);
        var afterMiles = await client.GetAsync(UnitsPath, ct);
        var putKm = await PutUnitsAsync(client, token, PreferredUnits.Kilometers, ct);
        var afterKm = await client.GetAsync(UnitsPath, ct);

        // Assert
        putMiles.StatusCode.Should().Be(HttpStatusCode.OK);
        (await afterMiles.Content.ReadFromJsonAsync<UnitPreferenceDto>(cancellationToken: ct))!
            .PreferredUnits.Should().Be(PreferredUnits.Miles);

        putKm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await afterKm.Content.ReadFromJsonAsync<UnitPreferenceDto>(cancellationToken: ct))!
            .PreferredUnits.Should().Be(
                PreferredUnits.Kilometers, because: "the second write wins — the row is last-write-wins");
    }

    [Fact]
    public async Task Units_ArePerUserScoped()
    {
        // Arrange — user A sets Miles; user B has never set a preference.
        var ct = TestContext.Current.CancellationToken;
        var (clientA, _, tokenA) = await RegisterLoginAndPrimeAsync();
        var (clientB, _, _) = await RegisterLoginAndPrimeAsync();
        (await PutUnitsAsync(clientA, tokenA, PreferredUnits.Miles, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var bUnits = await clientB.GetAsync(UnitsPath, ct);
        var aUnits = await clientA.GetAsync(UnitsPath, ct);

        // Assert
        (await bUnits.Content.ReadFromJsonAsync<UnitPreferenceDto>(cancellationToken: ct))!
            .PreferredUnits.Should().Be(PreferredUnits.Kilometers, because: "user B never set a preference");
        (await aUnits.Content.ReadFromJsonAsync<UnitPreferenceDto>(cancellationToken: ct))!
            .PreferredUnits.Should().Be(PreferredUnits.Miles, because: "user A's preference is unaffected by B");
    }

    [Fact]
    public async Task PutUnits_WithoutAntiforgeryToken_Rejected_NoRowPersisted()
    {
        // Arrange — authenticated, but no antiforgery token attached.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Put, UnitsPath)
        {
            Content = JsonContent.Create(new UnitPreferenceDto(PreferredUnits.Miles)),
        };
        var response = await client.SendAsync(request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var row = await GetSettingsRowAsync(userId, ct);
        row.Should().BeNull(because: "a request rejected for a missing antiforgery token must not persist a settings row");
    }

    [Fact]
    public async Task GetUnits_Unauthenticated_Returns401()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = CreateCookieClient(Factory);

        // Act
        var response = await client.GetAsync(UnitsPath, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutUnits_UndefinedEnumValue_Returns400_NoRowPersisted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);

        // Act — a numerically valid but undefined PreferredUnits value.
        using var request = BuildRequest(HttpMethod.Put, UnitsPath, token);
        request.Content = new StringContent("{\"preferredUnits\":5}", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var row = await GetSettingsRowAsync(userId, ct);
        row.Should().BeNull(because: "an undefined enum value is rejected at the boundary and never persisted");
    }

    private static async Task<HttpResponseMessage> PutUnitsAsync(
        HttpClient client, string antiforgeryToken, PreferredUnits units, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Put, UnitsPath, antiforgeryToken);
        request.Content = JsonContent.Create(new UnitPreferenceDto(units));
        return await client.SendAsync(request, ct);
    }

    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(RunCoachAppFactory factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
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

    private static string GenerateEmail() => $"settings-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, Guid UserId, string Token)> RegisterLoginAndPrimeAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);
        return (client, userId, token);
    }

    private async Task<UserSettings?> GetSettingsRowAsync(Guid userId, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        return await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct);
    }
}
