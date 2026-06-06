using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Integration tests for <c>POST /api/v1/workouts/logs/query</c> (slice-2b Unit 4 /
/// PR4), one per scenario in <c>history-query-endpoint.feature</c>. Drives the live
/// HTTP + auth + DB-driven keyset query against the Testcontainers Postgres and
/// asserts newest-first ordering, keyset paging across a page boundary, the
/// exact-page-multiple cursor termination, the read projection of a rich log
/// (notes/metrics/splits), user scoping, and that the sort/limit execute in SQL
/// (DEC-076 § C).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class WorkoutLogsControllerQueryIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryCookieName = AuthCookieNames.Antiforgery;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string QueryLogPath = "/api/v1/workouts/logs/query";

    private static readonly Uri BaseUri = new("https://localhost");

    [Fact]
    public async Task Query_NoCursor_ReturnsLogsNewestFirst()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        await SeedLogAsync(userId, new DateOnly(2026, 6, 1), ct);
        await SeedLogAsync(userId, new DateOnly(2026, 6, 8), ct);
        await SeedLogAsync(userId, new DateOnly(2026, 6, 15), ct);

        // Act
        var page = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: null, Cursor: null), ct);

        // Assert
        page.Logs.Should().HaveCount(3);
        page.Logs.Select(l => l.OccurredOn).Should().ContainInOrder(
            new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 1));
        page.NextCursor.Should().BeNull(because: "three logs is fewer than the default page size");
    }

    [Fact]
    public async Task Query_KeysetPaging_CrossesPageBoundary_WithoutSkipsOrDuplicates()
    {
        // Arrange — five logs on distinct dates.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var seededIds = new List<Guid>();
        foreach (var day in new[] { 1, 2, 3, 4, 5 })
        {
            seededIds.Add(await SeedLogAsync(userId, new DateOnly(2026, 6, day), ct));
        }

        // Act — three pages of two, each using the prior page's cursor.
        var page1 = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: 2, Cursor: null), ct);
        var page2 = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: 2, Cursor: page1.NextCursor), ct);
        var page3 = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: 2, Cursor: page2.NextCursor), ct);

        // Assert — page sizes and cursor termination.
        page1.Logs.Should().HaveCount(2);
        page2.Logs.Should().HaveCount(2);
        page3.Logs.Should().HaveCount(1);
        page1.NextCursor.Should().NotBeNull();
        page2.NextCursor.Should().NotBeNull();
        page3.NextCursor.Should().BeNull(because: "the final short page exhausts the history");

        // Assert — the three pages together cover all five logs exactly once, newest-first throughout.
        var returned = page1.Logs.Concat(page2.Logs).Concat(page3.Logs).ToList();
        returned.Select(l => l.WorkoutLogId).Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(seededIds);
        returned.Select(l => l.OccurredOn).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Query_ExactPageMultiple_TerminatesWithEmptyTrailingPage()
    {
        // Arrange — four logs at limit 2: the total is an exact multiple of the page size,
        // so the final full page cannot tell it is last without one more (empty) fetch.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        var seededIds = new List<Guid>();
        foreach (var day in new[] { 1, 2, 3, 4 })
        {
            seededIds.Add(await SeedLogAsync(userId, new DateOnly(2026, 6, day), ct));
        }

        // Act — two full pages, then a terminal fetch on the second page's cursor.
        var page1 = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: 2, Cursor: null), ct);
        var page2 = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: 2, Cursor: page1.NextCursor), ct);
        var page3 = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: 2, Cursor: page2.NextCursor), ct);

        // Assert — the second full page still hands back a cursor (row count == limit); the
        // terminal fetch returns zero rows and a null cursor.
        page1.Logs.Should().HaveCount(2);
        page2.Logs.Should().HaveCount(2);
        page1.NextCursor.Should().NotBeNull();
        page2.NextCursor.Should().NotBeNull(
            because: "a full final page cannot know it is the last without one more fetch");
        page3.Logs.Should().BeEmpty();
        page3.NextCursor.Should().BeNull();

        // Assert — the two full pages cover all four logs exactly once; the empty terminal
        // page skips and duplicates nothing.
        var returned = page1.Logs.Concat(page2.Logs).ToList();
        returned.Select(l => l.WorkoutLogId).Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(seededIds);
        returned.Select(l => l.OccurredOn).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Query_RichLog_EchoesNotesMetricsAndSplitsThroughTheReadProjection()
    {
        // Arrange — one log carrying every optional field: a freeform note, a metrics jsonb
        // bag, and two typed splits. Exercises MapToDto / DeserializeMetrics / MapSplitsToDto,
        // which the empty-seed scenarios never reach.
        var ct = TestContext.Current.CancellationToken;
        var (client, userId) = await RegisterAndLoginAsync();
        const string notes = "Negative split, felt strong on the back half.";
        var expectedSplits = new[]
        {
            new WorkoutLogSplitDto(1, 1000.0, 300.0, 300.0, 138),
            new WorkoutLogSplitDto(2, 1000.0, 295.0, 295.0, 145),
        };
        var logId = await SeedRichLogAsync(
            userId,
            new DateOnly(2026, 6, 10),
            notes,
            """{"hrAvg":142,"rpe":7}""",
            [
                new WorkoutSplit(1, 1000.0, 300.0, 300.0, 138),
                new WorkoutSplit(2, 1000.0, 295.0, 295.0, 145),
            ],
            ct);

        // Act
        var page = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: null, Cursor: null), ct);

        // Assert — the read projection faithfully rehydrates every optional field.
        var dto = page.Logs.Should().ContainSingle().Which;
        dto.WorkoutLogId.Should().Be(logId);
        dto.Notes.Should().Be(notes);

        dto.Metrics.Should().NotBeNull();
        dto.Metrics!["hrAvg"].GetInt32().Should().Be(142);
        dto.Metrics["rpe"].GetInt32().Should().Be(7);

        dto.Splits.Should().NotBeNull();
        dto.Splits!.Should().Equal(expectedSplits);
    }

    [Fact]
    public async Task Query_EmptyHistory_ReturnsNoLogsAndNullCursor()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync();

        // Act
        var page = await QueryPageAsync(client, new QueryWorkoutLogsRequestDto(Limit: null, Cursor: null), ct);

        // Assert
        page.Logs.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Query_ReturnsOnlyTheAuthenticatedRunnersOwnLogs()
    {
        // Arrange — runner A with two logs, runner B with three (overlapping dates).
        var ct = TestContext.Current.CancellationToken;
        var (clientA, userA) = await RegisterAndLoginAsync();
        var (_, userB) = await RegisterAndLoginAsync();
        var a1 = await SeedLogAsync(userA, new DateOnly(2026, 6, 1), ct);
        var a2 = await SeedLogAsync(userA, new DateOnly(2026, 6, 2), ct);
        await SeedLogAsync(userB, new DateOnly(2026, 6, 1), ct);
        await SeedLogAsync(userB, new DateOnly(2026, 6, 2), ct);
        await SeedLogAsync(userB, new DateOnly(2026, 6, 3), ct);

        // Act
        var page = await QueryPageAsync(clientA, new QueryWorkoutLogsRequestDto(Limit: null, Cursor: null), ct);

        // Assert — only A's two logs, never B's.
        page.Logs.Select(l => l.WorkoutLogId).Should().BeEquivalentTo(new[] { a1, a2 });
    }

    [Fact]
    public async Task Query_OrderingAndLimit_ExecuteInSql_NotInTheApplicationLayer()
    {
        // Arrange — three logs; capture the SQL the repository the endpoint delegates to emits.
        var ct = TestContext.Current.CancellationToken;
        var userId = await SeedUserAsync();
        await SeedLogAsync(userId, new DateOnly(2026, 6, 1), ct);
        await SeedLogAsync(userId, new DateOnly(2026, 6, 8), ct);
        await SeedLogAsync(userId, new DateOnly(2026, 6, 15), ct);

        var capturedSql = new List<string>();
        var options = new DbContextOptionsBuilder<RunCoachDbContext>()
            .UseNpgsql(Factory.ConnectionString)
            .LogTo(capturedSql.Add, new[] { RelationalEventId.CommandExecuted })
            .Options;

        // Act — the exact GetByUserAsync the query service calls, with a limit below the row count.
        await using var db = new RunCoachDbContext(options);
        var repository = new WorkoutLogRepository(db, NullLogger<WorkoutLogRepository>.Instance);
        var page = await repository.GetByUserAsync(userId, cursor: null, limit: 2, ct);

        // Assert — the DB returned only two rows, newest-first: it did the trimming and sorting.
        page.Should().HaveCount(2);
        page.Select(w => w.OccurredOn).Should().ContainInOrder(
            new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 8));

        var selectSql = capturedSql.Should().Contain(
            sql => sql.Contains("\"WorkoutLog\"", StringComparison.Ordinal)
                && sql.Contains("ORDER BY", StringComparison.Ordinal)).Which;
        selectSql.Should().Contain("\"OccurredOn\" DESC")
            .And.Contain("\"WorkoutLogId\" DESC")
            .And.Contain("LIMIT");
    }

    [Fact]
    public async Task Query_MalformedCursor_Returns400()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await RegisterAndLoginAsync();

        // Act
        using var response = await PostQueryAsync(
            client, new QueryWorkoutLogsRequestDto(Limit: null, Cursor: "not-a-valid-cursor!!"), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Query_Unauthenticated_Returns401()
    {
        // Arrange — a client that never registered or logged in.
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = CreateCookieClient(Factory);

        // Act
        using var response = await PostQueryAsync(
            client, new QueryWorkoutLogsRequestDto(Limit: null, Cursor: null), ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<QueryWorkoutLogsResponseDto> QueryPageAsync(
        HttpClient client, QueryWorkoutLogsRequestDto request, CancellationToken ct)
    {
        using var response = await PostQueryAsync(client, request, ct);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK, because: $"query must succeed — got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<QueryWorkoutLogsResponseDto>(cancellationToken: ct);
        body.Should().NotBeNull();
        return body!;
    }

    private static async Task<HttpResponseMessage> PostQueryAsync(
        HttpClient client, QueryWorkoutLogsRequestDto dto, CancellationToken ct)
    {
        // A read: no antiforgery token (DEC-055). The auth cookie rides on the handler.
        using var request = new HttpRequestMessage(HttpMethod.Post, QueryLogPath)
        {
            Content = JsonContent.Create(dto),
        };
        return await client.SendAsync(request, ct);
    }

    private static WorkoutLog NewLog(Guid userId, DateOnly occurredOn)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkoutLog
        {
            WorkoutLogId = Guid.NewGuid(),
            UserId = userId,
            TenantId = userId.ToString(),
            IdempotencyKey = Guid.NewGuid(),
            OccurredOn = occurredOn,
            Distance = Distance.FromMeters(5000.0),
            Duration = Duration.FromMinutes(25.0),
            CompletionStatus = CompletionStatus.Complete,
            CreatedOn = now,
            ModifiedOn = now,
        };
    }

    private static (HttpClient Client, CookieContainer Container) CreateCookieClient(RunCoachAppFactory factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return (client, container);
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

    private static HttpRequestMessage BuildRequest(HttpMethod method, string path, string antiforgeryToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        return request;
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

    private static string GenerateEmail() => $"workoutlog-query-{Guid.NewGuid():N}@example.test";

    private async Task<Guid> SeedLogAsync(Guid userId, DateOnly occurredOn, CancellationToken ct)
    {
        var log = NewLog(userId, occurredOn);
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        await repository.CreateAsync(log, ct);
        return log.WorkoutLogId;
    }

    private async Task<Guid> SeedRichLogAsync(
        Guid userId,
        DateOnly occurredOn,
        string notes,
        string metricsJson,
        IReadOnlyList<WorkoutSplit> splits,
        CancellationToken ct)
    {
        var log = NewLog(userId, occurredOn);
        log.Notes = notes;
        log.Metrics = metricsJson;
        log.Splits = [.. splits];
        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkoutLogRepository>();
        await repository.CreateAsync(log, ct);
        return log.WorkoutLogId;
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = GenerateEmail();
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, StrongPassword);
        result.Succeeded.Should().BeTrue(
            because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterAndLoginAsync()
    {
        var (client, container) = CreateCookieClient(Factory);
        var email = GenerateEmail();
        var userId = await RegisterAsync(client, container, email, StrongPassword);
        await LoginAsync(client, container, email, StrongPassword);
        return (client, userId);
    }
}
