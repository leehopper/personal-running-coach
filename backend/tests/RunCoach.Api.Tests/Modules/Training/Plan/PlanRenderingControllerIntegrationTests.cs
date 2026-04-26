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
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Integration coverage for <see cref="PlanRenderingController.GetCurrent"/>.
/// Drives the real <see cref="RunCoachAppFactory"/> SUT against the shared
/// Testcontainers Postgres so the Marten projection lookup, EF
/// <c>UserProfile.CurrentPlanId</c> read, and <c>CookieOrBearer</c>
/// authorization gate are all exercised end-to-end. Bearer auth is used for
/// every case so the tests do not also depend on the antiforgery / cookie
/// pipeline already covered by <see cref="Modules.Identity.AuthControllerIntegrationTests"/>.
/// </summary>
[Trait("Category", "Integration")]
public class PlanRenderingControllerIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    private static readonly Uri BaseUri = new("https://localhost");

    private static readonly DateTimeOffset PlanGeneratedAt = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Anonymous request to <c>/api/v1/plan/current</c> returns 401. The
    /// <see cref="AuthPolicies.CookieOrBearer"/> policy must reject callers
    /// that present neither a session cookie nor a bearer token.
    /// </summary>
    [Fact]
    public async Task GetCurrent_Anonymous_Returns_401()
    {
        // Arrange
        using var client = CreateAnonymousClient(Factory);

        // Act
        var response = await client.GetAsync(
            "/api/v1/plan/current",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// When the runner has a registered account but no <c>UserProfile</c> row
    /// (e.g. has not started onboarding), the endpoint returns 404 — the
    /// projection-driven <c>CurrentPlanId</c> is unset because the EF
    /// projection has not run yet.
    /// </summary>
    [Fact]
    public async Task GetCurrent_NoUserProfile_Returns_404()
    {
        // Arrange
        var userId = await SeedUserAsync();
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync(
            "/api/v1/plan/current",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// When the runner has a <c>UserProfile</c> but <c>CurrentPlanId</c> is
    /// null (mid-onboarding, before <c>PlanLinkedToUser</c> applied), the
    /// endpoint returns 404 — there is no plan to render yet.
    /// </summary>
    [Fact]
    public async Task GetCurrent_UserProfile_With_Null_CurrentPlanId_Returns_404()
    {
        // Arrange
        var userId = await SeedUserAsync();
        await SeedUserProfileAsync(userId, currentPlanId: null);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync(
            "/api/v1/plan/current",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Happy path: runner has a <c>UserProfile</c> with <c>CurrentPlanId</c>
    /// pointing at an existing Plan stream that has been projected to a
    /// <see cref="PlanProjectionDto"/>. The controller returns 200 with the
    /// canonical Slice 1 shape (macro present, four meso weeks, week-1 micro
    /// detail).
    /// </summary>
    [Fact]
    public async Task GetCurrent_UserProfile_With_Valid_PlanId_Returns_200_With_PlanProjection()
    {
        // Arrange
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        await SeedUserProfileAsync(userId, currentPlanId: planId);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync(
            "/api/v1/plan/current",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actual = await response.Content.ReadFromJsonAsync<PlanProjectionDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        actual.Should().NotBeNull();
        actual!.PlanId.Should().Be(planId);
        actual.UserId.Should().Be(userId);
        actual.GeneratedAt.Should().Be(PlanGeneratedAt);
        actual.PromptVersion.Should().Be("coaching-v1");
        actual.ModelId.Should().Be("claude-sonnet-4-5");
        actual.PreviousPlanId.Should().BeNull();

        actual.Macro.Should().NotBeNull();
        actual.Macro!.TotalWeeks.Should().Be(16);
        actual.Macro.GoalDescription.Should().Be("Half Marathon");

        actual.MesoWeeks.Should().HaveCount(4, because: "Slice 1 always lands four meso weeks for the initial plan");
        actual.MesoWeeks.Select(w => w.WeekNumber).Should().Equal(1, 2, 3, 4);
        actual.MesoWeeks[3].IsDeloadWeek.Should().BeTrue();

        actual.MicroWorkoutsByWeek.Should().ContainKey(1);
        actual.MicroWorkoutsByWeek[1].Workouts.Should().NotBeEmpty();
    }

    /// <summary>
    /// When the EF row points at a Plan stream that has not produced a
    /// <see cref="PlanProjectionDto"/> document, the endpoint returns 404
    /// rather than 500. With inline projection this is unreachable in steady
    /// state but the defensive 404 keeps the API contract stable across
    /// future projection-lifecycle changes.
    /// </summary>
    [Fact]
    public async Task GetCurrent_CurrentPlanId_Pointing_At_Missing_Stream_Returns_404()
    {
        // Arrange
        var userId = await SeedUserAsync();
        var orphanPlanId = Guid.NewGuid();
        await SeedUserProfileAsync(userId, currentPlanId: orphanPlanId);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var response = await client.GetAsync(
            "/api/v1/plan/current",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Re-issuing the same <c>GET /api/v1/plan/current</c> call returns the
    /// byte-identical document — the projection is persisted via the
    /// stream-creation event, never regenerated. This is the spec's
    /// "page reload re-renders identically" invariant exercised at the
    /// transport layer.
    /// </summary>
    [Fact]
    public async Task GetCurrent_RepeatedCalls_Return_Identical_Payload()
    {
        // Arrange
        var userId = await SeedUserAsync();
        var planId = Guid.NewGuid();
        await SeedPlanStreamAsync(userId, planId);
        await SeedUserProfileAsync(userId, currentPlanId: planId);
        using var client = CreateBearerClient(Factory, MintBearerToken(userId));

        // Act
        var first = await client.GetAsync("/api/v1/plan/current", TestContext.Current.CancellationToken);
        var second = await client.GetAsync("/api/v1/plan/current", TestContext.Current.CancellationToken);

        // Assert
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        secondBody.Should().Be(firstBody, because: "the projection is a persisted document, not regenerated");
    }

    /// <summary>
    /// Reset Marten event storage in addition to the public schema so streams
    /// seeded by one test do not survive into the next.
    /// </summary>
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
        var client = factory.CreateClient();
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MacroPlanOutput BuildMacro()
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 16,
            GoalDescription = "Half Marathon",
            Phases = new[]
            {
                new PlanPhaseOutput
                {
                    PhaseType = PhaseType.Base,
                    Weeks = 8,
                    WeeklyDistanceStartKm = 30,
                    WeeklyDistanceEndKm = 50,
                    IntensityDistribution = "80/20 easy/hard",
                    AllowedWorkoutTypes = new[] { WorkoutType.Easy, WorkoutType.LongRun, WorkoutType.Recovery },
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 300,
                    Notes = "Aerobic base build.",
                    IncludesDeload = true,
                },
            },
            Rationale = "Progressive base then build to race specificity.",
            Warnings = "Stop and reassess if any sharp pain emerges.",
        };
    }

    private static MesoWeekOutput BuildMeso(int weekNumber, PhaseType phase, bool isDeload)
    {
        var restSlot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Rest,
            WorkoutType = null,
            Notes = "Recovery.",
        };
        var easySlot = new MesoDaySlotOutput
        {
            SlotType = DaySlotType.Run,
            WorkoutType = WorkoutType.Easy,
            Notes = "Easy aerobic.",
        };

        return new MesoWeekOutput
        {
            WeekNumber = weekNumber,
            PhaseType = phase,
            WeeklyTargetKm = isDeload ? 30 : 45,
            IsDeloadWeek = isDeload,
            Sunday = easySlot,
            Monday = restSlot,
            Tuesday = easySlot,
            Wednesday = restSlot,
            Thursday = easySlot,
            Friday = restSlot,
            Saturday = easySlot,
            WeekSummary = $"Week {weekNumber} - {phase}.",
        };
    }

    private static MicroWorkoutListOutput BuildMicro()
    {
        return new MicroWorkoutListOutput
        {
            Workouts = new[]
            {
                new WorkoutOutput
                {
                    DayOfWeek = 0,
                    WorkoutType = WorkoutType.Easy,
                    Title = "Easy Aerobic Run",
                    TargetDistanceKm = 8,
                    TargetDurationMinutes = 50,
                    TargetPaceEasySecPerKm = 360,
                    TargetPaceFastSecPerKm = 360,
                    Segments = new[]
                    {
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Warmup,
                            DurationMinutes = 10,
                            TargetPaceSecPerKm = 400,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Warm up gradually.",
                        },
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Work,
                            DurationMinutes = 30,
                            TargetPaceSecPerKm = 360,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Steady aerobic effort.",
                        },
                        new WorkoutSegmentOutput
                        {
                            SegmentType = SegmentType.Cooldown,
                            DurationMinutes = 10,
                            TargetPaceSecPerKm = 420,
                            Intensity = IntensityProfile.Easy,
                            Repetitions = 1,
                            Notes = "Cool down easy.",
                        },
                    },
                    WarmupNotes = "10 min walk-jog.",
                    CooldownNotes = "10 min walk-jog.",
                    CoachingNotes = "Conversational pace.",
                    PerceivedEffort = 3,
                },
            },
        };
    }

    private static string MintBearerToken(Guid userId)
    {
        // Mirrors the helper in AuthControllerIntegrationTests but locally
        // scoped so the plan-rendering tests remain self-contained. Signed
        // with the deterministic test JWT material so the SUT's
        // JwtBearerOptionsSetup validator accepts it.
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
        // Provision an Identity row directly through UserManager so the JWT's
        // sub claim resolves to a real ApplicationUser. Skipping the
        // /api/v1/auth/register flow keeps this test focused on the plan
        // controller — auth registration is covered by AuthControllerIntegrationTests.
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"plan-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, "Str0ngTestPassw0rd!");
        result.Succeeded.Should()
            .BeTrue(because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }

    private async Task SeedUserProfileAsync(Guid userId, Guid? currentPlanId)
    {
        // Insert directly via DbContext so we don't depend on the projection
        // wiring — this test only proves the controller surface, not the
        // upstream projection flow.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.UserProfiles.Add(new UserProfile
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
        // Append the canonical Slice 1 event sequence
        // [PlanGenerated, MesoCycleCreated x4, FirstMicroCycleCreated] so the
        // inline PlanProjection materializes a complete PlanProjectionDto
        // document keyed on planId.
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();
        var generated = new PlanGenerated(
            planId,
            userId,
            BuildMacro(),
            PlanGeneratedAt,
            PromptVersion: "coaching-v1",
            ModelId: "claude-sonnet-4-5",
            PreviousPlanId: null);
        var events = new object[]
        {
            generated,
            new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)),
            new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)),
            new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)),
            new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)),
            new FirstMicroCycleCreated(BuildMicro()),
        };
        session.Events.StartStream<PlanProjectionDto>(planId, events);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
