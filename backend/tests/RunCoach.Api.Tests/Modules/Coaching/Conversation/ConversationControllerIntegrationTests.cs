using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Identity;
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
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Coaching.Conversation;

/// <summary>
/// Integration coverage for the read-only conversation endpoint
/// <c>GET /api/v1/conversation/turns</c> (Slice 3 Unit 2, DEC-079). Drives the real
/// <see cref="RunCoachAppFactory"/> SUT so the CookieOrBearer gate, the EF
/// <c>RunnerOnboardingProfile.CurrentPlanId</c> resolve, and the tenanted Marten
/// <see cref="ConversationLogView"/> load are exercised end-to-end. Bearer auth is
/// used throughout (the cookie/antiforgery pipeline is covered elsewhere). The
/// endpoint ships returning empty until PR5 wires production appends, so these tests
/// seed the events directly to prove the read path.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class ConversationControllerIntegrationTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private static readonly Uri BaseUri = new("https://localhost");
    private static readonly DateTimeOffset PlanGeneratedAt = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetTurns_Anonymous_Returns401()
    {
        // Arrange
        using var client = CreateAnonymousClient(Factory);

        // Act
        var response = await client.GetAsync("/api/v1/conversation/turns", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTurns_NoActivePlan_Returns200_WithEmptyTurns()
    {
        // Arrange — a runner whose profile carries no current plan id yet.
        var userId = await SeedUserAsync();
        await SeedUserProfileAsync(userId, currentPlanId: null);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync("/api/v1/conversation/turns", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConversationTurnsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Turns.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTurns_PlanWithNoAdaptations_Returns200_WithEmptyTurns()
    {
        // Arrange — an active plan exists but no adaptation/safety events appended.
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        await SeedUserProfileAsync(userId, currentPlanId: planId);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync("/api/v1/conversation/turns", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConversationTurnsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Turns.Should().BeEmpty(because: "the conversation log exists but no turns have been appended");
    }

    [Fact]
    public async Task GetTurns_WithAdaptationAndSafety_Returns200_TurnsNewestFirst()
    {
        // Arrange — active plan + one adaptation then one safety signal (two
        // separate appends so their Marten timestamps are distinct and ordered).
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        var adaptationLogId = Guid.NewGuid();
        var safetyLogId = Guid.NewGuid();
        await AppendAsync(userId, planId, new PlanAdaptedFromLog(
            adaptationLogId,
            AdaptationKind.Nudge,
            EscalationLevel.MicroAdjust,
            SafetyTier.Green,
            "Shuffled your easy run so the week still works.",
            PlanAdaptationDiff.Empty));
        await AppendAsync(userId, planId, new SafetySignalRaised(
            safetyLogId,
            SafetyTier.Amber,
            ReferralCategory.Injury,
            "Let's ease back and have that niggle looked at by a physio."));
        await SeedUserProfileAsync(userId, currentPlanId: planId);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync("/api/v1/conversation/turns", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConversationTurnsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Turns.Should().HaveCount(2);
        body.Turns.Should().BeInDescendingOrder(t => t.CreatedAt, because: "the panel renders newest-first");

        var newest = body.Turns[0];
        newest.Role.Should().Be(ConversationRole.SystemSafety, because: "the safety signal was appended last");
        newest.SafetyTier.Should().Be(SafetyTier.Amber);
        newest.ReferralCategory.Should().Be(ReferralCategory.Injury);
        newest.TriggeringWorkoutLogId.Should().Be(safetyLogId);
        newest.EscalationLevel.Should().BeNull(because: "a safety turn carries no escalation level");
        newest.AdaptationKind.Should().BeNull(because: "a safety turn is not an adaptation");
        newest.Diff.Should().BeNull(because: "a safety turn carries no plan diff");

        var oldest = body.Turns[1];
        oldest.Role.Should().Be(ConversationRole.AssistantAdaptation);
        oldest.AdaptationKind.Should().Be(AdaptationKind.Nudge);
        oldest.EscalationLevel.Should().Be(EscalationLevel.MicroAdjust);
        oldest.TriggeringWorkoutLogId.Should().Be(adaptationLogId);
    }

    [Fact]
    public async Task GetTurns_OtherRunnersPlan_Returns200_WithEmptyTurns()
    {
        // Arrange — runner A has turns; runner B (the caller) has no active plan.
        var userAId = await SeedUserAsync();
        var planAId = Guid.NewGuid();
        await SeedPlanStreamAsync(userAId, planAId);
        await AppendAsync(userAId, planAId, new PlanAdaptedFromLog(
            Guid.NewGuid(), AdaptationKind.Nudge, EscalationLevel.MicroAdjust, SafetyTier.Green, "A's nudge.", PlanAdaptationDiff.Empty));
        await SeedUserProfileAsync(userAId, currentPlanId: planAId);

        var userBId = await SeedUserAsync();
        await SeedUserProfileAsync(userBId, currentPlanId: null);
        using var client = CreateBearerClient(Factory, MintBearerToken(userBId));

        // Act
        var response = await client.GetAsync("/api/v1/conversation/turns", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ConversationTurnsResponseDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        body!.Turns.Should().BeEmpty(because: "the caller has no active plan, so no turns are visible to them");
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
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
        var email = $"conversation-{Guid.NewGuid():N}@example.test";
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

    private async Task AppendAsync(Guid userId, Guid planId, object @event)
    {
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        session.Events.Append(planId, @event);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
