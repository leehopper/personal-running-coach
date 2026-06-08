using FluentAssertions;
using JasperFx.Events;
using Marten;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Modules.Training.Plan;

/// <summary>
/// Targeted DEC-067 registration guard for the Slice 3 adaptation events. Unlike
/// <c>MartenStoreOptionsCompositionTests.AllRegisteredEventsHaveSchemaVersion</c>
/// — which iterates <see cref="MartenConfiguration.RegisteredEventTypes"/> itself and
/// therefore cannot detect an event dropped from that list — this test names
/// <see cref="PlanAdaptedFromLog"/> and <see cref="SafetySignalRaised"/> directly. If
/// either is removed from <see cref="MartenConfiguration.RegisteredEventTypes"/>, the
/// <c>Apply</c> registration loop never maps it, the EventGraph never learns it, and
/// the assertion below fails the build — exactly the omission the spec requires the
/// guard to catch (a forgotten registration would persist the event without a
/// <c>_v{N}</c> suffix and be silently dropped by <c>SkipUnknownEvents</c>).
/// </summary>
public sealed class PlanAdaptationEventRegistrationTests(RunCoachAppFactory factory)
{
    public static TheoryData<Type> AdaptationEventTypes() =>
    [
        typeof(PlanAdaptedFromLog),
        typeof(SafetySignalRaised),
    ];

    [Theory]
    [MemberData(nameof(AdaptationEventTypes))]
    public void AdaptationEvent_IsRegistered_WithSchemaVersionSuffix(Type adaptationEventType)
    {
        // Arrange — bare production-shape store; no session opened.
        using var store = DocumentStore.For(opts =>
        {
            MartenConfiguration.Apply(opts, factory.ConnectionString);
        });

        // Act — the EventGraph as registered by MartenConfiguration.Apply.
        IReadOnlyList<IEventType> known = store.Events.AllKnownEventTypes();

        // Assert — the event is present (caught the omission) and carries the
        // _v{N} schema-version suffix (DEC-067), independently of RegisteredEventTypes.
        var mapping = known.SingleOrDefault(m => m.EventType == adaptationEventType);
        mapping.Should().NotBeNull(
            because: $"{adaptationEventType.Name} must be listed in MartenConfiguration.RegisteredEventTypes; " +
                     "a missing entry persists the event without a schema-version suffix and is silently dropped by SkipUnknownEvents");
        mapping!.EventTypeName.Should().MatchRegex(
            @"_v\d+$",
            because: $"{adaptationEventType.Name} must carry the MapEventTypeWithSchemaVersion<T>(N) suffix (DEC-067)");
    }
}
