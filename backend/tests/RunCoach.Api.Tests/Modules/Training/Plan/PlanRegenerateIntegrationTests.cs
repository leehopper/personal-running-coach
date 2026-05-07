using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Idempotency;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Onboarding.Entities;
using RunCoach.Api.Modules.Identity.Contracts;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Tests.Infrastructure;
using Wolverine;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Integration coverage for Slice 1 § Unit 5 R05.1 / DEC-060 — the regenerate
/// flow's atomic event-sourcing wiring (idempotency check + view load + plan
/// stream creation + <see cref="PlanLinkedToUser"/> append +
/// <see cref="UserProfileFromOnboardingProjection"/> EF write) against the
/// shared Testcontainers Postgres. Covers the four scenarios per task
/// description:
/// (a) regenerate with intent produces a second Plan stream linked back to
///     the prior plan via <see cref="PlanGenerated.PreviousPlanId"/>;
/// (b) regenerate without intent succeeds with the same shape;
/// (c) regenerate before onboarding completion returns HTTP 409;
/// (d) the same idempotency key returns the byte-identical response and
///     stages no extra writes.
/// </summary>
/// <remarks>
/// <para>
/// The four scenarios split across two surfaces:
/// </para>
/// <list type="bullet">
/// <item>
///   <description>
///   Scenario (c) runs through the live HTTP pipeline because the 409 gate
///   sits on the controller — short-circuited before the Wolverine handler is
///   dispatched. No Wolverine routing is exercised here.
///   </description>
/// </item>
/// <item>
///   <description>
///   Scenarios (a), (b), (d) drive the
///   <see cref="RegeneratePlanHandler.Handle"/> entry point directly against
///   a real Marten <see cref="IDocumentSession"/> resolved from the fixture's
///   DI container. This exercises the full Marten + EF projection stack
///   (<see cref="OnboardingProjection"/> + <see cref="UserProfileFromOnboardingProjection"/>
///   + <see cref="PlanProjection"/>) under one transaction commit per call —
///   the same wiring Wolverine's transactional middleware brackets in
///   production. Going around <c>IMessageBus.InvokeAsync</c> avoids the test
///   harness needing to seed Wolverine handler discovery for the WAF-derived
///   host (a known Slice 1 limitation tracked in <c>InvokeAsyncTransactionScopeTests</c>'s
///   prose; the Wolverine routing seam itself is exercised by the smoke
///   curl proof captured at T05.1 commit).
///   </description>
/// </item>
/// </list>
/// <para>
/// The stub <see cref="StubPlanGenerationService"/> returns a deterministic
/// canonical-Slice-1 event sequence
/// (<c>[PlanGenerated, MesoCycleCreated×4, FirstMicroCycleCreated]</c>) with
/// the <see cref="PlanGenerated.PreviousPlanId"/> slot threaded through from
/// the parameter the handler passes. This is the same shape
/// <c>PlanGenerationService</c> produces in production; the only thing the
/// stub elides is the six LLM calls.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class PlanRegenerateIntegrationTests(RunCoachAppFactory factory) : DbBackedIntegrationTestBase(factory)
{
    private const string StrongPassword = "Str0ngTestPassw0rd!";
    private const string AntiforgeryHeaderName = AuthCookieNames.AntiforgeryHeader;
    private const string AntiforgeryRequestCookieName = AuthCookieNames.AntiforgeryRequest;
    private const string OnboardingNotCompleteType = "https://runcoach.app/problems/onboarding-not-complete";

    private static readonly Uri BaseUri = new("https://localhost");

    /// <summary>
    /// Scenario (a) per task description — seeds prior onboarding + plan
    /// stream, drives the regenerate handler with an intent, then asserts:
    /// (1) two Plan streams exist in Marten, (2) the new plan's
    /// <see cref="PlanGenerated.PreviousPlanId"/> equals the prior plan id,
    /// (3) the onboarding stream contains a fresh
    /// <see cref="PlanLinkedToUser"/> referencing the new plan, and
    /// (4) <c>UserProfile.CurrentPlanId</c> points at the new plan.
    /// </summary>
    [Fact]
    public async Task Regenerate_With_Intent_Creates_Second_Plan_Stream_With_PreviousPlanId_Linkage()
    {
        // Arrange — provision an Identity row + seed the prior plan stream
        //   and onboarding events directly so the runner is in the
        //   post-onboarding branch the regenerate flow targets.
        var userId = await SeedUserAsync();
        var initialPlanId = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        var idempotencyKey = Guid.NewGuid();
        var intent = new RegenerationIntent("I just got injured");

        // Act — invoke the handler directly with a real Marten session.
        var actualResponse = await InvokeHandlerAsync(
            new RegeneratePlanCommand(userId, intent, idempotencyKey));

        // Assert — handler returned a fresh planId tagged "generated".
        actualResponse.PlanId.Should().NotBe(initialPlanId, because: "regeneration must produce a new plan id");
        actualResponse.Status.Should().Be("generated");
        var newPlanId = actualResponse.PlanId;

        // (1) two distinct Plan streams exist in Marten.
        var allPlans = await LoadAllPlanProjectionsAsync(userId);
        allPlans.Should().HaveCount(2, because: "the prior plan stays in Marten as audit trail; the new plan is a second stream");
        allPlans.Select(p => p.PlanId).Should().BeEquivalentTo(new[] { initialPlanId, newPlanId });

        // (2) the new Plan stream's `PreviousPlanId` resolves to the prior plan id.
        var newPlan = allPlans.Single(p => p.PlanId == newPlanId);
        newPlan.PreviousPlanId.Should().Be(initialPlanId, because: "the regenerate handler threads priorPlanId through GeneratePlanAsync into PlanGenerated.PreviousPlanId");

        // (3) the onboarding stream now carries a fresh PlanLinkedToUser
        //     referencing the new plan id. The initial seed appended one such
        //     event for the prior plan, so we expect two PlanLinkedToUser
        //     events on the stream — order-preserved with the new event last.
        var planLinks = await LoadPlanLinkedEventsAsync(userId, userId);
        planLinks.Should().HaveCount(2);
        planLinks.Select(e => e.PlanId).Should().Equal(initialPlanId, newPlanId);

        // (4) UserProfile.CurrentPlanId now points at the new plan via the
        //     UserProfileFromOnboardingProjection apply branch on the
        //     freshly-appended PlanLinkedToUser event.
        var userProfile = await LoadUserProfileAsync(userId);
        userProfile.Should().NotBeNull();
        userProfile!.CurrentPlanId.Should().Be(newPlanId);
    }

    /// <summary>
    /// Scenario (b) per task description — regenerate without an intent body
    /// succeeds and produces the same shape as the with-intent flow. The
    /// handler accepts a null <c>RegeneratePlanCommand.Intent</c> and the
    /// stub plan generator does not branch on the intent value.
    /// </summary>
    [Fact]
    public async Task Regenerate_Without_Intent_Succeeds_And_Creates_New_Plan_Stream()
    {
        // Arrange
        var userId = await SeedUserAsync();
        var initialPlanId = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        // Act
        var actualResponse = await InvokeHandlerAsync(
            new RegeneratePlanCommand(userId, Intent: null, Guid.NewGuid()));

        // Assert
        actualResponse.PlanId.Should().NotBe(initialPlanId);
        actualResponse.Status.Should().Be("generated");

        var allPlans = await LoadAllPlanProjectionsAsync(userId);
        allPlans.Should().HaveCount(2);
        allPlans.Single(p => p.PlanId == actualResponse.PlanId)
            .PreviousPlanId.Should().Be(initialPlanId);
    }

    /// <summary>
    /// Scenario (c) per task description — submitting regenerate before the
    /// runner has finished onboarding returns 409 ProblemDetails. The
    /// controller's <c>UserProfile.OnboardingCompletedAt</c> gate trips
    /// before the command would reach the Wolverine handler. Driven through
    /// the live HTTP pipeline because the gate sits on the controller.
    /// </summary>
    [Fact]
    public async Task Regenerate_Before_Onboarding_Completion_Returns_409()
    {
        // Arrange — register but do NOT seed any onboarding events. The
        //   runner has an ApplicationUser row but no UserProfile row at all
        //   (and even if one existed, OnboardingCompletedAt would be null).
        //   Either case lands the controller's gate-fail path.
        var (client, _, antiforgeryToken, _) = await RegisterAndPrepareCookieClientAsync(Factory);

        // Act
        using var request = BuildRegenerateRequest(antiforgeryToken, idempotencyKey: Guid.NewGuid(), intent: null);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem!.Type.Should().Be(OnboardingNotCompleteType);
    }

    /// <summary>
    /// HTTP-layer happy path: drives <c>POST /api/v1/plan/regenerate</c> over
    /// the live ASP.NET Core pipeline (cookie auth + antiforgery + sanitizer +
    /// Wolverine dispatch + handler) and asserts the controller's
    /// <see cref="PlanRenderingController.Regenerate"/> success branch returns
    /// 200 with a fresh <c>planId</c> + <c>status: "generated"</c>. A follow-up
    /// <c>GET /api/v1/plan/current</c> proves the
    /// <c>UserProfileFromOnboardingProjection</c> apply branch flipped
    /// <c>RunnerOnboardingProfile.CurrentPlanId</c> to the new plan id atomically with
    /// the Marten append. Closes the SonarCloud coverage gap on the controller's
    /// success path that the direct-handler tests above cannot exercise.
    /// </summary>
    [Fact]
    public async Task Regenerate_HTTP_With_Valid_Onboarding_Returns_200_And_New_Plan()
    {
        // Arrange — register, complete onboarding, seed an initial plan stream
        //   so the regenerate handler has a prior `CurrentPlanId` to thread
        //   onto `PlanGenerated.PreviousPlanId`. Override `IMessageBus` with a
        //   stub that runs the real handler body against a fresh tenanted
        //   Marten session — mirrors what Wolverine transactional middleware
        //   brackets in production while bypassing the WAF-derived host
        //   handler discovery; `WithWebHostBuilder` does not re-run the
        //   Wolverine assembly scan. Threads `StubPlanGenerationService`
        //   through the real handler so no live LLM call fires.
        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svc =>
                svc.AddSingleton(BuildRegenerateBusStub(Factory))));

        var (client, _, antiforgeryToken, userId) = await RegisterAndPrepareCookieClientAsync(customFactory);
        var initialPlanId = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        var idempotencyKey = Guid.NewGuid();

        // Act — POST /api/v1/plan/regenerate with an intent free-text.
        using var request = BuildRegenerateRequest(antiforgeryToken, idempotencyKey, intent: "Need more long runs");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert — 200 with the controller's RegeneratePlanResponse shape.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualResponse = await response.Content.ReadFromJsonAsync<RegeneratePlanResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        actualResponse.Should().NotBeNull();
        actualResponse!.PlanId.Should().NotBe(initialPlanId, because: "regeneration must produce a new plan id");
        actualResponse.PlanId.Should().NotBe(Guid.Empty);
        actualResponse.Status.Should().Be("generated");

        // Follow-up GET /api/v1/plan/current must return the freshly-projected
        //   PlanProjectionDto for the new plan id — proves the Marten inline
        //   projection committed inside the same transaction the regenerate
        //   handler appended on, and that UserProfileFromOnboardingProjection
        //   moved RunnerOnboardingProfile.CurrentPlanId to the new plan id.
        using var getResponse = await client.GetAsync(
            "/api/v1/plan/current",
            TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualPlan = await getResponse.Content.ReadFromJsonAsync<PlanProjectionDto>(
            cancellationToken: TestContext.Current.CancellationToken);
        actualPlan.Should().NotBeNull();
        actualPlan!.PlanId.Should().Be(actualResponse.PlanId);
        actualPlan.PreviousPlanId.Should().Be(initialPlanId);
    }

    /// <summary>
    /// HTTP-layer happy path with no intent — same as the previous test but
    /// the request body omits the optional <c>intent</c> block. Exercises the
    /// controller branch where <c>request.Intent is null</c> bypasses
    /// sanitization entirely and a null <see cref="RegenerationIntent"/> flows
    /// to the handler.
    /// </summary>
    [Fact]
    public async Task Regenerate_HTTP_Without_Intent_Returns_200()
    {
        // Arrange — see BuildRegenerateBusStub doc; we override IMessageBus
        //   rather than IPlanGenerationService because the WAF-derived host
        //   does not re-run Wolverine's assembly scan and would otherwise
        //   surface IndeterminateRoutesException for RegeneratePlanCommand.
        using var customFactory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(svc =>
                svc.AddSingleton(BuildRegenerateBusStub(Factory))));

        var (client, _, antiforgeryToken, userId) = await RegisterAndPrepareCookieClientAsync(customFactory);
        var initialPlanId = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        // Act — POST regenerate with intent=null.
        using var request = BuildRegenerateRequest(antiforgeryToken, idempotencyKey: Guid.NewGuid(), intent: null);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var actualResponse = await response.Content.ReadFromJsonAsync<RegeneratePlanResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        actualResponse.Should().NotBeNull();
        actualResponse!.PlanId.Should().NotBe(initialPlanId);
        actualResponse.Status.Should().Be("generated");
    }

    /// <summary>
    /// HTTP-layer 400 path — the controller validates raw <c>intent.freeText</c>
    /// against <see cref="RegenerationIntent.RawMaxFreeTextLength"/> BEFORE
    /// invoking the sanitizer, so a 501-character payload trips the cap and
    /// returns 400 ProblemDetails without touching Wolverine or the handler.
    /// </summary>
    [Fact]
    public async Task Regenerate_HTTP_With_Oversize_FreeText_Returns_400()
    {
        // Arrange — onboarding seeded so the 400 path is exercised AFTER the
        //   onboarding-complete gate (otherwise the 409 gate would short-circuit
        //   first and we would not be testing the length validation branch).
        var (client, _, antiforgeryToken, userId) = await RegisterAndPrepareCookieClientAsync(Factory);
        var initialPlanId = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        var oversizeFreeText = new string('x', RegenerationIntent.RawMaxFreeTextLength + 1);

        // Act
        using var request = BuildRegenerateRequest(antiforgeryToken, idempotencyKey: Guid.NewGuid(), intent: oversizeFreeText);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(
            cancellationToken: TestContext.Current.CancellationToken);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// HTTP-layer 401 path — anonymous request (no cookie / no bearer token)
    /// must be rejected by the <see cref="AuthPolicies.CookieOrBearer"/> policy
    /// before the controller body executes.
    /// </summary>
    [Fact]
    public async Task Regenerate_HTTP_Without_Auth_Returns_401()
    {
        // Arrange — anonymous client. Antiforgery is bypassed by the auth gate
        //   running first; both unauthenticated and antiforgery-missing requests
        //   land on 401 because the auth challenge wins the precedence race.
        var client = Factory.CreateDefaultClient();
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/plan/regenerate")
        {
            Content = JsonContent.Create(new RegeneratePlanRequestDto(Guid.NewGuid(), Intent: null)),
        };

        // Act
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario (d) per task description — sending the same idempotency key
    /// twice returns the byte-identical response with the same plan id, and
    /// no third Plan stream / no extra <see cref="PlanLinkedToUser"/> event
    /// is appended on the second call. This is the load-bearing safety net
    /// against duplicate submissions on retry.
    /// </summary>
    [Fact]
    public async Task Regenerate_With_Same_IdempotencyKey_Returns_Same_PlanId_Without_Side_Effects()
    {
        // Arrange
        var userId = await SeedUserAsync();
        var initialPlanId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        await SeedInitialPlanStreamAsync(userId, initialPlanId);
        await SeedOnboardingCompletionAsync(userId, initialPlanId);

        // First call — produces the new plan.
        var firstResponse = await InvokeHandlerAsync(
            new RegeneratePlanCommand(userId, new RegenerationIntent("first"), idempotencyKey));
        var firstPlanId = firstResponse.PlanId;

        // Act — second call with the same key. The intent payload here
        //   intentionally differs from the first call so a (broken) re-run
        //   path that ignores idempotency would generate a different plan
        //   id, making the cache-hit vs. cache-miss branches visibly
        //   distinguishable in the assertion below.
        var secondResponse = await InvokeHandlerAsync(
            new RegeneratePlanCommand(userId, new RegenerationIntent("second-different-but-ignored"), idempotencyKey));

        // Assert — same plan id, same body shape.
        secondResponse.PlanId.Should().Be(firstPlanId, because: "the idempotency store memoizes the first response under this key");
        secondResponse.Status.Should().Be(firstResponse.Status);

        // No third Plan stream — exactly two exist (prior + first regenerate).
        var allPlans = await LoadAllPlanProjectionsAsync(userId);
        allPlans.Should().HaveCount(2, because: "the second submission is a memoized replay; nothing is appended");
        allPlans.Select(p => p.PlanId).Should().BeEquivalentTo(new[] { initialPlanId, firstPlanId });

        // No extra PlanLinkedToUser was appended — onboarding stream still
        //   carries exactly two such events (initial seed + first regenerate).
        var planLinks = await LoadPlanLinkedEventsAsync(userId, userId);
        planLinks.Should().HaveCount(2);
        planLinks.Select(e => e.PlanId).Should().Equal(initialPlanId, firstPlanId);
    }

    /// <summary>
    /// Marten state lives in <c>runcoach_events</c>, which Respawn skips.
    /// Reset it explicitly so seeded streams + idempotency markers do not
    /// leak between tests.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await Factory.Services.ResetAllMartenDataAsync();
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static async Task<string> PrimeAntiforgeryAsync(HttpClient client, CookieContainer container)
    {
        using var response = await client.GetAsync("/api/v1/auth/xsrf", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requestCookie = container.GetCookies(BaseUri)
            .Cast<Cookie>()
            .FirstOrDefault(c => c.Name == AntiforgeryRequestCookieName);
        requestCookie.Should().NotBeNull("/xsrf must issue the SPA-readable request token cookie");
        return requestCookie!.Value;
    }

    private static HttpRequestMessage BuildRegenerateRequest(string antiforgeryToken, Guid idempotencyKey, string? intent)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/plan/regenerate");
        request.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        request.Content = JsonContent.Create(new RegeneratePlanRequestDto(
            idempotencyKey,
            intent is null ? null : new RegenerationIntentRequestDto(intent)));
        return request;
    }

    private static MacroPlanOutput BuildMacro(string goal)
    {
        return new MacroPlanOutput
        {
            TotalWeeks = 16,
            GoalDescription = goal,
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

    /// <summary>
    /// Builds an <see cref="IMessageBus"/> stub whose
    /// <c>InvokeForTenantAsync&lt;RegeneratePlanResponse&gt;</c> implementation
    /// runs the real <see cref="RegeneratePlanHandler.Handle"/> body against a
    /// fresh tenanted Marten session — exactly the contract Wolverine's
    /// transactional middleware brackets in production. The stub threads
    /// <see cref="StubPlanGenerationService"/> as <see cref="IPlanGenerationService"/>
    /// so the six-call macro/meso/micro chain is replaced with the deterministic
    /// canonical event sequence (no live LLM). Used in HTTP integration tests
    /// where <c>WithWebHostBuilder</c> rebuilds the WAF host and Wolverine's
    /// assembly-discovery codegen does not re-fire — without this stub the
    /// controller's <c>bus.InvokeForTenantAsync</c> call surfaces
    /// <c>IndeterminateRoutesException</c>.
    /// </summary>
    private static IMessageBus BuildRegenerateBusStub(RunCoachAppFactory factory)
    {
        var bus = Substitute.For<IMessageBus>();
        bus
            .InvokeForTenantAsync<RegeneratePlanResponse>(
                Arg.Any<string>(),
                Arg.Any<object>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>())
            .Returns(async call =>
            {
                var tenantId = call.ArgAt<string>(0);
                var cmd = (RegeneratePlanCommand)call.ArgAt<object>(1);
                var ct = call.ArgAt<CancellationToken>(2);

                // Open a session tenanted to the dispatching userId, the same
                //   way `IMessageBus.InvokeForTenantAsync` would inside Wolverine's
                //   transactional middleware. The handler stages events +
                //   idempotency marker on this session; the framework would
                //   commit them via auto-applied transactions — here we call
                //   `SaveChangesAsync` explicitly to mirror that contract.
                var store = factory.Services.GetRequiredService<IDocumentStore>();
                await using var session = store.LightweightSession(tenantId);
                var idempotency = new MartenIdempotencyStore(
                    session,
                    TimeProvider.System,
                    NullLogger<MartenIdempotencyStore>.Instance);
                var planGen = new StubPlanGenerationService();

                var response = await RegeneratePlanHandler.Handle(
                    cmd,
                    session,
                    planGen,
                    idempotency,
                    NullLogger<RegeneratePlanHandler>.Instance,
                    ct);

                await session.SaveChangesAsync(ct);
                return response;
            });
        return bus;
    }

    private static async Task<(HttpClient Client, CookieContainer Container, string AntiforgeryToken, Guid UserId)>
        RegisterAndPrepareCookieClientAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory)
    {
        var container = new CookieContainer();
        var client = factory.CreateDefaultClient(new CookieContainerHandler(container));
        client.BaseAddress = BaseUri;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var antiforgeryToken = await PrimeAntiforgeryAsync(client, container);

        var email = $"regen-{Guid.NewGuid():N}@example.test";
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        registerRequest.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        registerRequest.Content = JsonContent.Create(new RegisterRequestDto(email, StrongPassword));
        using var registerResponse = await client.SendAsync(registerRequest, TestContext.Current.CancellationToken);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created, because: "register helper must succeed");

        // /register creates the ApplicationUser but does NOT sign in — the
        //   SPA flow is register → login. Run the login call here so the
        //   session cookie lands in the container.
        antiforgeryToken = await PrimeAntiforgeryAsync(client, container);
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        loginRequest.Headers.Add(AntiforgeryHeaderName, antiforgeryToken);
        loginRequest.Content = JsonContent.Create(new LoginRequestDto(email, StrongPassword));
        using var loginResponse = await client.SendAsync(loginRequest, TestContext.Current.CancellationToken);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, because: "login helper must succeed");

        antiforgeryToken = await PrimeAntiforgeryAsync(client, container);

        // Resolve the registered ApplicationUser's id off UserManager so seed
        // helpers (SeedInitialPlanStreamAsync / SeedOnboardingCompletionAsync)
        // can land events on the same userId the cookie-authenticated client
        // identifies as. Both factories share the same Testcontainers Postgres
        // via the env-var override on the assembly-wide RunCoachAppFactory, so
        // a UserManager scope from the inner factory's Services container sees
        // the row that was just created through the outer client's HTTP call.
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var registered = await users.FindByEmailAsync(email);
        registered.Should().NotBeNull(because: "the registered user must exist after the /register call");
        return (client, container, antiforgeryToken, registered!.Id);
    }

    private async Task<Guid> SeedUserAsync()
    {
        // Provision an Identity row directly through UserManager so the
        // UserProfile projection has an FK target. The cookie-flow register
        // path is exercised by Scenario (c); here we want a user without
        // having to drive register + login + antiforgery on every test.
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"regen-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email };
        var result = await users.CreateAsync(user, StrongPassword);
        result.Succeeded.Should()
            .BeTrue(because: $"seed must succeed — got [{string.Join(", ", result.Errors.Select(e => e.Code))}]");
        return user.Id;
    }

    private async Task<RegeneratePlanResponse> InvokeHandlerAsync(RegeneratePlanCommand cmd)
    {
        // Open a single per-call DI scope so `IDocumentSession`,
        //   `IIdempotencyStore`, and `IPlanGenerationService` (via the stub
        //   factory below) all share one session — the same lifetime contract
        //   Wolverine's transactional middleware enforces in production.
        // Marten is configured with TenancyStyle.Conjoined; the production
        // path uses InvokeForTenantAsync(userId, …) to bracket the handler in
        // a tenanted session. Mirror that here by opening a session tenanted
        // to the command's UserId so IIdempotencyStore.Record can stamp the
        // marker's tenant column without rejecting the *DEFAULT* placeholder.
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(cmd.UserId.ToString());
        var idempotency = new MartenIdempotencyStore(
            session,
            TimeProvider.System,
            NullLogger<MartenIdempotencyStore>.Instance);
        var planGen = new StubPlanGenerationService();

        var response = await RegeneratePlanHandler.Handle(
            cmd,
            session,
            planGen,
            idempotency,
            NullLogger<RegeneratePlanHandler>.Instance,
            TestContext.Current.CancellationToken);

        // The Wolverine handler itself never calls SaveChangesAsync — the
        //   transactional middleware does. Stand in for the framework here so
        //   the staged events + idempotency marker actually commit. The
        //   MartenEntityFrameworkCore transaction-participant wiring then
        //   runs the inline projection's EF write inside the same DB
        //   transaction (per DEC-060 / R-069).
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return response;
    }

    private async Task SeedInitialPlanStreamAsync(Guid userId, Guid planId)
    {
        // Append the canonical Slice 1 event sequence so the inline
        // PlanProjection materializes a complete PlanProjectionDto document.
        // Tenant the session to the user — Marten is configured with
        // TenancyStyle.Conjoined so the regenerate handler reads the stream
        // back through a userId-tenanted session, and a default-tenant seed
        // would land on a different physical row.
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var generated = new PlanGenerated(
            planId,
            userId,
            BuildMacro(goal: "Initial plan"),
            new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
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

    private async Task SeedOnboardingCompletionAsync(Guid userId, Guid initialPlanId)
    {
        // Drive the onboarding stream to completion via the projection
        // pipeline rather than direct EF row inserts. Three events are the
        // minimum the regenerate flow needs:
        //   - OnboardingStarted  -> seeds OnboardingView + UserProfile rows
        //   - PlanLinkedToUser   -> sets CurrentPlanId on both projections
        //   - OnboardingCompleted-> sets OnboardingCompletedAt on the EF row
        // OnboardingProjection is registered ProjectionLifecycle.Inline, so
        // this commit also materializes the OnboardingView the regenerate
        // handler reads via session.LoadAsync<OnboardingView>(userId).
        // Marten's TenancyStyle.Conjoined requires the seed to use a
        // userId-tenanted session so the projection lands on the same
        // tenant column the handler queries.
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession(userId.ToString());
        var startedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var completedAt = new DateTimeOffset(2026, 4, 1, 12, 5, 0, TimeSpan.Zero);
        session.Events.StartStream<OnboardingView>(
            userId,
            new OnboardingStarted(userId, startedAt),
            new PlanLinkedToUser(userId, initialPlanId),
            new OnboardingCompleted(initialPlanId, completedAt));
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<PlanProjectionDto>> LoadAllPlanProjectionsAsync(Guid tenantId)
    {
        // Marten is configured with TenancyStyle.Conjoined so the projections
        // live behind the tenant column; query through the user-tenanted
        // session that mirrors how the controller reads them.
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession(tenantId.ToString());
        var query = session.Query<PlanProjectionDto>();
        return await Marten.QueryableExtensions.ToListAsync(query, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<PlanLinkedToUser>> LoadPlanLinkedEventsAsync(Guid streamId, Guid tenantId)
    {
        // FetchStreamAsync also resolves through the tenant column when
        // TenancyStyle.Conjoined is active.
        var store = Factory.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession(tenantId.ToString());
        var events = await session.Events.FetchStreamAsync(streamId, token: TestContext.Current.CancellationToken);
        return events
            .Select(e => e.Data)
            .OfType<PlanLinkedToUser>()
            .ToList();
    }

    private async Task<RunnerOnboardingProfile?> LoadUserProfileAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
        var query = db.RunnerOnboardingProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId);
        return await EntityFrameworkQueryableExtensions
            .SingleOrDefaultAsync(query, TestContext.Current.CancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deterministic stub for <see cref="IPlanGenerationService"/> used by
    /// every direct-handler integration test in this class. Returns the
    /// canonical Slice 1 event sequence threading <paramref name="previousPlanId"/>
    /// onto <see cref="PlanGenerated.PreviousPlanId"/> exactly the way the
    /// production service does — so the integration tests cover the
    /// regenerate handler's wiring (idempotency check, view load,
    /// stream-creation, PlanLinkedToUser append) without depending on the
    /// live LLM.
    /// </summary>
    private sealed class StubPlanGenerationService : IPlanGenerationService
    {
        public Task<PlanEventSequence> GeneratePlanAsync(
            OnboardingView profileSnapshot,
            Guid userId,
            Guid planId,
            RegenerationIntent? intent,
            Guid? previousPlanId,
            CancellationToken ct)
        {
            _ = profileSnapshot;
            _ = intent;

            var generated = new PlanGenerated(
                planId,
                userId,
                BuildMacro(goal: "Regenerated plan"),
                new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero),
                PromptVersion: "coaching-v1",
                ModelId: "claude-sonnet-4-5",
                PreviousPlanId: previousPlanId);

            var mesos = new[]
            {
                new MesoCycleCreated(1, BuildMeso(1, PhaseType.Base, isDeload: false)),
                new MesoCycleCreated(2, BuildMeso(2, PhaseType.Base, isDeload: false)),
                new MesoCycleCreated(3, BuildMeso(3, PhaseType.Build, isDeload: false)),
                new MesoCycleCreated(4, BuildMeso(4, PhaseType.Build, isDeload: true)),
            };
            var micro = new FirstMicroCycleCreated(BuildMicro());

            return Task.FromResult(new PlanEventSequence(generated, mesos, micro));
        }
    }
}
