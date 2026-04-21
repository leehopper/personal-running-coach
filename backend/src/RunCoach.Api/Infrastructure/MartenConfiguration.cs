using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using Marten;
using Marten.Services;
using Marten.Storage;
using Wolverine.Marten;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Registers the Marten document store with the production-shape configuration:
/// stream-per-user Guid identity, conjoined multitenancy on the
/// <c>runcoach_events</c> schema, Quick append mode, Solo async daemon, and
/// Wolverine outbox integration via <c>IntegrateWithWolverine()</c>.
/// Registration-only for Slice 0; no documents or streams are written yet.
/// </summary>
public static class MartenConfiguration
{
    public const string EventsSchema = "runcoach_events";

    /// <summary>
    /// Registers Marten against the shared <see cref="Npgsql.NpgsqlDataSource"/>
    /// and applies <see cref="CritterStackDefaults"/> so development auto-builds
    /// schema while production runs statically compiled code and never touches DDL.
    /// </summary>
    public static IServiceCollection AddRunCoachMarten(this IServiceCollection services)
    {
        services.AddMarten(opts =>
            {
                opts.DatabaseSchemaName = EventsSchema;
                opts.Events.DatabaseSchemaName = EventsSchema;
                opts.Events.StreamIdentity = StreamIdentity.AsGuid;
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;
                opts.Events.AppendMode = EventAppendMode.Quick;
                opts.Events.UseIdentityMapForAggregates = true;

                // `EnableAdvancedAsyncTracking` intentionally left at its
                // default (`false`) — it adds per-session cost with no current
                // load-bearing value. Flip on only when a concrete test-side
                // `WaitForNonStaleData` assertion needs it.
                opts.Policies.AllDocumentsAreMultiTenanted();

                opts.Projections.Errors.SkipUnknownEvents = true;

                // Surface Marten connection-use spans (`marten.connection`) and
                // the appended-event counter to the OTel pipeline wired in
                // Program.cs. `TrackLevel.Normal` is the spec default; bump to
                // `Verbose` temporarily when chasing N+1 or write-amplification
                // regressions in dev.
                opts.OpenTelemetry.TrackConnections = TrackLevel.Normal;
                opts.OpenTelemetry.TrackEventCounters();
            })
            .UseLightweightSessions()
            .UseNpgsqlDataSource()
            .IntegrateWithWolverine()
            .ApplyAllDatabaseChangesOnStartup()
            .AddAsyncDaemon(DaemonMode.Solo);

        // CritterStackDefaults covers both Marten and Wolverine in the shared
        // JasperFx code-generation pipeline — one setting, both tools aligned.
        services.CritterStackDefaults(x =>
        {
            x.Development.ResourceAutoCreate = AutoCreate.CreateOrUpdate;
            x.Production.ResourceAutoCreate = AutoCreate.None;
            x.Production.GeneratedCodeMode = TypeLoadMode.Static;
        });

        return services;
    }
}
