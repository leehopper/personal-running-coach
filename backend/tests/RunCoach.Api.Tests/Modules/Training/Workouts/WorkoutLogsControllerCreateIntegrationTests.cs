using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http;
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
/// Integration tests for <c>POST /api/v1/workouts/logs</c> (slice-2b Unit 3 / PR3),
/// one per scenario in <c>create-endpoint.feature</c>. Drives the live HTTP +
/// auth + antiforgery + persistence stack against the Testcontainers Postgres and
/// asserts the persisted <see cref="WorkoutLog"/> — in particular the
/// server-authoritative prescription snapshot and idempotency contract (DEC-076).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class WorkoutLogsControllerCreateIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string CreateLogPath = "/api/v1/workouts/logs";

    // 2026-06-07 is a Sunday — a valid PlanCalendar week-1/day-0 anchor.
    private static readonly DateOnly PlanStart = new(2026, 6, 7);

    // 2026-06-18 is a Thursday in plan week 2 (offset 11 days → week 2, day 4).
    private static readonly DateOnly Week2Day4 = new(2026, 6, 18);

    private static readonly Uri BaseUri = new("https://localhost");
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Create_MinimumPayload_Returns201_PersistsLog_WithNullPrescription()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();

        // Act
        var response = await PostLogAsync(client, token, MinimalRequest(Week2Day4), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body.Should().NotBeNull();
        body!.WorkoutLogId.Should().NotBeEmpty();

        var persisted = await GetByIdAsync(userId, body.WorkoutLogId, ct);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(userId);
        persisted.Prescription.Should().BeNull(because: "no active plan was seeded, so the run is off-plan");
    }

    [Fact]
    public async Task Create_RichPayload_PersistsNotesMetricsAndSplits()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var request = MinimalRequest(Week2Day4) with
        {
            Notes = "Negative split, felt strong on the back half.",
            Metrics = new Dictionary<string, JsonElement>
            {
                ["hrAvg"] = JsonSerializer.SerializeToElement(148),
                ["rpe"] = JsonSerializer.SerializeToElement(8),
            },
            Splits =
            [
                new WorkoutLogSplitDto(1, 1000.0, 300.0, 300.0, 150),
                new WorkoutLogSplitDto(2, 1000.0, 295.0, 295.0, 155),
            ],
        };

        // Act
        var response = await PostLogAsync(client, token, request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        body.Should().NotBeNull();

        var persisted = await GetByIdAsync(userId, body!.WorkoutLogId, ct);
        persisted.Should().NotBeNull();
        persisted!.Notes.Should().Be("Negative split, felt strong on the back half.");

        using var metrics = JsonDocument.Parse(persisted.Metrics!);
        metrics.RootElement.GetProperty("hrAvg").GetInt32().Should().Be(148);
        metrics.RootElement.GetProperty("rpe").GetInt32().Should().Be(8);

        persisted.Splits.Should().NotBeNull();
        persisted.Splits!.Should().HaveCount(2);
        persisted.Splits![0].Should().Be(new WorkoutSplit(1, 1000.0, 300.0, 300.0, 150));
        persisted.Splits![1].Should().Be(new WorkoutSplit(2, 1000.0, 295.0, 295.0, 155));
    }

    [Fact]
    public async Task Create_OnPlanRun_PersistsServerResolvedPrescription_NotClientValues()
    {
        // Arrange — an active plan whose week-2/day-4 slot prescribes a 10km/50min tempo.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, PlanStart, ct);

        // The client logs actuals that DIFFER from the plan (6km / 30min).
        var request = MinimalRequest(Week2Day4) with
        {
            DistanceMeters = 6000.0,
            DurationSeconds = 1800.0,
        };

        // Act
        var response = await PostLogAsync(client, token, request, ct);

        // Assert
        var raw = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        raw.Should().NotContainEquivalentOf(
            "vdot",
            because: "prescribed-pace data must use Daniels-Gilbert / pace-zone terminology only");
        var body = JsonSerializer.Deserialize<CreateWorkoutLogResponseDto>(raw, WebJson);
        body.Should().NotBeNull();

        var persisted = await GetByIdAsync(userId, body!.WorkoutLogId, ct);
        persisted.Should().NotBeNull();

        // The snapshot is resolved from the plan slot, NOT echoed from the client body.
        var prescription = persisted!.Prescription;
        prescription.Should().NotBeNull(because: "the run's date maps to the plan's week-2/day-4 slot");
        prescription!.SourcePlanId.Should().Be(planId);
        prescription.WeekNumber.Should().Be(2);
        prescription.DayOfWeek.Should().Be(4);
        prescription.WorkoutType.Should().Be(WorkoutType.Tempo);
        prescription.PrescribedDistance.Kilometers.Should().Be(10.0, because: "the plan prescribed 10km, not the client's 6km");
        prescription.PrescribedDuration.TotalMinutes.Should().Be(50.0, because: "the plan prescribed 50min, not the client's 30min");

        // The logged actuals remain the client's values, distinct from the snapshot.
        persisted.Distance.Kilometers.Should().Be(6.0);
        persisted.Duration.TotalMinutes.Should().Be(30.0);
    }

    [Fact]
    public async Task Create_OnPlanDate_WithNoMatchingSlot_PersistsOffPlan()
    {
        // Arrange — active plan, but log a date that maps to a slot with no workout.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var planId = Guid.NewGuid();
        await SeedActivePlanAsync(userId, planId, PlanStart, ct);

        // 2026-06-15 is a Monday → plan week 2, day 1 — the seeded week-2 micro has
        // a workout only at day 4, so day 1 has no matching WorkoutOutput slot.
        var request = MinimalRequest(new DateOnly(2026, 6, 15));

        // Act
        var response = await PostLogAsync(client, token, request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        var persisted = await GetByIdAsync(userId, body!.WorkoutLogId, ct);
        persisted!.Prescription.Should().BeNull(because: "no WorkoutOutput sits at the resolved (week 2, day 1) slot");
    }

    [Fact]
    public async Task Create_WithNoActivePlan_PersistsOffPlan()
    {
        // Arrange — a runner whose CurrentPlanId is null (no plan seeded).
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();

        // Act
        var response = await PostLogAsync(client, token, MinimalRequest(Week2Day4), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        var persisted = await GetByIdAsync(userId, body!.WorkoutLogId, ct);
        persisted!.Prescription.Should().BeNull();
    }

    [Fact]
    public async Task Create_ReplayedIdempotencyKey_ReturnsOriginalId_WithoutDuplicating()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var key = Guid.NewGuid();
        var request = MinimalRequest(Week2Day4, key);

        // Act — identical body sent twice with the same idempotency key.
        var first = await PostLogAsync(client, token, request, ct);
        var firstBody = await first.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);
        var second = await PostLogAsync(client, token, request, ct);
        var secondBody = await second.Content.ReadFromJsonAsync<CreateWorkoutLogResponseDto>(
            cancellationToken: ct);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        ((int)second.StatusCode).Should().BeOneOf(
            StatusCodes.Status200OK, StatusCodes.Status201Created);
        firstBody.Should().NotBeNull();
        secondBody.Should().NotBeNull();
        secondBody!.WorkoutLogId.Should().Be(firstBody!.WorkoutLogId, because: "a replay returns the original id");

        var logs = await GetByUserAsync(userId, ct);
        logs.Should().ContainSingle(because: "the replayed key must not create a second row")
            .Which.WorkoutLogId.Should().Be(firstBody.WorkoutLogId);
    }

    [Fact]
    public async Task Create_FailedAttempt_DoesNotPoisonKey_RetryWithSameKeySucceedsOnce()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId, token) = await RegisterLoginAndPrimeAsync();
        var key = Guid.NewGuid();

        // Act — first attempt fails (invalid negative distance); a failed attempt
        // must not record an idempotency marker that would poison the retry.
        var bad = MinimalRequest(Week2Day4, key) with { DistanceMeters = -1.0 };
        var failed = await PostLogAsync(client, token, bad, ct);

        // Retry the same key with a valid body.
        var retry = await PostLogAsync(client, token, MinimalRequest(Week2Day4, key), ct);

        // Assert
        ((int)failed.StatusCode).Should().BeGreaterThanOrEqualTo(
            400, because: "the invalid first attempt must not succeed");
        retry.StatusCode.Should().Be(HttpStatusCode.Created, because: "the same key with a valid body must still create the log");
        var logs = await GetByUserAsync(userId, ct);
        logs.Should().ContainSingle(because: "exactly one log is persisted across the failed + retried attempts");
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = CreateCookieClient(Factory);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, CreateLogPath)
        {
            Content = JsonContent.Create(MinimalRequest(Week2Day4)),
        };
        var response = await client.SendAsync(request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_AuthenticatedNoAntiforgery_Rejected_NoRecordCreated()
    {
        // Arrange — authenticated, but no antiforgery token is primed/attached.
        var ct = TestContext.Current.CancellationToken;
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, CreateLogPath)
        {
            Content = JsonContent.Create(MinimalRequest(Week2Day4)),
        };
        var response = await client.SendAsync(request, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var logs = await GetByUserAsync(userId, ct);
        logs.Should().BeEmpty(because: "a request rejected for a missing antiforgery token must not persist a log");
    }

    public override async ValueTask DisposeAsync()
    {
        // Plan projection docs + idempotency markers live in Marten's
        // runcoach_events schema, which Respawn skips — reset them explicitly so
        // seeded state never leaks into a sibling fixture.
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static CreateWorkoutLogRequestDto MinimalRequest(DateOnly occurredOn, Guid? key = null) =>
        new(
            IdempotencyKey: key ?? Guid.NewGuid(),
            OccurredOn: occurredOn,
            DistanceMeters: 5000.0,
            DurationSeconds: 1500.0,
            CompletionStatus: CompletionStatus.Complete,
            Notes: null,
            Metrics: null,
            Splits: null);

    private static async Task<HttpResponseMessage> PostLogAsync(
        HttpClient client, string antiforgeryToken, CreateWorkoutLogRequestDto dto, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Post, CreateLogPath, antiforgeryToken);
        request.Content = JsonContent.Create(dto);
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

    private static string GenerateEmail() => $"workoutlog-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, Guid UserId, string Token)> RegisterLoginAndPrimeAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        var token = await PrimeAntiforgeryAsync(client, container);
        return (client, userId, token);
    }

    private async Task SeedActivePlanAsync(Guid userId, Guid planId, DateOnly planStartDate, CancellationToken ct)
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

        // (2) Marten plan projection doc with a week-2/day-4 Tempo prescription.
        var tempo = new WorkoutOutput
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
            MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>
            {
                [2] = new MicroWorkoutListOutput { Workouts = [tempo] },
            },
        };

        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Store(plan);
        await session.SaveChangesAsync(ct);
    }

    private async Task<WorkoutLog?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByIdAsync(userId, id, ct);
    }

    private async Task<IReadOnlyList<WorkoutLog>> GetByUserAsync(Guid userId, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        return await repo.GetByUserAsync(userId, cursor: null, limit: 100, ct);
    }
}
