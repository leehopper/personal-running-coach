using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Pins the <see cref="AsyncLocal{T}"/> semantics of <see cref="RetryAfterCapture"/> that the
/// DEC-073 Retry-After plumbing depends on: scope isolation across concurrent logical calls, no
/// stale-value leakage after a scope is disposed, last-write-wins across attempts within one
/// scope, and no-op recording when no scope is active. The single-element-array-through-AsyncLocal
/// pattern exists because the HTTP handler records from a child async flow whose AsyncLocal
/// writes are invisible to the parent — these tests guard that contract.
/// </summary>
public sealed class RetryAfterCaptureTests
{
    [Fact]
    public void Record_IsNoOp_WhenNoScopeIsActive()
    {
        // Arrange + Act — no scope has been begun on this async flow.
        RetryAfterCapture.Record(30);
        var actualSeconds = RetryAfterCapture.CurrentSeconds;

        // Assert
        actualSeconds.Should().BeNull("recording outside a scope must not allocate or leak state");
    }

    [Fact]
    public void CurrentSeconds_ReturnsNull_AfterScopeIsDisposed()
    {
        // Arrange — a completed scope that captured a value.
        using (RetryAfterCapture.BeginScope())
        {
            RetryAfterCapture.Record(30);
        }

        // Act — a later record on the same async context, with no active scope.
        RetryAfterCapture.Record(45);
        var actualSeconds = RetryAfterCapture.CurrentSeconds;

        // Assert — the disposed scope's value must not leak into a later call.
        actualSeconds.Should().BeNull("a disposed scope must clear its captured value");
    }

    [Fact]
    public void Record_LastWriteWins_AcrossAttemptsWithinOneScope()
    {
        // Arrange
        using var scope = RetryAfterCapture.BeginScope();

        // Act — one record per simulated SDK retry attempt.
        RetryAfterCapture.Record(10);
        RetryAfterCapture.Record(20);
        RetryAfterCapture.Record(7);
        var actualSeconds = RetryAfterCapture.CurrentSeconds;

        // Assert
        actualSeconds.Should().Be(7, "the final attempt's Retry-After is the value the translation attaches");
    }

    [Fact]
    public void CurrentSeconds_ReturnsNull_WhenScopeActiveButNothingRecorded()
    {
        // Arrange
        using var scope = RetryAfterCapture.BeginScope();

        // Act
        var actualSeconds = RetryAfterCapture.CurrentSeconds;

        // Assert
        actualSeconds.Should().BeNull("a scope with no observed Retry-After header carries no hint");
    }

    [Fact]
    public async Task BeginScope_IsolatesConcurrentLogicalCalls()
    {
        // Arrange — two concurrent logical calls, each opening its own scope and recording after
        // an await (mirroring the HTTP handler's child async flow). Each must read back only its
        // own value. The completion sources hold both scopes open until both have recorded.
        var firstRecorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRecorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        static async Task<int?> RunLogicalCall(int seconds, TaskCompletionSource mine, Task other)
        {
            using var scope = RetryAfterCapture.BeginScope();
            await Task.Yield();
            RetryAfterCapture.Record(seconds);
            mine.SetResult();
            await other;
            return RetryAfterCapture.CurrentSeconds;
        }

        // Act
        var firstCall = RunLogicalCall(11, firstRecorded, secondRecorded.Task);
        var secondCall = RunLogicalCall(22, secondRecorded, firstRecorded.Task);
        var actualFirstSeconds = await firstCall;
        var actualSecondSeconds = await secondCall;

        // Assert
        actualFirstSeconds.Should().Be(11, "each logical call must observe only its own scope's capture");
        actualSecondSeconds.Should().Be(22, "each logical call must observe only its own scope's capture");
    }
}
