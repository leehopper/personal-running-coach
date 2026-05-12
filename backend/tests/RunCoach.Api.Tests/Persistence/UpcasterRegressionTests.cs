using System.Diagnostics;
using System.Globalization;
using FluentAssertions;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Infrastructure.Marten;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Tests.Infrastructure;
using LegacyOnboardingV1 = RunCoach.Api.Modules.Coaching.Onboarding.Legacy.Events.V1;

namespace RunCoach.Api.Tests.Persistence;

/// <summary>
/// Synthetic-row regression for the Marten event-upcasting pipeline registered
/// in <see cref="MartenConfiguration"/> (DEC-067 / R-072 §7 Technique B).
/// </summary>
/// <remarks>
/// <para>
/// The test materializes the production-deserialization path against an
/// on-disk row that the new binary has never written. It exercises the
/// <em>registration shape</em>, not a real V1 -> V2 transform — no
/// production event has a V2 yet, so the registered upcaster is an identity
/// transform between byte-identical CLR records. The point is to prove the
/// pipeline is wired so a future V2 schema bump can drop in a real
/// transformation body with confidence.
/// </para>
/// <para>
/// Three layers of evidence land in a single test class:
/// </para>
/// <list type="number">
/// <item><c>RawInsert_ThenFetchStream_LoadsLegacyRowThroughDeserializer</c> —
/// inserts a synthetic legacy row via raw SQL on <c>session.Connection</c>
/// then reads the stream back through the production
/// <c>FetchStreamAsync</c> path, asserting the row materialized at all
/// (proves the V1 EventMapping is reachable from on-disk
/// <c>mt_events.type</c>).</item>
/// <item><c>UpcasterDelegate_IsWired_ForLegacyEventMapping</c> — inspects
/// the live <see cref="EventGraph"/> via reflection to assert the V1 type
/// is registered AND its <c>ReadEventData</c> delegate was replaced by a
/// <c>JsonTransformation</c> closure (Marten's signature that the
/// Upcast&lt;TOld,TNew&gt; registration completed).</item>
/// <item><c>UpcasterTelemetry_EmitsSpan_WithFromAndToTypeTags</c> —
/// directly invokes <see cref="UpcasterTelemetry.TraceUpcast{TOld,TNew}"/>
/// inside an in-process <see cref="ActivityListener"/> to assert the
/// <c>upcast.&lt;event_type&gt;</c> span lands with
/// <c>from_type</c>/<c>to_type</c> tags. This is the load-bearing assertion
/// for R01.10 (Jaeger visibility when any production upcaster fires).</item>
/// </list>
/// </remarks>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class UpcasterRegressionTests(RunCoachAppFactory factory)
    : DbBackedIntegrationTestBase(factory)
{
    private const string TenantId = "00000000-0000-0000-0000-000000000abc";
    private static readonly Guid StreamId = new("11111111-1111-1111-1111-1111111111aa");
    private static readonly DateTimeOffset StartedAt =
        new(2026, 4, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RawInsert_ThenFetchStream_LoadsLegacyRowThroughDeserializer()
    {
        // Arrange — pick up the live store from the SUT host.
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        await InsertLegacyOnboardingStartedRowAsync();

        // Act — read the stream through the production deserialization
        // path. Marten resolves mt_events.type -> EventMapping -> deserializer
        // for each row.
        await using var session = store.LightweightSession(TenantId);
        var events = await session.Events.FetchStreamAsync(
            StreamId,
            token: TestContext.Current.CancellationToken);

        // Assert — the V1 mapping was reachable from the on-disk type
        // string. Any wiring break (missing AddEventType for the legacy
        // CLR, wrong mt_dotnet_type / type column format) surfaces here
        // as zero events read, a deserialization throw, or a wrong CLR
        // type.
        events.Should().HaveCount(
            1,
            because: "exactly one synthetic legacy row was inserted into mt_events");
        var ev = events[0];
        ev.Data.Should().NotBeNull(
            because: "Marten must successfully deserialize the synthetic row through " +
                     "the registered LegacyOnboardingV1 EventMapping (DEC-067)");

        // The Upcast<TOld,TNew>(Func<TOld,TNew>) overload's lambda runs at
        // EventMapping.ReadEventData time; depending on Marten 8.32's
        // dispatch path the IEvent.Data property may carry the V1 type
        // wrapper (with the upcasted value inside) or the current type
        // directly. Either shape proves the row reached the mapping —
        // assert on shape parity instead of the CLR type identity.
        switch (ev.Data)
        {
            case OnboardingStarted current:
                current.UserId.Should().Be(StreamId);
                current.StartedAt.Should().Be(StartedAt);
                break;
            case LegacyOnboardingV1.OnboardingStarted v1:
                v1.UserId.Should().Be(StreamId);
                v1.StartedAt.Should().Be(StartedAt);
                break;
            default:
                throw new InvalidOperationException(BuildUnexpectedTypeMessage(ev.Data));
        }
    }

    [Fact]
    public void UpcasterDelegate_IsWired_ForLegacyEventMapping()
    {
        // Arrange — pick up the live store; inspect the EventGraph state.
        using var scope = Factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var graph = ((global::Marten.DocumentStore)store).Events;
        var known = graph.AllKnownEventTypes();

        // Act — locate the V1 mapping.
        var v1Type = typeof(LegacyOnboardingV1.OnboardingStarted);
        var v1Mapping = known.SingleOrDefault(k => k.EventType == v1Type);

        // Assert — both that the V1 type is registered (AddEventType call
        // landed) and that its read delegate was replaced by the upcaster
        // closure (Upcast<TOld,TNew> registration landed). Without these,
        // legacy rows would deserialize via the default route and silently
        // miss the V1 -> current transform.
        v1Mapping.Should().NotBeNull(
            because: "MartenConfiguration must AddEventType<LegacyOnboardingV1.OnboardingStarted>() " +
                     "so legacy rows route to a V1 EventMapping (DEC-067)");

        var readProp = v1Mapping!.GetType().GetProperty("ReadEventData");
        readProp.Should().NotBeNull(because: "Marten 8.32 EventMapping exposes ReadEventData publicly");
        var reader = readProp!.GetValue(v1Mapping) as Delegate;
        reader.Should().NotBeNull(because: "the deserializer delegate must be initialized at startup");
        var readerReason = "the Upcast<TOld,TNew> registration replaces ReadEventData with the " +
                           "JsonTransformation closure that drives the V1 -> current transform";
        reader!.Method.Name.Should().Contain("JsonTransformation", readerReason);
    }

    [Fact]
    public void UpcasterTelemetry_EmitsSpan_WithFromAndToTypeTags()
    {
        // Arrange — install an in-process ActivityListener on the same
        // ActivitySource Program.cs registers in the OTel pipeline.
        var captured = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == UpcasterTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => captured.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        // Act — invoke the telemetry-wrapping helper the production
        // upcaster lambda uses (see MartenConfiguration.Apply).
        var legacy = new LegacyOnboardingV1.OnboardingStarted(StreamId, StartedAt);
        var current = UpcasterTelemetry.TraceUpcast<LegacyOnboardingV1.OnboardingStarted, OnboardingStarted>(
            legacy,
            v1 => new OnboardingStarted(v1.UserId, v1.StartedAt));

        // Assert — transform produced the current shape AND a span landed
        // with the required from_type / to_type tags. R01.10 explicitly
        // requires these for Jaeger filtering.
        current.UserId.Should().Be(StreamId);
        current.StartedAt.Should().Be(StartedAt);

        captured.Should().ContainSingle(
            because: "exactly one upcast invocation should produce exactly one span");
        var span = captured[0];
        span.OperationName.Should().Be("upcast.OnboardingStarted");
        span.GetTagItem("from_type").Should().Be(typeof(LegacyOnboardingV1.OnboardingStarted).FullName);
        span.GetTagItem("to_type").Should().Be(typeof(OnboardingStarted).FullName);
        span.Status.Should().Be(ActivityStatusCode.Ok);
    }

    private static string BuildUnexpectedTypeMessage(object data) =>
        $"Unexpected event.Data CLR type {data.GetType().FullName} — neither current " +
        "OnboardingStarted nor the registered Legacy V1 record. MartenConfiguration " +
        "registration is broken.";

    private async Task InsertLegacyOnboardingStartedRowAsync()
    {
        var legacyClrType = typeof(LegacyOnboardingV1.OnboardingStarted);
        var dotnetTypeName =
            $"{legacyClrType.FullName}, {legacyClrType.Assembly.GetName().Name}";

        // mt_events.type column value Marten uses to pick an EventMapping
        // on read. The current OnboardingStarted is registered via
        // MapEventTypeWithSchemaVersion<T>(1) so its name is
        // `onboarding_started_v1`; the legacy V1 record registers under the
        // default snake_case name `onboarding_started` (no suffix). A
        // synthetic row tagged with the no-suffix name therefore routes to
        // the V1 EventMapping.
        const string EventTypeColumn = "onboarding_started";

        var legacyJson = string.Format(
            CultureInfo.InvariantCulture,
            """{{"UserId":"{0}","StartedAt":"{1:o}"}}""",
            StreamId,
            StartedAt);

        var sql = $"""
            INSERT INTO {MartenConfiguration.EventsSchema}.mt_events
                (seq_id, id, stream_id, version, data, type, timestamp,
                 tenant_id, mt_dotnet_type, is_archived)
            VALUES
                (nextval('{MartenConfiguration.EventsSchema}.mt_events_sequence'),
                 @id, @stream, 1, CAST(@data AS jsonb),
                 @type, now(), @tenant, @dotnet_type, false);
            """;

        await using var conn = new NpgsqlConnection(Factory.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);

        // Seed the stream row so the read path finds the stream metadata.
        await using (var streamCmd = conn.CreateCommand())
        {
            streamCmd.CommandText = $"""
                INSERT INTO {MartenConfiguration.EventsSchema}.mt_streams
                    (id, type, version, timestamp, tenant_id, is_archived)
                VALUES (@id, NULL, 1, now(), @tenant, false)
                ON CONFLICT (id, tenant_id) DO NOTHING;
                """;
            streamCmd.Parameters.Add("id", NpgsqlDbType.Uuid).Value = StreamId;
            streamCmd.Parameters.Add("tenant", NpgsqlDbType.Varchar).Value = TenantId;
            await streamCmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (var eventCmd = conn.CreateCommand())
        {
            eventCmd.CommandText = sql;
            eventCmd.Parameters.Add("id", NpgsqlDbType.Uuid).Value = Guid.NewGuid();
            eventCmd.Parameters.Add("stream", NpgsqlDbType.Uuid).Value = StreamId;
            eventCmd.Parameters.Add("data", NpgsqlDbType.Text).Value = legacyJson;
            eventCmd.Parameters.Add("type", NpgsqlDbType.Varchar).Value = EventTypeColumn;
            eventCmd.Parameters.Add("tenant", NpgsqlDbType.Varchar).Value = TenantId;
            eventCmd.Parameters.Add("dotnet_type", NpgsqlDbType.Varchar).Value = dotnetTypeName;
            await eventCmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
    }
}
