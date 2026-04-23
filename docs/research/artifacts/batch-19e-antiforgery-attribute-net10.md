# Antiforgery attributes on ASP.NET Core 10 MVC controllers

**Verdict.** For an MVC-hosted JSON API protected by `app.UseAntiforgery()`, the canonical 2026 gate is **`[RequireAntiforgeryToken]` from `Microsoft.AspNetCore.Antiforgery`**, paired with `builder.Services.AddControllers()` (no view-engine services required). The MVC filter `[ValidateAntiForgeryToken]` is not broken by `UseAntiforgery` on .NET 10 — the two don't interact at all, and that's precisely the problem: the MVC filter still ships in `Microsoft.AspNetCore.Mvc.ViewFeatures`, so it **requires `AddControllersWithViews()`** to register its internal `ValidateAntiforgeryTokenAuthorizationFilter`. The parent task's "broken" language is imprecise but its *recommendation* is correct: adopt the middleware-metadata path and drop the view stack. The current Slice 0 wiring works, but it carries a Razor view engine it will never use and it diverges from the direction Microsoft has steered the framework since .NET 8. Migration is a three-line diff.

The one meaningful caveat: `[RequireAntiforgeryToken]` on MVC controllers is **undocumented on Microsoft Learn**. The canonical reference is the API-review issue that shipped the middleware (`dotnet/aspnetcore#49237`) plus an open Learn-docs issue (`dotnet/AspNetCore.Docs#33740`) asking Microsoft to document exactly this usage. Primary-source evidence — the attribute's `AttributeUsage` and the middleware's endpoint-metadata check — is unambiguous, but you should expect code review pushback from anyone who has only read the Learn antiforgery page.

## How `UseAntiforgery` actually decides to validate

The middleware's trigger condition is a two-line predicate in `AntiforgeryMiddleware.InvokeAsync` on `main` (same on `release/10.0`):

```csharp
var method = context.Request.Method;
if (!HttpExtensions.IsValidHttpMethodForForm(method))
{
    return _next(context);
}
if (endpoint?.Metadata.GetMetadata<IAntiforgeryMetadata>() is { RequiresValidation: true })
{
    return InvokeAwaited(context);
}
return _next(context);
```

Two conditions must both hold: the method is unsafe (POST/PUT/PATCH/DELETE), and the matched endpoint exposes `IAntiforgeryMetadata` with `RequiresValidation == true`. Anything else is a pass-through. **This is why plain MVC controllers with `[ValidateAntiForgeryToken]` see zero interaction with the middleware** — the MVC filter implements `IFilterFactory`/`IAuthorizationFilter`, not `IAntiforgeryMetadata`, so `GetMetadata<IAntiforgeryMetadata>()` returns null and the middleware exits before any filter pipeline runs. The Microsoft Q&A ticket *"Customize antiforgery failure response"* confirms this empirically: *"the AntiforgeryMiddleware is not being invoked at all (probably because I am not using minimal APIs but rather MVC)."* So **there is no dual validation today** on the current Slice 0 wiring — the MVC filter does 100% of the work and `UseAntiforgery()` is a registered-but-dormant middleware.

That is also the mechanism by which the **recommended shape works**: `[RequireAntiforgeryToken]` implements `IAntiforgeryMetadata`, so the middleware picks up the metadata, runs `IAntiforgery.ValidateRequestAsync`, and short-circuits with a 400 on failure — all without the MVC filter ever being involved.

## Answers to the seven sub-questions

**1. Canonical attribute for MVC JSON controllers + `UseAntiforgery`.** Use `[RequireAntiforgeryToken]` (namespace `Microsoft.AspNetCore.Antiforgery`, assembly `Microsoft.AspNetCore.Antiforgery`; **note**: the task brief lists `Microsoft.AspNetCore.Http` — the primary source in issue `#49237` and in the `RequireAntiforgeryTokenAttribute.cs` source file places it in `Microsoft.AspNetCore.Antiforgery`, with only the `IAntiforgeryMetadata` interface living in `Http.Abstractions`). Its declaration from the API-approved proposal in `#49237`:

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAntiforgeryTokenAttribute(bool required = true)
    : Attribute, IAntiforgeryMetadata { public bool Required { get; } }
```

The `AttributeTargets.Class | AttributeTargets.Method` range legally covers MVC controller classes and action methods. There is **no primary Learn doc page** that explicitly recommends this for MVC controllers — `dotnet/AspNetCore.Docs#33740` ("Describe how to require antiforgery validation on an API controller") is an open documentation gap that the community has asked Microsoft to close. The authoritative recommendation therefore comes from (a) the approved API proposal, (b) the middleware source code, and (c) the Learn page for `DisableAntiforgery` which shows the same metadata model in the `Microsoft.AspNetCore.Builder` namespace.

**2. `[RequireAntiforgeryToken]` on MVC actions — does it work, what are the caveats?** Yes, it works, with no MVC filter registration required. The endpoint-metadata system is shared between Minimal APIs and MVC: routing reflects controller/action attributes into `Endpoint.Metadata`, so `[RequireAntiforgeryToken]` on `AuthController.Register` puts `IAntiforgeryMetadata(RequiresValidation: true)` on that endpoint, and `UseAntiforgery()` validates the request before your action runs. There are no filter-ordering concerns because validation happens in the middleware, not in the MVC filter pipeline. `[AllowAnonymous]` is orthogonal (it governs authorization, not antiforgery). The one genuine pitfall is **middleware ordering**: `UseAntiforgery()` must be placed **after** `UseAuthentication()`/`UseAuthorization()` and **before** `UseEndpoints()`/`MapControllers()` — the framework throws `InvalidOperationException: Endpoint ... contains anti-forgery metadata, but a middleware was not found that supports anti-forgery` if the middleware is absent, and validation can behave unexpectedly if it runs before authentication.

**3. Carve-out for `GET /xsrf`.** `[IgnoreAntiforgeryToken]` is **MVC-only** — its source signature is `public class IgnoreAntiforgeryTokenAttribute : Attribute, IAntiforgeryPolicy, IOrderedFilter`. It does not implement `IAntiforgeryMetadata`, so it is **invisible to `UseAntiforgery`**. If you adopt the metadata path you must use the endpoint-convention equivalent: **`DisableAntiforgery()`** from `Microsoft.AspNetCore.Builder.RoutingEndpointConventionBuilderExtensions`, documented on Learn as "Disables anti-forgery token validation for all endpoints produced on the target `IEndpointConventionBuilder`". Its implementation is literally `builder.WithMetadata(new RequireAntiforgeryTokenAttribute(required: false))`. In practice: since `GET /xsrf` is already a safe method, `UseAntiforgery` skips it via the `IsValidHttpMethodForForm` check regardless — so **no carve-out is actually needed for `GET /xsrf`**. You only need the carve-out if the endpoint were a POST or if you wanted defensive clarity. If you want it, attach it to the route: `app.MapControllers().DisableAntiforgery()` as a blanket convention, or apply `[RequireAntiforgeryToken(required: false)]` to the specific action.

**4. `AddControllersWithViews()` side-effects.** The delta over `AddControllers()` is `AddViews()`, which registers: the Razor view engine, view-compilation services, `IViewEngine`/`IViewComponentDescriptorProvider`/`IHtmlGenerator`, the tag-helper activator, `TempData`, partial view services, and the authorization filter types including `ValidateAntiforgeryTokenAuthorizationFilter` and `AutoValidateAntiforgeryTokenAuthorizationFilter`. Most of these services are DI-lazy — the view-compiler doesn't touch disk until a controller returns a `ViewResult` — so steady-state CPU cost is negligible. The real costs are **startup memory (~several MB of registered services and assembly loads), increased surface area for future accidental coupling** (a newcomer who writes `return View()` in a controller will now silently succeed because the view engine is available), and the more subtle maintainability signal that the service registration stops advertising "this is a pure JSON API." There is no primary-source security advisory here, but the Strathweb write-up and Microsoft's own description of `AddControllers()` as registering "services for controllers to the specified IServiceCollection. This method will not register services used for views or pages" make the intent clear: APIs should use `AddControllers`.

**5. Is `[ValidateAntiForgeryToken]` "broken" with `UseAntiforgery` in .NET 10?** **No** — and the parent task's phrasing conflates two separate issues. First, the .NET 10 release notes list no antiforgery breaking changes (the last antiforgery-related breaking change was the .NET 8 `IFormFile` auto-validation, still current). Second, the runtime exception your T02.5 hit — `No service for type 'Microsoft.AspNetCore.Mvc.ViewFeatures.Filters.ValidateAntiforgeryTokenAuthorizationFilter' has been registered` — is the manifestation of **dotnet/aspnetcore#22189** ("Antiforgery should be useable without views"), an **open-since-May-2020** backlog issue where the MVC filter's implementation ships in the ViewFeatures assembly and is only wired up by `AddViews()`. This is not a .NET 10 regression; it's a long-standing architectural seam that the team has chosen not to close because the middleware path supersedes it. The current (`release/10.0` branch) Microsoft Learn antiforgery doc states this directly: *"Calling AddControllers does not enable antiforgery tokens. AddControllersWithViews must be called to have built-in antiforgery token support."* So `[ValidateAntiForgeryToken]` works correctly in .NET 10 — it just requires `AddControllersWithViews()` — and the cleaner path is to stop using it.

**6. Dual validation.** No dual validation occurs in the current Slice 0 shape. The MVC filter does not emit `IAntiforgeryMetadata`, so `UseAntiforgery` short-circuits on the metadata check and never calls `ValidateRequestAsync`. If you adopted a hybrid where an action carries **both** `[ValidateAntiForgeryToken]` and `[RequireAntiforgeryToken]`, the middleware would validate first (and reject with 400 on failure, before the action even materializes), then the MVC filter would validate again in the authorization filter stage on success; token validation is idempotent — `IAntiforgery.ValidateRequestAsync` reads cookies and headers and does not consume state — so correctness is preserved, but you'd log two `Microsoft.AspNetCore.Antiforgery` events per request and pay a redundant HMAC verification. Don't do it.

**7. Test-host pattern for `WebApplicationFactory<Program>`.** Use **Approach A** (real HTTP flow through `GET /xsrf`) rather than synthesizing tokens via `IAntiforgery.GetAndStoreTokens`. It mirrors exactly what the T03 SPA will do, exercises the same code path an attacker would have to defeat, and avoids the trap that `GetAndStoreTokens` binds the token to whatever `ClaimsPrincipal` is on the scope's synthetic `HttpContext` — not the one on your real authenticated `HttpClient`. The canonical wiring uses `Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler` via `factory.CreateDefaultClient(handler)` (not `CreateClient()`, which hides the cookie container) and an `https://localhost/` base address so `__Host-` prefixed cookies are retained:

```csharp
var cookies = new CookieContainer();
var client = factory.CreateDefaultClient(new CookieContainerHandler(cookies));
client.BaseAddress = new Uri("https://localhost/");

(await client.GetAsync("/xsrf")).EnsureSuccessStatusCode();
var xsrf = cookies.GetCookies(client.BaseAddress)["__Host-Xsrf-Request"]!.Value;

using var req = new HttpRequestMessage(HttpMethod.Post, "/register")
{ Content = JsonContent.Create(new { email, password }) };
req.Headers.Add("X-XSRF-TOKEN", xsrf);
var resp = await client.SendAsync(req);
```

The Microsoft Learn integration-tests page links directly to Martin Costello's "Antiforgery testing with Application Parts" post as its canonical SPA-style reference — your production `/xsrf` endpoint is functionally identical to his injected test-only token controller, so you should consume it the same way from tests that the SPA consumes it in production. Remember to **re-fetch `/xsrf` after authentication state changes**: the antiforgery token binds to the user identity, so a token minted pre-login will fail validation on post-login POSTs (`dotnet/aspnetcore#56325`, and Mickaël Derriey's "Authentication, antiforgery, and order of execution" post both document this).

## Migration delta

**Program.cs:**

```diff
- builder.Services.AddControllersWithViews();
+ builder.Services.AddControllers();

  builder.Services.AddAntiforgery(o =>
  {
      o.HeaderName = "X-XSRF-TOKEN";
      o.Cookie.Name = "__Host-Xsrf";
      o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
      o.Cookie.SameSite = SameSiteMode.Strict;
      o.Cookie.HttpOnly = true;
  });

  // ...
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseAntiforgery();            // already present; keep between Auth and Endpoints
  app.MapControllers();
```

**AuthController.cs:**

```diff
- using Microsoft.AspNetCore.Mvc;
+ using Microsoft.AspNetCore.Antiforgery;
+ using Microsoft.AspNetCore.Mvc;

  [ApiController]
  [Route("/")]
  public sealed class AuthController(/* ... */) : ControllerBase
  {
      [HttpGet("xsrf")]
-     [IgnoreAntiforgeryToken]
      public IActionResult Xsrf([FromServices] IAntiforgery antiforgery)
      {
          var tokens = antiforgery.GetAndStoreTokens(HttpContext);
          Response.Cookies.Append("__Host-Xsrf-Request", tokens.RequestToken!,
              new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict,
                                  HttpOnly = false, Path = "/" });
          return NoContent();
      }

      [HttpPost("register")]
-     [ValidateAntiForgeryToken]
+     [RequireAntiforgeryToken]
      public Task<IActionResult> Register([FromBody] RegisterRequest r) { /* ... */ }

      [HttpPost("logout")]
-     [ValidateAntiForgeryToken]
+     [RequireAntiforgeryToken]
      public Task<IActionResult> Logout() { /* ... */ }
  }
```

The `[IgnoreAntiforgeryToken]` on `Xsrf` is removable because (a) it was MVC-filter-only and has no effect with the middleware path, and (b) `GET` is already exempt via the middleware's `IsValidHttpMethodForForm` guard. If you prefer belt-and-braces, replace it with `[RequireAntiforgeryToken(required: false)]` or chain `.DisableAntiforgery()` onto `MapControllers()` for the `/xsrf` route only.

For Slice 1+ controllers, the hygienic default is to apply `[RequireAntiforgeryToken]` at the **class level** on any controller that exposes unsafe methods, mirroring how ASP.NET Core templates apply `[Authorize]`. Consider a small `[AntiCsrf]` alias attribute subclassing `RequireAntiforgeryTokenAttribute` if you want a project-local vocabulary that signals intent more loudly than the framework name.

## What to record in the decision log

The parent task's core recommendation — `[RequireAntiforgeryToken]` over `[ValidateAntiForgeryToken]` — is correct and should be kept. The supporting language ("broken with `UseAntiforgery` in .NET 10") should be rewritten to reflect the actual mechanism: *"`[ValidateAntiForgeryToken]` (namespace `Microsoft.AspNetCore.Mvc`) is an MVC authorization filter whose implementation ships in `Microsoft.AspNetCore.Mvc.ViewFeatures` and is only registered by `AddViews()`. It does not emit `IAntiforgeryMetadata`, so it is not seen by `UseAntiforgery()` and requires `AddControllersWithViews()` to function. For a JSON-only API on .NET 10, prefer `[RequireAntiforgeryToken]` from `Microsoft.AspNetCore.Antiforgery`, which emits the metadata the middleware consumes and works with plain `AddControllers()`. See `dotnet/aspnetcore#49237` (API approval), `AntiforgeryMiddleware.cs#L31` (metadata check), `dotnet/aspnetcore#22189` (the registration gap, open since 2020), and `dotnet/AspNetCore.Docs#33740` (open Learn docs gap for this exact scenario)."*

The T02.4 → T02.5 drift retrospective is worth capturing separately: the commit that shipped `[ValidateAntiForgeryToken]` deviated from the parent spec silently, the integration tests caught the DI error, and the fix (`AddControllersWithViews()`) resolved the symptom while cementing the deviation. That pattern — *tests pass, so we ship* — is exactly what a decision log prevents on the next cycle.

## Sources

### Primary — source code and API approvals
- `AntiforgeryMiddleware.cs` on `main` — the two-gate validation predicate: `github.com/dotnet/aspnetcore/blob/main/src/Antiforgery/src/AntiforgeryMiddleware.cs`
- `IgnoreAntiforgeryTokenAttribute.cs` — confirms MVC-only (`IAntiforgeryPolicy, IOrderedFilter`, not `IAntiforgeryMetadata`): `github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.ViewFeatures/src/IgnoreAntiforgeryTokenAttribute.cs`
- `AutoValidateAntiforgeryTokenAttribute.cs` — for context on the older filter-based model: `github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.ViewFeatures/src/AutoValidateAntiforgeryTokenAttribute.cs`
- `IAntiforgery.cs` — the validation service called by both filter and middleware: `github.com/dotnet/aspnetcore/blob/main/src/Antiforgery/src/IAntiforgery.cs`
- **Issue #49237** — API-approved proposal that introduced `UseAntiforgery`, `RequireAntiforgeryTokenAttribute`, `IAntiforgeryMetadata`, `IAntiforgeryValidationFeature`: `github.com/dotnet/aspnetcore/issues/49237`
- **Issue #38338** — the design thread for making antiforgery a middleware, explains why the MVC filter was kept alongside rather than removed: `github.com/dotnet/aspnetcore/issues/38338`
- **Issue #22189** — "Antiforgery should be useable without views," open since May 2020, primary-source confirmation that `AddControllers()` does not register the MVC filter services: `github.com/dotnet/aspnetcore/issues/22189`
- **Issue #51194** — `DisableAntiforgery()` behavior on `RouteGroupBuilder`, includes the exact error message for missing middleware: `github.com/dotnet/aspnetcore/issues/51194`

### Primary — Microsoft documentation and Learn
- `anti-request-forgery.md` on `main` (Learn .NET 10 source): `github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/security/anti-request-forgery.md` — contains the explicit statement *"Calling AddControllers does not enable antiforgery tokens. AddControllersWithViews must be called to have built-in antiforgery token support."*
- Microsoft Learn — `RoutingEndpointConventionBuilderExtensions.DisableAntiforgery<TBuilder>`: `learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.routingendpointconventionbuilderextensions.disableantiforgery`
- Microsoft Learn — .NET 8 breaking change *"IFormFile parameters require anti-forgery checks"* (still current in .NET 10; no subsequent antiforgery breaking changes): `learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/8.0/antiforgery-checks`
- **Docs issue #33740** — "Describe how to require antiforgery validation on an API controller," the open documentation gap for `[RequireAntiforgeryToken]` on MVC: `github.com/dotnet/AspNetCore.Docs/issues/33740`
- Microsoft Q&A "Customize antiforgery failure response" — empirical confirmation that `UseAntiforgery` does not fire for plain MVC endpoints: `learn.microsoft.com/en-us/answers/questions/2121664/customize-antiforgery-failure-response`

### Secondary — high-quality corroboration
- Martin Costello, *"Integration testing antiforgery with Application Parts"* — the test pattern referenced from Microsoft Learn integration-tests docs: `blog.martincostello.com/integration-testing-antiforgery-with-application-parts/` and sample repo `github.com/martincostello/antiforgery-testing-application-part`
- Andrew Lock, *"Automatically validating anti-forgery tokens with AutoValidateAntiforgeryTokenAttribute"* — pre-`UseAntiforgery` reference for the MVC filter path: `andrewlock.net/automatically-validating-anti-forgery-tokens-in-asp-net-core-with-the-autovalidateantiforgerytokenattribute/`
- Mickaël Derriey, *"Authentication, antiforgery and order of execution"* — primary-source-backed analysis of token-to-user binding: `mderriey.com/2019/11/06/authentication-antiforgery-and-order-of-execution/`
- Duende Software, *"Understanding antiforgery in ASP.NET Core"* (Mar 2025) — current third-party overview that shows the filter-based approach with `AddControllersWithViews`: `duendesoftware.com/blog/20250325-understanding-antiforgery-in-aspnetcore`
- Filip W. (Strathweb), breakdown of `AddMvcCore`/`AddControllers`/`AddControllersWithViews`/`AddMvc`: `strathweb.com/2020/02/`