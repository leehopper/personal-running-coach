using System.Text;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RunCoach.Api.Infrastructure;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Identity.Entities;
using Wolverine;
using Wolverine.EntityFrameworkCore;

// WebApplication.CreateBuilder registers the three default host-config sources
// (appsettings.json, appsettings.{Env}.json, user-secrets) with
// `reloadOnChange: true`, which installs a FileSystemWatcher per source. On
// macOS arm64, each watcher's StartRaisingEvents calls `Interop.Sys.Sync()`
// synchronously and stalls unboundedly — CreateBuilder never returns. Disabling
// host-config reload elides the watchers and lets CreateBuilder complete in
// under a second. Config reload on file change is a dev-loop nicety, not a
// runtime contract.
Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

var builder = WebApplication.CreateBuilder(args);

// JasperFx code-generation hook — must run before AddMarten / UseWolverine so
// `CritterStackDefaults` reach both Marten and Wolverine generators (DEC-048 /
// R-054). Without this, Production `TypeLoadMode.Static` applies to Marten only
// and Wolverine keeps running Roslyn at startup.
builder.Host.ApplyJasperFxExtensions();

// Shared NpgsqlDataSource — EF Core, Marten, Wolverine, and DataProtection all
// resolve against this single Aspire-registered singleton so
// `UsePeriodicPasswordProvider` (DEC-046) will later flow credential rotation
// through every consumer without restart. Never construct a second
// `NpgsqlDataSourceBuilder` anywhere; doing so forks the rotation seam.
builder.AddNpgsqlDataSource("runcoach");

// Marten store with production-shape configuration. `.IntegrateWithWolverine()`
// installs Wolverine envelope-storage tables in the `runcoach_events` schema as
// a side effect — that call is the SOLE envelope-storage wiring (DEC-048).
// `opts.PersistMessagesWithPostgresql(...)` inside `UseWolverine` is prohibited
// by the same decision — calling both double-wires the envelope tables and is
// the leading suspected cause of the 2026-04-20 startup hang.
builder.Services.AddRunCoachMarten();

// Wolverine host. Envelope storage is already wired by Marten; this block
// configures transactional middleware, durability mode, the EF-context
// integration Wolverine's handler-discovery codegen needs to see, and the
// per-environment code-generation mode.
builder.Host.UseWolverine(opts =>
{
    opts.Policies.AutoApplyTransactions();

    // Must match Marten's `AddAsyncDaemon(DaemonMode.Solo)` — `HotCold` takes
    // advisory locks that collide with `ApplyAllDatabaseChangesOnStartup`.
    opts.Durability.Mode = DurabilityMode.Solo;
    opts.Durability.MessageStorageSchemaName = MartenConfiguration.EventsSchema;

    // The DbContext registration MUST live inside this callback — Wolverine's
    // codegen only discovers DbContexts registered via `opts.Services`, and
    // top-level `builder.Services.AddDbContextWithWolverineIntegration<T>(...)`
    // silently disables `AutoApplyTransactions` middleware (DEC-048).
    opts.Services.AddDbContextWithWolverineIntegration<RunCoachDbContext>((sp, options) =>
        options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));

    opts.CodeGeneration.TypeLoadMode = builder.Environment.IsProduction()
        ? TypeLoadMode.Static
        : TypeLoadMode.Auto;
});

// Thin DbContext exposing the DataProtection key entity set. Parameterless
// `UseNpgsql()` auto-resolves the DI-registered `NpgsqlDataSource` singleton
// (EF 9 / Npgsql 8+ canonical behavior per efcore.pg #2821).
builder.Services.AddDbContext<DpKeysContext>(options => options.UseNpgsql());

// Apply pending EF migrations as an `IHostedService` on startup in Development.
// Running inside `StartAsync` rather than between `Build()` and `RunAsync()`
// keeps the `HostFactoryResolver` 5-min window safe (DEC-048). Production
// migrations ship as an `efbundle` deploy step per R-046.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<DevelopmentMigrationService>();
}

// DataProtection persists the application-cookie / antiforgery signing keys to
// Postgres via `DpKeysContext` (DEC-046). No filesystem `/keys` volume. 90-day
// rotation per framework defaults + OWASP / NIST cadence. `ProtectKeysWith*`
// wraps this registration at MVP-1 pre-public-release.
builder.Services.AddDataProtection()
    .SetApplicationName("runcoach")
    .PersistKeysToDbContext<DpKeysContext>()
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

// ASP.NET Core Identity (core services only — NOT `AddIdentityApiEndpoints` or
// `AddIdentity`). `AddIdentityCore` avoids the auto-mounted `/register` +
// `/login` endpoints `AddIdentityApiEndpoints` ships; Slice 0 hand-rolls those
// in `Modules/Identity/AuthController` so the contract stays under project
// control and lands the `CookieOrBearer`-ready dual-scheme seam (R-045).
// `AddRoles` brings the role-store services `UserManager.IsInRoleAsync` and
// downstream authorization policies require. Password policy meets OWASP
// ASVS L1 composition guidance (≥ 12 chars, four character classes).
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        // Identity's default is false; without this flag email uniqueness is
        // only enforced incidentally via `RequireUniqueUserName = true` +
        // `user.UserName = user.Email` at register time. Duplicates then fire
        // `DuplicateUserName` instead of `DuplicateEmail`, and any future
        // decoupling of UserName from Email silently drops email uniqueness.
        // Setting it true closes the latent bug (DEC-054 / R-058).
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<RunCoachDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var jwtAuthOptions = builder.Services
    .AddOptions<JwtAuthOptions>()
    .BindConfiguration(JwtAuthOptions.SectionName);

// Fail-fast in Production / Staging if Auth:Jwt is missing or malformed.
// The validator registration itself is gated — not just ValidateOnStart —
// because IValidateOptions<T> fires lazily on every IOptions<T>.Value
// access, and the JwtBearer handler resolves IOptions<JwtAuthOptions>.Value
// whenever PolicyEvaluator iterates the CookieOrBearer policy's scheme
// list (even on requests with no bearer token). Registering the validator
// only outside Development keeps Dev / CI tests booting with an unset
// Auth:Jwt section while keeping Prod / Staging fail-fast.
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IValidateOptions<JwtAuthOptions>, JwtAuthOptionsValidator>();
    jwtAuthOptions.ValidateOnStart();
}

// The bearer scheme is registered as a non-default handler so an iOS client
// (DEC-033) can authenticate against the same authorization policies without
// further changes. `AddJwtBearer` hangs off `AuthenticationBuilder`, whereas
// `AddIdentityCookies` returns an `IdentityCookiesBuilder`, so the two calls
// cannot be chained. `TokenValidationParameters` are composed by the options
// pipeline below, not here, so env-var overrides applied at test-init time
// are picked up.
var authBuilder = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);
authBuilder.AddIdentityCookies(options =>
{
    options.ApplicationCookie?.Configure(cookie =>
    {
        cookie.Cookie.Name = "__Host-RunCoach";

        // RFC 6265bis §5.6 requires the Path attribute to be present (not merely
        // defaulted) for the __Host- prefix to satisfy the browser storage check
        // (R-056). Omitting it silently drops the cookie in Chrome / Safari.
        cookie.Cookie.Path = "/";
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        cookie.Cookie.SameSite = SameSiteMode.Lax;
        cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
        cookie.SlidingExpiration = true;
    });
});
authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

// Strict-always validation per R-057. With config absent, IssuerSigningKey is
// null and every token fails closed with IDX10500; when config lands the same
// flags stay on. Gating Validate* on config presence (the previous shape)
// inverts "secure by default" and becomes a latent landmine under future
// Authority= / HMAC-only edits (RFC 8725 §2.1 / §3.9, OWASP JWT Cheat Sheet).
// ValidAlgorithms pinned to HS256 closes the HS/RS key-confusion attack class.
// MapInboundClaims = false keeps raw JWT claim names (sub / role) instead of
// the SOAP-style claim rewriting JwtSecurityTokenHandler inherits by default.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtAuthOptions>>((bearer, jwt) =>
    {
        var o = jwt.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidIssuer = o.Issuer,
            ValidAudience = o.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(o.SigningKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey))
                {
                    KeyId = o.KeyId ?? "current",
                },
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthPolicies.CookieOrBearer,
        policy => policy
            .AddAuthenticationSchemes(
                IdentityConstants.ApplicationScheme,
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "__Host-Xsrf";
});

// 307 is the HttpsRedirectionMiddleware default; pinning it explicitly avoids
// a later contributor flipping to 308 (aggressively cached by browsers /
// intermediates — dotnet/aspnetcore docs warn against permanent HTTPS
// redirects). HttpsPort is resolved from `ASPNETCORE_HTTPS_PORT`, the
// `https_port` host setting, or `IServerAddressesFeature` in that order — the
// RunCoachAppFactory sets the host setting so the warning log stays silent
// under the in-memory TestServer (R-056).
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});

// OpenTelemetry tracing + metrics. ActivitySources + Meters are registered
// unconditionally so instrumentation is always wired; the OTLP exporter is
// gated on `OTEL_EXPORTER_OTLP_ENDPOINT` being set so test runs without a
// collector never block on the 30s shutdown flush (DEC-048). The Jaeger
// overlay (`docker-compose.otel.yml`) sets the env var; default Compose and
// `dotnet test` leave it unset.
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otlpExporterEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "runcoach-api",
        serviceVersion: "0.1.0",
        serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Marten", "Wolverine", "RunCoach.Llm", "Npgsql")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (otlpExporterEnabled)
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Marten", "Wolverine", "RunCoach.Llm", "Npgsql")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (otlpExporterEnabled)
        {
            metrics.AddOtlpExporter();
        }
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddApplicationModules(builder.Configuration);

// Enables the RFC 7807 problem-details writer (IProblemDetailsService) for
// every unhandled status code + for responses written via UseExceptionHandler.
// [ApiController] already produces ValidationProblemDetails on 400s; this
// registration extends the ProblemDetails shape to 500s and the built-in
// status-code middleware without further configuration.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandlingMiddleware>();

// CORS — explicit origin is mandatory because `AllowCredentials()` is
// incompatible with `AllowAnyOrigin()` (per the CORS spec, and ASP.NET Core
// throws at the first preflight). HTTPS-only because the `__Host-RunCoach`
// application cookie is `Secure`-flagged and only transmitted over TLS.
// `AllowCredentials` is required so the browser includes the cookie (and
// echoes the `X-XSRF-TOKEN` header) on cross-origin API calls from the Vite
// dev server.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:5173")
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Surface scoped-from-root DI bugs as Build-time errors rather than as silent
// 5-min `HostFactoryResolver` timeouts (DEC-048). `ValidateOnBuild` stays off
// until Wolverine handler types are pre-generated via `dotnet run -- codegen write`.
if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = false;
    });
}

var app = builder.Build();

// Fail fast at startup if any configured prompt YAML files are missing on disk.
var promptStore = app.Services.GetRequiredService<IPromptStore>();
if (promptStore is YamlPromptStore yamlStore)
{
    yamlStore.ValidateConfiguredVersions();
}

// Global exception handler runs first so it catches anything thrown by
// downstream middleware or endpoint handlers. Combined with AddProblemDetails
// every uncaught exception surfaces as an RFC 7807 application/problem+json
// response rather than an HTML error page. The Development detail is verbose;
// other environments return a generic detail (handled inside ErrorHandlingMiddleware).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ungated per .NET 10 template idiom (R-056). The middleware safely no-ops
// when it cannot resolve an HTTPS port — `HttpsRedirectionMiddleware` logs
// `[3] "Failed to determine the https port for redirect."` and passes through
// as HTTP, rather than redirect-looping. Under `WebApplicationFactory` the
// test client's `BaseAddress = https://localhost` flips `Request.IsHttps=true`
// so the middleware short-circuits without any redirect at all.
app.UseHttpsRedirection();

// Middleware order (Microsoft guidance + R-044 auth pipeline):
//   Routing → CORS → Authentication → Authorization → Antiforgery → Endpoints.
// `UseAntiforgery` must run after `UseAuthorization` so the antiforgery
// middleware sees the authenticated principal (spec 12 §Unit 2). Static
// files + SPA fallback are not wired in Slice 0 — the Vite dev server
// serves the SPA; when the API eventually hosts the built SPA those two
// slots land between `UseHttpsRedirection` and `UseRouting` / after
// `MapControllers` respectively.
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

// Process-liveness probe — DB connectivity is covered by Marten /
// `ApplyAllDatabaseChangesOnStartup` and EF Core's `MigrateAsync` path.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = static (context, _) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync("""{"status":"ok"}""");
    },
});

await app.RunAsync();

// `public partial class Program;` trailer intentionally omitted — the .NET 10
// Web SDK source-generates it (DEC-048 / analyzer ASP0027).
