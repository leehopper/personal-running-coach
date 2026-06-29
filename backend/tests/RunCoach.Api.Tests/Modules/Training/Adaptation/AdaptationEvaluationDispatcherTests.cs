using System.Runtime.CompilerServices;
using FluentAssertions;
using JasperFx;
using JasperFx.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching.Adaptation;
using RunCoach.Api.Modules.Training.Adaptation;
using Wolverine;

namespace RunCoach.Api.Tests.Modules.Training.Adaptation;

/// <summary>
/// Unit tests for <see cref="AdaptationEvaluationDispatcher"/> — the shared post-create
/// adaptation seam extracted from <see cref="Workouts.WorkoutLogsController"/> so the
/// Slice 4B conversational-logging confirm path reuses the IDENTICAL dispatch + lost-race
/// mapping without drift (Slice 3 § Unit 5 / DEC-073). The committed log row always wins:
/// a concurrency conflict that escapes the handler's bounded retries maps to a generic
/// retryable <c>Kind=Error</c> envelope, never a thrown 5xx; any other fault propagates.
/// </summary>
public sealed class AdaptationEvaluationDispatcherTests
{
    [Fact]
    public async Task EvaluateAsync_DispatchesCommandUnderTheUserTenant()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workoutLogId = Guid.NewGuid();
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(AdaptationResponseDto.Adapted(AdaptationKind.Absorb)));
        var dispatcher = new AdaptationEvaluationDispatcher(bus, NullLogger<AdaptationEvaluationDispatcher>.Instance);
        var ct = TestContext.Current.CancellationToken;

        // Act
        await dispatcher.EvaluateAsync(workoutLogId, userId, ct);

        // Assert — the command carries the log id + user id, dispatched under the user's Marten tenant.
        await bus.Received(1).InvokeForTenantAsync<AdaptationResponseDto>(
            userId.ToString(),
            new EvaluateAdaptationCommand(workoutLogId, userId),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsTheHandlerEnvelopeVerbatim()
    {
        // Arrange
        var expected = AdaptationResponseDto.Adapted(AdaptationKind.Restructure);
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns(Task.FromResult(expected));
        var dispatcher = new AdaptationEvaluationDispatcher(bus, NullLogger<AdaptationEvaluationDispatcher>.Instance);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await dispatcher.EvaluateAsync(Guid.NewGuid(), Guid.NewGuid(), ct);

        // Assert — the handler maps terminal coaching-LLM failures to Kind=Error itself; the
        // dispatcher passes whatever the handler resolved straight through.
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task EvaluateAsync_StreamVersionConflictEscapesDispatch_MapsToRetryableError()
    {
        // Arrange — the event-appending paths' lost Rich-append-mode race that outlives the
        // handler's bounded retries surfaces as EventStreamUnexpectedMaxEventIdException (a
        // JasperFx.ConcurrencyException). The log row is already committed, so this maps to a
        // retryable Kind=Error envelope, never a 5xx.
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns<Task<AdaptationResponseDto>>(_ =>
                throw new EventStreamUnexpectedMaxEventIdException(
                    Guid.NewGuid(), aggregateType: null, expected: 12, actual: 13));
        var dispatcher = new AdaptationEvaluationDispatcher(bus, NullLogger<AdaptationEvaluationDispatcher>.Instance);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await dispatcher.EvaluateAsync(Guid.NewGuid(), Guid.NewGuid(), ct);

        // Assert
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeTrue();
        actual.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        actual.AdaptationKind.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DuplicateMarkerConflictEscapesDispatch_MapsToRetryableError()
    {
        // Arrange — the marker-only paths (off-plan no-op, L0 absorb, Red short-circuit) stage
        // just the WorkoutLogId-keyed marker; a concurrent duplicate's Insert loses the race and
        // surfaces DocumentAlreadyExistsException, which the chain-scoped retry (keyed on the
        // stream-version conflict) does not catch. The instance is built without its constructor:
        // only the catch's type match and the envelope mapping are under test.
        var conflict = (DocumentAlreadyExistsException)RuntimeHelpers.GetUninitializedObject(
            typeof(DocumentAlreadyExistsException));
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns<Task<AdaptationResponseDto>>(_ => throw conflict);
        var dispatcher = new AdaptationEvaluationDispatcher(bus, NullLogger<AdaptationEvaluationDispatcher>.Instance);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var actual = await dispatcher.EvaluateAsync(Guid.NewGuid(), Guid.NewGuid(), ct);

        // Assert
        actual.Kind.Should().Be(AdaptationResponseKind.Error);
        actual.Retryable.Should().BeTrue();
        actual.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        actual.AdaptationKind.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_UnrelatedFault_Propagates()
    {
        // Arrange — only the two known lost-race surfaces are swallowed; a genuine server fault
        // must still bubble as a 5xx rather than be masked as a retryable adaptation error.
        var bus = Substitute.For<IMessageBus>();
        bus.InvokeForTenantAsync<AdaptationResponseDto>(
                Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
            .Returns<Task<AdaptationResponseDto>>(_ => throw new InvalidOperationException("boom"));
        var dispatcher = new AdaptationEvaluationDispatcher(bus, NullLogger<AdaptationEvaluationDispatcher>.Instance);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var act = async () => await dispatcher.EvaluateAsync(Guid.NewGuid(), Guid.NewGuid(), ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
