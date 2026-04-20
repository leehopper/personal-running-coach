using Microsoft.EntityFrameworkCore;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Prompts;
using Wolverine;
using Wolverine.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Shared Npgsql data source — EF Core, Marten, Wolverine, and DataProtection all
// resolve against this one instance so `UsePeriodicPasswordProvider` (DEC-046)
// will later flow DB-credential rotation through every consumer without restart.
builder.AddNpgsqlDataSource("runcoach");

// Minimal Wolverine host bootstrap so `AddDbContextWithWolverineIntegration`
// can register its transactional middleware; outbox + CritterStack defaults
// land in T01.2.
builder.Host.UseWolverine();

// Primary relational DbContext — owns Identity + DataProtection keys +
// Wolverine envelope storage tables in a single migration stream.
builder.Services.AddDbContextWithWolverineIntegration<RunCoachDbContext>(
    options => options.UseNpgsql());

// Thin DbContext exposing the DataProtection key entity set. Schema for the
// table is materialized by RunCoachDbContext's initial migration; registration
// of `PersistKeysToDbContext<DpKeysContext>` lands in T01.3.
builder.Services.AddDbContext<DpKeysContext>(options => options.UseNpgsql());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddApplicationModules(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Apply pending EF migrations on Development startup. EF Core 10's
// `IMigrationsDatabaseLock` makes this safe under concurrent starts; prod
// migrations ship as a bundled step per R-046 (deferred to MVP-1).
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RunCoachDbContext>();
    await db.Database.MigrateAsync();
}

// Fail fast at startup if any configured prompt YAML files are missing on disk.
var promptStore = app.Services.GetRequiredService<IPromptStore>();
if (promptStore is YamlPromptStore yamlStore)
{
    yamlStore.ValidateConfiguredVersions();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

#pragma warning disable S1118 // Class is required partial for WebApplicationFactory test support
public partial class Program;
#pragma warning restore S1118
