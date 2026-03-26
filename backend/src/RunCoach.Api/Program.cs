using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Prompts;

var builder = WebApplication.CreateBuilder(args);

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
