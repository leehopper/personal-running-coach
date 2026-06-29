using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit tests for the <see cref="WorkoutLogsController.CreateLog"/> adaptation
/// trigger boundary (Slice 3 § Unit 5): the synchronous post-commit adaptation
/// dispatch via the shared <see cref="IAdaptationEvaluationDispatcher"/>, the
/// <see cref="AdaptationResponseDto"/> envelope riding the 201 body, and the
/// never-fail-the-create error semantics. The lost-race-to-Kind=Error mapping now
/// lives in (and is tested by) <see cref="Adaptation.AdaptationEvaluationDispatcherTests"/>;
/// these tests pin the controller's wiring with substituted
/// <see cref="IWorkoutLogService"/> + <see cref="IAdaptationEvaluationDispatcher"/>.
/// </summary>
public class WorkoutLogsControllerTests
{
    [Fact]
    public async Task CreateLog_DispatchesAdaptation_AfterCreateCommits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workoutLogId = Guid.NewGuid();
        var (controller, service, dispatcher) = CreateController(userId, workoutLogId);
        var request = ValidRequest();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await controller.CreateLog(request, ct);

        // Assert — adaptation is evaluated for the committed log id + user id, strictly
        // AFTER the EF create returned.
        Received.InOrder(() =>
        {
            service.CreateAsync(userId, request, Arg.Any<CancellationToken>());
            dispatcher.EvaluateAsync(workoutLogId, userId, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task CreateLog_AdaptedEnvelope_RidesThe201Body()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workoutLogId = Guid.NewGuid();
        var expectedEnvelope = AdaptationResponseDto.Adapted(AdaptationKind.Nudge);
        var (controller, _, _) = CreateController(userId, workoutLogId, expectedEnvelope);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.CreateLog(ValidRequest(), ct);

        // Assert — envelope passthrough: the dispatcher's response surfaces verbatim.
        var created = actual.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Location.Should().Be($"/api/v1/workouts/logs/{workoutLogId}");
        created.Value.Should().Be(new CreateWorkoutLogResponseDto(workoutLogId, expectedEnvelope));
    }

    [Fact]
    public async Task CreateLog_ErrorEnvelope_NeverFailsTheCreate()
    {
        // Arrange — the dispatcher mapped a terminal failure (or lost race) to Kind=Error
        // (DEC-073); the create must still answer 201 with that envelope.
        var userId = Guid.NewGuid();
        var workoutLogId = Guid.NewGuid();
        var errorEnvelope = new AdaptationResponseDto
        {
            Kind = AdaptationResponseKind.Error,
            ErrorMessage = "The coach is briefly unavailable.",
            Retryable = true,
            RetryAfterSeconds = 30,
        };
        var (controller, _, _) = CreateController(userId, workoutLogId, errorEnvelope);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.CreateLog(ValidRequest(), ct);

        // Assert
        var created = actual.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().Be(new CreateWorkoutLogResponseDto(workoutLogId, errorEnvelope));
    }

    [Fact]
    public async Task CreateLog_ReplayedCreate_DispatchesUnconditionally()
    {
        // Arrange — DEC-077: CreateAsync may return an EXISTING log id on a
        // replayed idempotency key. The controller still dispatches; the handler's
        // WorkoutLogId-keyed marker makes the re-dispatch a designed no-op (or
        // recovers a missing evaluation after a crash between the two commits).
        var userId = Guid.NewGuid();
        var existingLogId = Guid.NewGuid();
        var (controller, _, dispatcher) = CreateController(userId, existingLogId);
        var ct = TestContext.Current.CancellationToken;

        // Act
        await controller.CreateLog(ValidRequest(), ct);

        // Assert
        await dispatcher.Received(1).EvaluateAsync(existingLogId, userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLog_InvalidLog_Returns400AndNeverDispatches()
    {
        // Arrange — a domain-invariant rejection means nothing committed, so no
        // adaptation evaluation may run.
        var userId = Guid.NewGuid();
        var (controller, service, dispatcher) = CreateController(userId, Guid.NewGuid());
        service.CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateWorkoutLogRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new ArgumentException("Distance must be non-negative."));
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.CreateLog(ValidRequest(), ct);

        // Assert
        actual.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        dispatcher.ReceivedCalls().Should().BeEmpty();
    }

    private static CreateWorkoutLogRequestDto ValidRequest() =>
        new(
            IdempotencyKey: Guid.NewGuid(),
            OccurredOn: new DateOnly(2026, 6, 8),
            DistanceMeters: 10_000,
            DurationSeconds: 3_600,
            CompletionStatus: CompletionStatus.Complete,
            Notes: null,
            Metrics: null,
            Splits: null);

    private static (WorkoutLogsController Controller, IWorkoutLogService Service, IAdaptationEvaluationDispatcher Dispatcher) CreateController(
        Guid userId,
        Guid workoutLogId,
        AdaptationResponseDto? envelope = null)
    {
        var service = Substitute.For<IWorkoutLogService>();
        service.CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateWorkoutLogRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(workoutLogId));

        var dispatcher = Substitute.For<IAdaptationEvaluationDispatcher>();
        dispatcher.EvaluateAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(envelope ?? AdaptationResponseDto.Adapted(AdaptationKind.Absorb)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "TestAuth"));

        var controller = new WorkoutLogsController(
            service, dispatcher, NullLogger<WorkoutLogsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
            ProblemDetailsFactory = StubProblemDetailsFactory(),
        };

        return (controller, service, dispatcher);
    }

    private static ProblemDetailsFactory StubProblemDetailsFactory()
    {
        // ControllerBase.Problem resolves a ProblemDetailsFactory; outside a real
        // host none is registered, so the 400 path gets a minimal pass-through stub.
        var factory = Substitute.For<ProblemDetailsFactory>();
        factory.CreateProblemDetails(
                Arg.Any<HttpContext>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>())
            .Returns(ci => new ProblemDetails
            {
                Status = ci.ArgAt<int?>(1),
                Title = ci.ArgAt<string?>(2),
                Type = ci.ArgAt<string?>(3),
                Detail = ci.ArgAt<string?>(4),
                Instance = ci.ArgAt<string?>(5),
            });
        return factory;
    }
}
