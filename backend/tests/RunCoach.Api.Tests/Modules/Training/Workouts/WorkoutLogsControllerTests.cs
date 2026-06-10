using System.Security.Claims;
using FluentAssertions;
using JasperFx.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Workouts;
using Wolverine;

namespace RunCoach.Api.Tests.Modules.Training.Workouts;

/// <summary>
/// Unit tests for the <see cref="WorkoutLogsController.CreateLog"/> adaptation
/// trigger boundary (Slice 3 § Unit 5): the synchronous post-commit
/// <see cref="EvaluateAdaptationCommand"/> dispatch, the
/// <see cref="AdaptationResponseDto"/> envelope riding the 201 body, and the
/// never-fail-the-create error semantics. Full end-to-end adaptation scenarios
/// live in the integration suite; these tests pin the controller's wiring with
/// substituted <see cref="IWorkoutLogService"/> + <see cref="IMessageBus"/>.
/// </summary>
public class WorkoutLogsControllerTests
{
    [Fact]
    public async Task CreateLog_DispatchesEvaluateAdaptation_AfterCreateCommits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workoutLogId = Guid.NewGuid();
        var (controller, service, bus) = CreateController(userId, workoutLogId);
        var request = ValidRequest();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await controller.CreateLog(request, ct);

        // Assert — the command carries the committed log id + user id, dispatched
        // under the user's Marten tenant, strictly AFTER the EF create returned.
        Received.InOrder(() =>
        {
            service.CreateAsync(userId, request, Arg.Any<CancellationToken>());
            bus.InvokeForTenantAsync<AdaptationResponseDto>(
                userId.ToString(),
                new EvaluateAdaptationCommand(workoutLogId, userId),
                Arg.Any<CancellationToken>(),
                Arg.Any<TimeSpan?>());
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

        // Assert — envelope passthrough: the handler's response surfaces verbatim.
        var created = actual.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Location.Should().Be($"/api/v1/workouts/logs/{workoutLogId}");
        created.Value.Should().Be(new CreateWorkoutLogResponseDto(workoutLogId, expectedEnvelope));
    }

    [Fact]
    public async Task CreateLog_ErrorEnvelope_NeverFailsTheCreate()
    {
        // Arrange — the handler already mapped a terminal coaching-LLM failure to
        // Kind=Error (DEC-073); the create must still answer 201 with that envelope.
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
    public async Task CreateLog_ConcurrencyConflictEscapesDispatch_MapsToRetryableErrorEnvelope()
    {
        // Arrange — the one known failure shape that escapes the handler's bounded
        // retries: the stream-version conflict a lost Rich-append-mode race
        // actually throws (`EventStreamUnexpectedMaxEventIdException`, a
        // `JasperFx.ConcurrencyException`). The log row is already committed, so
        // the boundary maps it to a generic retryable Kind=Error envelope instead
        // of a 5xx.
        var userId = Guid.NewGuid();
        var workoutLogId = Guid.NewGuid();
        var (controller, _, bus) = CreateController(userId, workoutLogId);
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns<Task<AdaptationResponseDto>>(_ =>
                throw new EventStreamUnexpectedMaxEventIdException(
                    Guid.NewGuid(), aggregateType: null, expected: 12, actual: 13));
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.CreateLog(ValidRequest(), ct);

        // Assert — still 201; the envelope reports the saved log + retryable error.
        var created = actual.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<CreateWorkoutLogResponseDto>().Subject;
        body.WorkoutLogId.Should().Be(workoutLogId);
        body.Adaptation.Kind.Should().Be(AdaptationResponseKind.Error);
        body.Adaptation.Retryable.Should().BeTrue();
        body.Adaptation.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        body.Adaptation.AdaptationKind.Should().BeNull();
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
        var (controller, _, bus) = CreateController(userId, existingLogId);
        var ct = TestContext.Current.CancellationToken;

        // Act
        await controller.CreateLog(ValidRequest(), ct);

        // Assert
        await bus.Received(1).InvokeForTenantAsync<AdaptationResponseDto>(
            userId.ToString(),
            new EvaluateAdaptationCommand(existingLogId, userId),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task CreateLog_InvalidLog_Returns400AndNeverDispatches()
    {
        // Arrange — a domain-invariant rejection means nothing committed, so no
        // adaptation evaluation may run.
        var userId = Guid.NewGuid();
        var (controller, service, bus) = CreateController(userId, Guid.NewGuid());
        service.CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateWorkoutLogRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new ArgumentException("Distance must be non-negative."));
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await controller.CreateLog(ValidRequest(), ct);

        // Assert
        actual.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        bus.ReceivedCalls().Should().BeEmpty();
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

    private static (WorkoutLogsController Controller, IWorkoutLogService Service, IMessageBus Bus) CreateController(
        Guid userId,
        Guid workoutLogId,
        AdaptationResponseDto? envelope = null)
    {
        var service = Substitute.For<IWorkoutLogService>();
        service.CreateAsync(Arg.Any<Guid>(), Arg.Any<CreateWorkoutLogRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(workoutLogId));

        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(envelope ?? AdaptationResponseDto.Adapted(AdaptationKind.Absorb)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "TestAuth"));

        var controller = new WorkoutLogsController(
            service, bus, NullLogger<WorkoutLogsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal },
            },
            ProblemDetailsFactory = StubProblemDetailsFactory(),
        };

        return (controller, service, bus);
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
