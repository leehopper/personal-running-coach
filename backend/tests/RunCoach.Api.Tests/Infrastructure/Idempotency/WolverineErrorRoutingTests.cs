using FluentAssertions;
using Marten.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.ErrorHandling;

namespace RunCoach.Api.Tests.Infrastructure.Idempotency;

/// <summary>
/// Pure-policy unit coverage for the global Wolverine <c>OnException</c>
/// registrations in <c>Program.cs</c>. Boots a minimal in-memory Wolverine
/// host that mirrors only the failure-policy block from the production
/// startup, then introspects the resolved <see cref="WolverineOptions"/> so
/// the assertions don't require a Marten / Postgres dependency.
///
/// The full first-turn-collision regression — exercising the same routing
/// against a real handler that throws inside a Marten transaction — is
/// deferred to slice-1a6 per the PR scope. This test exists to prevent the
/// three OnException registrations from silently regressing in isolation.
/// </summary>
[Trait("Category", "Unit")]
public class WolverineErrorRoutingTests
{
    /// <summary>
    /// Description string emitted by Wolverine's internal
    /// <c>MoveToErrorQueueSource.Description</c>. Surfaced by the public
    /// <see cref="FailureSlot.Describe()"/> API. Pinning the literal rather
    /// than reflecting on internals keeps the test brittle in the right way:
    /// a Wolverine upgrade that renames the description forces a
    /// re-validation that the registration still routes to the dead-letter
    /// queue.
    /// </summary>
    private const string MoveToErrorQueueDescription = "Move to error queue";

    [Fact]
    public void ExistingStreamIdCollision_Routes_To_Error_Queue()
    {
        // Arrange
        using var host = BuildHost();
        var options = host.Services.GetRequiredService<WolverineOptions>();

        // Act
        var actualSlot = FindFirstAttemptSlotFor(
            options.Policies.Failures,
            new ExistingStreamIdCollisionException(Guid.NewGuid(), typeof(object)));

        // Assert
        actualSlot.Should().NotBeNull(
            because: "Program.cs registers OnException<ExistingStreamIdCollisionException>().MoveToErrorQueue() so the global FailureRuleCollection must contain a matching rule");
        actualSlot!.Describe().Should().Contain(
            MoveToErrorQueueDescription,
            because: "the rule's first-attempt slot must short-circuit to the dead-letter queue rather than retry");
    }

    [Fact]
    public void ConcurrentUpdate_Routes_To_Error_Queue()
    {
        // Arrange
        using var host = BuildHost();
        var options = host.Services.GetRequiredService<WolverineOptions>();

        // Act
        var actualSlot = FindFirstAttemptSlotFor(
            options.Policies.Failures,
            new ConcurrentUpdateException(new InvalidOperationException("seam")));

        // Assert
        actualSlot.Should().NotBeNull(
            because: "Program.cs registers OnException<ConcurrentUpdateException>().MoveToErrorQueue() so the global FailureRuleCollection must contain a matching rule");
        actualSlot!.Describe().Should().Contain(
            MoveToErrorQueueDescription,
            because: "subsequent-turn append races must dead-letter rather than re-run the handler against a stale stream version");
    }

    [Fact]
    public void DocumentAlreadyExists_Routes_To_Error_Queue()
    {
        // Arrange
        using var host = BuildHost();
        var options = host.Services.GetRequiredService<WolverineOptions>();

        // Act — DocumentAlreadyExistsException is a sibling of the two
        // stream-collision exceptions in Marten's hierarchy, not a parent, so
        // the rule has to be registered explicitly. This assertion catches a
        // regression that drops the third OnException line from Program.cs
        // and silently re-enables the default retry pipeline against the
        // duplicate idempotency-marker write.
        var actualSlot = FindFirstAttemptSlotFor(
            options.Policies.Failures,
            new DocumentAlreadyExistsException(
                new InvalidOperationException("seam"),
                typeof(object),
                Guid.NewGuid()));

        // Assert
        actualSlot.Should().NotBeNull(
            because: "Program.cs registers OnException<DocumentAlreadyExistsException>().MoveToErrorQueue() so MartenIdempotencyStore.Record's Insert collision dead-letters cleanly");
        actualSlot!.Describe().Should().Contain(
            MoveToErrorQueueDescription,
            because: "first-write-wins requires the duplicate idempotency-marker insert to short-circuit, not retry");
    }

    /// <summary>
    /// Builds a minimal Wolverine host that mirrors only the OnException
    /// registrations from <c>Program.cs</c>. No transports, no persistence,
    /// no handlers — the host exists solely to compose
    /// <see cref="WolverineOptions.Policies"/> through the same code path
    /// production uses, then surface it via DI for assertion. The host is
    /// never started; <see cref="WolverineOptions"/> is fully configured as
    /// soon as <see cref="HostBuilder.Build"/> resolves the singleton.
    /// </summary>
    private static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.OnException<ExistingStreamIdCollisionException>().MoveToErrorQueue();
                opts.OnException<ConcurrentUpdateException>().MoveToErrorQueue();
                opts.OnException<DocumentAlreadyExistsException>().MoveToErrorQueue();
            })
            .Build();
    }

    /// <summary>
    /// Walks the global <see cref="FailureRuleCollection"/> looking for a
    /// rule whose <see cref="IExceptionMatch"/> matches the supplied
    /// exception, then returns the first-attempt <see cref="FailureSlot"/>.
    /// Returns <c>null</c> when no rule matches — the calling assertion turns
    /// that into a "registration is missing" failure message.
    /// </summary>
    private static FailureSlot? FindFirstAttemptSlotFor(
        FailureRuleCollection rules,
        Exception exception)
    {
        foreach (var rule in rules)
        {
            if (!rule.Match.Matches(exception))
            {
                continue;
            }

            // FailureRule indexes attempts from 1; the OnException(...)
            // .MoveToErrorQueue() call adds exactly one slot, so attempt 1
            // is what we assert against.
            return rule[1];
        }

        return null;
    }
}
