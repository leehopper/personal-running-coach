using FluentAssertions;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Helpers for condition-based waiting in tests, replacing brittle fixed
/// <see cref="Task.Delay(int, CancellationToken)"/> sleeps that flake on
/// slower CI runners. Both overloads fail the test (via FluentAssertions)
/// when the timeout fires without the condition becoming true.
/// </summary>
internal static class AsyncWait
{
    /// <summary>
    /// Polls <paramref name="condition"/> every <paramref name="pollInterval"/>
    /// (default 25 ms) until it returns <c>true</c> or
    /// <paramref name="timeout"/> elapses.
    /// </summary>
    public static Task UntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        string becauseMessage,
        CancellationToken ct,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return UntilAsync(() => Task.FromResult(condition()), timeout, becauseMessage, ct, pollInterval);
    }

    /// <summary>
    /// Polls the async <paramref name="condition"/> every
    /// <paramref name="pollInterval"/> (default 25 ms) until it returns
    /// <c>true</c> or <paramref name="timeout"/> elapses.
    /// </summary>
    public static async Task UntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        string becauseMessage,
        CancellationToken ct,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var interval = pollInterval ?? TimeSpan.FromMilliseconds(25);
        var deadline = DateTime.UtcNow + timeout;
        bool reached = false;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition().ConfigureAwait(false))
            {
                reached = true;
                break;
            }

            await Task.Delay(interval, ct).ConfigureAwait(false);
        }

        reached.Should().BeTrue(becauseMessage);
    }
}
