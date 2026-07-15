using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Modules.Training.Workouts;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Integration coverage for the composed timeline endpoint
/// <c>GET /api/v1/conversation/timeline</c> (Slice 4B Unit 3, DEC-085). Drives the
/// real <see cref="RunCoachAppFactory"/> SUT so the CookieOrBearer gate, the
/// user-scoped interactive <see cref="ConversationView"/> load, the EF
/// <c>CurrentPlanId</c> resolve, and the plan-scoped <see cref="ConversationLogView"/>
/// union are exercised end-to-end. The read is a GET (no antiforgery), user-scoped via
/// the tenanted Marten session.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConversationTimelineControllerIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private static readonly Uri BaseUri = new("https://localhost");
    private static readonly DateTimeOffset PlanGeneratedAt = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetTimeline_Anonymous_Returns401()
    {
        // Arrange
        using var client = CreateAnonymousClient(Factory);

        // Act
        var response = await client.GetAsync("/api/v1/conversation/timeline", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTimeline_NoActivePlan_ReturnsInteractiveTurnsOnly_OldestFirst()
    {
        // Arrange — a runner with interactive turns but no active plan. The bearer GET
        // carries no antiforgery token (it is a read) and must still succeed.
        var userId = await SeedUserAsync();
        await SeedUserProfileAsync(userId, currentPlanId: null);
        await SeedInteractiveTurnsAsync(userId, "What's on for today?", "Easy 5, keep it conversational.");
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var body = await GetTimelineAsync(client);

        // Assert — both interactive turns, oldest-first, no proactive turns.
        body.Turns.Should().HaveCount(2);
        body.Turns.Should().BeInAscendingOrder(t => t.CreatedAt, because: "the chat composer renders oldest-first");
        body.Turns[0].Kind.Should().Be(ConversationTimelineTurnKind.User);
        body.Turns[0].Interactive!.Content.Should().Be("What's on for today?");
        body.Turns[0].Proactive.Should().BeNull();
        body.Turns[1].Kind.Should().Be(ConversationTimelineTurnKind.Coach);
        body.Turns[1].Interactive!.Content.Should().Be("Easy 5, keep it conversational.");
    }

    [Fact]
    public async Task GetTimeline_UnionsInteractiveAndProactiveTurns_OldestFirst()
    {
        // Arrange — interactive turns first, then a proactive adaptation on the plan
        // stream (a later, separate transaction → a later timestamp).
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        await SeedInteractiveTurnsAsync(userId, "Logged my long run, felt strong.", "Good — that earns Tuesday's tempo.");
        var adaptationLogId = Guid.NewGuid();
        await AppendProactiveAsync(userId, planId, new PlanAdaptedFromLog(
            adaptationLogId,
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Bumped Tuesday's tempo a notch.",
            PlanAdaptationDiff.Empty));
        await SeedUserProfileAsync(userId, currentPlanId: planId);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var body = await GetTimelineAsync(client);

        // Assert — three turns, oldest-first, unioning interactive + proactive.
        body.Turns.Should().HaveCount(3);
        body.Turns.Should().BeInAscendingOrder(t => t.CreatedAt);
        body.Turns[0].Kind.Should().Be(ConversationTimelineTurnKind.User);
        body.Turns[1].Kind.Should().Be(ConversationTimelineTurnKind.Coach);

        var proactiveTurn = body.Turns[2];
        proactiveTurn.Kind.Should().Be(ConversationTimelineTurnKind.Adaptation);
        proactiveTurn.Interactive.Should().BeNull();
        proactiveTurn.Proactive.Should().NotBeNull(because: "the proactive turn reuses the existing adaptation turn shape");
        proactiveTurn.Proactive!.Role.Should().Be(ConversationRole.AssistantAdaptation);
        proactiveTurn.Proactive.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        proactiveTurn.Proactive.TriggeringWorkoutLogId.Should().Be(adaptationLogId);
    }

    [Fact]
    public async Task GetTimeline_InteractiveTurns_SurvivePlanRegeneration()
    {
        // Arrange — interactive turns + a proactive adaptation under plan A.
        var userId = await SeedUserAsync();
        var planA = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planA);
        await SeedInteractiveTurnsAsync(userId, "Are we still on track for the half?", "Yes — the build's holding.");
        await AppendProactiveAsync(userId, planA, new PlanAdaptedFromLog(
            Guid.NewGuid(), AdaptationKind.Nudge, EscalationLevel.MicroAdjust, SafetyTier.Green, "A's nudge.", PlanAdaptationDiff.Empty));
        await SeedUserProfileAsync(userId, currentPlanId: planA);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        var before = await GetTimelineAsync(client);
        before.Turns.Should().HaveCount(3, because: "two interactive turns + one proactive turn under plan A");

        // Act — regenerate to plan B (a fresh plan-scoped log, no proactive turns).
        var planB = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planB);
        await UpdateCurrentPlanAsync(userId, planB);

        // Assert — the interactive turns survive; the plan A proactive turn is gone.
        var after = await GetTimelineAsync(client);
        after.Turns.Should().HaveCount(2, because: "the interactive conversation persists across plan regeneration; proactive turns reset");
        after.Turns.Should().OnlyContain(
            t => t.Kind == ConversationTimelineTurnKind.User || t.Kind == ConversationTimelineTurnKind.Coach,
            because: "only the user-scoped interactive turns remain after the plan reset");
    }

    [Fact]
    public async Task GetTimeline_EchoesLoggedRunOnConfirmAckTurn()
    {
        // Arrange — a confirmed conversational log: the durable-first user turn, then the
        // confirm-ack coach turn carrying a structured LoggedRun (Slice 3, DEC-091). This is the
        // ONLY test exercising the actual wire bytes the frontend's hand-typed TS interface trusts.
        var userId = await SeedUserAsync();
        await SeedUserProfileAsync(userId, currentPlanId: null);
        var expectedWorkoutLogId = Guid.NewGuid();
        var expectedLoggedRun = new LoggedRunSummary(
            WorkoutLogId: expectedWorkoutLogId,
            DistanceKm: 9.2,
            DurationSeconds: 2460d,
            OccurredOn: new DateOnly(2026, 7, 8),
            CompletionStatus: CompletionStatus.Complete);
        await SeedConfirmAckTurnAsync(userId, "Logged your run. Nice work.", expectedLoggedRun);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act — read the raw JSON so the actual wire shape (camelCase property names, the
        // DateOnly ISO YYYY-MM-DD serialization) is verified directly, not just the deserialized
        // C# DTO every other test (and the frontend's hand-typed TS interface) merely trusts.
        var response = await client.GetAsync("/api/v1/conversation/timeline", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var actualDocument = JsonDocument.Parse(actualJson);
        var actualTurns = actualDocument.RootElement.GetProperty("turns");
        actualTurns.GetArrayLength().Should().Be(2);
        var actualAckTurnJson = actualTurns[1];
        actualAckTurnJson.GetProperty("kind").GetInt32().Should().Be((int)ConversationTimelineTurnKind.Coach);

        var actualLoggedRunJson = actualAckTurnJson.GetProperty("interactive").GetProperty("loggedRun");
        actualLoggedRunJson.GetProperty("workoutLogId").GetGuid().Should().Be(expectedWorkoutLogId);
        actualLoggedRunJson.GetProperty("distanceKm").GetDouble().Should().Be(9.2);
        actualLoggedRunJson.GetProperty("durationSeconds").GetDouble().Should().Be(2460d);
        actualLoggedRunJson.GetProperty("occurredOn").GetString().Should().Be(
            "2026-07-08", because: "DateOnly serializes as an ISO YYYY-MM-DD string on the wire");
        actualLoggedRunJson.GetProperty("completionStatus").GetInt32().Should().Be((int)CompletionStatus.Complete);

        // Cross-check the deserialized DTO — the shape the frontend's hand-typed TS interface trusts.
        var actualBody = JsonSerializer.Deserialize<ConversationTimelineDto>(actualJson, WebOptions);
        actualBody!.Turns[1].Interactive!.LoggedRun.Should().BeEquivalentTo(
            new LoggedRunSummaryDto(expectedWorkoutLogId, 9.2, 2460d, new DateOnly(2026, 7, 8), CompletionStatus.Complete));
    }

    [Fact]
    public async Task GetTimeline_OtherRunnersInteractiveTurns_NotVisible()
    {
        // Arrange — runner A has interactive turns; runner B (the caller) has none.
        // Conjoined tenancy keys the interactive ConversationView by user id, so B's
        // tenanted session can never load A's conversation.
        var userAId = await SeedUserAsync();
        await SeedInteractiveTurnsAsync(userAId, "A's question", "A's answer");
        await SeedUserProfileAsync(userAId, currentPlanId: null);

        var userBId = await SeedUserAsync();
        await SeedUserProfileAsync(userBId, currentPlanId: null);
        using var client = CreateBearerClient(Factory, MintBearerToken(userBId));

        // Act
        var body = await GetTimelineAsync(client);

        // Assert
        body.Turns.Should().BeEmpty(because: "the timeline is user-scoped — B never sees A's interactive turns");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static async Task<ConversationTimelineDto> GetTimelineAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/conversation/timeline", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConversationTimelineDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        return body!;
    }

    private static HttpClient CreateAnonymousClient(RunCoachAppFactory factory)
    {
        var client = factory.CreateClient();
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static HttpClient CreateBearerClient(RunCoachAppFactory factory, string token)
    {
        var client = CreateAnonymousClient(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string MintBearerToken(Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(RunCoachAppFactory.TestJwtSigningKey))
        {
            KeyId = RunCoachAppFactory.TestJwtKeyId,
        };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: RunCoachAppFactory.TestJwtIssuer,
            audience: RunCoachAppFactory.TestJwtAudience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"timeline-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, "Str0ngTestPassw0rd!");
        result.Succeeded.Should()
            .BeTrue(because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }

    private async Task SeedUserProfileAsync(Guid userId, Guid? currentPlanId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.RunnerOnboardingProfiles.Add(new RunnerOnboardingProfile
        {
            UserId = userId,
            CurrentPlanId = currentPlanId,
            CreatedOn = now,
            ModifiedOn = now,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task UpdateCurrentPlanAsync(Guid userId, Guid currentPlanId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();

        // Call EF Core's SingleAsync via the explicit class — both Marten and EF Core
        // expose a SingleAsync extension, so an unqualified call is ambiguous.
        var profile = await EntityFrameworkQueryableExtensions.SingleAsync(
            db.RunnerOnboardingProfiles.Where(p => p.UserId == userId),
            TestContext.Current.CancellationToken);
        profile.CurrentPlanId = currentPlanId;
        profile.ModifiedOn = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedInteractiveTurnsAsync(Guid userId, string userMessage, string coachMessage)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        // Durable-first user turn (creates the stream), then the coach turn — separate
        // transactions, mirroring the two-write persistence so timestamps are distinct.
        await using (var session = store.LightweightSession(userId.ToString()))
        {
            session.Events.StartStream<ConversationView>(userId, new UserMessagePosted(userId, Guid.NewGuid(), userMessage));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = store.LightweightSession(userId.ToString()))
        {
            session.Events.Append(userId, new CoachMessagePosted(userId, Guid.NewGuid(), coachMessage, false, LoggedRun: null));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task SeedConfirmAckTurnAsync(Guid userId, string coachMessage, LoggedRunSummary loggedRun)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        // Durable-first user turn (creates the stream), then the confirm-ack coach turn carrying
        // the structured LoggedRun — separate transactions, mirroring the real two-write flow.
        await using (var session = store.LightweightSession(userId.ToString()))
        {
            session.Events.StartStream<ConversationView>(
                userId, new UserMessagePosted(userId, Guid.NewGuid(), "Did my 9.2k this morning."));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = store.LightweightSession(userId.ToString()))
        {
            session.Events.Append(userId, new CoachMessagePosted(userId, Guid.NewGuid(), coachMessage, false, loggedRun));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task SeedPlanStreamAsync(Guid userId, Guid planId)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var sequence = StubPlanGenerationService.BuildCanonicalSequence(
            planId, userId, goal: "Half Marathon", PlanGeneratedAt, previousPlanId: null);
        session.Events.StartStream<PlanProjectionDto>(planId, [.. sequence.ToEvents()]);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AppendProactiveAsync(Guid userId, Guid planId, params object[] events)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.Append(planId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
