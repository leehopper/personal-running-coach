using System.Diagnostics;
using FluentAssertions;
using RunCoach.Api.Infrastructure.Marten;

namespace RunCoach.Api.Tests.Infrastructure.MartenUpcaster;

/// <summary>
/// Unit coverage for <see cref="UpcasterTelemetry"/>. Asserts that the span
/// shape (name, tags, status) matches the contract described in DEC-067 /
/// R-072 §9, including the failure-path tags that the success-only smoke
/// test in UpcasterRegressionTests cannot exercise.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UpcasterTelemetryTests
{
    [Fact]
    public void TraceUpcast_HappyPath_EmitsSpanWithFromAndToTags()
    {
        // Arrange: subscribe an ActivityListener to the upcaster source so
        // started Activities are sampled. Without an active listener,
        // ActivitySource.StartActivity returns null and the SetTag calls
        // silently no-op.
        var captured = new List<Activity>();
        using var listener = AttachListener(captured);

        // Act
        var actual = UpcasterTelemetry.TraceUpcast<LegacyShape, CurrentShape>(
            new LegacyShape("payload"),
            old => new CurrentShape(old.Value));

        // Assert: returned value untouched, one Activity captured with the
        // documented tag shape and Ok status.
        actual.Value.Should().Be("payload");

        var upcastSpans = captured
            .Where(a => a.OperationName == $"upcast.{nameof(CurrentShape)}"
                        && (a.GetTagItem("to_type") as string) == typeof(CurrentShape).FullName)
            .ToList();
        var activity = upcastSpans.Should().ContainSingle().Subject;
        activity.OperationName.Should().Be($"upcast.{nameof(CurrentShape)}");
        activity.GetTagItem("from_type").Should().Be(typeof(LegacyShape).FullName);
        activity.GetTagItem("to_type").Should().Be(typeof(CurrentShape).FullName);
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void TraceUpcast_ExceptionPath_TagsActivityWithErrorStatusAndRethrows()
    {
        // Arrange
        var captured = new List<Activity>();
        using var listener = AttachListener(captured);

        var thrown = new InvalidOperationException("upcaster blew up");

        // Act
        var act = () => UpcasterTelemetry.TraceUpcast<LegacyShape, CurrentShape>(
            new LegacyShape("payload"),
            _ => throw thrown);

        // Assert: exception is rethrown unchanged.
        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(thrown);

        // The Activity is disposed inside TraceUpcast on the throw path, so the
        // captured reference still carries the error status + tags the catch
        // block set before rethrowing.
        var upcastSpans = captured
            .Where(a => a.OperationName == $"upcast.{nameof(CurrentShape)}"
                        && (a.GetTagItem("to_type") as string) == typeof(CurrentShape).FullName)
            .ToList();
        var activity = upcastSpans.Should().ContainSingle().Subject;
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("upcaster blew up");
        activity.GetTagItem("exception.type").Should().Be(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void SourceName_MatchesPublishedConstant()
    {
        // Arrange
        const string expectedSourceName = "RunCoach.Marten.Upcaster";

        // Act
        var actualSourceName = UpcasterTelemetry.SourceName;
        var actualActivitySourceName = UpcasterTelemetry.Source.Name;

        // Assert — the Program.cs OTel registration adds a source by this
        // literal; if it ever drifts, traces silently stop being emitted.
        actualSourceName.Should().Be(expectedSourceName);
        actualActivitySourceName.Should().Be(expectedSourceName);
    }

    private static ActivityListener AttachListener(List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == UpcasterTelemetry.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => captured.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed record LegacyShape(string Value);

    private sealed record CurrentShape(string Value);
}
