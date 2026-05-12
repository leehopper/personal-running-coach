using FluentAssertions;
using JasperFx.Events;
using Marten;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Tests.Infrastructure;

namespace RunCoach.Api.Tests.Persistence;

/// <summary>
/// Pure-options regression guarding DEC-067's schema-version invariant: every
/// event type listed in <see cref="MartenConfiguration.RegisteredEventTypes"/>
/// must carry a <c>_v{N}</c> suffix on its Marten <c>EventTypeName</c> (which
/// becomes <c>mt_events.type</c>) so a future V2 upcaster can land an
/// unambiguous direct V1 -> current transform without a column-content scan.
/// </summary>
/// <remarks>
/// <para>
/// The test runs against a bare <see cref="DocumentStore.For(System.Action{StoreOptions})"/>
/// — no schema bootstrap, no projection daemon, no Wolverine — so it stays
/// in the fast pre-push budget and detects orphans on every
/// <c>dotnet build</c>. The Testcontainers connection string is supplied
/// only so <c>StoreOptions.Validate()</c> can satisfy its tenancy check; no
/// session is ever opened. The failure mode this defends against: a
/// contributor adds a new event record, wires it into a <c>StartStream</c>
/// call, but forgets to list it in <see cref="MartenConfiguration.RegisteredEventTypes"/>.
/// That row would persist without a version suffix and silently consume
/// new upcasters at deserialization time. This test fails on the omission.
/// </para>
/// </remarks>
public sealed class MartenStoreOptionsCompositionTests(RunCoachAppFactory factory)
{
    [Fact]
    public void AllRegisteredEventsHaveSchemaVersion()
    {
        // Arrange — build a bare store with production-shape options.
        // StoreOptions.Validate requires a tenancy/connection; we satisfy
        // it via the assembly-fixture's Testcontainers connection string
        // but never open a session.
        using var store = DocumentStore.For(opts =>
        {
            MartenConfiguration.Apply(opts, factory.ConnectionString);
        });

        // Act — enumerate every registered event type.
        IReadOnlyList<IEventType> known = store.Events.AllKnownEventTypes();

        // Assert — every event in RegisteredEventTypes has an EventGraph
        // entry whose EventTypeName carries the snake_cased base name plus
        // a `_v{N}` suffix (the canonical MapEventTypeWithSchemaVersion shape).
        foreach (var expectedType in MartenConfiguration.RegisteredEventTypes)
        {
            var mapping = known.SingleOrDefault(m => m.EventType == expectedType);
            mapping.Should().NotBeNull(
                because: $"event type {expectedType.Name} must be present in the EventGraph; " +
                         "registration likely missing from MartenConfiguration.Apply or RegisteredEventTypes");
            var reason = $"event type {expectedType.Name} must have a schema-version suffix " +
                         "(MapEventTypeWithSchemaVersion<T>(N)) so mt_events.type is unambiguous (DEC-067)";
            mapping!.EventTypeName.Should().MatchRegex(@"_v\d+$", reason);
        }
    }

    [Fact]
    public void RegisteredEventTypes_IsNonEmptyAndDistinct()
    {
        // Arrange — the source-of-truth list.
        var types = MartenConfiguration.RegisteredEventTypes;

        // Act + Assert — keep the list well-formed so the version-suffix
        // assertion above is meaningful. Empty list passes that test
        // vacuously; duplicates indicate a copy-paste error in Apply().
        types.Should().NotBeEmpty(
            because: "RunCoach has at least the Slice 1 events registered (DEC-067)");
        types.Should().OnlyHaveUniqueItems(
            because: "duplicate entries would double-register the same type");
    }
}
