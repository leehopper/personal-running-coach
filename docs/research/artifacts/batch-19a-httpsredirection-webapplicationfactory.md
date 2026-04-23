# UseHttpsRedirection + `__Host-` cookies + WebApplicationFactory: the canonical 2026 pattern

**Research artifact** — stored at `docs/research/artifacts/batch-19a-httpsredirection-webapplicationfactory.md`. Dates verified against primary sources as of April 21, 2026; .NET 10 GA shipped November 2025.

---

## Executive summary

- 🔴 **`__Host-RunCoach` + `Secure` is functionally broken on `http://localhost` in Chrome and Safari** — only Firefox accepts it. `dotnet dev-certs https --trust` is not optional; it is a contributor prerequisite. Dev-only cookie weakening is an anti-pattern and is not used by any major .NET OSS project.
- 🟠 **Microsoft's .NET 10 templates leave `UseHttpsRedirection` ungated** and only wrap `UseHsts` + `UseExceptionHandler` in `if (!app.Environment.IsDevelopment())`. The current RunCoach gate `!IsDevelopment()` on `UseHttpsRedirection` is defensible but non-idiomatic; the cleaner pattern is **ungated `UseHttpsRedirection` + explicit `HttpsPort` configuration**.
- 🟠 **`WebApplicationFactory<Program>`'s in-memory `TestServer` safely no-ops `UseHttpsRedirection` when it can't resolve a port** (logs warning, passes through). There is no redirect loop risk inside the factory; there is only log noise. The canonical fix is `ClientOptions.BaseAddress = new Uri("https://localhost")` which flips `Request.IsHttps=true` and makes the middleware short-circuit cleanly. Microsoft docs recommend this explicitly.
- 🟠 **.NET 10 added `WebApplicationFactory.UseKestrel()` + `StartServer()`** for real-port/real-HTTPS E2E. RunCoach does not need this for `Set-Cookie` header assertions — stay with the in-memory `TestServer` + HTTPS base address — but it's the right tool when Playwright/browser E2E is added later.
- 🟡 **Keep cookie `Path = "/"` explicit on the `__Host-` cookie.** Browsers ignore a cookie whose `Path` attribute is absent under the `__Host-` rule (RFC 6265bis §5.6 requires the attribute to be *present*, not merely defaulted).

---

## 1. Primary recommendation: `Program.cs` shape

Match the Microsoft .NET 10 template exactly, then add one explicit configuration line to pin the HTTPS port for environments where `IServerAddressesFeature` is not populated (e.g., `WebApplicationFactory`). Rationale follows.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Identity cookie — __Host- requires Secure, no Domain, explicit Path=/
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name         = "__Host-RunCoach";
    options.Cookie.Path         = "/";                         // 🔴 required by __Host- prefix
    options.Cookie.HttpOnly     = true;
    options.Cookie.SameSite     = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;   // never downgrade
    // Do NOT set options.Cookie.Domain — __Host- requires it absent.
});

// Pin HTTPS port so HttpsRedirectionMiddleware never has to guess.
// In production, bind via ASPNETCORE_HTTPS_PORT env var (recommended) or here:
builder.Services.AddHttpsRedirection(o =>
{
    // 307 is the default; preserve request body on POST /login redirect.
    o.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    // Leave HttpsPort unset here; read it from ASPNETCORE_HTTPS_PORT / "https_port"
    // which the test factory also sets. Explicit fallback when behind a proxy:
    // o.HttpsPort = 443;
});

// (optional) Forwarded Headers if behind a reverse proxy (YARP, Nginx, Azure Linux App Service, K8s ingress).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownProxies.Clear();
    o.KnownNetworks.Clear();
    // Populate with your proxy's IP(s) or network(s) in appsettings.Production.json.
});

var app = builder.Build();

// Forwarded Headers MUST be first so Request.Scheme reflects the edge scheme.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();                   // 🟠 gated — browsers cache HSTS for localhost
}

app.UseHttpsRedirection();           // 🟢 UNGATED — matches .NET 10 template

app.UseStaticFiles();
app.UseRouting();
app.UseCors();                       // if SPA is on a different origin
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;        // .NET 10 source generator makes this public automatically,
                                     // but keeping the declaration is harmless and self-documenting.
```

**Why ungated `UseHttpsRedirection`?** The .NET 10 Razor Pages / MVC / Blazor / Identity template generates it outside any environment check (verified in `github.com/dotnet/aspnetcore/blob/main/src/ProjectTemplates/Web.ProjectTemplates/content/` and in the reference `Program.cs` in the .NET 10 fundamentals doc). The middleware is designed to **safely no-op** when it cannot determine an HTTPS port — it logs `HttpsRedirectionMiddleware[3] "Failed to determine the https port for redirect."` and passes through (`HttpsRedirectionMiddleware.cs`). Dev with the HTTPS launch profile already serves HTTPS, so the middleware is a no-op there anyway.

**Why pin `HttpsPort` explicitly?** The resolution order is `HttpsRedirectionOptions.HttpsPort` → `ASPNETCORE_HTTPS_PORT` / `"https_port"` config → `IServerAddressesFeature`. Production behind a reverse proxy has no `IServerAddressesFeature` HTTPS endpoint (TLS is terminated at the edge), so explicit configuration is required to avoid the silent no-op footgun (`dotnet/aspnetcore#27951`, still open).

**Why keep `Path = "/"` explicit?** RFC 6265bis §5.6 requires `__Host-` cookies to be set with `Path=/` as an explicit attribute; the browser storage model checks the presence of the attribute, not the effective path.

---

## 2. Test-host contract: `CustomWebApplicationFactory<Program>` for `__Host-` cookies

The canonical 2026 pattern is **in-memory `TestServer` + HTTPS base address**, not real Kestrel. This maximizes fidelity to the production cookie-issuance code path without forcing TLS on the test handler.

```csharp
// tests/RunCoach.IntegrationTests/RunCoachAppFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public sealed class RunCoachAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresContainer _postgres = new();  // Testcontainers

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");           // or "Testing" — see note below

        // 🟢 Pin HTTPS port so HttpsRedirectionMiddleware does not warn.
        //    The TestServer has no IServerAddressesFeature to walk.
        builder.UseSetting("https_port", "443");

        builder.ConfigureTestServices(services =>
        {
            // Replace the Postgres connection with the Testcontainer.
            services.RemoveAll<DbContextOptions<RunCoachDbContext>>();
            services.AddDbContext<RunCoachDbContext>(opt =>
                opt.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();
    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

// Per-test client — HTTPS base address is non-negotiable for Secure cookies.
public static class FactoryExtensions
{
    public static HttpClient CreateAuthTestClient(this RunCoachAppFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress       = new Uri("https://localhost"),   // 🔴 flips Request.IsHttps=true
            AllowAutoRedirect = false,                          // 🔴 inspect Set-Cookie manually
            HandleCookies     = false                           // don't let HttpClient mutate them
        });
}

// Assembly fixture wiring (xUnit v3)
[assembly: Xunit.AssemblyFixture(typeof(RunCoachAppFactory))]

public sealed class LoginEndpointTests(RunCoachAppFactory factory)
{
    [Fact]
    public async Task Login_sets_Host_prefixed_Secure_HttpOnly_Lax_cookie()
    {
        var client = factory.CreateAuthTestClient();

        var resp = await client.PostAsJsonAsync("/auth/login",
            new { email = "alice@example.com", password = "correct horse" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var setCookies = resp.Headers.GetValues("Set-Cookie").ToArray();
        var runCoach = Assert.Single(setCookies.Where(c => c.StartsWith("__Host-RunCoach=")));

        Assert.Contains("Secure",      runCoach, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly",    runCoach, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/",      runCoach, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax",runCoach, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Domain=", runCoach, StringComparison.OrdinalIgnoreCase);
    }
}
```

**Three decisions made here, with rationale:**

1. **`BaseAddress = new Uri("https://localhost")`.** The in-memory `TestServer` honors the URI scheme from the outgoing `HttpRequestMessage` — it sets `HttpRequest.Scheme = "https"` and `HttpRequest.IsHttps = true` inside the pipeline. This makes `UseHttpsRedirection` a short-circuit (no redirect), makes `CookieSecurePolicy.Always` semantically consistent (request IS HTTPS), and silences the `Failed to determine the https port for redirect` warning in test output. Microsoft docs explicitly recommend this (learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0).

2. **`AllowAutoRedirect = false` + `HandleCookies = false`.** Auth tests need to see the raw `Set-Cookie` header. `HttpClient`'s `CookieContainer` strips non-essential attributes when projecting into `Cookie` headers and has historical edge cases with `Secure` attribute on cross-scheme redirects (`dotnet/runtime#25226`, `#16983`). Assert on the raw `Set-Cookie` string and build follow-up requests by hand.

3. **`UseEnvironment("Development")` is fine; `"Testing"` is only needed if you ever want a third gate.** RunCoach's current gate `!IsDevelopment()` means the factory-set `Development` environment disables `UseHsts` and (under your current code) `UseHttpsRedirection`. With the ungated `UseHttpsRedirection` recommended in §1, the HTTPS `BaseAddress` already neutralizes it. If you later need to enable `UseHsts` in Production but also exercise it from the test project, introduce a `"Testing"` environment and change the gate to `!IsDevelopment() && !IsEnvironment("Testing")`.

**.NET 10 `UseKestrel()` — when to use it.** .NET 10 added `WebApplicationFactory.UseKestrel()`, `UseKestrel(int port)`, `UseKestrel(Action<KestrelServerOptions>)`, and `StartServer()` (`dotnet/aspnetcore#60758`, March 2025; API docs on learn.microsoft.com). Use this **only** when you need a real browser (Playwright/Selenium) to interact with `__Host-` cookies through Chromium — because Chrome/Safari reject `__Host-` on `http://localhost`, browser-driven E2E must run against a real Kestrel HTTPS listener. The HTTPS story with `UseKestrel` is still rough (`dotnet/aspnetcore#63012`, open July 2025): you must configure `listenOptions.UseHttps()` manually and fix up `BaseAddress`. For Set-Cookie header assertions, in-memory `TestServer` is simpler and exercises the same emission code path.

---

## 3. Dev-environment recipe

### 3a. Dev with HTTPS certs (the paved path, required for `__Host-`)

One-time per machine:

```bash
dotnet dev-certs https --trust        # Windows, macOS, Linux (Ubuntu/Fedora official)
```

Per clone:

```bash
git clone <runcoach>
cd runcoach
dotnet run --project src/RunCoach.Web --launch-profile https
# binds https://localhost:7xxx ; __Host-RunCoach cookie works in all browsers
```

**.NET 10 specifics** (learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs, accessed Apr 2026):

- Dev cert now includes SANs for `localhost`, `127.0.0.1`, `::1`, `*.dev.localhost`, `*.dev.internal`, `host.docker.internal`, `host.containers.internal` — covers Vite dev server on the same host and future containerization.
- `--trust` from WSL now propagates trust to the Windows host.
- macOS Sequoia broke dev-certs in late 2024; fixed in the October 2024 SDK.
- Cert rotates annually; re-run `--trust` if login breaks with TLS errors.

**Vite SPA coexistence (`https://localhost:5173`):**

- Two options. **Preferred:** configure Vite `server.proxy` so `/api/*` forwards to the backend. The browser only ever talks to `https://localhost:5173` — same-origin, no CORS, no cross-origin cookie decisions.
- **Alternative:** separate origins (`5173` → `7xxx`). Both are `https://localhost:*`, which is **same-site** (same registrable host + scheme) but cross-origin. `SameSite=Lax` sends cookies on fetches with `credentials: 'include'` because the request is same-site. You still need CORS configured with `AllowCredentials()` and an explicit origin (wildcard `*` is forbidden with credentials per Fetch spec).

### 3b. Dev without HTTPS certs (explicitly NOT supported)

**Do not** weaken the cookie in Development. Every major .NET OSS reference (eShop, Ardalis CleanArchitecture, jasontaylordev CleanArchitecture, Duende quickstarts, damienbod samples) expects `dotnet dev-certs --trust` and none drops the `__Host-` prefix in dev. The risks of a dev/prod cookie split are:

- Cookie-tossing and path-scoped overwrite vulnerabilities go uncaught in dev because `__Host-` is the exact attribute that prevents them.
- Code that hardcodes the cookie name breaks silently between environments.
- CSRF regressions in cross-origin/cross-port scenarios are invisible in dev.

If `dotnet dev-certs --trust` genuinely cannot run on a contributor's platform (corporate Linux without NSS certutil, unusual SELinux policies), fall back to **mkcert** (`mkcert -install` once, then `mkcert localhost 127.0.0.1 ::1` and point Kestrel at the file via `ASPNETCORE_Kestrel__Certificates__Default__Path`). Document this in `CONTRIBUTING.md` as the escape hatch, not the default.

---

## 4. Gotchas table

| # | Name | Applies to | Severity | Symptom | Root cause | Fix |
|---|------|-----------|----------|---------|-----------|-----|
| 1 | `__Host-` dropped on `http://localhost` in Chrome/Safari | browser, all | 🔴 | Cookie arrives in DevTools response but vanishes from storage; login loop | Chromium issue 40202941 / WebKit bugs 218980, 231035, 232088 still partial/open in 2026 | Always run dev on `https://localhost` via `dotnet dev-certs --trust` |
| 2 | `HttpsRedirectionMiddleware[3]` "Failed to determine the https port for redirect" in test logs | 8/9/10 | 🟠 | Warning spam; request continues as HTTP inside `TestServer` | `IServerAddressesFeature` has no HTTPS endpoint under `TestServer` | Set `BaseAddress = new Uri("https://localhost")` on the test client **and** `UseSetting("https_port", "443")` on the factory |
| 3 | Silent no-op in `UseHttpsRedirection` when port unresolvable (security footgun) | 8/9/10 | 🔴 | Middleware logs and continues in plain HTTP rather than failing closed | `HttpsRedirectionMiddleware.cs` returns `PortNotFound` and skips redirect instead of throwing; tracked by `dotnet/aspnetcore#27951` (open) | Set `HttpsPort` explicitly in Production via `AddHttpsRedirection(o => o.HttpsPort = 443)` or `ASPNETCORE_HTTPS_PORT` env var |
| 4 | Reverse-proxy `ERR_TOO_MANY_REDIRECTS` | 8/9/10 | 🔴 | Infinite 307 loop behind Nginx / Azure Linux App Service / K8s ingress | TLS terminated at edge; Kestrel sees HTTP; `UseHttpsRedirection` redirects; proxy forwards HTTP again | `app.UseForwardedHeaders()` **first**, with `KnownProxies` or `KnownNetworks` populated (per learn.microsoft.com/host-and-deploy/proxy-load-balancer) |
| 5 | HSTS cached by Chrome for `localhost` | all | 🟠 | Later HTTP localhost apps are force-upgraded to HTTPS; `ERR_SSL_PROTOCOL_ERROR` | Chrome caches `Strict-Transport-Security` host-wide | Never call `UseHsts()` in Development; clear via `chrome://net-internals/#hsts` (delete `localhost`) |
| 6 | `UseHsts` default-excludes `localhost`, `127.0.0.1`, `::1` | 8/9/10 | 🔵 | HSTS header never emitted in dev or when binding to loopback in Production | `HstsOptions.ExcludedHosts` default list | Keep default; clear `ExcludedHosts` only when deliberately testing HSTS against `/etc/hosts`-mapped names |
| 7 | CORS preflight `ERR_INVALID_REDIRECT` with `UseHttpsRedirection` | 8/9/10 | 🟠 | OPTIONS preflight gets 307; browser refuses to follow; CORS fails | Fetch spec forbids following redirects on CORS preflight | For pure APIs, drop `UseHttpsRedirection` and simply don't listen on HTTP (remove HTTP URL from `ASPNETCORE_URLS`) |
| 8 | `CookieContainer` nuances across HTTP→HTTPS redirect | `HttpClient` runtime | 🟠 | Test passes with `TestServer` but real `HttpClient` drops `Secure` cookies on the HTTP leg | `CookieContainer` enforces Secure strictly per RFC 6265; `TestServer`'s handler bypasses it | In integration tests, use `AllowAutoRedirect=false` + `HandleCookies=false` and build follow-up requests by hand |
| 9 | `__Host-` with implicit Path | browser spec | 🟡 | Cookie silently rejected even over HTTPS | RFC 6265bis §5.6 requires `Path` attribute to be *present and equal to `/`* — not merely defaulted | Set `options.Cookie.Path = "/"` explicitly in `ConfigureApplicationCookie` |
| 10 | `CookieSecurePolicy.Always` emits `Secure` even on HTTP requests | all .NET | 🟡 | `Set-Cookie` has `Secure` attribute but arrived on HTTP response; browser drops it silently | `CookieBuilder.Build` checks `SecurePolicy == Always` without sniffing `Request.IsHttps` | Intentional; requires HTTPS end-to-end (which is what `__Host-` demands anyway) |
| 11 | 307 vs 308 for HTTPS redirect | 8/9/10 | 🔵 | 308 is cached aggressively by browsers and intermediates | `RedirectStatusCode` default is 307 (correct); don't change to 308 without reason | Leave at `Status307TemporaryRedirect`; docs explicitly warn against permanent redirects |
| 12 | `WebApplicationFactory` default `BaseAddress = http://localhost` | all | 🟠 | `Request.IsHttps == false` inside tests; `ProblemDetails` URLs render as HTTP; cookies with `Secure` flag behave inconsistently | Hard-coded in `WebApplicationFactoryClientOptions` | Always override to `https://localhost` in auth/cookie tests |
| 13 | `WebApplicationFactory.UseKestrel()` HTTPS is still rough | 10 only | 🔵 | Need to configure `UseHttps()` manually and fix up `CreateClient().BaseAddress` | `dotnet/aspnetcore#63012` open since July 2025 | Use in-memory `TestServer` for cookie header tests; use `UseKestrel()` only when a real browser is in the loop |

---

## 5. Alternatives considered and rejected

**(a) Always-on `UseHttpsRedirection` with `HTTPS_PORT` pinned via env var.** This *is* the Microsoft template pattern in .NET 10 and is arguably the cleanest. **Acceptable alternative** to the current `!IsDevelopment()` gate; either is defensible. The template pattern wins on idiomaticity and on not having to reason about the gate when onboarding a new contributor. Adopt this if you want the minimal cognitive load. The only cost is a warning-log line in test output unless you also configure `UseSetting("https_port", "443")` on the factory — which the recommended factory code above does.

**(b) `IsProduction()`-only gate (i.e., let Staging also skip redirect).** Rejected. Staging should behave like Production for security invariants; the whole point of Staging is to catch HTTPS-related bugs before they ship. `!IsDevelopment()` correctly includes Staging.

**(c) Drop `__Host-` prefix in Development, keep in Production.** Rejected. Every significant .NET OSS project (eShop, Ardalis/jasontaylordev CleanArchitecture, Duende quickstarts, damienbod samples) keeps cookie attributes stable across environments. Divergence silently hides the attack classes the prefix was designed to close (subdomain cookie-tossing, path-scoped overwrite) and creates false confidence. The one-time cost of `dotnet dev-certs --trust` is smaller than the ongoing cost of a split cookie contract.

**(d) Local reverse proxy (YARP / Nginx / Caddy / Traefik) terminating TLS.** Rejected as default, kept as an option for containerized dev. Adds a moving part that isn't justified for a pre-Alpha single-service app. .NET Aspire 13.1 (2026) adds first-class `WithHttpsDeveloperCertificate` / `WithDeveloperCertificateTrust` APIs that are a better path if RunCoach later grows into a multi-service Aspire topology.

**(e) mkcert as the recommended default.** Rejected as default, kept as an escape hatch. `dotnet dev-certs --trust` in .NET 10 now matches mkcert on SANs, WSL passthrough, and cross-platform support; introducing a second trust chain is unnecessary friction. Document mkcert only for contributors whose environment prevents `dev-certs --trust` from working.

**(f) `WebApplicationFactory.UseKestrel()` for integration tests.** Rejected as default. Set-Cookie header assertions don't need real TLS; in-memory `TestServer` with HTTPS base address is faster, has no port allocation, and has no certificate story to maintain. Add `UseKestrel()` only when Playwright or Selenium enters the picture.

---

## 6. What changed in .NET 10 specifically

- **`WebApplicationFactory<T>` gained `UseKestrel()` / `UseKestrel(int)` / `UseKestrel(Action<KestrelServerOptions>)` and `StartServer()`** (`dotnet/aspnetcore#60758`, March 2025; shipped preview4; API docs `learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1.usekestrel?view=aspnetcore-10.0`). Must be called before the factory initializes; not reversible.
- **Top-level `Program` is now public automatically** — the `public partial class Program {}` boilerplate is no longer required; a source generator emits it when the test project references the entry assembly (Safia Abdalla, ASP.NET Core PM, blog.safia.rocks, November 10 2025).
- **`dotnet dev-certs https` ships additional SANs** (`*.dev.localhost`, `host.docker.internal`, `host.containers.internal`) and **WSL→Windows trust passthrough** (learn.microsoft.com/en-us/dotnet/core/tools/dotnet-dev-certs, Apr 2026).
- **`HttpsRedirectionMiddleware` behavior is unchanged** from .NET 8/9. Silent no-op on unresolvable port remains. No redirect-specific regressions filed under `area-httpsredirection` in 2025–2026.
- **`Microsoft.AspNetCore.Mvc.Testing` v10.0.0** default `ClientOptions` are unchanged (`http://localhost`, `AllowAutoRedirect=true`, `HandleCookies=true`, `MaxAutomaticRedirections=7`).
- **.NET Aspire 13.1 (2026)** adds `WithHttpsDeveloperCertificate` and `WithDeveloperCertificateTrust` for distributing the ASP.NET dev cert to non-.NET resources (Vite, Uvicorn, YARP/Redis/Keycloak containers). Relevant only if RunCoach adopts Aspire later.

---

## 7. Conclusion

The RunCoach setup is already 90% correct. The `__Host-RunCoach` cookie shape (Secure/Always, HttpOnly, Lax, no Domain) is exactly the 2026 canonical shape for interactive browser auth — add `Path = "/"` explicitly and it's complete. The current `!IsDevelopment()` gate on `UseHttpsRedirection` is defensible but subtly non-idiomatic: **the .NET 10 templates leave `UseHttpsRedirection` ungated and let the middleware self-disable when the HTTPS port cannot be resolved**. Follow that template pattern and add one `UseSetting("https_port", "443")` in the `RunCoachAppFactory` to silence the warning log. Drop `BaseAddress = new Uri("https://localhost")` on every auth-test client to make `Request.IsHttps=true` and exercise the exact production cookie-emission code path.

The structural risk in this configuration is **not** redirect loops or silently-dropped cookies inside `WebApplicationFactory` — the in-memory `TestServer` is robust against both — it is the **`__Host-`-over-`http://localhost`** contract violation in Chrome and Safari, which is invisible in server logs and silent in the test suite. This bug can only be caught by a real browser. Make `dotnet dev-certs https --trust` a documented prerequisite in `CONTRIBUTING.md` now, before T02.4 ships the login endpoint and a contributor discovers the hard way that their Chrome session never receives the cookie.

When T02.5 begins writing integration tests, the code patterns in §2 will compose correctly against Testcontainers Postgres, the xUnit v3 `AssemblyFixture` lifecycle, and the existing `UseEnvironment("Development")` gate. No changes to the `UseHttpsRedirection` gate are strictly required for correctness today; the recommendation is to align with the .NET 10 template pattern when Lee next edits `Program.cs` for convergence with the wider ecosystem.