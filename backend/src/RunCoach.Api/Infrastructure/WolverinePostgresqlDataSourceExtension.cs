using Npgsql;
using Wolverine;
using Wolverine.Postgresql;

namespace RunCoach.Api.Infrastructure;

/// <summary>
/// Points Wolverine's durable outbox at the shared <see cref="NpgsqlDataSource"/>
/// resolved from DI — never the connection-string overload — so future Postgres
/// password rotation flows through the bus without a restart (DEC-046 /
/// wolverine#691). Wolverine discovers registered <see cref="IWolverineExtension"/>
/// implementations from the IoC container at bootstrap time, giving us access to
/// the built service provider that the raw <c>UseWolverine(opts =&gt; ...)</c>
/// callback does not have.
/// </summary>
public class WolverinePostgresqlDataSourceExtension(IServiceProvider provider) : IWolverineExtension
{
    public void Configure(WolverineOptions options)
    {
        options.PersistMessagesWithPostgresql(provider.GetRequiredService<NpgsqlDataSource>());
    }
}
