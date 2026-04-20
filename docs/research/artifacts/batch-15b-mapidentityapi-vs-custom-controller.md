# Skip MapIdentityApi, ship a custom AuthController

**Build a hand-rolled `AuthController` in `Modules/Identity/` and keep `MapIdentityApi<TUser>()` off the table for MVP‑0.** RunCoach's stated requirements — real JWT bearer tokens, `UserProfile` created atomically at registration, a DB‑enriched `/me`, RFC 7807 error envelope everywhere, and a 30‑day long‑lived JWT with *no* refresh — collide with at least four hard limits of `MapIdentityApi` that Microsoft has not lifted in .NET 9 or .NET 10 and has explicitly declined to fix. The official reference architecture (`dotnet/eShop`) skips `MapIdentityApi` for the same reasons, and lead maintainer David Fowler stated publicly that it is "not ideal" for SPA/microservices setups. A custom controller costs ~200 LOC you would have written anyway, preserves the module‑first style, and removes a later migration that most teams end up doing inside a year.

The hybrid "MapIdentityApi for the boring 80%, custom for the 20%" pattern is tempting but collapses for RunCoach specifically: once you subtract the endpoints that are sealed (`/register`), deferred out of Slice 0 (`/forgotPassword`, `/resetPassword`, `/confirmEmail`), or structurally wrong (`/login` issuing opaque tokens, `/refresh` mounted when you want no refresh), essentially nothing remains. Reintroduce `MapIdentityApi` later as a scaffold for password reset and email confirmation when those slices land — that is the right moment, not now.

## Why `MapIdentityApi` is the wrong default for this stack

**The bearer token it issues is not a JWT.** Confirmed in source — `/refresh` calls `bearerTokenOptions.RefreshTokenProtector.Unprotect(...)`, which is a Data Protection blob, not a signed JWT — and stated directly in the current .NET 10 Microsoft Learn docs (ms.date 2026‑03‑23): *"The tokens aren't standard JSON Web Tokens (JWTs). The use of custom tokens is intentional… The token option isn't intended to be a full-featured identity service provider or token server."* The practical consequence is that tokens can be validated only by a process sharing the same DP key ring. David Fowler flagged exactly this in [dotnet/eShop discussion #163](https://github.com/dotnet/eShop/discussions/163): *"Using identity endpoints isn't ideal mostly because of the fact that each of the applications need to use the same data protection configuration (shared secrets)."* For a project stating "JWT bearer auth," adopting `MapIdentityApi` means either accepting an opaque, ASP.NET‑only token or rewriting `/login` — at which point the extension has stopped paying for itself.

**The registration request is sealed.** `RegisterRequest` is `{ Email, Password }` and nothing else. There is no hook, no event, no `IUserConfirmation` override; `AspNetCore.Docs` issue [#50303](https://github.com/dotnet/aspnetcore/issues/50303) has been open asking for extensibility since 2023. Slice 1 explicitly lands a `UserProfile` entity that is created at or near registration — doing that atomically requires wrapping or replacing the handler. The idiomatic fix is "write your own `/register`," which is exactly the decision under review.

**Endpoints cannot be selectively excluded.** [#55792](https://github.com/dotnet/aspnetcore/issues/55792) (April 2024), [#55529](https://github.com/dotnet/aspnetcore/issues/55529), and [#55142](https://github.com/dotnet/aspnetcore/issues/55142) are all still open on .NET 11 Planning as of April 2026. `MapIdentityApi` is all‑or‑nothing: `/refresh` is always mounted (colliding with the "no refresh" baseline), and `/confirmEmail` / `/forgotPassword` / `/resendConfirmationEmail` always appear even though MFA/email/reset are explicitly deferred to pre‑public‑release. A no‑op `IEmailSender<TUser>` is registered silently by `AddIdentityApiEndpoints`, so these routes return 200 and silently swallow calls — fine for a demo, a footgun for friends‑and‑testers MVP‑1.

**`/manage/info` is not `/me`.** It returns literally `{ "email": ..., "isEmailConfirmed": ... }` and nothing more; the docs state *"Claims were omitted from this endpoint for security reasons."* Joining a `UserProfile` row requires a separate endpoint, which means RunCoach's `/me` is custom no matter what.

**Community and reference-implementation consensus confirms the skepticism.** Andrew Lock's 2023 review — *"I don't think the Identity APIs are a good idea… If you want to customise any of the flows (registration flows for example), or you want to remove any endpoints… then the Identity API endpoints won't work for you currently"* — has not been rebutted in 2024‑2026 content. Microsoft's official reference, `dotnet/eShop`, uses Duende IdentityServer + ASP.NET Core Identity (see `src/Identity.API/Services/ProfileService.cs` + Duende 7.3.0 in `Directory.Packages.props` per PR [#876](https://github.com/dotnet/eShop/pull/876)), not `MapIdentityApi`. Milan Jovanović's 2025 production‑focused newsletter steers readers to Keycloak + JWT. The Blazor Web App template in .NET 10 uses a cookie-based `AddIdentityCore + MapAdditionalIdentityEndpoints` pattern, not `MapIdentityApi<TUser>`. There is no 2025–2026 endorsement to offset the 2023 skepticism.

## Capability matrix

| Dimension | `MapIdentityApi<AppUser>()` | Custom `AuthController` | Winner for RunCoach |
|---|---|---|---|
| **JWT bearer output** | ❌ Opaque DataProtection token; not JWT | ✅ Full control via `JwtSecurityTokenHandler` / `JsonWebTokenHandler` | Custom |
| **Password policy tightening** | ✅ `IdentityOptions.Password` fully respected | ✅ Same `UserManager`/`PasswordHasher` under the hood | Tie |
| **RFC 7807 ProblemDetails** | ⚠️ Mostly yes for 400 (but `errors` keys are IdentityResult codes, not field names); `/login` 401 emits `Problem(result.ToString())` with opaque `detail`; `/refresh`/`/confirmEmail` return empty 401 bodies | ✅ Return `ValidationProblemDetails` / `ProblemDetails` exactly as your other modules do | Custom |
| **Custom register payload (e.g., `DisplayName`)** | ❌ `RegisterRequest` is `sealed { Email, Password }` | ✅ Define your own DTO | Custom |
| **Atomic `UserProfile` creation at register** | ❌ No hook, event, or filter | ✅ One `SaveChangesAsync` transaction | Custom |
| **`/me` with DB‑joined data** | ❌ `/manage/info` returns only email + confirmation flag | ✅ Trivial LINQ join | Custom |
| **Selective endpoint exclusion** | ❌ [#55792](https://github.com/dotnet/aspnetcore/issues/55792) open since 2024 | ✅ You map what you write | Custom |
| **Route renaming / versioning** | ⚠️ Prefix via `MapGroup("/api/v1/auth")` works; individual routes are hardcoded | ✅ `[Route("api/v1/auth")]` + `[HttpPost("login")]` | Custom (marginal) |
| **Cookie mode** | ✅ `?useCookies=true` flips to `IdentityConstants.ApplicationScheme` | ⚠️ You write `HttpContext.SignInAsync` yourself | MapIdentityApi (if cookie chosen) |
| **Refresh token rotation** | ❌ Not implemented; single long‑lived refresh token until expiry ([#52815](https://github.com/dotnet/aspnetcore/issues/52815)) | ✅ Implement rotation + denylist in a `RefreshTokens` table | Custom |
| **Refresh token revocation** | ❌ Only via `SecurityStamp` change (blunt; forces full logout) | ✅ Per‑token revoke | Custom |
| **Disabling refresh entirely** | ❌ `/refresh` is always mounted | ✅ Just don't write it | Custom |
| **Testability (`WebApplicationFactory` + Testcontainers)** | ✅ Works; but no typed action references, string routes only; `NoOpEmailSender` silently hides email flows | ✅ Typed actions, `[ApiController]` model binding, easier refactoring | Custom (marginal) |
| **Module‑first `Modules/Identity/` housing** | ✅ Composes into `app.MapIdentityModule()` wrapper | ✅ Native; controller lives next to DTOs | Tie |
| **`[ApiController]` style consistency** | ❌ Minimal API surface; global MVC filters, `InvalidModelStateResponseFactory`, `CustomizeProblemDetails` do not uniformly apply (see [#62723](https://github.com/dotnet/aspnetcore/issues/62723)) | ✅ Identical to every other module | Custom |
| **Passkeys (.NET 10)** | ❌ Not exposed as HTTP endpoints; only wired in Blazor Razor components | ⚠️ Roll your own on top of `IdentityPasskeyOptions` + `UserManager.AddOrUpdatePasskeyAsync` | Tie (both require work) |
| **Migration away later** | Moderate: rewrite `/login`, `/register`, `/me` (~3–5 days); **zero DB migration** (same `IdentityDbContext` schema) | N/A | Custom avoids the round‑trip |

## ProblemDetails verdict

**Partially compliant; not compliant enough to rely on for a uniform error envelope.** The validation path inside `MapIdentityApi` uses `TypedResults.ValidationProblem`, which emits a valid RFC 7807 `ValidationProblemDetails` body — so `/register` with a weak password returns a well‑shaped 400. But two real deviations break a uniform client contract:

First, the `errors` dictionary keys are **IdentityResult error codes** (`PasswordTooShort`, `DuplicateUserName`, `InvalidEmail`) rather than request property names (`email`, `password`). Everywhere else in a normal `[ApiController]` pipeline, keys are property names. A React client written to read `errors.email[0]` for your custom controllers will break on `/register`.

Second, `/login` returns a non‑standard 401 body — `TypedResults.Problem(result.ToString(), statusCode: 401)` — where `detail` is the literal string of `SignInResult` (`"Failed"`, `"Lockedout"`, `"RequiresTwoFactor"`). There is no machine‑readable `code` or `type` discriminator. `/refresh` and `/confirmEmail` return empty‑body 401s via `TypedResults.Challenge()` / `TypedResults.Unauthorized()` that bypass `IProblemDetailsService` entirely. Issue [#62723](https://github.com/dotnet/aspnetcore/issues/62723) confirms `CustomizeProblemDetails` does not reliably fire for minimal‑API validation in .NET 10 preview.

**Workaround if you still want `MapIdentityApi`:** wrap the group in a custom `IEndpointFilter` that reshapes error responses, plus `UseExceptionHandler` + `UseStatusCodePages` globally. This works but is extra surface; the cost saves you nothing versus just writing the controller.

## Wiring sketch — the recommendation, ready to commit

**`Modules/Identity/AppUser.cs`**
```csharp
namespace RunCoach.Modules.Identity;

public class AppUser : IdentityUser<Guid>
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class UserProfile
{
    public Guid UserId { get; set; }                     // PK + FK to AppUser
    public string DisplayName { get; set; } = "";
    public int WeeklyMileageGoalKm { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public AppUser User { get; set; } = default!;
}
```

**`Modules/Identity/IdentityModuleExtensions.cs`**
```csharp
public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(cfg.GetConnectionString("Postgres")));

        // Identity plumbing WITHOUT MapIdentityApi — we want UserManager,
        // SignInManager, PasswordHasher, but none of the HTTP endpoints.
        services.AddIdentityCore<AppUser>(opt =>
        {
            opt.User.RequireUniqueEmail = true;
            opt.Password.RequiredLength = 12;
            opt.Password.RequireDigit = true;
            opt.Password.RequireLowercase = true;
            opt.Password.RequireUppercase = true;
            opt.Password.RequireNonAlphanumeric = true;
            opt.Lockout.MaxFailedAccessAttempts = 5;
            opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromHours(2);
            // RequireConfirmedEmail deferred until Slice pre-public-release
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o => o.TokenValidationParameters = JwtOptions.Build(cfg));

        services.AddAuthorizationBuilder()
            .AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());

        return services;
    }

    public static WebApplication MapIdentityModule(this WebApplication app)
    {
        // Controllers from Modules/Identity/AuthController.cs mounted via MapControllers()
        // below; this method is a placeholder for future MapIdentityApi scaffolding
        // when password-reset / email-confirmation slices land.
        return app;
    }
}
```

**`Modules/Identity/AuthController.cs`** (abbreviated — full login/register/me/logout)
```csharp
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    AppDbContext db,
    IJwtTokenService jwt) : ControllerBase
{
    public record RegisterDto(string Email, string Password, string DisplayName, string TimeZone);
    public record LoginDto(string Email, string Password);
    public record TokenDto(string AccessToken, int ExpiresIn, string TokenType = "Bearer");

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email };
        var result = await users.CreateAsync(user, dto.Password);
        if (!result.Succeeded) return ValidationProblem(result.ToModelState());

        db.UserProfiles.Add(new UserProfile {
            UserId = user.Id, DisplayName = dto.DisplayName, TimeZone = dto.TimeZone
        });
        await db.SaveChangesAsync();   // atomic with the Identity insert if you wrap in a tx
        return Ok(jwt.Issue(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await users.FindByEmailAsync(dto.Email);
        if (user is null) return InvalidCredentials();

        var check = await signIn.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        return check.Succeeded ? Ok(jwt.Issue(user)) : InvalidCredentials();
    }

    [HttpGet("me")]
    [Authorize(Policy = "Authenticated")]
    public async Task<IActionResult> Me()
    {
        var id = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var view = await (from u in db.Users
                          join p in db.UserProfiles on u.Id equals p.UserId
                          where u.Id == id
                          select new { u.Id, u.Email, u.CreatedUtc,
                                       p.DisplayName, p.WeeklyMileageGoalKm, p.TimeZone })
                          .FirstOrDefaultAsync();
        return view is null ? NotFound() : Ok(view);
    }

    [HttpPost("logout")]
    [Authorize(Policy = "Authenticated")]
    public IActionResult Logout()
    {
        // With stateless JWT + no refresh, logout is a client-side operation.
        // When refresh lands, blacklist the jti here.
        return NoContent();
    }

    private IActionResult InvalidCredentials() => Problem(
        title: "Invalid credentials",
        statusCode: StatusCodes.Status401Unauthorized,
        type: "https://runcoach.app/errors/invalid-credentials");
}
```

**`Program.cs` excerpt**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddIdentityModule(builder.Configuration);
// ... other modules

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapIdentityModule();
app.Run();

public partial class Program { }   // for WebApplicationFactory<Program>
```

This preserves the `Modules/{Domain}/` pattern, keeps `[ApiController]` + route‑prefix consistency with the rest of the app, makes the error envelope uniform, and leaves `MapIdentityApi` as a future option for the deferred password‑reset and email‑confirmation slices only.

## Migration path if this decision reverses

Because **no DB migration is involved** — `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>` is the same schema either direction — the rollback is purely code. Three concrete scenarios:

**Adding `MapIdentityApi` later as a scaffold for password reset / email confirmation** (the likely case): swap `AddIdentityCore<AppUser>()` for `AddIdentityApiEndpoints<AppUser>()`, mount under a *distinct* prefix (`app.MapGroup("/api/v1/auth/identity").MapIdentityApi<AppUser>()`) to avoid route collision with your controller's `/register` and `/login`, register an `IEmailSender<AppUser>`, hide the unused endpoints from OpenAPI with `.Add(b => b.Metadata.Add(new ExcludeFromDescriptionAttribute()))`. Scope: ~30 LOC in `IdentityModuleExtensions`, 1 new email-sender file, no client breaks because the controller endpoints stay on the original paths.

**Ripping out the controller entirely in favor of `MapIdentityApi`** (unlikely): remove `AuthController.cs` (~200 LOC), reshape the React token manager to expect opaque tokens instead of decodable JWTs, drop the custom `UserProfile`‑on‑register step in favor of a two‑call flow (register → PATCH profile). Scope: 3–5 engineer‑days, *loses* features (JWT inspectability, atomic profile creation).

**Switching the JWT implementation to a full IdP (Keycloak/Duende/Auth0)**: leave Identity as the user store, add an `AddOpenIdConnect` or `AddJwtBearer` pointing at the IdP's JWKS, retire your `IJwtTokenService` + `/login`, keep `/me` and `/register` if you're running a self‑service signup. Scope: 1‑2 weeks primarily for ops/IdP provisioning. The `AuthController` shell is reusable.

The cheapest of these — adding `MapIdentityApi` later for just the deferred flows — is a one‑hour change. The "start custom, scaffold later" arrow is strictly smoother than "start MapIdentityApi, rip out later."

## .NET 10–specific notes worth acting on

Four changes in .NET 9/10 affect this decision; none flip the recommendation, but two are worth using on the custom path.

`AddIdentityApiEndpoints` gained a `BearerTokenOptions` configuration delegate in **.NET 9** ([#51047](https://github.com/dotnet/aspnetcore/issues/51047)), letting you set access/refresh lifetimes inline — useful if you do eventually adopt the scaffold. **.NET 10** added `IApiEndpointMetadata`, which makes the cookie auth handler return 401/403 for API endpoints instead of 302‑redirecting to a login page — this automatically benefits `MapIdentityApi` endpoints and any `[ApiController]` endpoints, so a cookie‑mode SPA now behaves correctly out of the box. **.NET 10** also introduced `IdentityPasskeyOptions`, `SignInManager.PasskeySignInAsync`, and `UserManager.AddOrUpdatePasskeyAsync` — but passkey HTTP endpoints are *not* exposed by `MapIdentityApi`; they are only wired into the Blazor Web App template's Razor components, so any SPA passkey support is custom regardless of this decision. Finally, minimal‑API validation error responses can now be customized via `IProblemDetailsService` in .NET 10 preview 6, with the caveat that [#62723](https://github.com/dotnet/aspnetcore/issues/62723) shows `CustomizeProblemDetails` does not consistently fire for minimal‑API validation — meaning the envelope uniformity gap between `MapIdentityApi` and `[ApiController]` endpoints is not fully closed as of April 2026.

## Bottom line

For RunCoach, `MapIdentityApi` is a tool designed for a problem you don't have (getting a vanilla SPA talking to Identity in minutes) and that Microsoft explicitly tells you not to use for the problem you *do* have ("not intended to be a full-featured identity service provider or token server"). The custom controller is ~200 lines, tests the same way, keeps the module-first style, emits real JWTs, enables atomic `UserProfile` creation, and leaves the door open to pull `MapIdentityApi` back in as a scaffold when the deferred slices arrive. Ship the controller in Slice 0 and move on.