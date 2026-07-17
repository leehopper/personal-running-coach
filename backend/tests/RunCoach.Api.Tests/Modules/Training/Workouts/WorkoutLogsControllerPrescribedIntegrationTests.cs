using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Integration tests for <c>GET /api/v1/workouts/logs/prescribed</c>. Drives the
/// live HTTP + auth stack against the Testcontainers Postgres + Marten fixture
/// and asserts the full null-branch matrix the prescription resolver collapses
/// to a uniform 200 response for: on-plan (the required wire test), rest day,
/// before plan start, after plan end, no active plan, and an in-range week
/// absent from <see cref="PlanProjectionDto.MicroWorkoutsByWeek"/> (DEC-090
/// rolling-horizon — only week 1 ever gets micro workouts in production today).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class WorkoutLogsControllerPrescribedIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string PrescribedPath = "/api/v1/workouts/logs/prescribed";

    // 2026-06-07 is a Sunday — a valid PlanCalendar week-1/day-0 anchor (mirrors
    // WorkoutLogsControllerCreateIntegrationTests's anchor).
    private static readonly DateOnly PlanStart = new(2026, 6, 7);

    // 2026-06-18 is a Thursday, 11 days after PlanStart -> week 2, day 4 — the
    // slot the seeded plan's Tempo workout occupies.
    private static readonly DateOnly Week2Day4 = new(2026, 6, 18);

    private static readonly Uri BaseUri = new("https://localhost");
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetPrescribed_OnPlanRunDay_Returns200_WithPrescriptionMatchingPlanSlot()
    {
        // Arrange — an active plan whose week-2/day-4 slot prescribes a 10km/50min
        // Tempo run (280/330 sec/km fast/easy bounds).
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, PlanStart, DefaultWeek2Tempo(), ct);

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={Week2Day4:O}", ct);

        // Assert — the required wire test: assert the literal serialized JSON
        // property names (camelCase over the wire), not just the deserialized
        // object, so a serialization-contract regression is caught.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Object, because: "an on-plan day resolves a real prescription body");
        root.GetProperty("workoutType").GetString().Should().Be("Tempo");
        root.GetProperty("distanceMeters").GetDouble().Should().Be(10_000.0);
        root.GetProperty("durationSeconds").GetDouble().Should().Be(3_000.0);
        root.GetProperty("paceFastSecPerKm").GetDouble().Should().Be(280.0);
        root.GetProperty("paceEasySecPerKm").GetDouble().Should().Be(330.0);

        // Assert — the deserialized shape agrees, so a JsonPropertyName /
        // camelCase-policy mismatch can't hide behind a raw-string-only check.
        var typed = JsonSerializer.Deserialize<PrescribedWorkoutDto>(raw, WebJson);
        typed.Should().NotBeNull();
        typed!.WorkoutType.Should().Be("Tempo");
        typed.DistanceMeters.Should().Be(10_000.0);
        typed.DurationSeconds.Should().Be(3_000.0);
        typed.PaceFastSecPerKm.Should().Be(280.0);
        typed.PaceEasySecPerKm.Should().Be(330.0);
    }

    [Fact]
    public async Task GetPrescribed_RestDay_Returns200_WithNullBody()
    {
        // Arrange — the same active plan as the on-plan test, but a within-plan
        // date (week 2, day 1 — Monday) whose week-2 micro has no workout at that
        // day (only day 4 is populated). This is the "workout lookup misses on the
        // day" null branch, distinct from the "week key missing" branch below.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, PlanStart, DefaultWeek2Tempo(), ct);
        var restDay = new DateOnly(2026, 6, 15);

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={restDay:O}", ct);

        // Assert
        await AssertNullPrescriptionBodyAsync(response, ct);
    }

    [Fact]
    public async Task GetPrescribed_BeforePlanStart_Returns200_WithNullBody()
    {
        // Arrange — a date before the plan's start anchor so PlanCalendar.ResolveSlot
        // returns null through the service.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, PlanStart, DefaultWeek2Tempo(), ct);
        var beforeStart = PlanStart.AddDays(-7);

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={beforeStart:O}", ct);

        // Assert
        await AssertNullPrescriptionBodyAsync(response, ct);
    }

    [Fact]
    public async Task GetPrescribed_AfterPlanEnd_Returns200_WithNullBody()
    {
        // Arrange — a date beyond the seeded plan's Macro.TotalWeeks (16, via
        // StubPlanGenerationService.BuildMacro), so ResolveSlot's weekNumber >
        // weekCount branch returns null.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, PlanStart, DefaultWeek2Tempo(), ct);
        var afterEnd = PlanStart.AddDays(120); // week 18 of a 16-week plan.

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={afterEnd:O}", ct);

        // Assert
        await AssertNullPrescriptionBodyAsync(response, ct);
    }

    [Fact]
    public async Task GetPrescribed_NoActivePlan_Returns200_WithNullBody()
    {
        // Arrange — a runner whose RunnerOnboardingProfile.CurrentPlanId is null
        // (no plan seeded at all).
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync();

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={Week2Day4:O}", ct);

        // Assert
        await AssertNullPrescriptionBodyAsync(response, ct);
    }

    [Fact]
    public async Task GetPrescribed_InRangeWeekMissingFromMicroWorkouts_Returns200_WithNullBody()
    {
        // Arrange — DEC-090 rolling-horizon: only week 1 ever gets micro workouts
        // in production today. Seed a plan whose MicroWorkoutsByWeek carries only
        // week 1, then query a week-3 date: in-range per the plan's 16-week Macro
        // (ResolveSlot succeeds), but the dictionary has no entry for week 3 — the
        // sixth null branch (the resolver's MicroWorkoutsByWeek TryGetValue miss),
        // distinct from both the "no active plan" and "day has no workout" branches.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var planId = Guid.NewGuid();
        var week1Only = new Dictionary<int, MicroWorkoutListOutput>
        {
            [1] = new MicroWorkoutListOutput { Workouts = [BuildWorkout(dayOfWeek: 0)] },
        };
        await SeedActivePlanAsync(userId, planId, PlanStart, week1Only, ct);

        // 2026-06-21 is 14 days after PlanStart -> week 3, day 0 (Sunday).
        var week3Day0 = PlanStart.AddDays(14);

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={week3Day0:O}", ct);

        // Assert
        await AssertNullPrescriptionBodyAsync(response, ct);
    }

    [Fact]
    public async Task GetPrescribed_Unauthenticated_Returns401()
    {
        // Arrange — a client that never registered or logged in.
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = CreateCookieClient(Factory);

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date={Week2Day4:O}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPrescribed_MissingDate_Returns400()
    {
        // Arrange — an authenticated client whose request omits the `date` query
        // key entirely. Pins facet 1 of the confirmed defect: before
        // [BindRequired], ValueProviderResult.None made SimpleTypeModelBinder
        // return without a ModelState error, so `date` silently took
        // default(DateOnly) = 0001-01-01 and the endpoint answered 200+null —
        // indistinguishable from a legitimate off-plan day. [BindRequired] turns
        // the unset binding result into a ModelState error, which
        // [ApiController]'s automatic model-state validation surfaces as this
        // 400 before the action body ever runs.
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync();

        // Act — no query string at all.
        using var response = await client.GetAsync(PrescribedPath, ct);

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.BadRequest,
            because: "an omitted date must be an honest 400, never a silent DateOnly.MinValue");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: ct);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("date");
    }

    [Fact]
    public async Task GetPrescribed_MalformedDate_Returns400()
    {
        // Arrange — facet 2: a `date` value that fails TypeConverter conversion
        // (already produced a ModelState error and an automatic 400 before this
        // fix; what was missing was the declared [ProducesResponseType] contract,
        // now added alongside the [BindRequired] fix).
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync();

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date=not-a-date", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: ct);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("date");
    }

    [Fact]
    public async Task GetPrescribed_EmptyDate_Returns400()
    {
        // Arrange — `?date=` (present key, empty value) exercises a THIRD,
        // distinct SimpleTypeModelBinder branch from the two above: an empty
        // value short-circuits to a null model before any TypeConverter runs,
        // and — because DateOnly is a non-nullable value type — the binder
        // rejects the null with a "value must not be null" ModelState error
        // rather than the conversion-exception message the malformed case
        // produces. Kept as its own test rather than folded into the malformed
        // case because it pins that branch specifically.
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync();

        // Act
        using var response = await client.GetAsync($"{PrescribedPath}?date=", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken: ct);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("date");
    }

    public override async ValueTask DisposeAsync()
    {
        // Plan projection docs live in Marten's runcoach_events schema, which
        // Respawn skips — reset them explicitly so seeded state never leaks into
        // a sibling fixture (mirrors WorkoutLogsControllerCreateIntegrationTests).
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asserts the 200-with-literal-null-body contract: both the status code AND
    /// the body being a bare JSON <c>null</c>, not an empty object or empty
    /// string. There is no existing precedent in the repo for a 200+null
    /// response, so this is deliberately explicit and unambiguous rather than
    /// relying on <c>ReadFromJsonAsync</c> alone silently accepting either.
    /// </summary>
    private static async Task AssertNullPrescriptionBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: "absence is 200+null, never 404 (the /conversation/turns precedent)");
        var raw = (await response.Content.ReadAsStringAsync(ct)).Trim();
        raw.Should().Be("null", because: "the endpoint returns a literal JSON null body, not an empty object or empty string");
        var typed = JsonSerializer.Deserialize<PrescribedWorkoutDto?>(raw);
        typed.Should().BeNull();
    }

    private static Dictionary<int, MicroWorkoutListOutput> DefaultWeek2Tempo() =>
        new()
        {
            [2] = new MicroWorkoutListOutput { Workouts = [BuildTempoWorkout()] },
        };

    private static WorkoutOutput BuildTempoWorkout() => new()
    {
        DayOfWeek = 4,
        WorkoutType = WorkoutType.Tempo,
        Title = "Threshold Tempo",
        TargetDistanceKm = 10,
        TargetDurationMinutes = 50,
        TargetPaceEasySecPerKm = 330,
        TargetPaceFastSecPerKm = 280,
        Segments = [],
        WarmupNotes = "10 min easy.",
        CooldownNotes = "10 min easy.",
        CoachingNotes = "Hold threshold effort.",
        PerceivedEffort = 7,
    };

    private static WorkoutOutput BuildWorkout(int dayOfWeek) => new()
    {
        DayOfWeek = dayOfWeek,
        WorkoutType = WorkoutType.Easy,
        Title = "Easy shakeout",
        TargetDistanceKm = 5,
        TargetDurationMinutes = 30,
        TargetPaceEasySecPerKm = 360,
        TargetPaceFastSecPerKm = 330,
        Segments = [],
        WarmupNotes = "n/a",
        CooldownNotes = "n/a",
        CoachingNotes = "n/a",
        PerceivedEffort = 3,
    };

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
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
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
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, because: $"login helper must succeed — got {(int)response.StatusCode}");
    }

    private static Cookie? GetCookie(CookieContainer container, string name)
    {
        foreach (Cookie cookie in container.GetCookies(BaseUri))
        {
            if (string.Equals(cookie.Name, name, StringComparison.Ordinal))
            {
                return cookie;
            }
        }

        return null;
    }

    private static string GenerateEmail() => $"workoutlog-prescribed-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, Guid UserId)> RegisterAndLoginAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        return (client, userId);
    }

    private async Task SeedActivePlanAsync(
        Guid userId,
        Guid planId,
        DateOnly planStartDate,
        IReadOnlyDictionary<int, MicroWorkoutListOutput> microWorkoutsByWeek,
        CancellationToken ct)
    {
        // (1) EF profile carrying the active-plan pointer the endpoint reads.
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

        // (2) Marten plan projection doc — a direct document store, not an event
        // append, because ResolvePrescriptionAsync does a plain
        // session.LoadAsync<PlanProjectionDto>(planId, ct) document load.
        var plan = new PlanProjectionDto
        {
            PlanId = planId,
            UserId = userId,
            GeneratedAt = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero),
            PlanStartDate = planStartDate,
            PromptVersion = "coaching-v1",
            ModelId = "claude-sonnet-4-5",
            Macro = StubPlanGenerationService.BuildMacro("Seeded plan"),
            MesoWeeks = [],
            MicroWorkoutsByWeek = microWorkoutsByWeek,
        };

        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Store(plan);
        await session.SaveChangesAsync(ct);
    }
}
