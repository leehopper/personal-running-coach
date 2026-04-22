# Translating `IdentityResult.Errors` to `ValidationProblemDetails` in ASP.NET Core 10

## Decision

- **Use a per-action manual `ModelState.AddModelError(propertyKey, description)` loop followed by `return ValidationProblem(ModelState);`.** This is the canonical controller idiom in ASP.NET Core 10 — it routes through `ProblemDetailsFactory.CreateValidationProblemDetails`, yields the exact requested JSON shape, and preserves `[ApiController]` auto-400 consistency.
- **Pre-validate the DTO with DataAnnotations (simple) or FluentValidation 12+ (complex).** This makes weak-password translation a rare fallback; Identity then only surfaces genuinely server-side concerns (duplicate email/username, concurrency).
- **Map `IdentityError.Code` → DTO property name with a small static helper** (`IdentityErrorCodeMapper`). Group password rule codes to `"password"`, email codes to `"email"`, username codes to `"username"`.
- **Split 409 from 400 at the controller.** `DuplicateEmail` / `DuplicateUserName` return a plain `ProblemDetails` via `Problem(...)`, not `ValidationProblemDetails`. This respects RFC 9110 §15.5.10 and matches the RunCoach contract.
- **Do not try to centralize translation via `CustomizeProblemDetails`** — that callback is not invoked for `ValidationProblemDetails` produced by MVC's `ValidationProblem(ModelState)` (confirmed in `dotnet/aspnetcore#62723`). If you want centralization, use a typed `IdentityFailureException` + a dedicated `IExceptionHandler`, which coexists cleanly with the existing 500-handler.

---

## 1. Primary recommendation

### Controller

```csharp
// src/RunCoach.Api/Auth/AuthController.cs
namespace RunCoach.Api.Auth;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager)
    : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // [ApiController] has already auto-400'd on DataAnnotations/model-binding failure,
        // so ModelState is valid here. Identity runs its own server-side validators next.

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
            return CreatedAtAction(nameof(Register), new { id = user.Id }, null);

        return result.ToRegistrationActionResult(this, request);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var r = await signInManager.PasswordSignInAsync(
            request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);

        // Strict enumeration resistance: identical 401 for every non-success, regardless of which flag tripped.
        if (!r.Succeeded)
            return Problem(
                type:       "https://runcoach.app/problems/invalid-credentials",
                title:      "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized);

        return Ok(/* token / session body */);
    }
}
```

### Request DTO (DataAnnotations pre-validation)

```csharp
// src/RunCoach.Api/Auth/RegisterRequest.cs
namespace RunCoach.Api.Auth;

using System.ComponentModel.DataAnnotations;

public sealed record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(254)]
    string Email,

    [property: Required, MinLength(12), MaxLength(128)]
    string Password);
```

DataAnnotations enforces format + basic length so the common "too short" path short-circuits through `[ApiController]` auto-400 and never reaches Identity. Identity still enforces complexity rules (upper/lower/digit/non-alphanumeric) and uniqueness.

### Translator helper

```csharp
// src/RunCoach.Api/Auth/IdentityResultExtensions.cs
namespace RunCoach.Api.Auth;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public static class IdentityResultExtensions
{
    /// <summary>
    /// Translates a failed <see cref="IdentityResult"/> from <c>POST /register</c> into the
    /// RunCoach error contract: 409 Conflict for duplicate-identity errors (plain ProblemDetails),
    /// 400 Bad Request with a DTO-property-keyed ValidationProblemDetails for everything else.
    /// </summary>
    public static IActionResult ToRegistrationActionResult(
        this IdentityResult result,
        ControllerBase controller,
        RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded) throw new InvalidOperationException("Result is successful.");

        // If ANY error is a uniqueness conflict, the whole response is 409.
        var conflict = result.Errors.FirstOrDefault(e =>
            e.Code is nameof(IdentityErrorDescriber.DuplicateEmail)
                   or nameof(IdentityErrorDescriber.DuplicateUserName));

        if (conflict is not null)
        {
            // Plain ProblemDetails — NOT ValidationProblemDetails — per RunCoach contract.
            // Title is deliberately generic to avoid enumeration leakage beyond the status code.
            return controller.Problem(
                type:       "https://runcoach.app/problems/registration-conflict",
                title:      "The account could not be created.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // Validation path: group by DTO property and hand to ProblemDetailsFactory via ValidationProblem.
        foreach (var error in result.Errors)
        {
            var key = IdentityErrorCodeMapper.Map(error).PropertyName switch
            {
                IdentityErrorBuckets.Password => nameof(request.Password).ToCamelCase(),
                IdentityErrorBuckets.Email    => nameof(request.Email).ToCamelCase(),
                IdentityErrorBuckets.UserName => nameof(request.Email).ToCamelCase(), // UserName == Email
                _                             => string.Empty, // top-level / general
            };
            controller.ModelState.AddModelError(key, error.Description);
        }

        return controller.ValidationProblem(controller.ModelState);
    }

    private static string ToCamelCase(this string s) =>
        s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
```

### Why this is idiomatic (source-level confirmation)

`ControllerBase.ValidationProblem(ModelStateDictionary)` — decorated `[DefaultStatusCode(400)]` in `src/Mvc/Mvc.Core/src/ControllerBase.cs` — calls `ProblemDetailsFactory.CreateValidationProblemDetails(HttpContext, ModelState)`. `DefaultProblemDetailsFactory` (in `src/Mvc/Mvc.Core/src/Infrastructure/DefaultProblemDetailsFactory.cs`) auto-populates `title = "One or more validation errors occurred."`, `type = ApiBehaviorOptions.ClientErrorMapping[400].Link` (the legacy rfc7231 URL — override via `ConfigureApiBehaviorOptions` if you want RFC 9110), `status = 400`, and adds a `traceId` extension from `Activity.Current?.Id ?? HttpContext.TraceIdentifier`. The result is wrapped in `BadRequestObjectResult` and emitted with `Content-Type: application/problem+json`.

**Key gotcha**: The `errors` dictionary keys are serialized *literally*, with no camelCase naming policy applied (`dotnet/aspnetcore#17856`). Pass `"password"`, not `"Password"`.

---

## 2. `IdentityError.Code` → DTO property mapping

Canonical source: [`src/Identity/Extensions.Core/src/IdentityErrorDescriber.cs`](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/IdentityErrorDescriber.cs). The set of **22 codes is stable across Identity 6, 7, 8, 9, and 10** — each code is produced by `nameof(...)`, so it is invariant to localization and unchanged by the .NET 10 passkey work.

| Code | DTO bucket | HTTP status | Kind | Notes |
|------|------------|-------------|------|-------|
| `PasswordTooShort` | `password` | 400 | Validation | Prefer DTO `MinLength` to avoid this path |
| `PasswordRequiresUniqueChars` | `password` | 400 | Validation | |
| `PasswordRequiresNonAlphanumeric` | `password` | 400 | Validation | |
| `PasswordRequiresDigit` | `password` | 400 | Validation | |
| `PasswordRequiresLower` | `password` | 400 | Validation | |
| `PasswordRequiresUpper` | `password` | 400 | Validation | |
| `PasswordMismatch` | `password` | 401 | Unauthorized | ChangePassword / CheckPassword paths |
| `UserAlreadyHasPassword` | `password` | 409 | Conflict | AddPassword on user who has one |
| `InvalidEmail` | `email` | 400 | Validation | Format / EmailAddressAttribute |
| `DuplicateEmail` | `email` | **409** | Conflict | Enumeration-sensitive — see §7 |
| `InvalidUserName` | `username` | 400 | Validation | Disallowed chars in UserOptions |
| `DuplicateUserName` | `username` | **409** | Conflict | Enumeration-sensitive |
| `InvalidRoleName` | `role` | 400 | Validation | Admin endpoints |
| `DuplicateRoleName` | `role` | 409 | Conflict | Admin endpoints |
| `UserAlreadyInRole` | `role` | 409 | Conflict | |
| `UserNotInRole` | `role` | 409 | Conflict | |
| `UserLockoutNotEnabled` | *(general)* | 409 | Conflict | State/config mismatch |
| `ConcurrencyFailure` | *(general)* | 409 | Conflict | Optimistic concurrency; often retryable |
| `InvalidToken` | *(general)* | 400 | Validation | Confirm/reset/2FA flows |
| `RecoveryCodeRedemptionFailed` | *(general)* | 401 | Unauthorized | 2FA recovery |
| `LoginAlreadyAssociated` | *(general)* | 409 | Conflict | External login rebinding |
| `DefaultError` | *(general)* | 500 | Unknown | Catch-all — log and escalate |

### Reusable mapper

```csharp
// src/RunCoach.Api/Auth/IdentityErrorCodeMapper.cs
namespace RunCoach.Api.Auth;

using Microsoft.AspNetCore.Identity;

public enum IdentityErrorKind { Validation, Conflict, Unauthorized, Unknown }

public static class IdentityErrorBuckets
{
    public const string Password = "password";
    public const string Email    = "email";
    public const string UserName = "username";
    public const string Role     = "role";
    public const string General  = "general";
}

/// <summary>
/// Maps ASP.NET Core Identity error codes to a DTO bucket and HTTP semantic class.
/// Source of truth: dotnet/aspnetcore src/Identity/Extensions.Core/src/IdentityErrorDescriber.cs
/// (22 codes; stable across Identity 6–10).
/// </summary>
internal static class IdentityErrorCodeMapper
{
    public readonly record struct Mapping(string PropertyName, IdentityErrorKind Kind);

    public static Mapping Map(IdentityError error) => error.Code switch
    {
        // Password policy
        nameof(IdentityErrorDescriber.PasswordTooShort)                 => new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars)      => new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric)  => new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit)            => new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.PasswordRequiresLower)            => new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper)            => new(IdentityErrorBuckets.Password, IdentityErrorKind.Validation),
        // Credential / password state
        nameof(IdentityErrorDescriber.PasswordMismatch)                 => new(IdentityErrorBuckets.Password, IdentityErrorKind.Unauthorized),
        nameof(IdentityErrorDescriber.UserAlreadyHasPassword)           => new(IdentityErrorBuckets.Password, IdentityErrorKind.Conflict),
        // Email
        nameof(IdentityErrorDescriber.InvalidEmail)                     => new(IdentityErrorBuckets.Email,    IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.DuplicateEmail)                   => new(IdentityErrorBuckets.Email,    IdentityErrorKind.Conflict),
        // Username
        nameof(IdentityErrorDescriber.InvalidUserName)                  => new(IdentityErrorBuckets.UserName, IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.DuplicateUserName)                => new(IdentityErrorBuckets.UserName, IdentityErrorKind.Conflict),
        // Roles
        nameof(IdentityErrorDescriber.InvalidRoleName)                  => new(IdentityErrorBuckets.Role,     IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.DuplicateRoleName)                => new(IdentityErrorBuckets.Role,     IdentityErrorKind.Conflict),
        nameof(IdentityErrorDescriber.UserAlreadyInRole)                => new(IdentityErrorBuckets.Role,     IdentityErrorKind.Conflict),
        nameof(IdentityErrorDescriber.UserNotInRole)                    => new(IdentityErrorBuckets.Role,     IdentityErrorKind.Conflict),
        // Infrastructure / general
        nameof(IdentityErrorDescriber.ConcurrencyFailure)               => new(IdentityErrorBuckets.General,  IdentityErrorKind.Conflict),
        nameof(IdentityErrorDescriber.InvalidToken)                     => new(IdentityErrorBuckets.General,  IdentityErrorKind.Validation),
        nameof(IdentityErrorDescriber.RecoveryCodeRedemptionFailed)     => new(IdentityErrorBuckets.General,  IdentityErrorKind.Unauthorized),
        nameof(IdentityErrorDescriber.LoginAlreadyAssociated)           => new(IdentityErrorBuckets.General,  IdentityErrorKind.Conflict),
        nameof(IdentityErrorDescriber.UserLockoutNotEnabled)            => new(IdentityErrorBuckets.General,  IdentityErrorKind.Conflict),
        nameof(IdentityErrorDescriber.DefaultError)                     => new(IdentityErrorBuckets.General,  IdentityErrorKind.Unknown),
        // Custom describer / future codes
        _                                                                => new(IdentityErrorBuckets.General,  IdentityErrorKind.Unknown),
    };
}
```

---

## 3. Pre-validation decision

**For RunCoach Slice 0: use DataAnnotations on the DTO.** Keep the rules that are cheap, client-mirrorable, and format-shaped at the DTO. Let Identity own server-side-only rules.

| Concern | Belongs on DTO (DataAnnotations) | Belongs in Identity | Rationale |
|---|---|---|---|
| Email format | ✅ `[EmailAddress]` | Identity re-checks via `EmailAddressAttribute` | Cheap; mirrors frontend Zod |
| Password minimum length | ✅ `[MinLength(12)]` | Identity re-checks via `PasswordOptions.RequiredLength` | Eliminates the common "too short" translation entirely |
| Password max length | ✅ `[MaxLength(128)]` | — | Prevents DoS via giant inputs hitting the hash function |
| Password character classes (upper/lower/digit/nonalpha) | ❌ | ✅ `PasswordOptions.RequireUppercase` etc. | Rules may change; let Identity be source of truth |
| Email uniqueness | ❌ | ✅ `UserValidator` + `RequireUniqueEmail` | Requires DB lookup; translates to 409 |
| Username uniqueness | ❌ | ✅ `UserValidator` | Same |

**Upgrade to FluentValidation 12+ only when rules become conditional** (cross-field, async lookups, feature-flagged). FluentValidation integrates with `[ApiController]` via `SharpGrip.FluentValidation.AutoValidation.Mvc` (the former `FluentValidation.AspNetCore` package was retired by the maintainer in 2022 and the community successor is what to reference in 2026). DataAnnotations remain first-class and continue to benefit in .NET 10 from the minimal-API `AddValidation()` source-generator work, though that generator does not target MVC controllers specifically.

**Frontend parity**: Express the same rules as a Zod schema in the SPA. The one gap — FluentValidation → Zod auto-sync — is not solved by any mature library in 2026; write rules twice but colocate them in a `shared-contracts` folder and add a small contract-test that asserts equivalence (e.g., the string `"A1a!bcdefghij"` must pass both; `"short"` must fail both).

---

## 4. Response body JSON examples

### Weak password (400, `ValidationProblemDetails`)

```json
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json; charset=utf-8

{
  "type":   "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title":  "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
  "errors": {
    "password": [
      "Passwords must be at least 12 characters.",
      "Passwords must have at least one uppercase ('A'-'Z').",
      "Passwords must have at least one non-alphanumeric character."
    ]
  }
}
```

### Duplicate email (409, plain `ProblemDetails`)

```json
HTTP/1.1 409 Conflict
Content-Type: application/problem+json; charset=utf-8

{
  "type":   "https://runcoach.app/problems/registration-conflict",
  "title":  "The account could not be created.",
  "status": 409,
  "traceId": "00-..."
}
```

The title is intentionally generic. If your threat model accepts enumeration (see §7), use `"An account with that email already exists."` instead. Either way, no `errors` dictionary — it's `ProblemDetails`, not `ValidationProblemDetails`.

### Invalid login (401, generic `ProblemDetails`)

```json
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json; charset=utf-8

{
  "type":   "https://runcoach.app/problems/invalid-credentials",
  "title":  "Invalid email or password.",
  "status": 401,
  "traceId": "00-..."
}
```

Emitted identically for every non-success `SignInResult` — `Failed`, `IsLockedOut`, `IsNotAllowed`, `RequiresTwoFactor`. Always run the password hash even when the user lookup returned null, to equalize timing (OWASP Authentication Cheat Sheet, 2024).

### Account locked out

**Use 401, not 423.** RFC 4918 §11.3 scopes `423 Locked` to WebDAV resource locking; reusing it for auth lockout is non-idiomatic and also leaks enumeration information. Microsoft's `MapIdentityApi` `/login` handler ([source](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityApiEndpointRouteBuilderExtensions.cs)) collapses every failing `SignInResult` into a single 401 ProblemDetails. Follow the same pattern — the JSON body is identical to the "invalid login" example above.

### Malformed request (400, from `[ApiController]` auto-400)

```json
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json; charset=utf-8

{
  "type":   "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title":  "One or more validation errors occurred.",
  "status": 400,
  "traceId": "00-...",
  "errors": {
    "email":    ["The Email field is required."],
    "password": ["The field Password must be a string or array type with a minimum length of '12'."]
  }
}
```

Generated by `ApiBehaviorOptions.InvalidModelStateResponseFactory` (which calls the same `ProblemDetailsFactory.CreateValidationProblemDetails`). The shape is identical to the Identity-sourced 400 — the contract is uniform regardless of where validation failed.

---

## 5. Alternatives considered

### Manual `ModelState.AddModelError` loop + `ValidationProblem(ModelState)` — **RECOMMENDED**
**Pros.** Canonical; flows through `ProblemDetailsFactory`; identical shape to `[ApiController]` auto-400; preserves `traceId`; honors `CustomizeProblemDetails` for non-validation fields; trivial to unit-test; matches the shape of Microsoft's built-in `MapIdentityApi` (minus the DTO-keying difference).
**Cons.** Per-controller code — needs consistency discipline. Mitigated by the `ToRegistrationActionResult` extension which centralizes the logic.

### Throw `IdentityFailureException(IdentityResult)` + dedicated `IExceptionHandler`
**Pros.** Fully centralized; one `IdentityExceptionHandler` handles every controller. Coexists with the existing 500-handler — `IExceptionHandler` supports multiple instances invoked in registration order, each returning `bool` to short-circuit (`.NET 8+`). Works even from service-layer code that has no access to `ControllerBase`.
**Cons.** Exceptions for control flow smell; throws are 100×-1000× slower than returns; harder to trace in logs; the .NET 10 default `SuppressDiagnosticsCallback` now hides handled exceptions, which can surprise operators. Also, the handler writes through `IProblemDetailsService`, which uses a different JSON pipeline from MVC's `ObjectResult` — subtle shape drift is possible.
**Verdict.** Good fallback for service-layer error translation; not the primary recommendation for a controller that already holds the `IdentityResult`.

### Custom `ProblemDetailsFactory` subclass
**Pros.** Intercepts *all* MVC-produced ProblemDetails (both auto-400 and explicit `ValidationProblem`); single seam.
**Cons.** Does not intercept `IdentityResult` directly — you still need the per-action `ModelState.AddModelError` loop first. Provides no extra value over the helper.

### `AddProblemDetails(o => o.CustomizeProblemDetails = ...)` callback
**Pros.** One place to add cross-cutting fields (`traceId`, `instance`, correlation IDs).
**Cons. Does NOT fire for `ValidationProblemDetails` produced via MVC's `ValidationProblem(ModelState)`.** Confirmed in `dotnet/aspnetcore#62723` (.NET 10 preview). Useful for exception-path customization only. This is the single most misunderstood item in the ProblemDetails landscape.

### Custom `IProblemDetailsWriter`
**Pros.** Controls response serialization (content negotiation, extra keys at wire time).
**Cons.** Wrong layer — operates on the already-built object. Does not intercept MVC's direct `ObjectResult` write path.

### Middleware-based translator
**Pros.** Works for any framework.
**Cons.** Reads the response body after the fact — brittle, and `[ApiController]` already did the right thing upstream. Rejected.

### Result<T> functional pattern (`Ardalis.Result`, `FluentResults`, `OneOf`)
**Pros.** Testable service layer; clean separation of domain from HTTP; cited in `jasontaylordev/CleanArchitecture` as the canonical Clean-Architecture pattern (`IdentityResult.ToApplicationResult()` → `Result` → controller translates).
**Cons.** Adds a dependency; requires a `Result → IActionResult` translator anyway (you end up back at `ModelState.AddModelError` inside it); overkill for RunCoach Slice 0 which has a single auth action. Re-evaluate at Slice 1+ when service layer expands.

---

## 6. Production precedent

| Repo | Pattern | DTO-keyed? | Link |
|---|---|---|---|
| `dotnet/aspnetcore` — `MapIdentityApi<TUser>` | `foreach err → errors[err.Code]`; `TypedResults.ValidationProblem(dict)`; always 400 even for DuplicateEmail | ❌ keys by `IdentityError.Code` | [`IdentityApiEndpointRouteBuilderExtensions.cs`](https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Core/src/IdentityApiEndpointRouteBuilderExtensions.cs) |
| `dotnet/aspnetcore` — scaffolded Identity UI (Razor) | `foreach err → ModelState.AddModelError(string.Empty, err.Description)`; re-render page | N/A (empty key, not API) | `src/Identity/UI/src/Areas/Identity/Pages/V5/Account/Register.cshtml.cs` |
| `jasontaylordev/CleanArchitecture` | `IdentityResult.ToApplicationResult()` → domain `Result` with `.Errors = descriptions.ToArray()`; MediatR handler; controller translates | ❌ flat list of descriptions | [`IdentityResultExtensions.cs`](https://github.com/jasontaylordev/CleanArchitecture/blob/main/src/Infrastructure/Identity/IdentityResultExtensions.cs) |
| `dotnet/eShop` (Identity.API) | No public Register API; user creation is seeded. `AccountController` is Duende IdentityServer Quickstart (login only) | N/A | [repo root](https://github.com/dotnet/eShop) |
| `NimblePros/eShopOnWeb` (formerly `dotnet-architecture/eShopOnWeb`) | Scaffolded Razor Pages Identity UI; no API Register | N/A | [repo](https://github.com/NimblePros/eShopOnWeb) |
| `abpframework/abp` | `(await UserManager.CreateAsync(...)).CheckErrors()` throws localized `AbpIdentityResultException`; global exception filter translates | ❌ | [`IdentityUserManager`](https://github.com/abpframework/abp/blob/dev/modules/identity/src/Volo.Abp.Identity.Domain/Volo/Abp/Identity/IdentityUserManager.cs) |
| `ardalis/CleanArchitecture` / `Ardalis.Result` | `Result<T>` wrapping; `[TranslateResultToActionResult]` endpoint filter; no first-party Identity adapter — teams write their own | ❌ | [`Result`](https://github.com/ardalis/Result) |

**Dominant upstream pattern: key by `IdentityError.Code`** (MapIdentityApi). **RunCoach's DTO-property keying is a deliberate departure** aligned with frontend form-library convention (React Hook Form / Zod keying). No widely-used library implements the DTO-property mapping — ship your own as shown in §2.

**Precedent for our choice**: Kevin Smith's 2022 post "Extra Validation Errors In ASP.NET Core" ([`kevsoft.net/2022/01/02/extra-validation-errors-in-asp-net-core.html`](https://kevsoft.net/2022/01/02/extra-validation-errors-in-asp-net-core.html)) demonstrates the exact `AddModelError("propertyName", description)` + `ValidationProblem(ModelState)` pattern and is cited frequently in the Identity-API community as the reference for DTO-keyed validation errors.

---

## 7. Security — enumeration tradeoff

**OWASP ASVS 5.0 V6.3** (released May 2025; source: [OWASP ASVS GitHub](https://github.com/OWASP/ASVS/blob/master/5.0/en/0x15-V6-Authentication.md)) requires: *"Verify that valid users cannot be deduced from failed authentication challenges, such as by basing on error messages, HTTP response codes, or different response times. Registration and forgot password functionality must also have this protection."* **A 409 on duplicate registration technically violates this.**

**OWASP Authentication Cheat Sheet** grants an explicit escape hatch: CAPTCHA-protected endpoints may return specific errors when UX demands it. This is the prevailing industry choice for consumer SPAs.

**The pragmatic recommendation for RunCoach** (consumer fitness SPA with CAPTCHA + per-IP rate limit, no HIPAA/PCI/clinical data): **409 + generic title** protected by CAPTCHA and `EnableRateLimiting("register-strict")` (5 req/hour/IP). Document the deviation from ASVS V6.3 in the security doc. Note that Microsoft's own `MapIdentityApi` is inconsistent: `/forgotPassword` and `/resendConfirmationEmail` always return 200 with a "don't reveal" comment, but `/register` leaks via `ValidationProblem(DuplicateEmail)`. You are in large and principled company either way.

**Login endpoint must remain strict**: identical 401 ProblemDetails for every non-success `SignInResult`, equalized response time (always run the password hash, even for unknown users), no branching on `IsLockedOut` / `IsNotAllowed` / `RequiresTwoFactor` at the HTTP-shape level. This matches `MapIdentityApi` exactly and is the unambiguous OWASP guidance.

**Upgrade path** (consider for Slice 1+): migrate registration to a "success-or-reuse" pattern — always return 202 Accepted; if the email is already in use, email the existing account saying "someone attempted to register with your address." This eliminates the enumeration leak entirely without harming UX for legitimate new users.

---

## Conclusion

The canonical ASP.NET Core 10 idiom for `IdentityResult.Errors` → `ValidationProblemDetails` is pleasantly boring: a `foreach` loop over `result.Errors` calling `ModelState.AddModelError(propertyKey, error.Description)`, closed by `return ValidationProblem(ModelState);`. The machinery underneath — `ProblemDetailsFactory.CreateValidationProblemDetails`, `ApiBehaviorOptions.ClientErrorMapping`, automatic `traceId` extension, wrapping in `BadRequestObjectResult` — does exactly the right thing and has been stable since .NET 8's `AddProblemDetails()` integration. The only judgment calls are (1) keying by DTO property name rather than `IdentityError.Code` (a deliberate departure from `MapIdentityApi`, aligned with SPA form-library conventions), and (2) splitting the 409 Duplicate path out to a plain `ProblemDetails` via `Problem(...)`. Both are handled by a ~30-line `IdentityResultExtensions.ToRegistrationActionResult` helper that keeps the controller action a three-liner.

The meta-insight is negative: **do not try to centralize this through `CustomizeProblemDetails`**. The .NET 10 reality — confirmed as recently as preview issue `dotnet/aspnetcore#62723` — is that MVC's validation-problem path bypasses `IProblemDetailsService` and therefore bypasses `CustomizeProblemDetails`. The `[ApiController]` MVC pipeline and the minimal-API `IProblemDetailsService` pipeline remain two parallel systems, and translation from Identity's domain result to the MVC pipeline must happen inside the controller (or be thrown into an `IExceptionHandler`). The helper approach described here respects both pipelines and will continue to work unchanged into .NET 11.